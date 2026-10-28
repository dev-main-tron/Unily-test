using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.tests;

/// <summary>Business rules 2 (expiry), 3 (idempotency) and 4 (stock arithmetic).</summary>
public class ReservationLifecycleTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly InventoryService _svc;

    public ReservationLifecycleTests()
    {
        _svc = new InventoryService(
            new SimpleObjectCache<Guid, Product>(),
            new SimpleObjectCache<Guid, Reservation>(),
            _clock);
    }

    private async Task<(Product Product, Reservation Reservation)> ReserveAsync(int stock = 10, int quantity = 3, int ttl = 60)
    {
        var product = await _svc.CreateProductAsync("Widget", stock);
        var (result, reservation) = await _svc.TryReserveAsync(product.Id, quantity, ttl);
        result.Should().Be(ReserveResult.Success);
        return (product, reservation!);
    }

    private async Task<int> StockOf(Guid productId) => (await _svc.GetProductAsync(productId))!.Stock;

    [Fact]
    public async Task Confirm_Active_ConfirmsAndKeepsStockReserved()
    {
        var (product, reservation) = await ReserveAsync();

        (await _svc.ConfirmAsync(reservation.Id)).Should().Be(ConfirmResult.Success);

        (await _svc.GetReservationAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Confirmed);
        (await StockOf(product.Id)).Should().Be(7);
    }

    [Fact]
    public async Task Confirm_Twice_IsIdempotent()
    {
        var (_, reservation) = await ReserveAsync();

        await _svc.ConfirmAsync(reservation.Id);
        (await _svc.ConfirmAsync(reservation.Id)).Should().Be(ConfirmResult.AlreadyConfirmed);
    }

    [Fact]
    public async Task Confirm_UnknownId_ReturnsNotFound()
    {
        (await _svc.ConfirmAsync(Guid.NewGuid())).Should().Be(ConfirmResult.NotFound);
    }

    [Fact]
    public async Task Release_Active_RestoresExactQuantity_AndSecondReleaseIsNoOp()
    {
        var (product, reservation) = await ReserveAsync(stock: 10, quantity: 4);

        (await _svc.ReleaseAsync(reservation.Id)).Should().Be(ReleaseResult.Success);
        (await StockOf(product.Id)).Should().Be(10);

        (await _svc.ReleaseAsync(reservation.Id)).Should().Be(ReleaseResult.AlreadyReleased);
        (await StockOf(product.Id)).Should().Be(10);
        (await _svc.GetReservationAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task Release_Confirmed_ReturnsStock()
    {
        var (product, reservation) = await ReserveAsync(stock: 10, quantity: 4);
        await _svc.ConfirmAsync(reservation.Id);

        (await _svc.ReleaseAsync(reservation.Id)).Should().Be(ReleaseResult.Success);
        (await StockOf(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Release_UnknownId_ReturnsNotFound()
    {
        (await _svc.ReleaseAsync(Guid.NewGuid())).Should().Be(ReleaseResult.NotFound);
    }

    [Fact]
    public async Task Confirm_Released_IsIllegal()
    {
        var (_, reservation) = await ReserveAsync();
        await _svc.ReleaseAsync(reservation.Id);

        (await _svc.ConfirmAsync(reservation.Id)).Should().Be(ConfirmResult.IllegalState);
    }

    [Fact]
    public async Task Get_AfterExpiry_ObservesExpiredAndRestoredStock_WithoutWaitingForSweeper()
    {
        var (product, reservation) = await ReserveAsync(stock: 10, quantity: 3, ttl: 60);
        _clock.Advance(TimeSpan.FromSeconds(61));

        (await _svc.GetReservationAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Expired);
        (await StockOf(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task GetProduct_AfterExpiry_RestoresStockEvenIfReservationIsNeverRead()
    {
        var (product, _) = await ReserveAsync(stock: 10, quantity: 3, ttl: 60);
        _clock.Advance(TimeSpan.FromSeconds(61));

        (await StockOf(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Confirm_AfterExpiry_IsIllegal_AndStockIsRestoredOnce()
    {
        var (product, reservation) = await ReserveAsync(stock: 10, quantity: 3, ttl: 60);
        _clock.Advance(TimeSpan.FromSeconds(61));

        (await _svc.ConfirmAsync(reservation.Id)).Should().Be(ConfirmResult.IllegalState);
        (await _svc.ConfirmAsync(reservation.Id)).Should().Be(ConfirmResult.IllegalState);
        (await StockOf(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Release_AfterExpiry_IsNoOp_AndStockIsRestoredOnce()
    {
        var (product, reservation) = await ReserveAsync(stock: 10, quantity: 3, ttl: 60);
        _clock.Advance(TimeSpan.FromSeconds(61));

        await _svc.SweepExpiredAsync();
        (await _svc.ReleaseAsync(reservation.Id)).Should().Be(ReleaseResult.AlreadyReleased);
        await _svc.SweepExpiredAsync();

        (await StockOf(product.Id)).Should().Be(10);
        (await _svc.GetReservationAsync(reservation.Id))!.Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public async Task Sweep_ExpiresOnlyDueActiveReservations()
    {
        var product = await _svc.CreateProductAsync("Widget", 10);
        var (_, shortLived) = await _svc.TryReserveAsync(product.Id, 2, ttlSeconds: 10);
        var (_, longLived) = await _svc.TryReserveAsync(product.Id, 3, ttlSeconds: 600);
        var (_, confirmed) = await _svc.TryReserveAsync(product.Id, 1, ttlSeconds: 10);
        await _svc.ConfirmAsync(confirmed!.Id);

        _clock.Advance(TimeSpan.FromSeconds(30));
        await _svc.SweepExpiredAsync();

        (await _svc.GetReservationAsync(shortLived!.Id))!.Status.Should().Be(ReservationStatus.Expired);
        (await _svc.GetReservationAsync(longLived!.Id))!.Status.Should().Be(ReservationStatus.Active);
        (await _svc.GetReservationAsync(confirmed.Id))!.Status.Should().Be(ReservationStatus.Confirmed);
        (await StockOf(product.Id)).Should().Be(10 - 3 - 1);
    }

    [Fact]
    public async Task ExpiredStock_CanBeReservedAgain()
    {
        var (product, _) = await ReserveAsync(stock: 3, quantity: 3, ttl: 60);
        (await _svc.TryReserveAsync(product.Id, 1, 60)).Result.Should().Be(ReserveResult.InsufficientStock);

        _clock.Advance(TimeSpan.FromSeconds(61));

        (await _svc.TryReserveAsync(product.Id, 3, 60)).Result.Should().Be(ReserveResult.Success);
    }

    [Fact]
    public async Task ConcurrentReleasesAndSweeps_RestoreEachReservationExactlyOnce()
    {
        const int stock = 20;
        var product = await _svc.CreateProductAsync("Widget", stock);
        var reservations = new List<Reservation>();
        for (var i = 0; i < stock; i++)
        {
            reservations.Add((await _svc.TryReserveAsync(product.Id, 1, ttlSeconds: 60)).Reservation!);
        }
        _clock.Advance(TimeSpan.FromSeconds(30)); // half of them will be released, the rest left to expire

        var work = reservations.Take(10)
            .SelectMany(r => new[] { Task.Run(() => _svc.ReleaseAsync(r.Id)), Task.Run(() => _svc.ReleaseAsync(r.Id)) })
            .Cast<Task>()
            .Concat(Enumerable.Range(0, 10).Select(_ => Task.Run(() => _svc.SweepExpiredAsync())));
        await Task.WhenAll(work);

        _clock.Advance(TimeSpan.FromSeconds(31));
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => _svc.SweepExpiredAsync())));
        await Task.WhenAll(reservations.Skip(10).Select(r => Task.Run(() => _svc.ReleaseAsync(r.Id))));

        (await StockOf(product.Id)).Should().Be(stock);
    }

    [Fact]
    public void IsExpired_ComparesInUtc()
    {
        var reservation = new Reservation { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        InventoryService.IsExpired(reservation).Should().Be(true);

        reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(1);
        InventoryService.IsExpired(reservation).Should().Be(false);
    }
}
