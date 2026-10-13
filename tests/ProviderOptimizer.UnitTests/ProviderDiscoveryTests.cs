using ProviderOptimizer.Domain;
using Xunit;

namespace ProviderOptimizer.UnitTests;

/// <summary>
/// Covers every scenario of the provider-discovery specification.
/// </summary>
public sealed class ProviderDiscoveryTests
{
    private const string ProviderA = "11111111-1111-1111-1111-111111111111";
    private const string ProviderB = "22222222-2222-2222-2222-222222222222";
    private const string ProviderC = "33333333-3333-3333-3333-333333333333";

    /// <summary>Roughly 4.0 km north of the origin, so comfortably inside a 10 km radius.</summary>
    private const double InsideRadiusLatitude = 0.036d;

    /// <summary>Roughly 25.0 km north of the origin, so comfortably outside a 10 km radius.</summary>
    private const double OutsideRadiusLatitude = 0.225d;

    private const double RadiusKm = 10d;

    private static readonly GeoPoint Origin = GeoPoint.Create(0d, 0d);

    // Scenario: Only matching service types are returned
    [Fact]
    public void Only_matching_service_types_are_returned()
    {
        Provider[] providers =
        [
            NewProvider(ProviderA, ServiceType.TowTruck, InsideRadiusLatitude),
            NewProvider(ProviderB, ServiceType.Locksmith, InsideRadiusLatitude),
            NewProvider(ProviderC, ServiceType.Battery, InsideRadiusLatitude)
        ];

        var candidates = ProviderEligibility.Candidates(providers, ServiceType.Locksmith, Origin, RadiusKm);

        var candidate = Assert.Single(candidates);
        Assert.Equal(ServiceType.Locksmith, candidate.ServiceType);
    }

    // Scenario: No provider offers the requested service type
    [Fact]
    public void No_provider_offering_the_service_type_yields_an_empty_candidate_set()
    {
        Provider[] providers =
        [
            NewProvider(ProviderA, ServiceType.TowTruck, InsideRadiusLatitude),
            NewProvider(ProviderB, ServiceType.Locksmith, InsideRadiusLatitude)
        ];

        var candidates = ProviderEligibility.Candidates(providers, ServiceType.Battery, Origin, RadiusKm);

        Assert.Empty(candidates);
    }

    // Scenario: Provider inside the radius
    [Fact]
    public void Provider_inside_the_radius_is_a_candidate()
    {
        Provider[] providers = [NewProvider(ProviderA, ServiceType.Locksmith, InsideRadiusLatitude)];

        var candidates = ProviderEligibility.Candidates(providers, ServiceType.Locksmith, Origin, RadiusKm);

        Assert.Single(candidates);
    }

    // Scenario: Provider outside the radius
    [Fact]
    public void Provider_outside_the_radius_is_not_a_candidate()
    {
        Provider[] providers = [NewProvider(ProviderA, ServiceType.Locksmith, OutsideRadiusLatitude)];

        var candidates = ProviderEligibility.Candidates(providers, ServiceType.Locksmith, Origin, RadiusKm);

        Assert.Empty(candidates);
    }

    // Scenario: Unavailable provider is excluded
    [Fact]
    public void Unavailable_provider_is_excluded()
    {
        var available = NewProvider(ProviderA, ServiceType.Locksmith, InsideRadiusLatitude);
        var unavailable = NewProvider(ProviderB, ServiceType.Locksmith, InsideRadiusLatitude, available: false);

        var candidates = ProviderEligibility.Candidates(
            [available, unavailable],
            ServiceType.Locksmith,
            Origin,
            RadiusKm);

        var candidate = Assert.Single(candidates);
        Assert.Equal(available.Id, candidate.Id);
    }

    // Scenario: Unresolvable location is rejected
    [Fact]
    public void Unresolvable_customer_location_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CustomerLocation.FromCoordinates(null, null));
        Assert.Throws<ArgumentException>(() => CustomerLocation.FromCoordinates(InsideRadiusLatitude, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomerLocation.FromCoordinates(120d, 0d));
    }

    // Scenario: Repeated request yields identical order
    [Fact]
    public void Repeated_request_yields_identical_order()
    {
        Provider[] providers =
        [
            NewProvider(ProviderA, ServiceType.Locksmith, 0.05d),
            NewProvider(ProviderB, ServiceType.Locksmith, 0.01d),
            NewProvider(ProviderC, ServiceType.Locksmith, 0.03d)
        ];

        var first = ProviderEligibility.Candidates(providers, ServiceType.Locksmith, Origin, RadiusKm);
        var shuffled = providers.Reverse().ToArray();
        var second = ProviderEligibility.Candidates(shuffled, ServiceType.Locksmith, Origin, RadiusKm);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Select(provider => provider.Id), second.Select(provider => provider.Id));

        // Nearest first: the order is meaningful, not incidental.
        Assert.Equal(Guid.Parse(ProviderB), first[0].Id);
    }

    private static Provider NewProvider(
        string id,
        ServiceType serviceType,
        double latitude,
        bool available = true)
        => Provider.Create(
            Guid.Parse(id),
            "provider-" + id[..4],
            serviceType,
            GeoPoint.Create(latitude, 0d),
            etaMinutes: 10,
            rating: 4d,
            isAvailable: available);
}
