using RideMatching.Domain.Enums;

namespace RideMatching.Application.DTOs;

public record CreateRideRequestDto(
    Guid RiderId,
    LocationDto PickupLocation,
    string PickupAddress,
    LocationDto DropoffLocation,
    string DropoffAddress);

public record RideDto(
    Guid Id,
    Guid RiderId,
    string RiderName,
    Guid? DriverId,
    string? DriverName,
    string? VehicleModel,
    string? LicensePlate,
    LocationDto PickupLocation,
    string PickupAddress,
    LocationDto DropoffLocation,
    string DropoffAddress,
    RideStatus Status,
    decimal EstimatedFare,
    double EstimatedDistanceKm,
    DateTime RequestedAtUtc,
    DateTime? MatchedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    Guid? CurrentOfferedDriverId,
    DateTime? OfferExpiresAtUtc,
    int OfferAttemptCount);

public record RideOfferDto(
    Guid RideId,
    Guid DriverId,
    Guid RiderId,
    string RiderName,
    LocationDto PickupLocation,
    string PickupAddress,
    LocationDto DropoffLocation,
    string DropoffAddress,
    double DistanceToPickupKm,
    int EstimatedEtaToPickupMinutes,
    decimal EstimatedFare,
    DateTime ExpiresAtUtc,
    int SecondsRemaining);

public record FareEstimateDto(
    double DistanceKm,
    decimal EstimatedFare,
    int EstimatedDurationMinutes);

public record SystemMetricsDto(
    int TotalOnlineDrivers,
    int TotalAvailableDrivers,
    int TotalActiveRides,
    int TotalCompletedRides,
    int TotalPendingRequests);
