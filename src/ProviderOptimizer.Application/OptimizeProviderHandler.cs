using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Application;

/// <summary>
/// Optimizes which provider should serve a request, then records the resulting case.
/// </summary>
public sealed class OptimizeProviderHandler
{
    private readonly IProviderRepository _providers;
    private readonly IAssistanceCaseRepository _cases;
    private readonly IProviderScoringPolicy _scoring;
    private readonly IClock _clock;
    private readonly ScoringWeights _weights;

    public OptimizeProviderHandler(
        IProviderRepository providers,
        IAssistanceCaseRepository cases,
        IProviderScoringPolicy scoring,
        IClock clock,
        ScoringWeights weights)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(weights);

        _providers = providers;
        _cases = cases;
        _scoring = scoring;
        _clock = clock;
        _weights = weights;
    }

    public async Task<OptimizeOutcome> HandleAsync(
        AssistanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var origin = request.Location.Coordinates;

        var fetched = await _providers
            .FindByServiceTypeAsync(request.ServiceType, cancellationToken)
            .ConfigureAwait(false);

        var candidates = ProviderEligibility.Candidates(
            fetched,
            request.ServiceType,
            origin,
            request.SearchRadiusKm);

        var selection = _scoring.Select(candidates, origin, _weights);

        if (!selection.HasSelection)
        {
            return OptimizeOutcome.NoProviderAvailable();
        }

        var provider = selection.Selected!;
        var breakdown = selection.Breakdown!;

        var assistanceCase = AssistanceCase.Create(
            Guid.NewGuid(),
            request.ServiceType,
            request.Location,
            provider.Id,
            breakdown,
            _clock.UtcNow);

        await _cases.SaveAsync(assistanceCase, cancellationToken).ConfigureAwait(false);

        return OptimizeOutcome.Selected(provider, breakdown, assistanceCase);
    }
}
