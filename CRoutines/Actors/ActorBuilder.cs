using System.Threading.Channels;
using CRoutines.Contexts;
using CRoutines.Core;

namespace CRoutines.Actors;

/// <summary>
/// Actor interface - processes messages sequentially
/// </summary>
public interface IActor<T> : IDisposable
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

/// <summary>
/// Actor builders
/// </summary>
public static class ActorBuilder
{
    /// <summary>
    /// Creates an actor that processes messages sequentially
    /// Similar to Kotlin's actor
    /// </summary>
    public static IActor<T> Actor<T>(
        this CoroutineScope scope,
        Func<T, Task> handler,
        int capacity = 16)
    {
        var channel = capacity == int.MaxValue
            ? Channel.CreateUnbounded<T>()
            : Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        var job = scope.Launch(async ctx =>
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ctx.CancellationToken))
            {
                try
                {
                    await handler(message);
                }
                catch (Exception ex)
                {
                    // Log or handle actor errors
                    Console.WriteLine($"Actor error: {ex.Message}");
                }
            }
        });

        return new Actor<T>(channel, job);
    }

    /// <summary>
    /// Creates an actor with custom message processing
    /// </summary>
    public static IActor<T> Actor<T>(
        this CoroutineScope scope,
        Func<T, CancellationToken, Task> handler,
        int capacity = 16)
    {
        var channel = capacity == int.MaxValue
            ? Channel.CreateUnbounded<T>()
            : Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        var job = scope.Launch(async ctx =>
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ctx.CancellationToken))
            {
                try
                {
                    await handler(message, ctx.CancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"Actor error: {ex.Message}");
                }
            }
        });

        return new Actor<T>(channel, job);
    }
}
