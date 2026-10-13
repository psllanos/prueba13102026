using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Application.Ports;

/// <summary>
/// Reads providers from the store.
/// </summary>
public interface IProviderRepository
{
    /// <summary>
    /// Returns providers that could serve <paramref name="serviceType"/>. This is a coarse
    /// pre-filter only: availability, radius and candidate ordering are decided by
    /// <see cref="ProviderEligibility"/> so they stay testable without a database.
    /// </summary>
    Task<IReadOnlyCollection<Provider>> FindByServiceTypeAsync(
        ServiceType serviceType,
        CancellationToken cancellationToken = default);
}
