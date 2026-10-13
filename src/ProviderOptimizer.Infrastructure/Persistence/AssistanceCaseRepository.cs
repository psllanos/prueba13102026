using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence.Records;

namespace ProviderOptimizer.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class AssistanceCaseRepository : IAssistanceCaseRepository
{
    private readonly OptimizerDbContext _context;

    public AssistanceCaseRepository(OptimizerDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task SaveAsync(AssistanceCase assistanceCase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assistanceCase);

        var record = new AssistanceCaseRecord
        {
            CaseId = assistanceCase.CaseId,
            ServiceType = assistanceCase.ServiceType.Value,
            Latitude = assistanceCase.Location.Coordinates.Latitude,
            Longitude = assistanceCase.Location.Coordinates.Longitude,
            ProviderId = assistanceCase.ProviderId,
            Breakdown = assistanceCase.Breakdown,
            CreatedAt = assistanceCase.CreatedAt
        };

        _context.AssistanceCases.Add(record);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
