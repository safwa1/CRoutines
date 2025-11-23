namespace CRoutines.Testing;

/// <summary>
/// Abstraction for time operations to enable virtual time in tests
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Current time
    /// </summary>
    TimeSpan CurrentTime { get; }
    
    /// <summary>
    /// Delay for the specified duration
    /// </summary>
    Task Delay(TimeSpan duration, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delay for the specified milliseconds
    /// </summary>
    Task Delay(int milliseconds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time provider using actual system time
/// </summary>
public sealed class RealTimeProvider : ITimeProvider
{
    public static readonly RealTimeProvider Instance = new();
    
    private RealTimeProvider() { }
    
    public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(Environment.TickCount64);
    
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken = default)
        => Task.Delay(duration, cancellationToken);
    
    public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
        => Task.Delay(milliseconds, cancellationToken);
}
