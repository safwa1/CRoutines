using System.Threading.Channels;
using CRoutines.Core;

namespace CRoutines.Actors;

/// <summary>
/// Actor implementation
/// </summary>
internal class Actor<T> : IActor<T>
{
    private readonly Channel<T> _channel;
    private readonly Job _job;
    private bool _disposed;

    public Actor(Channel<T> channel, Job job)
    {
        _channel = channel;
        _job = job;
    }

    public async Task Send(T message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Actor<T>));

        await _channel.Writer.WriteAsync(message, _job.Cancellation.Token);
    }

    public bool TrySend(T message)
    {
        if (_disposed)
            return false;

        return _channel.Writer.TryWrite(message);
    }

    public void Close()
    {
        if (!_disposed)
        {
            _channel.Writer.Complete();
            _job.Cancel();
        }
    }

    public bool IsClosed => _disposed || _job.IsCancelled;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Close();
        }
    }
}