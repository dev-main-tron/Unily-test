using dotnet.challenge.api.Services;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.tests;

public class BasicTests
{
    private static InventoryService NewService() => new(
        new SimpleObjectCache<Guid, Product>(),
        new SimpleObjectCache<Guid, Reservation>());

    [Fact]
    public async Task CreateProduct_AssignsIdAndReturnsIt()
    {
        var svc = NewService();
        var product = await svc.CreateProductAsync("Widget", 10);

        product.Id.Should().NotBe(Guid.Empty);
        product.Name.Should().Be("Widget");
        product.Stock.Should().Be(10);

        var fetched = await svc.GetProductAsync(product.Id);
        fetched!.Stock.Should().Be(10);
    }

    [Fact]
    public async Task GetProduct_UnknownId_ReturnsNull()
    {
        var svc = NewService();
        var fetched = await svc.GetProductAsync(Guid.NewGuid());
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task Reserve_HappyPath_DecrementsStockAndCreatesActiveReservation()
    {
        var svc = NewService();
        var product = await svc.CreateProductAsync("Widget", 5);

        var (result, reservation) = await svc.TryReserveAsync(product.Id, 2, ttlSeconds: 60);

        result.Should().Be(ReserveResult.Success);
        reservation.Should().NotBeNull();
        reservation!.Quantity.Should().Be(2);
        reservation.Status.Should().Be(ReservationStatus.Active);

        var reloadedProduct = await svc.GetProductAsync(product.Id);
        reloadedProduct!.Stock.Should().Be(3);

        var reloadedReservation = await svc.GetReservationAsync(reservation.Id);
        reloadedReservation!.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Reserve_UnknownProduct_ReturnsProductNotFound()
    {
        var svc = NewService();
        var (result, reservation) = await svc.TryReserveAsync(Guid.NewGuid(), 1, 60);

        result.Should().Be(ReserveResult.ProductNotFound);
        reservation.Should().BeNull();
    }

    [Fact]
    public async Task Reserve_InsufficientStock_DoesNotDecrementStock()
    {
        var svc = NewService();
        var product = await svc.CreateProductAsync("Widget", 1);

        var (result, reservation) = await svc.TryReserveAsync(product.Id, 5, 60);

        result.Should().Be(ReserveResult.InsufficientStock);
        reservation.Should().BeNull();

        var reloaded = await svc.GetProductAsync(product.Id);
        reloaded!.Stock.Should().Be(1);
    }
}
