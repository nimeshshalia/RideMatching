using RideMatching.Domain.Enums;

namespace RideMatching.Application.DTOs;

public record LocationDto(double Latitude, double Longitude);

public record CreateDriverRequestDto(
    string Name,
    string Email,
    string Phone,
    string VehicleModel,
    string LicensePlate);

public record DriverDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string VehicleModel,
    string LicensePlate,
    DriverStatus Status,
    LocationDto? CurrentLocation,
    DateTime? LastLocationUpdateUtc,
    double Rating,
    Guid? CurrentRideId);

public record UpdateLocationDto(
    double Latitude,
    double Longitude,
    double? SpeedKmh = null,
    double? Bearing = null);

public record DriverCandidateDto(
    Guid DriverId,
    string Name,
    string VehicleModel,
    string LicensePlate,
    LocationDto Location,
    double DistanceKm,
    int EstimatedEtaMinutes);

public record DriverLocationSnapshot(
    Guid DriverId,
    LocationDto Location,
    DriverStatus Status,
    DateTime TimestampUtc);
