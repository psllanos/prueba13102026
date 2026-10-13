namespace ProviderOptimizer.Domain;

/// <summary>
/// Pure scoring policy. No I/O, no clock, no randomness: the same candidates, origin and weights
/// always produce the same selection.
/// </summary>
/// <remarks>
/// Each factor is normalised across the candidate set (min-max) and then weighted, so the score is
/// relative to the alternatives rather than an absolute quality measure. Ties are broken by
/// provider id, which keeps repeated identical requests stable.
/// </remarks>
public sealed class ProviderScoringPolicy : IProviderScoringPolicy
{
    public SelectionResult Select(IReadOnlyCollection<Provider> candidates, GeoPoint origin, ScoringWeights weights)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(weights);

        if (candidates.Count == 0)
        {
            return SelectionResult.None();
        }

        var etas = candidates.ToDictionary(provider => provider.Id, provider => (double)provider.EtaMinutes);
        var distances = candidates.ToDictionary(provider => provider.Id, provider => provider.Location.DistanceKmTo(origin));
        var ratings = candidates.ToDictionary(provider => provider.Id, provider => provider.Rating);

        var lowestEta = etas.Values.Min();
        var highestEta = etas.Values.Max();
        var shortestDistance = distances.Values.Min();
        var longestDistance = distances.Values.Max();
        var lowestRating = ratings.Values.Min();
        var highestRating = ratings.Values.Max();

        var best = candidates
            .Select(provider => Score(
                provider,
                etas,
                distances,
                ratings,
                weights,
                lowestEta,
                highestEta,
                shortestDistance,
                longestDistance,
                lowestRating,
                highestRating))
            .OrderByDescending(scored => scored.Breakdown.Total)
            .ThenBy(scored => scored.Provider.Id)
            .First();

        return SelectionResult.Of(best.Provider, best.Breakdown);
    }

    private static ScoredProvider Score(
        Provider provider,
        IReadOnlyDictionary<Guid, double> etas,
        IReadOnlyDictionary<Guid, double> distances,
        IReadOnlyDictionary<Guid, double> ratings,
        ScoringWeights weights,
        double lowestEta,
        double highestEta,
        double shortestDistance,
        double longestDistance,
        double lowestRating,
        double highestRating)
    {
        var eta = etas[provider.Id];
        var distance = distances[provider.Id];
        var rating = ratings[provider.Id];
        var availability = provider.IsAvailable ? 1d : 0d;

        var etaScore = LowerIsBetter(eta, lowestEta, highestEta);
        var distanceScore = LowerIsBetter(distance, shortestDistance, longestDistance);
        var ratingScore = HigherIsBetter(rating, lowestRating, highestRating);

        var weightTotal = weights.Total;

        var contributions = new List<ScoreFactorContribution>
        {
            Contribute("eta", eta, weights.Eta, etaScore, weightTotal),
            Contribute("distance", distance, weights.Distance, distanceScore, weightTotal),
            Contribute("availability", availability, weights.Availability, availability, weightTotal),
            Contribute("rating", rating, weights.Rating, ratingScore, weightTotal)
        };

        return new ScoredProvider(provider, new ScoreBreakdown(contributions));
    }

    private static ScoreFactorContribution Contribute(string factor, double raw, double weight, double score, double weightTotal)
        => new(factor, raw, weight, weight / weightTotal * score);

    private static double LowerIsBetter(double value, double lowest, double highest)
        => highest - lowest <= double.Epsilon ? 1d : (highest - value) / (highest - lowest);

    private static double HigherIsBetter(double value, double lowest, double highest)
        => highest - lowest <= double.Epsilon ? 1d : (value - lowest) / (highest - lowest);

    private sealed record ScoredProvider(Provider Provider, ScoreBreakdown Breakdown);
}
