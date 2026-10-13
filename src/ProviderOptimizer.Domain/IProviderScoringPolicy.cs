namespace ProviderOptimizer.Domain;

/// <summary>
/// Ranks a candidate set into exactly one optimal provider.
/// A port so the application layer can depend on the decision without owning its rules.
/// </summary>
public interface IProviderScoringPolicy
{
    SelectionResult Select(IReadOnlyCollection<Provider> candidates, GeoPoint origin, ScoringWeights weights);
}
