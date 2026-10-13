using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProviderOptimizer.Infrastructure.Persistence;

/// <summary>
/// Lets the EF Core tooling build the model without a startup project, so migrations can be
/// generated before the API exists. The connection string is only needed by the tooling.
/// </summary>
public sealed class OptimizerDbContextFactory : IDesignTimeDbContextFactory<OptimizerDbContext>
{
    /// <summary>
    /// Placeholder for `dotnet ef` at design time ONLY. It points at the local development
    /// container and matches <c>docker-compose.yml</c>. It is never used at runtime: the API
    /// fails fast when <c>ConnectionStrings__OptimizerDb</c> is absent rather than falling back
    /// to this value. Design-time tooling would otherwise refuse to start without a database.
    /// </summary>
    private const string LocalDesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=provider_optimizer;Username=postgres;Password=postgres";

    public OptimizerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__OptimizerDb")
                ?? LocalDesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<OptimizerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OptimizerDbContext(options);
    }
}
