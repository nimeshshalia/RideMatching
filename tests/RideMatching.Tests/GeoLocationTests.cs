using FluentAssertions;
using RideMatching.Domain.ValueObjects;
using Xunit;

namespace RideMatching.Tests;

public class GeoLocationTests
{
    [Fact]
    public void DistanceToKm_ShouldCalculateAccurateHaversineDistance()
    {
        // San Francisco Civic Center: (37.7765, -122.4172)
        // SF Union Square: (37.7879, -122.4074)
        var civicCenter = new GeoLocation(37.7765, -122.4172);
        var unionSquare = new GeoLocation(37.7879, -122.4074);

        double distance = civicCenter.DistanceToKm(unionSquare);

        // Distance is approx 1.52 km
        distance.Should().BeInRange(1.4, 1.6);
    }

    [Fact]
    public void DistanceToKm_SameLocation_ShouldBeZero()
    {
        var loc1 = new GeoLocation(37.7749, -122.4194);
        var loc2 = new GeoLocation(37.7749, -122.4194);

        loc1.DistanceToKm(loc2).Should().Be(0.0);
    }

    [Fact]
    public void BearingTo_ShouldCalculateCompassBearingCorrectly()
    {
        // True North
        var origin = new GeoLocation(0.0, 0.0);
        var north = new GeoLocation(10.0, 0.0);
        var east = new GeoLocation(0.0, 10.0);

        origin.BearingTo(north).Should().BeApproximately(0.0, 0.5);
        origin.BearingTo(east).Should().BeApproximately(90.0, 0.5);
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public void GeoLocation_InvalidCoordinates_ShouldThrowArgumentOutOfRangeException(double lat, double lon)
    {
        var act = () => new GeoLocation(lat, lon);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
