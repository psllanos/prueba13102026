namespace ProviderOptimizer.Application.Ports;

/// <summary>
/// The current time, as a port. Injected so case timestamps are assertable in tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
