namespace ProviderOptimizer.Domain;

/// <summary>
/// Relative importance of each scoring factor. Invalid configurations are rejected on creation
/// so a request can never be scored with a degenerate weight set.
/// </summary>
public sealed record ScoringWeights
{
    private ScoringWeights(double eta, double distance, double availability, double rating)
    {
        Eta = eta;
        Distance = distance;
        Availability = availability;
        Rating = rating;
    }

    public double Eta { get; }

    public double Distance { get; }

    public double Availability { get; }

    public double Rating { get; }

    public double Total => Eta + Distance + Availability + Rating;

    public static ScoringWeights Create(double eta, double distance, double availability, double rating)
    {
        if (eta < 0d || distance < 0d || availability < 0d || rating < 0d)
        {
            throw new ArgumentException("Scoring weights must not be negative.");
        }

        if (eta == 0d && distance == 0d && availability == 0d && rating == 0d)
        {
            throw new ArgumentException("At least one scoring weight must be greater than zero.");
        }

        return new ScoringWeights(eta, distance, availability, rating);
    }

    /// <summary>
    /// Binds weights that may be absent from configuration. A missing weight is a configuration
    /// error, not a silent zero.
    /// </summary>
    public static ScoringWeights FromConfiguration(double? eta, double? distance, double? availability, double? rating)
    {
        if (eta is null || distance is null || availability is null || rating is null)
        {
            throw new ArgumentException("Every scoring weight must be configured.");
        }

        return Create(eta.Value, distance.Value, availability.Value, rating.Value);
    }
}
