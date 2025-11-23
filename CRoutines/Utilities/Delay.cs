using System.Runtime.CompilerServices;
using CRoutines.Testing;

namespace CRoutines.Utilities;

public static class Delay
{
    private static readonly AsyncLocal<ITimeProvider?> _timeProvider = new();
    
    /// <summary>
    /// Gets or sets the current time provider (for testing)
    /// </summary>
    public static ITimeProvider TimeProvider
    {
        get => _timeProvider.Value ?? RealTimeProvider.Instance;
        set => _timeProvider.Value = value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task For(TimeSpan duration, CancellationToken ct = default)
        => TimeProvider.Delay(duration, ct);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task For(int milliseconds, CancellationToken ct = default)
        => TimeProvider.Delay(milliseconds, ct);
}