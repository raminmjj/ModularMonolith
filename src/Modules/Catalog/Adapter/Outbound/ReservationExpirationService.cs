using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound;

/// <summary>
/// Background reaper for expired stock reservations (docs/adr/0005).
/// Without this, a crash between "reserve committed in Catalog" and
/// "order persisted in Orders" (or a lost cancel call) leaks ReservedStock
/// forever. Runs every minute; each cycle uses its own DI scope.
/// </summary>
internal sealed class ReservationExpirationService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpirationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reservation TTL reaper started (interval: {Interval})", Interval);
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                try { await ReapOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // Keep the host alive — the next cycle retries.
                    logger.LogError(ex, "Reservation reaper cycle failed; will retry next tick");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Reservation TTL reaper stopping");
        }
    }

    private async Task ReapOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<Application.Ports.Outbound.IProductRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<Application.Ports.Outbound.IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;
        var products = await repo.FindWithExpiredReservationsAsync(now, ct);
        if (products.Count == 0) return;

        foreach (var product in products)
        {
            var released = product.ReleaseExpiredReservations(now);
            foreach (var id in released)
                logger.LogInformation("Released expired reservation {ReservationId} on product {ProductId} ({Quantity} unit(s))",
                    id, product.Id, released.Count);
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Reservation reaper reclaimed expired reservations on {ProductCount} product(s)", products.Count);
    }
}
