using dotnet.challenge.data.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainProduct = dotnet.challenge.data.Entities.Product;
using DomainReservation = dotnet.challenge.data.Entities.Reservation;
using ReservationStatus = dotnet.challenge.data.Entities.ReservationStatus;

namespace dotnet.challenge.data.sqlserver;

/// <summary>
/// <see cref="ISimpleObjectCache{TKey,TValue}"/> over SQL Server, so InventoryService works unchanged
/// on top of the scaffolded <see cref="UnilyDbContext"/>. A short-lived context per call (IDbContextFactory)
/// keeps the caches safe as singletons.
/// </summary>
/// <remarks>
/// InventoryService serialises stock changes per product <em>in process</em>. That is enough for a single
/// API instance; running several instances against the same database would additionally need a
/// database-level guard (rowversion or a conditional UPDATE ... WHERE Stock &gt;= @quantity).
/// </remarks>
public sealed class SqlServerProductCache(IDbContextFactory<UnilyDbContext> contexts)
    : ISimpleObjectCache<Guid, DomainProduct>
{
    public async Task AddAsync(Guid key, DomainProduct value)
    {
        await using var db = await contexts.CreateDbContextAsync();
        db.Products.Add(new Models.Product { Id = key, Name = value.Name, Stock = value.Stock });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid key)
    {
        await using var db = await contexts.CreateDbContextAsync();
        await db.Products.Where(p => p.Id == key).ExecuteDeleteAsync();
    }

    public async Task<DomainProduct?> GetAsync(Guid key)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var row = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == key);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IEnumerable<DomainProduct>> GetAllAsync()
    {
        await using var db = await contexts.CreateDbContextAsync();
        return (await db.Products.AsNoTracking().ToListAsync()).Select(ToDomain).ToList();
    }

    public async Task UpdateAsync(Guid key, DomainProduct value)
    {
        await using var db = await contexts.CreateDbContextAsync();
        await db.Products.Where(p => p.Id == key).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, value.Name)
            .SetProperty(p => p.Stock, value.Stock));
    }

    private static DomainProduct ToDomain(Models.Product row) => new() { Id = row.Id, Name = row.Name, Stock = row.Stock };
}

public sealed class SqlServerReservationCache(IDbContextFactory<UnilyDbContext> contexts)
    : ISimpleObjectCache<Guid, DomainReservation>
{
    public async Task AddAsync(Guid key, DomainReservation value)
    {
        await using var db = await contexts.CreateDbContextAsync();
        db.Reservations.Add(new Models.Reservation
        {
            Id = key,
            ProductId = value.ProductId,
            Quantity = value.Quantity,
            CreatedAt = value.CreatedAt,
            ExpiresAt = value.ExpiresAt,
            Status = value.Status.ToString()
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid key)
    {
        await using var db = await contexts.CreateDbContextAsync();
        await db.Reservations.Where(r => r.Id == key).ExecuteDeleteAsync();
    }

    public async Task<DomainReservation?> GetAsync(Guid key)
    {
        await using var db = await contexts.CreateDbContextAsync();
        var row = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == key);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IEnumerable<DomainReservation>> GetAllAsync()
    {
        await using var db = await contexts.CreateDbContextAsync();
        return (await db.Reservations.AsNoTracking().ToListAsync()).Select(ToDomain).ToList();
    }

    public async Task UpdateAsync(Guid key, DomainReservation value)
    {
        var status = value.Status.ToString();
        await using var db = await contexts.CreateDbContextAsync();
        await db.Reservations.Where(r => r.Id == key).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Quantity, value.Quantity)
            .SetProperty(r => r.ExpiresAt, value.ExpiresAt)
            .SetProperty(r => r.Status, status));
    }

    // SQL Server datetime2 has no kind; every value in this table is UTC.
    private static DomainReservation ToDomain(Models.Reservation row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Quantity = row.Quantity,
        CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
        ExpiresAt = DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc),
        Status = Enum.Parse<ReservationStatus>(row.Status)
    };
}

public static class SqlServerPersistenceExtensions
{
    public static IServiceCollection AddSqlServerObjectCaches(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<UnilyDbContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton<ISimpleObjectCache<Guid, DomainProduct>, SqlServerProductCache>();
        services.AddSingleton<ISimpleObjectCache<Guid, DomainReservation>, SqlServerReservationCache>();
        return services;
    }
}
