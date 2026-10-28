using dotnet.challenge.api.Services;
using Microsoft.Extensions.Options;

namespace dotnet.challenge.api.Background;

public sealed class ReservationExpiryOptions
{
    public const string Section = "ReservationExpiry";

    /// <summary>How often Active reservations past ExpiresAt are swept (seconds).</summary>
    public int SweepIntervalSeconds { get; set; } = 1;
}

/// <summary>
/// Periodically sweeps Active reservations whose ExpiresAt has passed and returns
/// their quantity to the owning product's Stock.
/// </summary>
/// <remarks>
/// A <see cref="BackgroundService"/> driven by <see cref="PeriodicTimer"/>: fully asynchronous (no
/// thread-pool thread blocked by Thread.Sleep or sync-over-async) and it stops when the host's
/// stopping token is cancelled. Reads already apply expiry lazily, so the sweeper only keeps Stock
/// current for products nobody is reading.
/// </remarks>
public sealed class ReservationExpirySweeper : BackgroundService
{
    private readonly IInventoryService _inventory;
    private readonly ILogger<ReservationExpirySweeper> _logger;
    private readonly TimeSpan _interval;

    public ReservationExpirySweeper(
        IInventoryService inventory,
        IOptions<ReservationExpiryOptions> options,
        ILogger<ReservationExpirySweeper> logger)
    {
        _inventory = inventory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.SweepIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _inventory.SweepExpiredAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Expiry sweep failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }
}
