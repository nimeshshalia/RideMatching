using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Interfaces;

namespace RideMatching.Infrastructure.BackgroundServices;

public class OfferTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferTimeoutBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    public OfferTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfferTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Offer Timeout Background Worker started (interval: 2s).");

        using var timer = new PeriodicTimer(CheckInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var matchingEngine = scope.ServiceProvider.GetRequiredService<IRideMatchingEngine>();
                await matchingEngine.CheckAndProcessExpiredOffersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking for expired ride offers.");
            }
        }

        _logger.LogInformation("Offer Timeout Background Worker stopping.");
    }
}
