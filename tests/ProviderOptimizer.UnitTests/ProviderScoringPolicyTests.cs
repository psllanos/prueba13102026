using ProviderOptimizer.Domain;
using Xunit;

namespace ProviderOptimizer.UnitTests;

/// <summary>
/// Covers every scenario of the provider-scoring specification.
/// </summary>
public sealed class ProviderScoringPolicyTests
{
    private const string ProviderA = "11111111-1111-1111-1111-111111111111";
    private const string ProviderB = "22222222-2222-2222-2222-222222222222";
    private const string ProviderC = "33333333-3333-3333-3333-333333333333";

    private static readonly GeoPoint Origin = GeoPoint.Create(0d, 0d);
    private static readonly ScoringWeights DefaultWeights = ScoringWeights.Create(0.5d, 0.2d, 0.1d, 0.2d);

    private readonly ProviderScoringPolicy _policy = new();

    // Scenario: The best overall candidate wins
    [Fact]
    public void Best_overall_candidate_wins()
    {
        var nearFastWellRated = NewProvider(ProviderA, 0.01d, 0d, etaMinutes: 5, rating: 4.8d);
        var farSlowPoorlyRated = NewProvider(ProviderB, 0.5d, 0d, etaMinutes: 60, rating: 3.0d);

        // Deliberately worst-first: the result must not depend on input order.
        var result = _policy.Select([farSlowPoorlyRated, nearFastWellRated], Origin, DefaultWeights);

        Assert.NotNull(result.Selected);
        Assert.Equal(nearFastWellRated.Id, result.Selected.Id);
    }

    // Scenario: A better rating does not override a much worse ETA
    [Fact]
    public void Better_rating_does_not_override_a_much_worse_eta()
    {
        var nearLowerRated = NewProvider(ProviderA, 0.01d, 0d, etaMinutes: 5, rating: 2.0d);
        var farHigherRated = NewProvider(ProviderB, 0.5d, 0d, etaMinutes: 60, rating: 5.0d);

        // Higher-rated candidate listed first: the heavier ETA weight must still decide it.
        var result = _policy.Select([farHigherRated, nearLowerRated], Origin, DefaultWeights);

        Assert.NotNull(result.Selected);
        Assert.Equal(nearLowerRated.Id, result.Selected.Id);
    }

    // Scenario: The breakdown accounts for the total
    [Fact]
    public void Breakdown_contributions_sum_to_the_total()
    {
        Provider[] candidates =
        [
            NewProvider(ProviderA, 0.01d, 0d, etaMinutes: 5, rating: 4.8d),
            NewProvider(ProviderB, 0.10d, 0d, etaMinutes: 20, rating: 3.5d),
            NewProvider(ProviderC, 0.50d, 0d, etaMinutes: 60, rating: 2.0d)
        ];

        var result = _policy.Select(candidates, Origin, DefaultWeights);

        var breakdown = result.Breakdown;
        Assert.NotNull(breakdown);
        Assert.Equal(4, breakdown.Contributions.Count);
        Assert.Equal(
            new[] { "eta", "distance", "availability", "rating" },
            breakdown.Contributions.Select(contribution => contribution.Factor).ToArray());
        AssertClose(breakdown.Total, breakdown.Contributions.Sum(contribution => contribution.Contribution));
    }

    // Scenario: Identical scores produce the same winner
    [Fact]
    public void Identical_scores_produce_the_same_winner()
    {
        var first = NewProvider(ProviderA, 0.01d, 0d, etaMinutes: 10, rating: 4.0d);
        var second = NewProvider(ProviderB, 0.01d, 0d, etaMinutes: 10, rating: 4.0d);

        var firstRun = _policy.Select([first, second], Origin, DefaultWeights);
        var secondRun = _policy.Select([first, second], Origin, DefaultWeights);

        Assert.NotNull(firstRun.Selected);
        Assert.Equal(firstRun.Selected.Id, secondRun.Selected!.Id);
        Assert.Equal(first.Id, firstRun.Selected.Id);
    }

    // Scenario: Exactly one provider is returned
    [Fact]
    public void Exactly_one_provider_is_selected()
    {
        Provider[] candidates =
        [
            NewProvider(ProviderA, 0.01d, 0d, etaMinutes: 5, rating: 4.8d),
            NewProvider(ProviderB, 0.02d, 0d, etaMinutes: 8, rating: 4.5d),
            NewProvider(ProviderC, 0.03d, 0d, etaMinutes: 11, rating: 4.2d),
            NewProvider("44444444-4444-4444-4444-444444444444", 0.04d, 0d, etaMinutes: 14, rating: 4.0d),
            NewProvider("55555555-5555-5555-5555-555555555555", 0.05d, 0d, etaMinutes: 17, rating: 3.8d)
        ];

        var result = _policy.Select(candidates, Origin, DefaultWeights);

        Assert.True(result.HasSelection);
        Assert.NotNull(result.Selected);
        Assert.NotNull(result.Breakdown);
    }

    // Scenario: No candidates are available
    [Fact]
    public void Empty_candidate_set_selects_nothing()
    {
        var result = _policy.Select([], Origin, DefaultWeights);

        Assert.False(result.HasSelection);
        Assert.Null(result.Selected);
        Assert.Null(result.Breakdown);
    }

    // Scenario: All-zero weights are rejected
    [Fact]
    public void All_zero_weights_are_rejected()
        => Assert.Throws<ArgumentException>(() => ScoringWeights.Create(0d, 0d, 0d, 0d));

    [Fact]
    public void Negative_weights_are_rejected()
        => Assert.Throws<ArgumentException>(() => ScoringWeights.Create(0.5d, -0.1d, 0.1d, 0.2d));

    [Fact]
    public void Missing_weights_are_rejected()
        => Assert.Throws<ArgumentException>(() => ScoringWeights.FromConfiguration(0.5d, null, 0.1d, 0.2d));

    private static Provider NewProvider(
        string id,
        double latitude,
        double longitude,
        int etaMinutes,
        double rating,
        bool available = true)
        => Provider.Create(
            Guid.Parse(id),
            "provider-" + id[..4],
            ServiceType.Locksmith,
            GeoPoint.Create(latitude, longitude),
            etaMinutes,
            rating,
            available);

    private static void AssertClose(double expected, double actual, double tolerance = 1e-9d)
        => Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {expected} but got {actual}.");
}
