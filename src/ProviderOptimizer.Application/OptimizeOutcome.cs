using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Application;

/// <summary>
/// Outcome of an optimization. Either a provider was selected and a case was recorded, or no
/// provider was available and nothing was persisted.
/// </summary>
public sealed record OptimizeOutcome
{
    private OptimizeOutcome(Provider? provider, ScoreBreakdown? breakdown, AssistanceCase? assistanceCase)
    {
        Provider = provider;
        Breakdown = breakdown;
        Case = assistanceCase;
    }

    public Provider? Provider { get; }

    public ScoreBreakdown? Breakdown { get; }

    public AssistanceCase? Case { get; }

    public bool HasSelection => Provider is not null && Case is not null;

    public static OptimizeOutcome NoProviderAvailable() => new(null, null, null);

    public static OptimizeOutcome Selected(Provider provider, ScoreBreakdown breakdown, AssistanceCase assistanceCase)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(assistanceCase);

        return new OptimizeOutcome(provider, breakdown, assistanceCase);
    }
}
