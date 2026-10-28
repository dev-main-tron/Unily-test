using System.Collections.Concurrent;
using dotnet.challenge.data.Cache;
using dotnet.challenge.data.Entities;

namespace dotnet.challenge.api.Services;

/// <summary>
/// Inventory and reservation rules.
/// </summary>
/// <remarks>
/// Every operation that reads and then changes a product's <see cref="Product.Stock"/> or a reservation's
/// <see cref="Reservation.Status"/> (reserve, confirm, release, expire) runs inside a per-product async gate.
/// That makes each check-then-act sequence atomic, so concurrent reservations cannot oversell (rule 1) and a
/// reservation's quantity is returned to stock at most once whatever the interleaving of release, expiry and
/// sweeps (rule 4). Different products never contend with each other.
/// Expiry is applied lazily on every read and write path (rule 2: a GET after ExpiresAt already observes
/// Expired and the restored Stock) and eagerly by <c>ReservationExpirySweeper</c>.
/// </remarks>
public sealed class InventoryService : IInventoryService
{
    private readonly ISimpleObjectCache<Guid, Product> _products;
    private readonly ISimpleObjectCache<Guid, Reservation> _reservations;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _productGates = new();

    public InventoryService(
        ISimpleObjectCache<Guid, Product> products,
        ISimpleObjectCache<Guid, Reservation> reservations)
        : this(products, reservations, TimeProvider.System)
    {
    }

    public InventoryService(
        ISimpleObjectCache<Guid, Product> products,
        ISimpleObjectCache<Guid, Reservation> reservations,
        TimeProvider timeProvider)
    {
        _products = products;
        _reservations = reservations;
        _time = timeProvider;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public async Task<Product> CreateProductAsync(string name, int stock, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(stock);
        cancellationToken.ThrowIfCancellationRequested();

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Stock = stock
        };
        await _products.AddAsync(product.Id, product);
        return product;
    }

