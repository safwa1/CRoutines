namespace CRoutines.Actors;

/// <summary>
/// Actor interface - processes messages sequentially
/// </summary>
public interface IActor<in T> : IDisposable
{
    /// <summary>
    /// Sends a message to the actor
    /// </summary>
    Task Send(T message);
    
    /// <summary>
    /// Offers a message without blocking
    /// </summary>
    bool TrySend(T message);
    
    /// <summary>
    /// Closes the actor
    /// </summary>
    void Close();
    
    /// <summary>
    /// Whether the actor is closed
    /// </summary>
    bool IsClosed { get; }
}