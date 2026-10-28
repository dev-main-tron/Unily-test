using dotnet.challenge.data.Cache;

namespace dotnet.challenge.tests;

public class SimpleObjectCacheTests
{
    private sealed record Item(int Value);

    [Fact]
    public async Task ParallelWrites_DoNotLoseOrCorruptEntries()
    {
        var cache = new SimpleObjectCache<Guid, Item>();
        var keys = Enumerable.Range(0, 5_000).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(keys.Select((k, i) => Task.Run(() => cache.AddAsync(k, new Item(i)))));
        await Task.WhenAll(keys.Select((k, i) => Task.Run(() => cache.UpdateAsync(k, new Item(i + 1)))));

        (await cache.GetAllAsync()).Count().Should().Be(keys.Length);
        (await cache.GetAsync(keys[42]))!.Value.Should().Be(43);
    }

    [Fact]
    public async Task Update_OfMissingKey_DoesNotInsert()
    {
        var cache = new SimpleObjectCache<Guid, Item>();
        var key = Guid.NewGuid();

        await cache.UpdateAsync(key, new Item(1));

        (await cache.GetAsync(key)).Should().BeNull();
    }
}
