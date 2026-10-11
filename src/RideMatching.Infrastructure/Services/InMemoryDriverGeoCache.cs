using System.Collections.Concurrent;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Infrastructure.Services;

public class InMemoryDriverGeoCache : IDriverGeoCache
{
    private class DriverGeoEntry
    {
        public Guid DriverId { get; init; }
        public string Name { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public GeoLocation Location { get; set; } = null!;
        public DriverStatus Status { get; set; }
        public DateTime LastUpdateUtc { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, DriverGeoEntry> _drivers = new();

    public void UpdateDriver(
        Guid driverId,
        string name,
        string vehicleModel,
        string licensePlate,
        GeoLocation location,
        DriverStatus status)
    {
        _drivers.AddOrUpdate(
            driverId,
            _ => new DriverGeoEntry
            {
                DriverId = driverId,
                Name = name,
                VehicleModel = vehicleModel,
                LicensePlate = licensePlate,
                Location = location,
                Status = status,
                LastUpdateUtc = DateTime.UtcNow
            },
            (_, existing) =>
            {
                existing.Name = name;
                existing.VehicleModel = vehicleModel;
                existing.LicensePlate = licensePlate;
                existing.Location = location;
                existing.Status = status;
                existing.LastUpdateUtc = DateTime.UtcNow;
                return existing;
            });
    }

    public void UpdateLocation(Guid driverId, GeoLocation location)
    {
        if (_drivers.TryGetValue(driverId, out var entry))
        {
            entry.Location = location;
            entry.LastUpdateUtc = DateTime.UtcNow;
        }
    }

    public void UpdateStatus(Guid driverId, DriverStatus status)
    {
        if (_drivers.TryGetValue(driverId, out var entry))
        {
            entry.Status = status;
            entry.LastUpdateUtc = DateTime.UtcNow;
        }
    }

    public void RemoveDriver(Guid driverId)
    {
        _drivers.TryRemove(driverId, out _);
    }

    public DriverLocationSnapshot? GetDriver(Guid driverId)
    {
        if (_drivers.TryGetValue(driverId, out var entry))
        {
            return new DriverLocationSnapshot(
                entry.DriverId,
                new LocationDto(entry.Location.Latitude, entry.Location.Longitude),
                entry.Status,
                entry.LastUpdateUtc);
        }

        return null;
    }

    public IReadOnlyList<DriverCandidateDto> FindNearestAvailableDrivers(
        GeoLocation pickupLocation,
        double radiusKm,
        int limit,
        IReadOnlySet<Guid>? excludedDriverIds = null)
    {
        // Spatial Bounding Box Pre-Filter:
        // 1 degree latitude ~ 111 km
        double latDelta = radiusKm / 111.0;
        // 1 degree longitude ~ 111 * cos(latitude in radians) km
        double lonFactor = Math.Cos(pickupLocation.Latitude * (Math.PI / 180.0));
        double lonDelta = lonFactor > 0.001 ? radiusKm / (111.0 * lonFactor) : radiusKm / 111.0;

        double minLat = pickupLocation.Latitude - latDelta;
        double maxLat = pickupLocation.Latitude + latDelta;
        double minLon = pickupLocation.Longitude - lonDelta;
        double maxLon = pickupLocation.Longitude + lonDelta;

        var candidates = new List<DriverCandidateDto>();

        foreach (var entry in _drivers.Values)
        {
            if (entry.Status != DriverStatus.Available)
                continue;

            if (excludedDriverIds != null && excludedDriverIds.Contains(entry.DriverId))
                continue;

            var loc = entry.Location;
            // Bounding box rejection test
            if (loc.Latitude < minLat || loc.Latitude > maxLat || loc.Longitude < minLon || loc.Longitude > maxLon)
                continue;

            // Exact Haversine distance
            double distanceKm = pickupLocation.DistanceToKm(loc);
            if (distanceKm <= radiusKm)
            {
                int etaMinutes = Math.Max(1, (int)Math.Ceiling(distanceKm * 2.5)); // ~24 km/h urban speed
                candidates.Add(new DriverCandidateDto(
                    entry.DriverId,
                    entry.Name,
                    entry.VehicleModel,
                    entry.LicensePlate,
                    new LocationDto(loc.Latitude, loc.Longitude),
                    distanceKm,
                    etaMinutes));
            }
        }

        return candidates
            .OrderBy(c => c.DistanceKm)
            .Take(limit)
            .ToList();
    }

    public IReadOnlyList<DriverCandidateDto> GetAllOnlineDrivers()
    {
        return _drivers.Values
            .Select(d => new DriverCandidateDto(
                d.DriverId,
                d.Name,
                d.VehicleModel,
                d.LicensePlate,
                new LocationDto(d.Location.Latitude, d.Location.Longitude),
                0,
                0))
            .ToList();
    }
}
