namespace ProviderOptimizer.Domain;

/// <summary>
/// Assistance service a provider can deliver. Closed set: an unknown value is a request error.
/// </summary>
public sealed record ServiceType
{
    public static readonly ServiceType TowTruck = new("tow-truck");
    public static readonly ServiceType Locksmith = new("locksmith");
    public static readonly ServiceType Battery = new("battery");

    private static readonly ServiceType[] All = [TowTruck, Locksmith, Battery];

    private ServiceType(string value) => Value = value;

    public string Value { get; }

    public static ServiceType Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"Unknown service type '{value}'.", nameof(value));
    }

    public override string ToString() => Value;
}

/// <summary>WGS-84 coordinate. Validated on creation so distance maths is always well defined.</summary>
public readonly record struct GeoPoint
{
    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static GeoPoint Create(double latitude, double longitude)
    {
        if (latitude is < -90d or > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180d or > 180d)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }

        return new GeoPoint(latitude, longitude);
    }

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public double DistanceKmTo(GeoPoint other)
    {
        const double EarthRadiusKm = 6371.0088d;

        var lat1 = double.DegreesToRadians(Latitude);
        var lat2 = double.DegreesToRadians(other.Latitude);
        var deltaLat = double.DegreesToRadians(other.Latitude - Latitude);
        var deltaLon = double.DegreesToRadians(other.Longitude - Longitude);

        var a = (Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d))
                + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLon / 2d) * Math.Sin(deltaLon / 2d));

        return 2d * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>
/// A resolved customer location. A location that cannot be resolved is not representable:
/// creation fails, which is how the "unresolvable location" requirement is enforced.
/// </summary>
public sealed record CustomerLocation
{
    private CustomerLocation(GeoPoint coordinates) => Coordinates = coordinates;

    public GeoPoint Coordinates { get; }

    public static CustomerLocation FromCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null || longitude is null)
        {
            throw new ArgumentException("Customer location could not be resolved: coordinates are required.");
        }

        return new CustomerLocation(GeoPoint.Create(latitude.Value, longitude.Value));
    }
}

/// <summary>An accepted assistance request, already validated.</summary>
public sealed record AssistanceRequest
{
    private AssistanceRequest(ServiceType serviceType, CustomerLocation location, double searchRadiusKm)
    {
        ServiceType = serviceType;
        Location = location;
        SearchRadiusKm = searchRadiusKm;
    }

    public ServiceType ServiceType { get; }

    public CustomerLocation Location { get; }

    public double SearchRadiusKm { get; }

    public static AssistanceRequest Create(ServiceType serviceType, CustomerLocation location, double searchRadiusKm)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(location);

        if (searchRadiusKm <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(searchRadiusKm), searchRadiusKm, "Search radius must be greater than zero.");
        }

        return new AssistanceRequest(serviceType, location, searchRadiusKm);
    }
}
