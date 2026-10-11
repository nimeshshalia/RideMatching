using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Interfaces;

namespace RideMatching.Infrastructure.BackgroundServices;

public class RideMatchingBackgroundService : BackgroundService
{
    private readonly IRideMatchingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RideMatchingBackgroundService> _logger;

    public RideMatchingBackgroundService(
        IRideMatchingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<RideMatchingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ride Matching Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var rideId = await _queue.DequeueAsync(stoppingToken);

                // Run match in a fire-and-forget scoped task to maintain high channel throughput
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var matchingEngine = scope.ServiceProvider.GetRequiredService<IRideMatchingEngine>();
                        await matchingEngine.MatchRideAsync(rideId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing ride matching for Ride {RideId}", rideId);
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Ride Matching Background Worker loop.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Ride Matching Background Worker stopping.");
    }
}
