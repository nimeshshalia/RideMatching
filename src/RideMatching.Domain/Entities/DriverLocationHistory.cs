using RideMatching.Domain.ValueObjects;

namespace RideMatching.Domain.Entities;

public class DriverLocationHistory
{
    public long Id { get; private set; }
    public Guid DriverId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public GeoLocation Location => new(Latitude, Longitude);
    public double? SpeedKmh { get; private set; }
    public double? Bearing { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    protected DriverLocationHistory() { }

    public DriverLocationHistory(Guid driverId, GeoLocation location, double? speedKmh = null, double? bearing = null)
    {
        DriverId = driverId;
        ArgumentNullException.ThrowIfNull(location);
        Latitude = location.Latitude;
        Longitude = location.Longitude;
        SpeedKmh = speedKmh;
        Bearing = bearing;
        TimestampUtc = DateTime.UtcNow;
    }
}
