using Microsoft.EntityFrameworkCore;
using ProviderOptimizer.Infrastructure.Persistence;
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

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public OptimizerDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OptimizerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
