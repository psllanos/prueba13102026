using System.Net;
using System.Net.Http.Json;
using ProviderOptimizer.Api.Contracts;
using Xunit;

namespace ProviderOptimizer.IntegrationTests;

/// <summary>
/// Drives <c>GET /providers/available</c> over real HTTP.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AvailableProvidersEndpointTests
{
    private readonly PostgresFixture _fixture;

    public AvailableProvidersEndpointTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Available_returns_only_eligible_providers_in_a_stable_order()
    {
        await _fixture.ResetAsync();

        var nearest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var furthest = Guid.NewGuid();
        var unavailable = Guid.NewGuid();
        var wrongType = Guid.NewGuid();
        var outOfRadius = Guid.NewGuid();

        await _fixture.SeedProvidersAsync(
            PostgresFixture.Provider(furthest, "tow-truck", latitude: 0.05d),
            PostgresFixture.Provider(nearest, "tow-truck", latitude: 0.01d),
            PostgresFixture.Provider(unavailable, "tow-truck", latitude: 0.02d, available: false),
            PostgresFixture.Provider(wrongType, "locksmith", latitude: 0.02d),
            PostgresFixture.Provider(outOfRadius, "tow-truck", latitude: 0.5d),
            PostgresFixture.Provider(middle, "tow-truck", latitude: 0.03d));

        var client = _fixture.ApiFactory.CreateClient();
        var response = await client.GetAsync(
            $"/providers/available?serviceType=tow-truck&latitude=0&longitude=0&radiusKm=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providers = await response.Content.ReadFromJsonAsync<List<ProviderPayload>>();
        Assert.NotNull(providers);

        var seeded = providers.Where(provider =>
            provider.Id == nearest || provider.Id == middle || provider.Id == furthest).ToList();

        Assert.Equal(3, seeded.Count);
        Assert.Equal([nearest, middle, furthest], seeded.Select(provider => provider.Id));

        Assert.All(seeded, provider => Assert.True(provider.Available));
        Assert.DoesNotContain(providers, provider => provider.Id == unavailable);
        Assert.DoesNotContain(providers, provider => provider.Id == wrongType);
        Assert.DoesNotContain(providers, provider => provider.Id == outOfRadius);
        Assert.All(providers, provider => Assert.Equal("tow-truck", provider.ServiceType));
    }

    [Fact]
    public async Task Available_returns_an_empty_array_when_nothing_matches()
    {
        await _fixture.ResetAsync();

        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.GetAsync(
            "/providers/available?serviceType=battery&latitude=0&longitude=0&radiusKm=0.001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var providers = await response.Content.ReadFromJsonAsync<List<ProviderPayload>>();
        Assert.NotNull(providers);
        Assert.Empty(providers);
    }

    [Fact]
    public async Task Available_returns_400_when_the_service_type_is_not_recognised()
    {
        var client = _fixture.ApiFactory.CreateClient();

        var response = await client.GetAsync(
            "/providers/available?serviceType=helicopter&latitude=0&longitude=0&radiusKm=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
