using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence.Records;

namespace ProviderOptimizer.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class ProviderRepository : IProviderRepository
{
    private readonly OptimizerDbContext _context;

    public ProviderRepository(OptimizerDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyCollection<Provider>> FindByServiceTypeAsync(
        ServiceType serviceType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var serviceTypeValue = serviceType.Value;

        var records = await _context
            .Providers
            .AsNoTracking()
            .Where(record => record.ServiceType == serviceTypeValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToDomain).ToList();
    }

    private static Provider ToDomain(ProviderRecord record)
        => Provider.Create(
            record.Id,
            record.Name,
            ServiceType.Parse(record.ServiceType),
            GeoPoint.Create(record.Latitude, record.Longitude),
            record.EtaMinutes,
            record.Rating,
            record.IsAvailable);
}
