using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProviderOptimizer.Application.Ports;
using ProviderOptimizer.Infrastructure.Persistence;

namespace ProviderOptimizer.Infrastructure;

/// <summary>Wires the persistence adapters into the composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOptimizerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<OptimizerDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IAssistanceCaseRepository, AssistanceCaseRepository>();

        return services;
    }
}
