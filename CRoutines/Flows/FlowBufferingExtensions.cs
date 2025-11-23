using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CRoutines.Flows;

/// <summary>
/// Advanced Flow operators for buffering, debouncing, and rate limiting
/// </summary>
public static class FlowBufferingExtensions
{
    /// <summary>
    /// Buffers emissions with the specified capacity
    /// </summary>
    public static async IAsyncEnumerable<T> Buffer<T>(
        this IAsyncEnumerable<T> source,
        int capacity,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        await producerTask;
    }

    /// <summary>
    /// Conflates emissions - keeps only the latest when consumer is slow
    /// </summary>
    public static async IAsyncEnumerable<T> Conflate<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        await producerTask;
    }

    /// <summary>
    /// Debounces emissions - emits only after a quiet period
    /// </summary>
    public static async IAsyncEnumerable<T> Debounce<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        T? latestValue = default;
        var hasValue = false;
        var lastEmission = DateTime.MinValue;

        await using var enumerator = source.GetAsyncEnumerator(ct);
        
        while (true)
        {
            var moveTask = enumerator.MoveNextAsync();
            
            if (hasValue)
            {
                var timeElapsed = DateTime.UtcNow - lastEmission;
                var remaining = timeout - timeElapsed;
                
                if (remaining > TimeSpan.Zero)
                {
                    var completedTask = await Task.WhenAny(
                        moveTask.AsTask(),
                        Task.Delay(remaining, ct));
                    
                    if (completedTask == moveTask.AsTask())
                    {
                        if (await moveTask)
                        {
                            latestValue = enumerator.Current;
                            lastEmission = DateTime.UtcNow;
                            hasValue = true;
                        }
                        else
                        {
                            yield return latestValue!;
                            break;
                        }
                    }
                    else
                    {
                        yield return latestValue!;
                        hasValue = false;
                    }
                }
                else
                {
                    yield return latestValue!;
                    hasValue = false;
                }
            }
            else
            {
                if (await moveTask)
                {
                    latestValue = enumerator.Current;
                    lastEmission = DateTime.UtcNow;
                    hasValue = true;
                }
                else
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Samples emissions at regular intervals
    /// </summary>
    public static async IAsyncEnumerable<T> Sample<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan period,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        T? latestValue = default;
        var hasValue = false;
        var channel = Channel.CreateUnbounded<T>();

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    latestValue = item;
                    hasValue = true;
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        using var timer = new PeriodicTimer(period);
        
        while (!ct.IsCancellationRequested)
        {
            var timerTask = timer.WaitForNextTickAsync(ct).AsTask();
            var completedTask = await Task.WhenAny(timerTask, producerTask);
            
            if (completedTask == producerTask)
            {
                if (hasValue)
                {
                    yield return latestValue!;
                }
                break;
            }
            
            if (await timerTask && hasValue)
            {
                yield return latestValue!;
                hasValue = false;
            }
        }

        await producerTask;
    }
}
