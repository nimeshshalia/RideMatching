using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Entities;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;
using RideMatching.Infrastructure.Data;
using RideMatching.Infrastructure.Services;
using Xunit;

namespace RideMatching.Tests;

public class ConcurrencyHandlingTests
{
    [Fact]
    public async Task ConcurrentMatches_ForSameDriver_PreventsDoubleBooking()
    {
        // Use a shared SQLite in-memory database to test real concurrency locks
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var initContext = new AppDbContext(options))
            {
                initContext.Database.EnsureCreated();

                var driver = new Driver("Solo Driver", "solo@test.com", "555-1111", "Hyundai", "CONCUR1");
                var loc = new GeoLocation(37.7749, -122.4194);
                driver.GoOnline(loc);
                initContext.Drivers.Add(driver);

                var rider1 = new Rider("Rider 1", "r1@test.com", "555-2222");
                var rider2 = new Rider("Rider 2", "r2@test.com", "555-3333");
                initContext.Riders.AddRange(rider1, rider2);

                var ride1 = new Ride(rider1.Id, loc, "Pickup 1", new GeoLocation(37.78, -122.41), "Dropoff 1");
                var ride2 = new Ride(rider2.Id, loc, "Pickup 2", new GeoLocation(37.79, -122.40), "Dropoff 2");
                initContext.Rides.AddRange(ride1, ride2);

                await initContext.SaveChangesAsync();
            }

            var geoCache = new InMemoryDriverGeoCache();
            using (var readContext = new AppDbContext(options))
            {
                var driver = await readContext.Drivers.FirstAsync();
                geoCache.UpdateDriver(driver.Id, driver.Name, driver.VehicleModel, driver.LicensePlate, driver.CurrentLocation!, DriverStatus.Available);
            }

            var queue = new Mock<IRideMatchingQueue>();
            var notifier = new Mock<IRealtimeNotifier>();
            var logger = new Mock<ILogger<RideMatchingEngine>>();

            // Two distinct DbContext scopes simulating concurrent requests
            using var context1 = new AppDbContext(options);
            using var context2 = new AppDbContext(options);

            var engine1 = new RideMatchingEngine(context1, geoCache, queue.Object, notifier.Object, logger.Object);
            var engine2 = new RideMatchingEngine(context2, geoCache, queue.Object, notifier.Object, logger.Object);

            Guid ride1Id, ride2Id;
            using (var readContext = new AppDbContext(options))
            {
                var rides = await readContext.Rides.ToListAsync();
                ride1Id = rides[0].Id;
                ride2Id = rides[1].Id;
            }

            // Run matching for ride 1 and ride 2 concurrently
            var task1 = engine1.MatchRideAsync(ride1Id);
            var task2 = engine2.MatchRideAsync(ride2Id);

            var results = await Task.WhenAll(task1, task2);

            // Exactly ONE of them should successfully acquire the driver offer
            int successCount = results.Count(r => r);
            successCount.Should().Be(1, "Only one ride can be offered to the solitary available driver");

            // Verify driver is in Offered state and not duplicated
            using (var verifyContext = new AppDbContext(options))
            {
                var verifyDriver = await verifyContext.Drivers.FirstAsync();
                verifyDriver.Status.Should().Be(DriverStatus.Offered);

                var r1 = await verifyContext.Rides.FindAsync(ride1Id);
                var r2 = await verifyContext.Rides.FindAsync(ride2Id);

                // One should be OfferPending, the other either Requested/Matching/NoDrivers
                var offerPendingCount = (r1!.Status == RideStatus.OfferPending ? 1 : 0) + (r2!.Status == RideStatus.OfferPending ? 1 : 0);
                offerPendingCount.Should().Be(1);
            }
        }
        finally
        {
            connection.Close();
        }
    }
}
