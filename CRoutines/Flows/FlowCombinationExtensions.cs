using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CRoutines.Flows;

/// <summary>
/// Flow operators for combining multiple flows
/// </summary>
public static class FlowCombinationExtensions
{
    /// <summary>
    /// Combines two flows by emitting whenever either flow emits (combines latest values)
    /// </summary>
    public static async IAsyncEnumerable<TResult> Combine<T1, T2, TResult>(
        this IAsyncEnumerable<T1> first,
        IAsyncEnumerable<T2> second,
        Func<T1, T2, TResult> combiner,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        T1? latest1 = default;
        T2? latest2 = default;
        var has1 = false;
        var has2 = false;

        var channel1 = Channel.CreateUnbounded<T1>();
        var channel2 = Channel.CreateUnbounded<T2>();

        var task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in first.WithCancellation(ct))
                {
                    await channel1.Writer.WriteAsync(item, ct);
                }
            }
            finally
            {
                channel1.Writer.Complete();
            }
        }, ct);

        var task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in second.WithCancellation(ct))
                {
                    await channel2.Writer.WriteAsync(item, ct);
                }
            }
            finally
            {
                channel2.Writer.Complete();
            }
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            var read1 = channel1.Reader.WaitToReadAsync(ct).AsTask();
            var read2 = channel2.Reader.WaitToReadAsync(ct).AsTask();

            var completed = await Task.WhenAny(read1, read2);

            if (completed == read1)
            {
                if (await read1 && channel1.Reader.TryRead(out var item1))
                {
                    latest1 = item1;
                    has1 = true;
                    if (has1 && has2)
                    {
                        yield return combiner(latest1!, latest2!);
                    }
                }
                else
                {
                    break;
                }
            }
            else
            {
                if (await read2 && channel2.Reader.TryRead(out var item2))
                {
                    latest2 = item2;
                    has2 = true;
                    if (has1 && has2)
                    {
                        yield return combiner(latest1!, latest2!);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        await Task.WhenAll(task1, task2);
    }

    /// <summary>
    /// Merges multiple flows into one
    /// </summary>
    public static async IAsyncEnumerable<T> Merge<T>(
        this IAsyncEnumerable<T> first,
        IEnumerable<IAsyncEnumerable<T>> others,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var allFlows = new[] { first }.Concat(others).ToArray();
        var channel = Channel.CreateUnbounded<T>();

        var producerTasks = allFlows.Select(flow => Task.Run(async () =>
        {
            await foreach (var item in flow.WithCancellation(ct))
            {
                await channel.Writer.WriteAsync(item, ct);
            }
        }, ct)).ToArray();

        var completionTask = Task.Run(async () =>
        {
            await Task.WhenAll(producerTasks);
            channel.Writer.Complete();
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        await completionTask;
    }

    /// <summary>
    /// Merges multiple flows into one (params overload)
    /// </summary>
    public static IAsyncEnumerable<T> Merge<T>(
        this IAsyncEnumerable<T> first,
        params IAsyncEnumerable<T>[] others)
    {
        return Merge(first, (IEnumerable<IAsyncEnumerable<T>>)others, CancellationToken.None);
    }

    /// <summary>
    /// FlatMaps and concatenates results sequentially
    /// </summary>
    public static async IAsyncEnumerable<TResult> FlatMapConcat<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TResult>> transform,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            await foreach (var inner in transform(item).WithCancellation(ct))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// FlatMaps and merges results concurrently
    /// </summary>
    public static async IAsyncEnumerable<TResult> FlatMapMerge<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TResult>> transform,
        int concurrency = 16,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<TResult>();
        var semaphore = new SemaphoreSlim(concurrency);
        var activeTasks = new List<Task>();

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await semaphore.WaitAsync(ct);
                    
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var inner in transform(item).WithCancellation(ct))
                            {
                                await channel.Writer.WriteAsync(inner, ct);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, ct);
                    
                    activeTasks.Add(task);
                }

                await Task.WhenAll(activeTasks);
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
    /// Flattens a flow of flows sequentially
    /// </summary>
    public static async IAsyncEnumerable<T> FlattenConcat<T>(
        this IAsyncEnumerable<IAsyncEnumerable<T>> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var innerFlow in source.WithCancellation(ct))
        {
            await foreach (var item in innerFlow.WithCancellation(ct))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Flattens a flow of flows concurrently
    /// </summary>
    public static async IAsyncEnumerable<T> FlattenMerge<T>(
        this IAsyncEnumerable<IAsyncEnumerable<T>> source,
        int concurrency = 16,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<T>();
        var semaphore = new SemaphoreSlim(concurrency);
        var activeTasks = new List<Task>();

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var innerFlow in source.WithCancellation(ct))
                {
                    await semaphore.WaitAsync(ct);
                    
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var item in innerFlow.WithCancellation(ct))
                            {
                                await channel.Writer.WriteAsync(item, ct);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, ct);
                    
                    activeTasks.Add(task);
                }

                await Task.WhenAll(activeTasks);
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
}
