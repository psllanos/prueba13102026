namespace ProviderOptimizer.Domain;

/// <summary>
/// An assistance provider that can be considered for a request.
/// </summary>
public sealed record Provider
{
    private Provider(
        Guid id,
        string name,
        ServiceType serviceType,
        GeoPoint location,
        int etaMinutes,
        double rating,
        bool isAvailable)
    {
        Id = id;
        Name = name;
        ServiceType = serviceType;
        Location = location;
        EtaMinutes = etaMinutes;
        Rating = rating;
        IsAvailable = isAvailable;
    }

    public Guid Id { get; }

    public string Name { get; }

    public ServiceType ServiceType { get; }

    public GeoPoint Location { get; }

    /// <summary>Announced time to arrival. Simulated in this service; a live feed is out of scope.</summary>
    public int EtaMinutes { get; }

    public double Rating { get; }

    public bool IsAvailable { get; }

    public static Provider Create(
        Guid id,
        string name,
        ServiceType serviceType,
        GeoPoint location,
        int etaMinutes,
        double rating,
        bool isAvailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(serviceType);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Provider id is required.", nameof(id));
        }

        if (etaMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(etaMinutes), etaMinutes, "ETA must not be negative.");
        }

        if (rating is < 0d or > 5d)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Rating must be between 0 and 5.");
        }

        return new Provider(id, name, serviceType, location, etaMinutes, rating, isAvailable);
    }
}
