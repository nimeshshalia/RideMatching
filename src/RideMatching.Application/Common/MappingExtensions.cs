using RideMatching.Application.DTOs;
using RideMatching.Domain.Entities;

namespace RideMatching.Application.Common;

public static class MappingExtensions
{
    public static DriverDto ToDto(this Driver driver) =>
        new(
            driver.Id,
            driver.Name,
            driver.Email,
            driver.Phone,
            driver.VehicleModel,
            driver.LicensePlate,
            driver.Status,
            driver.CurrentLocation is not null ? new LocationDto(driver.CurrentLocation.Latitude, driver.CurrentLocation.Longitude) : null,
            driver.LastLocationUpdateUtc,
            driver.Rating,
            driver.CurrentRideId);

    public static RiderDto ToDto(this Rider rider) =>
        new(
            rider.Id,
            rider.Name,
            rider.Email,
            rider.Phone,
            rider.Rating);

    public static RideDto ToDto(this Ride ride, string riderName = "", string? driverName = null, string? vehicleModel = null, string? licensePlate = null) =>
        new(
            ride.Id,
            ride.RiderId,
            riderName,
            ride.DriverId,
            driverName,
            vehicleModel,
            licensePlate,
            new LocationDto(ride.PickupLocation.Latitude, ride.PickupLocation.Longitude),
            ride.PickupAddress,
            new LocationDto(ride.DropoffLocation.Latitude, ride.DropoffLocation.Longitude),
            ride.DropoffAddress,
            ride.Status,
            ride.EstimatedFare,
            ride.EstimatedDistanceKm,
            ride.CreatedAtUtc,
            ride.MatchedAtUtc,
            ride.AcceptedAtUtc,
            ride.StartedAtUtc,
            ride.CompletedAtUtc,
            ride.CancelledAtUtc,
            ride.CancellationReason,
            ride.CurrentOfferedDriverId,
            ride.OfferExpiresAtUtc,
            ride.OfferAttemptCount);
}
