using ProviderOptimizer.Application.Ports;

namespace ProviderOptimizer.Infrastructure;

/// <summary>Default clock adapter. Time is injected so case timestamps stay assertable.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
