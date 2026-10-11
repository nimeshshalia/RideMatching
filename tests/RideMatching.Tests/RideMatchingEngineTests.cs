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

public class RideMatchingEngineTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task MatchRideAsync_ShouldMatchNearestAvailableDriverAndDispatchOffer()
    {
        using var context = CreateInMemoryDbContext();
        var geoCache = new InMemoryDriverGeoCache();
        var queue = new Mock<IRideMatchingQueue>();
        var notifier = new Mock<IRealtimeNotifier>();
        var logger = new Mock<ILogger<RideMatchingEngine>>();

        // Seed Rider
        var rider = new Rider("Alice", "alice@test.com", "555-0001");
        context.Riders.Add(rider);

        // Seed 2 Drivers (Driver 1 closer, Driver 2 farther)
        var driver1 = new Driver("Bob (Close)", "bob@test.com", "555-0002", "Tesla Model 3", "AAA111");
        var driver2 = new Driver("Charlie (Far)", "charlie@test.com", "555-0003", "Honda Civic", "BBB222");

        var pickup = new GeoLocation(37.7749, -122.4194);
        var closeLoc = new GeoLocation(37.7760, -122.4180); // ~0.17 km
        var farLoc = new GeoLocation(37.7850, -122.4080);   // ~1.5 km

        driver1.GoOnline(closeLoc);
        driver2.GoOnline(farLoc);

        context.Drivers.AddRange(driver1, driver2);

        // Seed Ride
        var dropoff = new GeoLocation(37.7900, -122.4000);
        var ride = new Ride(rider.Id, pickup, "Civic Center", dropoff, "Downtown");
        context.Rides.Add(ride);

        await context.SaveChangesAsync();

        // Populate Geo Cache
        geoCache.UpdateDriver(driver1.Id, driver1.Name, driver1.VehicleModel, driver1.LicensePlate, closeLoc, DriverStatus.Available);
        geoCache.UpdateDriver(driver2.Id, driver2.Name, driver2.VehicleModel, driver2.LicensePlate, farLoc, DriverStatus.Available);

        var engine = new RideMatchingEngine(context, geoCache, queue.Object, notifier.Object, logger.Object);

        // Act
        var matched = await engine.MatchRideAsync(ride.Id);

        // Assert
        matched.Should().BeTrue();

        var updatedRide = await context.Rides.FindAsync(ride.Id);
        updatedRide!.Status.Should().Be(RideStatus.OfferPending);
        updatedRide.CurrentOfferedDriverId.Should().Be(driver1.Id);

        var updatedDriver = await context.Drivers.FindAsync(driver1.Id);
        updatedDriver!.Status.Should().Be(DriverStatus.Offered);

        // Geo cache should reflect status
        geoCache.GetDriver(driver1.Id)!.Status.Should().Be(DriverStatus.Offered);

        // Notifier should have sent offer to driver1
        notifier.Verify(n => n.NotifyDriverRideOfferAsync(driver1.Id, It.IsAny<Application.DTOs.RideOfferDto>()), Times.Once);
    }

    [Fact]
    public async Task CheckAndProcessExpiredOffersAsync_ShouldReleaseDriverAndRequeueRide()
    {
        using var context = CreateInMemoryDbContext();
        var geoCache = new InMemoryDriverGeoCache();
        var queue = new Mock<IRideMatchingQueue>();
        var notifier = new Mock<IRealtimeNotifier>();
        var logger = new Mock<ILogger<RideMatchingEngine>>();

        var driver = new Driver("Dave", "dave@test.com", "555-0004", "Toyota", "CCC333");
        var loc = new GeoLocation(37.77, -122.41);
        driver.GoOnline(loc);
        context.Drivers.Add(driver);

        var rider = new Rider("Eve", "eve@test.com", "555-0005");
        context.Riders.Add(rider);

        var ride = new Ride(rider.Id, loc, "Pickup", new GeoLocation(37.78, -122.40), "Dropoff");
        context.Rides.Add(ride);
        await context.SaveChangesAsync();

        // Dispatch offer with negative expiration (already expired)
        driver.OfferRide(ride.Id);
        var offer = ride.DispatchOfferToDriver(driver.Id, TimeSpan.FromSeconds(-5));
        context.RideOffers.Add(offer);
        geoCache.UpdateDriver(driver.Id, driver.Name, driver.VehicleModel, driver.LicensePlate, loc, DriverStatus.Offered);
        await context.SaveChangesAsync();

        var engine = new RideMatchingEngine(context, geoCache, queue.Object, notifier.Object, logger.Object);

        // Act
        await engine.CheckAndProcessExpiredOffersAsync();

        // Assert
        var updatedDriver = await context.Drivers.FindAsync(driver.Id);
        updatedDriver!.Status.Should().Be(DriverStatus.Available);

        var updatedRide = await context.Rides.FindAsync(ride.Id);
        updatedRide!.Status.Should().Be(RideStatus.Matching);
        updatedRide.CurrentOfferedDriverId.Should().BeNull();

        // Should have re-enqueued the ride
        queue.Verify(q => q.EnqueueAsync(ride.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
