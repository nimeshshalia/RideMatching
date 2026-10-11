using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RideMatching.Application.Interfaces;
using RideMatching.Application.Services;
using RideMatching.Infrastructure.BackgroundServices;
using RideMatching.Infrastructure.Data;
using RideMatching.Infrastructure.Services;

namespace RideMatching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ridematching.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // High-performance thread-safe in-memory geospatial cache (Singleton)
        services.AddSingleton<IDriverGeoCache, InMemoryDriverGeoCache>();

        // Asynchronous Bounded Channel Queue for ride matching tasks (Singleton)
        services.AddSingleton<IRideMatchingQueue, ChannelRideMatchingQueue>();

        // SignalR Realtime Notifier
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

        // Matching Engine
        services.AddScoped<IRideMatchingEngine, RideMatchingEngine>();

        // Domain & Application Services
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IRideService, RideService>();
        services.AddScoped<IRiderService, RiderService>();

        // Background workers
        services.AddHostedService<RideMatchingBackgroundService>();
        services.AddHostedService<OfferTimeoutBackgroundService>();

        return services;
    }
}
