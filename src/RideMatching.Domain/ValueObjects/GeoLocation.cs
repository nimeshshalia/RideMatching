namespace RideMatching.Domain.ValueObjects;

public record GeoLocation
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    // Required for EF Core parameterless constructor
    protected GeoLocation() { }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude is < -90.0 or > 90.0)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90 degrees.");

        if (longitude is < -180.0 or > 180.0)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180 degrees.");

        Latitude = Math.Round(latitude, 6);
        Longitude = Math.Round(longitude, 6);
    }

    /// <summary>
    /// Calculates great-circle distance between two locations in Kilometers using the Haversine formula.
    /// </summary>
    public double DistanceToKm(GeoLocation other)
    {
        const double EarthRadiusKm = 6371.0;

        double dLat = ToRadians(other.Latitude - Latitude);
        double dLon = ToRadians(other.Longitude - Longitude);

        double lat1 = ToRadians(Latitude);
        double lat2 = ToRadians(other.Latitude);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(EarthRadiusKm * c, 3);
    }

    /// <summary>
    /// Calculates initial compass bearing in degrees (0 to 360) from this location to destination.
    /// </summary>
    public double BearingTo(GeoLocation destination)
    {
        double lat1 = ToRadians(Latitude);
        double lat2 = ToRadians(destination.Latitude);
        double dLon = ToRadians(destination.Longitude - Longitude);

        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) -
                   Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        double initialBearing = Math.Atan2(y, x);
        return (ToDegrees(initialBearing) + 360.0) % 360.0;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
    private static double ToDegrees(double radians) => radians * (180.0 / Math.PI);

    public override string ToString() => $"({Latitude}, {Longitude})";
}
