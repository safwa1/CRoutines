using System.Threading.Channels;

namespace CRoutines.Channels;

/// <summary>
/// Scope for channel producers
/// </summary>
public interface IProducerScope<T>
{
    /// <summary>
    /// Sends a value to the channel
    /// </summary>
    Task Send(T value, CancellationToken ct = default);
    
    /// <summary>
    /// Closes the channel
    /// </summary>
    void Close(Exception? cause = null);
    
    /// <summary>
    /// Cancellation token for the producer
    /// </summary>
    CancellationToken CancellationToken { get; }
    
    /// <summary>
    /// Whether the channel is closed
    /// </summary>
    bool IsClosed { get; }
}

/// <summary>
/// Implementation of producer scope
/// </summary>
internal class ProducerScope<T> : IProducerScope<T>
{
    private readonly ChannelWriter<T> _writer;
    private readonly CancellationToken _cancellationToken;
    private bool _isClosed;
    private Exception? _closeException;

    public ProducerScope(ChannelWriter<T> writer, CancellationToken cancellationToken)
    {
        _writer = writer;
        _cancellationToken = cancellationToken;
    }

    public async Task Send(T value, CancellationToken ct = default)
    {
        if (_isClosed)
            throw new InvalidOperationException("Producer is closed");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, ct);
        await _writer.WriteAsync(value, cts.Token);
    }

    public void Close(Exception? cause = null)
    {
        if (!_isClosed)
        {
            _isClosed = true;
            _closeException = cause;
            
            if (cause != null)
                _writer.Complete(cause);
            else
                _writer.Complete();
        }
    }

    public CancellationToken CancellationToken => _cancellationToken;
    public bool IsClosed => _isClosed;

    internal void CompleteIfNotClosed()
    {
        if (!_isClosed)
        {
            _writer.Complete();
            _isClosed = true;
        }
    }
}
