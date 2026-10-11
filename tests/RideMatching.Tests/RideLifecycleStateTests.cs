using FluentAssertions;
using RideMatching.Domain.Entities;
using RideMatching.Domain.Enums;
using RideMatching.Domain.Exceptions;
using RideMatching.Domain.ValueObjects;
using Xunit;

namespace RideMatching.Tests;

public class RideLifecycleStateTests
{
    [Fact]
    public void CreateRide_ShouldInitializeWithCalculatedFareAndDistance()
    {
        var pickup = new GeoLocation(37.7765, -122.4172);
        var dropoff = new GeoLocation(37.7879, -122.4074);
        var riderId = Guid.NewGuid();

        var ride = new Ride(riderId, pickup, "Civic Center", dropoff, "Union Square");

        ride.Status.Should().Be(RideStatus.Requested);
        ride.EstimatedDistanceKm.Should().BeInRange(1.4, 1.6);
        ride.EstimatedFare.Should().BeGreaterThan(5.00m); // Above minimum fare
        ride.Offers.Should().BeEmpty();
    }

    [Fact]
    public void DispatchOffer_And_Accept_ShouldTransitionRideAndRecordOffer()
    {
        var pickup = new GeoLocation(37.7765, -122.4172);
        var dropoff = new GeoLocation(37.7879, -122.4074);
        var ride = new Ride(Guid.NewGuid(), pickup, "Civic Center", dropoff, "Union Square");
        var driverId = Guid.NewGuid();

        // Dispatch offer
        ride.DispatchOfferToDriver(driverId, TimeSpan.FromSeconds(15));
        ride.Status.Should().Be(RideStatus.OfferPending);
        ride.CurrentOfferedDriverId.Should().Be(driverId);
        ride.Offers.Should().HaveCount(1);
        ride.Offers.First().Status.Should().Be(OfferStatus.Pending);

        // Driver accepts
        ride.AcceptByDriver(driverId);
        ride.Status.Should().Be(RideStatus.Accepted);
        ride.DriverId.Should().Be(driverId);
        ride.CurrentOfferedDriverId.Should().BeNull();
        ride.Offers.First().Status.Should().Be(OfferStatus.Accepted);
    }

    [Fact]
    public void FullTripProgression_ShouldCompleteSuccessfully()
    {
        var pickup = new GeoLocation(37.7765, -122.4172);
        var dropoff = new GeoLocation(37.7879, -122.4074);
        var ride = new Ride(Guid.NewGuid(), pickup, "Pickup", dropoff, "Dropoff");
        var driverId = Guid.NewGuid();

        ride.DispatchOfferToDriver(driverId, TimeSpan.FromSeconds(15));
        ride.AcceptByDriver(driverId);

        ride.MarkDriverArriving();
        ride.Status.Should().Be(RideStatus.DriverArriving);

        ride.StartTrip();
        ride.Status.Should().Be(RideStatus.InProgress);
        ride.StartedAtUtc.Should().NotBeNull();

        ride.CompleteTrip();
        ride.Status.Should().Be(RideStatus.Completed);
        ride.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void CompleteTrip_WhenNotInProgress_ShouldThrowInvalidStateTransitionException()
    {
        var pickup = new GeoLocation(37.7765, -122.4172);
        var dropoff = new GeoLocation(37.7879, -122.4074);
        var ride = new Ride(Guid.NewGuid(), pickup, "Pickup", dropoff, "Dropoff");

        var act = () => ride.CompleteTrip();
        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Driver_GoOffline_WhileBusy_ShouldThrowInvalidStateTransitionException()
    {
        var driver = new Driver("John", "john@test.com", "555-1234", "Prius", "XYZ");
        driver.GoOnline(new GeoLocation(37.77, -122.41));
        var rideId = Guid.NewGuid();

        driver.OfferRide(rideId);
        driver.AcceptRide(rideId);
        driver.Status.Should().Be(DriverStatus.Busy);

        var act = () => driver.GoOffline();
        act.Should().Throw<InvalidStateTransitionException>();
    }
}
