using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Entities;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, IDriverGeoCache geoCache, ILogger logger)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Drivers.AnyAsync())
        {
            // Re-populate in-memory cache with all existing online drivers
            var activeDrivers = await context.Drivers.Where(d => d.Status != Domain.Enums.DriverStatus.Offline).ToListAsync();
            foreach (var d in activeDrivers)
            {
                if (d.CurrentLocation != null)
                {
                    geoCache.UpdateDriver(d.Id, d.Name, d.VehicleModel, d.LicensePlate, d.CurrentLocation, d.Status);
                }
            }
            return;
        }

        logger.LogInformation("Seeding initial drivers and riders...");

        // San Francisco coordinates reference center: (37.7749, -122.4194)
        var drivers = new List<Driver>
        {
            new("Alex Johnson", "alex@rideshare.dev", "+1-415-555-0101", "Tesla Model 3 (White)", "7ABC123"),
            new("Samira Patel", "samira@rideshare.dev", "+1-415-555-0102", "Toyota Camry (Silver)", "8XYZ789"),
            new("Marcus Chen", "marcus@rideshare.dev", "+1-415-555-0103", "Honda Civic (Black)", "6DEF456"),
            new("Elena Gomez", "elena@rideshare.dev", "+1-415-555-0104", "Hyundai Ioniq 5 (Blue)", "9GHI999"),
            new("David Kim", "david@rideshare.dev", "+1-415-555-0105", "Ford Mustang Mach-E (Red)", "5JKL321")
        };

        // Seed initial locations in different neighborhood clusters of SF
        var locations = new[]
        {
            new GeoLocation(37.7765, -122.4172), // Civic Center (~0.25 km)
            new GeoLocation(37.7840, -122.4075), // Union Square (~1.4 km)
            new GeoLocation(37.7891, -122.4014), // Financial District (~2.2 km)
            new GeoLocation(37.7690, -122.4467), // Buena Vista (~2.5 km)
            new GeoLocation(37.7599, -122.4148)  // Mission District (~1.7 km)
        };

        for (int i = 0; i < drivers.Count; i++)
        {
            drivers[i].GoOnline(locations[i]);
            context.Drivers.Add(drivers[i]);
        }

        var riders = new List<Rider>
        {
            new("Sarah Connor", "sarah@example.com", "+1-415-555-1001"),
            new("James Holden", "james@example.com", "+1-415-555-1002"),
            new("Ada Lovelace", "ada@example.com", "+1-415-555-1003")
        };

        context.Riders.AddRange(riders);

        await context.SaveChangesAsync();

        // Populate Geo Cache
        foreach (var driver in drivers)
        {
            geoCache.UpdateDriver(
                driver.Id,
                driver.Name,
                driver.VehicleModel,
                driver.LicensePlate,
                driver.CurrentLocation!,
                driver.Status);
        }

        logger.LogInformation("Database seeded successfully with {DriverCount} drivers and {RiderCount} riders.", drivers.Count, riders.Count);
    }
}
