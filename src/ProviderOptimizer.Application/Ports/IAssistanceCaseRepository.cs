using ProviderOptimizer.Domain;

namespace ProviderOptimizer.Application.Ports;

/// <summary>
/// Persists an assistance case and its decision record as one unit of work.
/// </summary>
public interface IAssistanceCaseRepository
{
    /// <summary>
    /// Stores the case together with its decision record. The two must become visible together:
    /// a stored case without its decision is never a valid state.
    /// </summary>
    Task SaveAsync(AssistanceCase assistanceCase, CancellationToken cancellationToken = default);
}
