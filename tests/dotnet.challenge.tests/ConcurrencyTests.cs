using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.tests;

/// <summary>
/// Acceptance test for business rule #1 (no overselling).
///
/// You MUST NOT modify this file. Your implementation must make this test pass reliably.
/// </summary>
public class ConcurrencyTests
{
    private static readonly TimeSpan IterationTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Reserve_UnderConcurrentLoad_NeverOversells()
    {
        // Run the scenario multiple times to avoid a fluke pass that relies on
        // a lucky thread schedule.
        for (var iteration = 0; iteration < 3; iteration++)
        {
            await RunIteration(iteration);
        }
    }

    private static async Task RunIteration(int iteration)
    {
        var svc = new InventoryService(
            new SimpleObjectCache<Guid, Product>(),
            new SimpleObjectCache<Guid, Reservation>());

        const int initialStock = 10;
        const int concurrency = 50;

        // Pre-seed the thread pool so the Barrier doesn't block waiting for
        // worker threads to spin up (which can add ~500ms per thread).
        ThreadPool.SetMinThreads(concurrency + 10, concurrency + 10);

        var product = await svc.CreateProductAsync("Widget", initialStock);

        var barrier = new Barrier(concurrency);
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await svc.TryReserveAsync(product.Id, 1, ttlSeconds: 60);
            }))
            .ToArray();

        var allTasks = Task.WhenAll(tasks);
        var timeout = Task.Delay(IterationTimeout);

        if (await Task.WhenAny(allTasks, timeout) == timeout)
        {
            throw new Xunit.Sdk.XunitException(
                $"iteration {iteration}: concurrent reservation timed out — likely data-structure corruption under race.");
        }

        var results = await allTasks;

        var successes = results.Count(r => r.Result == ReserveResult.Success);
        var insufficient = results.Count(r => r.Result == ReserveResult.InsufficientStock);

        successes.Should().Be(initialStock,
            $"iteration {iteration}: must honour stock exactly — no overselling, no lost writes");
        insufficient.Should().Be(concurrency - initialStock,
            $"iteration {iteration}: every non-winner must observe insufficient stock");

        var reloaded = await svc.GetProductAsync(product.Id);
        reloaded!.Stock.Should().Be(0,
            $"iteration {iteration}: final stock must equal initial minus reserved successes");
    }
}
