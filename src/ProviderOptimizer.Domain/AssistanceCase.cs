namespace ProviderOptimizer.Domain;

/// <summary>
/// A recorded assistance case together with the provider decision made for it.
/// The decision is part of the same aggregate, so a persisted case can never exist without one.
/// </summary>
public sealed record AssistanceCase
{
    private AssistanceCase(
        Guid caseId,
        ServiceType serviceType,
        CustomerLocation location,
        Guid providerId,
        ScoreBreakdown breakdown,
        DateTimeOffset createdAt)
    {
        CaseId = caseId;
        ServiceType = serviceType;
        Location = location;
        ProviderId = providerId;
        Breakdown = breakdown;
        CreatedAt = createdAt;
    }

    public Guid CaseId { get; }

    public ServiceType ServiceType { get; }

    public CustomerLocation Location { get; }

    public Guid ProviderId { get; }

    /// <summary>The decision record: the reason this provider was selected.</summary>
    public ScoreBreakdown Breakdown { get; }

    public DateTimeOffset CreatedAt { get; }

    public static AssistanceCase Create(
        Guid caseId,
        ServiceType serviceType,
        CustomerLocation location,
        Guid providerId,
        ScoreBreakdown breakdown,
        DateTimeOffset createdAt)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("Case id is required.", nameof(caseId));
        }

        if (providerId == Guid.Empty)
        {
            throw new ArgumentException(
                "A case cannot be created without a selected provider.",
                nameof(providerId));
        }

        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(breakdown);

        return new AssistanceCase(caseId, serviceType, location, providerId, breakdown, createdAt);
    }
}
