using dotnet.challenge.data.Entities;

namespace dotnet.challenge.api.Services;

public enum ReserveResult
{
    Success,
    ProductNotFound,
    InsufficientStock
}

public enum ConfirmResult
{
    Success,
    NotFound,
    AlreadyConfirmed,
    IllegalState
}

public enum ReleaseResult
{
    Success,
    NotFound,
    AlreadyReleased
}

public interface IInventoryService
{
    Task<Product> CreateProductAsync(string name, int stock, CancellationToken cancellationToken = default);

    /// <summary>Returns the product with any due expiries already applied to its Stock.</summary>
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(ReserveResult Result, Reservation? Reservation)> TryReserveAsync(
        Guid productId, int quantity, int ttlSeconds, CancellationToken cancellationToken = default);

    /// <summary>Returns the reservation; an Active reservation past its ExpiresAt is expired first.</summary>
    Task<Reservation?> GetReservationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ConfirmResult> ConfirmAsync(Guid reservationId, CancellationToken cancellationToken = default);

    Task<ReleaseResult> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default);

    Task SweepExpiredAsync(CancellationToken cancellationToken = default);
}
