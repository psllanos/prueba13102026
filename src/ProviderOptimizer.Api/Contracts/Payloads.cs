namespace ProviderOptimizer.Api.Contracts;

/// <summary>Body of <c>POST /optimize</c>.</summary>
public sealed record OptimizeRequest
{
    public string? ServiceType { get; init; }

    public LocationPayload? Location { get; init; }

    public double? SearchRadiusKm { get; init; }
}

public sealed record LocationPayload(double? Latitude, double? Longitude);

public sealed record ScoreFactorPayload(string Factor, double Raw, double Weight, double Contribution);

public sealed record ProviderPayload(
    Guid Id,
    string Name,
    string ServiceType,
    double Latitude,
    double Longitude,
    double Rating,
    int EtaMinutes,
    bool Available);

/// <summary>Body of a successful <c>POST /optimize</c>.</summary>
public sealed record OptimizePayload(
    Guid CaseId,
    ProviderPayload SelectedProvider,
    double TotalScore,
    IReadOnlyList<ScoreFactorPayload> Breakdown,
    DateTimeOffset DecidedAt);

/// <summary>Body returned for a rejected or unsatisfiable request.</summary>
public sealed record ProblemPayload(string Error, string Detail);
