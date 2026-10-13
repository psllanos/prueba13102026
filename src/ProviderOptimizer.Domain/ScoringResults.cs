namespace ProviderOptimizer.Domain;

/// <summary>One factor's share of the selected provider's total score.</summary>
public sealed record ScoreFactorContribution(string Factor, double Raw, double Weight, double Contribution);

/// <summary>
/// The auditable reason a provider was selected. Contributions are already weight-normalised,
/// so they always sum to <see cref="Total"/>.
/// </summary>
public sealed record ScoreBreakdown
{
    public ScoreBreakdown(IReadOnlyList<ScoreFactorContribution> contributions)
        => Contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));

    public IReadOnlyList<ScoreFactorContribution> Contributions { get; }

    public double Total => Contributions.Sum(contribution => contribution.Contribution);
}

/// <summary>Outcome of scoring: either one provider, or an explicit no-selection.</summary>
public sealed record SelectionResult
{
    private SelectionResult(Provider? selected, ScoreBreakdown? breakdown)
    {
        Selected = selected;
        Breakdown = breakdown;
    }

    public Provider? Selected { get; }

    public ScoreBreakdown? Breakdown { get; }

    public bool HasSelection => Selected is not null;

    public static SelectionResult Of(Provider provider, ScoreBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(breakdown);

        return new SelectionResult(provider, breakdown);
    }

    public static SelectionResult None() => new(null, null);
}
