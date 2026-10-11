using FluentAssertions;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;
using RideMatching.Infrastructure.Services;
using Xunit;

namespace RideMatching.Tests;

public class InMemoryDriverGeoCacheTests
{
    [Fact]
    public void FindNearestAvailableDrivers_ShouldReturnClosestDriversInAscendingOrder()
    {
        var cache = new InMemoryDriverGeoCache();
        var pickup = new GeoLocation(37.7749, -122.4194); // SF City Hall

        var closeDriverId = Guid.NewGuid();
        var farDriverId = Guid.NewGuid();
        var unavailableDriverId = Guid.NewGuid();

        // 0.3 km away
        cache.UpdateDriver(closeDriverId, "Driver Close", "Prius", "111", new GeoLocation(37.7765, -122.4172), DriverStatus.Available);
        // 2.5 km away
        cache.UpdateDriver(farDriverId, "Driver Far", "Camry", "222", new GeoLocation(37.7890, -122.4014), DriverStatus.Available);
        // 0.1 km away but Busy
        cache.UpdateDriver(unavailableDriverId, "Driver Busy", "Tesla", "333", new GeoLocation(37.7750, -122.4190), DriverStatus.Busy);

        var nearest = cache.FindNearestAvailableDrivers(pickup, radiusKm: 5.0, limit: 10);

        nearest.Should().HaveCount(2);
        nearest[0].DriverId.Should().Be(closeDriverId);
        nearest[1].DriverId.Should().Be(farDriverId);
        nearest[0].DistanceKm.Should().BeLessThan(nearest[1].DistanceKm);
    }

    [Fact]
    public void FindNearestAvailableDrivers_WithExcludedDrivers_ShouldOmitExcluded()
    {
        var cache = new InMemoryDriverGeoCache();
        var pickup = new GeoLocation(37.7749, -122.4194);

        var driver1 = Guid.NewGuid();
        var driver2 = Guid.NewGuid();

        cache.UpdateDriver(driver1, "Driver 1", "Tesla", "111", new GeoLocation(37.7755, -122.4180), DriverStatus.Available);
        cache.UpdateDriver(driver2, "Driver 2", "Honda", "222", new GeoLocation(37.7770, -122.4160), DriverStatus.Available);

        var excluded = new HashSet<Guid> { driver1 };
        var nearest = cache.FindNearestAvailableDrivers(pickup, radiusKm: 5.0, limit: 10, excludedDriverIds: excluded);

        nearest.Should().HaveCount(1);
        nearest[0].DriverId.Should().Be(driver2);
    }

    [Fact]
    public void RemoveDriver_ShouldRemoveFromSpatialCache()
    {
        var cache = new InMemoryDriverGeoCache();
        var driverId = Guid.NewGuid();

        cache.UpdateDriver(driverId, "Driver", "Tesla", "111", new GeoLocation(37.7755, -122.4180), DriverStatus.Available);
        cache.GetDriver(driverId).Should().NotBeNull();

        cache.RemoveDriver(driverId);
        cache.GetDriver(driverId).Should().BeNull();
    }
}
