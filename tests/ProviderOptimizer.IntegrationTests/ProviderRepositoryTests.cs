using ProviderOptimizer.Domain;
using ProviderOptimizer.Infrastructure.Persistence;
using ProviderOptimizer.Infrastructure.Persistence.Records;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Exercises the EF Core provider adapter against a real Postgres, so the SQL, the migrations and
/// the domain mapping are all proven together rather than against a fake.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProviderRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public ProviderRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FindByServiceTypeAsync_returns_only_the_requested_service_type()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var locksmithId = Guid.NewGuid();
        var towTruckId = Guid.NewGuid();

        await using var seeding = _fixture.CreateContext();
        seeding.Providers.AddRange(
            NewRecord(locksmithId, $"locksmith-{suffix}", "locksmith"),
            NewRecord(towTruckId, $"tow-truck-{suffix}", "tow-truck"));
        await seeding.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repository = new ProviderRepository(context);

        var found = await repository.FindByServiceTypeAsync(ServiceType.Locksmith);

        var matches = found.Where(provider => provider.Id == locksmithId).ToList();
        var match = Assert.Single(matches);
        Assert.Equal(ServiceType.Locksmith, match.ServiceType);
        Assert.DoesNotContain(found, provider => provider.Id == towTruckId);
    }

    [Fact]
    public async Task FindByServiceTypeAsync_round_trips_every_field()
    {
        var id = Guid.NewGuid();

        await using var seeding = _fixture.CreateContext();
        seeding.Providers.Add(new ProviderRecord
        {
            Id = id,
            Name = "round-trip",
            ServiceType = "battery",
            Latitude = -34.6037d,
            Longitude = -58.3816d,
            EtaMinutes = 17,
            Rating = 4.25d,
            IsAvailable = false
        });
        await seeding.SaveChangesAsync();

        await using var context = _fixture.CreateContext();
        var repository = new ProviderRepository(context);

        var found = await repository.FindByServiceTypeAsync(ServiceType.Battery);
        var matches = found.Where(candidate => candidate.Id == id).ToList();
        var provider = Assert.Single(matches);

        Assert.Equal("round-trip", provider.Name);
        Assert.Equal(-34.6037d, provider.Location.Latitude, precision: 9);
        Assert.Equal(-58.3816d, provider.Location.Longitude, precision: 9);
        Assert.Equal(17, provider.EtaMinutes);
        Assert.Equal(4.25d, provider.Rating, precision: 9);
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public async Task FindByServiceTypeAsync_returns_an_empty_set_when_nothing_matches()
    {
        await using var context = _fixture.CreateContext();
        var repository = new ProviderRepository(context);

        var found = await repository.FindByServiceTypeAsync(ServiceType.Parse("tow-truck"));

        Assert.DoesNotContain(found, provider => provider.ServiceType != ServiceType.TowTruck);
    }

    private static ProviderRecord NewRecord(Guid id, string name, string serviceType)
        => new()
        {
            Id = id,
            Name = name,
            ServiceType = serviceType,
            Latitude = 0d,
            Longitude = 0d,
            EtaMinutes = 10,
            Rating = 4d,
            IsAvailable = true
        };
}