    public async Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _products.GetAsync(id) is null)
        {
            return null;
        }

        await using (await EnterAsync(id, cancellationToken))
        {
            await ExpireDueLockedAsync(id);
            return await _products.GetAsync(id);
        }
    }

    public async Task<(ReserveResult Result, Reservation? Reservation)> TryReserveAsync(
        Guid productId, int quantity, int ttlSeconds, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ttlSeconds);

        if (await _products.GetAsync(productId) is null)
        {
            return (ReserveResult.ProductNotFound, null);
        }

        await using (await EnterAsync(productId, cancellationToken))
        {
            // Stock that has already expired is available again before this reservation is judged.
            await ExpireDueLockedAsync(productId);

            var product = await _products.GetAsync(productId);
            if (product is null)
            {
                return (ReserveResult.ProductNotFound, null);
            }

            if (product.Stock < quantity)
            {
                return (ReserveResult.InsufficientStock, null);
            }

            product.Stock -= quantity;
            await _products.UpdateAsync(productId, product);

            var now = UtcNow;
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = quantity,
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(ttlSeconds),
                Status = ReservationStatus.Active
            };
            await _reservations.AddAsync(reservation.Id, reservation);

            return (ReserveResult.Success, reservation);
        }
    }

    public async Task<Reservation?> GetReservationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reservation = await _reservations.GetAsync(id);
        if (reservation is null || reservation.Status != ReservationStatus.Active || !IsExpired(reservation, UtcNow))
        {
            return reservation;
        }

        await using (await EnterAsync(reservation.ProductId, cancellationToken))
        {
            reservation = await _reservations.GetAsync(id);
            if (reservation is { Status: ReservationStatus.Active } && IsExpired(reservation, UtcNow))
            {
                await ExpireLockedAsync(reservation);
            }
            return reservation;
        }
    }

    public async Task<ConfirmResult> ConfirmAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var existing = await _reservations.GetAsync(reservationId);
        if (existing is null)
        {
            return ConfirmResult.NotFound;
        }

        await using (await EnterAsync(existing.ProductId, cancellationToken))
        {
            var reservation = await _reservations.GetAsync(reservationId);
            if (reservation is null)
            {
                return ConfirmResult.NotFound;
            }

            switch (reservation.Status)
            {
                case ReservationStatus.Confirmed:
                    return ConfirmResult.AlreadyConfirmed;

                case ReservationStatus.Active when IsExpired(reservation, UtcNow):
                    // Too late: apply the expiry (returning the stock) and refuse.
                    await ExpireLockedAsync(reservation);
                    return ConfirmResult.IllegalState;

                case ReservationStatus.Active:
                    reservation.Status = ReservationStatus.Confirmed;
                    await _reservations.UpdateAsync(reservation.Id, reservation);
                    return ConfirmResult.Success;

                default: // Released or Expired
                    return ConfirmResult.IllegalState;
            }
        }
    }

    public async Task<ReleaseResult> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var existing = await _reservations.GetAsync(reservationId);
        if (existing is null)
        {
            return ReleaseResult.NotFound;
        }

        await using (await EnterAsync(existing.ProductId, cancellationToken))
        {
            var reservation = await _reservations.GetAsync(reservationId);
            if (reservation is null)
            {
                return ReleaseResult.NotFound;
            }

            switch (reservation.Status)
            {
                case ReservationStatus.Released:
                case ReservationStatus.Expired:
                    return ReleaseResult.AlreadyReleased;

                case ReservationStatus.Active when IsExpired(reservation, UtcNow):
                    // Already expired in time: record it as Expired (stock returned once) and treat the
                    // release as the idempotent no-op the spec asks for.
                    await ExpireLockedAsync(reservation);
                    return ReleaseResult.AlreadyReleased;

                default: // Active (not yet expired) or Confirmed
                    reservation.Status = ReservationStatus.Released;
                    await _reservations.UpdateAsync(reservation.Id, reservation);
                    await ReturnStockLockedAsync(reservation.ProductId, reservation.Quantity);
                    return ReleaseResult.Success;
            }
        }
    }

    public async Task SweepExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var productsWithDueReservations = (await _reservations.GetAllAsync())
            .Where(r => r.Status == ReservationStatus.Active && IsExpired(r, now))
            .Select(r => r.ProductId)
            .Distinct();

        foreach (var productId in productsWithDueReservations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (await EnterAsync(productId, cancellationToken))
            {
                await ExpireDueLockedAsync(productId);
            }
        }
    }

    /// <summary>
    /// True if the reservation's expiry has passed. Both sides are UTC.
    /// </summary>
    public static bool IsExpired(Reservation reservation) => IsExpired(reservation, DateTime.UtcNow);

    public static bool IsExpired(Reservation reservation, DateTime utcNow) => reservation.ExpiresAt <= utcNow;

    // ───────── helpers: callers must hold the product's gate ─────────

    private async Task ExpireDueLockedAsync(Guid productId)
    {
        var now = UtcNow;
        var due = (await _reservations.GetAllAsync())
            .Where(r => r.ProductId == productId && r.Status == ReservationStatus.Active && IsExpired(r, now));

        foreach (var reservation in due)
        {
            await ExpireLockedAsync(reservation);
        }
    }

    private async Task ExpireLockedAsync(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Expired;
        await _reservations.UpdateAsync(reservation.Id, reservation);
        await ReturnStockLockedAsync(reservation.ProductId, reservation.Quantity);
    }

    private async Task ReturnStockLockedAsync(Guid productId, int quantity)
    {
        var product = await _products.GetAsync(productId);
        if (product is null)
        {
            return;
        }
        product.Stock += quantity;
        await _products.UpdateAsync(productId, product);
    }

    private async Task<Gate> EnterAsync(Guid productId, CancellationToken cancellationToken)
    {
        var semaphore = _productGates.GetOrAdd(productId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Gate(semaphore);
    }

    private readonly struct Gate(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
