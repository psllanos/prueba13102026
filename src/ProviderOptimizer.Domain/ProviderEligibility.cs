namespace ProviderOptimizer.Domain;

/// <summary>
/// Decides which providers are eligible candidates for a request, before any ranking happens.
/// Pure and I/O-free so every eligibility rule is unit-testable without a database.
/// </summary>
/// <remarks>
/// The repository performs only a coarse pre-filter. Service type, availability, radius and the
/// stable candidate order are decided here, which keeps the rules in one verifiable place rather
/// than split between SQL and application code.
/// </remarks>
public static class ProviderEligibility
{
    public static IReadOnlyList<Provider> Candidates(
        IEnumerable<Provider> providers,
        ServiceType serviceType,
        GeoPoint origin,
        double radiusKm)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusKm);

        return providers
            .Where(provider => provider.ServiceType == serviceType && provider.IsAvailable)
            .Select(provider => new Candidate(provider, provider.Location.DistanceKmTo(origin)))
            .Where(candidate => candidate.DistanceKm <= radiusKm)
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.Provider.EtaMinutes)
            .ThenBy(candidate => candidate.Provider.Id)
            .Select(candidate => candidate.Provider)
            .ToList();
    }

    private sealed record Candidate(Provider Provider, double DistanceKm);
}
