using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Infrastructure.Persistence;
using ProviderOptimizer.Infrastructure.Persistence.Records;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Starts one throwaway Postgres per test collection and applies the real migrations to it.
/// Nothing here touches the Neon instance: integration tests never share a remote database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("provider_optimizer")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private OptimizerApiFactory? _apiFactory;

    /// <summary>Hosts the real application against this collection's container.</summary>
    public OptimizerApiFactory ApiFactory => _apiFactory ??= new OptimizerApiFactory(ConnectionString);

    public async Task DisposeAsync()
    {
        _apiFactory?.Dispose();
        await _container.DisposeAsync();
    }

    public OptimizerDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OptimizerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    public async Task SeedProvidersAsync(params ProviderRecord[] providers)
    {
        await using var context = CreateContext();
        context.Providers.AddRange(providers);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Empties both tables. Every test in the collection shares one container, so a test that
    /// depends on the exact contents of the store must start from a known-empty one.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE assistance_cases, providers");
    }

    public static ProviderRecord Provider(
        Guid id,
        string serviceType,
        double latitude,
        int etaMinutes = 10,
        double rating = 4d,
        bool available = true)
        => new()
        {
            Id = id,
            Name = $"seed-{id:N}",
            ServiceType = serviceType,
            Latitude = latitude,
            Longitude = 0d,
            EtaMinutes = etaMinutes,
            Rating = rating,
            IsAvailable = available
        };
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
