using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Proves the migration really produced the intended schema, including the structural guarantee
/// that a case cannot be stored without its decision record.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SchemaTests
{
    private readonly PostgresFixture _fixture;

    public SchemaTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_creates_both_tables()
    {
        await using var context = _fixture.CreateContext();

        var tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_name IN ('providers', 'assistance_cases')")
            .ToListAsync();

        Assert.Contains("providers", tables);
        Assert.Contains("assistance_cases", tables);
    }

    [Fact]
    public async Task The_decision_record_is_stored_as_a_required_jsonb_column_on_the_case()
    {
        await using var context = _fixture.CreateContext();

        var columnTypes = await context.Database
            .SqlQueryRaw<string>(
                "SELECT data_type FROM information_schema.columns " +
                "WHERE table_name = 'assistance_cases' AND column_name = 'breakdown'")
            .ToListAsync();

        Assert.Equal("jsonb", Assert.Single(columnTypes));

        var nullability = await context.Database
            .SqlQueryRaw<string>(
                "SELECT is_nullable FROM information_schema.columns " +
                "WHERE table_name = 'assistance_cases' AND column_name = 'breakdown'")
            .ToListAsync();

        // A case row without a decision is not representable, which is what the specification asks
        // for. This is stronger than a transactional guarantee, so no separate write can fail.
        Assert.Equal("NO", Assert.Single(nullability));
    }

    [Fact]
    public async Task Migrations_are_idempotent()
    {
        await using var context = _fixture.CreateContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);

        await context.Database.MigrateAsync();

        var reapplied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal(applied, reapplied);
    }
}
