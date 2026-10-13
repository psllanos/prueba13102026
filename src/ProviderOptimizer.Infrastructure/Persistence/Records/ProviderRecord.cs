namespace ProviderOptimizer.Infrastructure.Persistence.Records;

/// <summary>Relational shape of a provider. Kept separate from the domain entity.</summary>
public sealed class ProviderRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ServiceType { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int EtaMinutes { get; set; }

    public double Rating { get; set; }

    public bool IsAvailable { get; set; }
}
