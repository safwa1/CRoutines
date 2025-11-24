using System.Threading.Channels;
using CRoutines.Contexts;

namespace CRoutines.Actors;

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
