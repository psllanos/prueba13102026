using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Application;

/// <summary>
/// Lists the providers eligible for a request, without scoring or recording anything.
/// </summary>
public sealed class ListAvailableProvidersHandler
{
    private readonly IProviderRepository _providers;

    public ListAvailableProvidersHandler(IProviderRepository providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers;
    }

    public async Task<IReadOnlyList<Provider>> HandleAsync(
        AssistanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fetched = await _providers
            .FindByServiceTypeAsync(request.ServiceType, cancellationToken)
            .ConfigureAwait(false);

        return ProviderEligibility.Candidates(
            fetched,
            request.ServiceType,
            request.Location.Coordinates,
            request.SearchRadiusKm);
    }
}
