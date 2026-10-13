using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Infrastructure.Persistence.Records;

/// <summary>
/// Relational shape of an assistance case. The decision record lives in the same row as a jsonb
/// column, so a persisted case physically cannot exist without its decision.
/// </summary>
public sealed class AssistanceCaseRecord
{
    public Guid CaseId { get; set; }

    public string ServiceType { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public Guid ProviderId { get; set; }

    public ScoreBreakdown Breakdown { get; set; } = new([]);

    public DateTimeOffset CreatedAt { get; set; }
}
