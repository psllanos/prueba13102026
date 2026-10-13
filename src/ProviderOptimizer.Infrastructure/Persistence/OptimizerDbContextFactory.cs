using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProviderOptimizer.Infrastructure.Persistence;

/// <summary>
/// Lets the EF Core tooling build the model without a startup project, so migrations can be
/// generated before the API exists. The connection string is only needed by the tooling, never
/// hard-coded here.
/// </summary>
public sealed class OptimizerDbContextFactory : IDesignTimeDbContextFactory<OptimizerDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=provider_optimizer;Username=postgres;Password=postgres";

    public OptimizerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__OptimizerDb") ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<OptimizerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OptimizerDbContext(options);
    }
}
