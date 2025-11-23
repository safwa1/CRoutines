using System.Runtime.CompilerServices;

namespace CRoutines.Flows;

public static class FlowExtensions
{
    #region Existing Operators

    public static async IAsyncEnumerable<TResult> Map<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, TResult> transform,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
            yield return transform(item);
    }

    public static async IAsyncEnumerable<T> Filter<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
            if (predicate(item))
                yield return item;
    }

    public static async IAsyncEnumerable<TResult> FlatMapLatest<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TResult>> transform,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            await foreach (var inner in transform(item).WithCancellation(ct))
                yield return inner;
        }
    }

    public static async IAsyncEnumerable<(T1, T2)> Zip<T1, T2>(
        this IAsyncEnumerable<T1> first,
        IAsyncEnumerable<T2> second,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var e1 = first.GetAsyncEnumerator(ct);
        await using var e2 = second.GetAsyncEnumerator(ct);

        while (await e1.MoveNextAsync() && await e2.MoveNextAsync())
            yield return (e1.Current, e2.Current);
    }

    public static async Task<List<T>> ToList<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct))
            list.Add(item);
        return list;
    }

    public static async Task<T?> FirstOrDefault<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
            return item;
        return default;
    }

    #endregion

    #region Error Handling Operators

    /// <summary>
    /// Catches exceptions from the upstream flow and allows recovery
    /// </summary>
    public static async IAsyncEnumerable<T> Catch<T>(
        this IAsyncEnumerable<T> source,
        Func<Exception, IAsyncEnumerable<T>> handler,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<T>();
        
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await foreach (var item in handler(ex).WithCancellation(ct))
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
    /// Retries the flow on exception up to maxAttempts times
    /// </summary>
    public static async IAsyncEnumerable<T> Retry<T>(
        this IAsyncEnumerable<T> source,
        int maxAttempts = 3,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<T>();
        
        var producerTask = Task.Run(async () =>
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    await foreach (var item in source.WithCancellation(ct))
                    {
                        await channel.Writer.WriteAsync(item, ct);
                    }
                    break; // Success
                }
                catch (Exception) when (attempt < maxAttempts - 1)
                {
                    await Task.Delay(100 * (attempt + 1), ct);
                }
            }
            
            channel.Writer.Complete();
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        await producerTask;
    }

    /// <summary>
    /// Retries the flow when predicate returns true
    /// </summary>
    public static async IAsyncEnumerable<T> RetryWhen<T>(
        this IAsyncEnumerable<T> source,
        Func<Exception, int, bool> shouldRetry,
        Func<Exception, int, TimeSpan>? delayFactory = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<T>();
        
        var producerTask = Task.Run(async () =>
        {
            int attempt = 0;
            
            while (true)
            {
                try
                {
                    await foreach (var item in source.WithCancellation(ct))
                    {
                        await channel.Writer.WriteAsync(item, ct);
                    }
                    break; // Success
                }
                catch (Exception ex)
                {
                    if (!shouldRetry(ex, attempt))
                    {
                        channel.Writer.Complete(ex);
                        throw;
                    }
                    
                    var delay = delayFactory?.Invoke(ex, attempt) 
                        ?? TimeSpan.FromMilliseconds(100 * (attempt + 1));
                    
                    await Task.Delay(delay, ct);
                    attempt++;
                }
            }
            
            channel.Writer.Complete();
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        try
        {
            await producerTask;
        }
        catch
        {
            throw;
        }
    }

    #endregion

    #region Lifecycle Operators

    /// <summary>
    /// Performs a side effect for each element
    /// </summary>
    public static async IAsyncEnumerable<T> OnEach<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task> action,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            await action(item);
            yield return item;
        }
    }

    /// <summary>
    /// Performs an action before the flow starts
    /// </summary>
    public static async IAsyncEnumerable<T> OnStart<T>(
        this IAsyncEnumerable<T> source,
        Func<Task> action,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await action();
        await foreach (var item in source.WithCancellation(ct))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Performs an action after completion
    /// </summary>
    public static async IAsyncEnumerable<T> OnCompletion<T>(
        this IAsyncEnumerable<T> source,
        Func<Exception?, Task> action,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<T>();
        Exception? caughtException = null;
        
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            catch (Exception ex)
            {
                caughtException = ex;
                throw;
            }
            finally
            {
                await action(caughtException);
                channel.Writer.Complete();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        try
        {
            await producerTask;
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Emits fallback value if flow is empty
    /// </summary>
    public static async IAsyncEnumerable<T> OnEmpty<T>(
        this IAsyncEnumerable<T> source,
        T fallbackValue,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var hasEmitted = false;
        
        await foreach (var item in source.WithCancellation(ct))
        {
            hasEmitted = true;
            yield return item;
        }

        if (!hasEmitted)
        {
            yield return fallbackValue;
        }
    }

    #endregion

    #region Transformation Operators

    /// <summary>
    /// Transforms elements using a custom collector
    /// </summary>
    public static async IAsyncEnumerable<TResult> Transform<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<IFlowCollector<TResult>, T, Task> transformer,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<TResult>();
        var collector = new FlowCollector<TResult>(channel.Writer);

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct))
                {
                    await transformer(collector, item);
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
    /// Accumulates values
    /// </summary>
    public static async IAsyncEnumerable<TAcc> Scan<T, TAcc>(
        this IAsyncEnumerable<T> source,
        TAcc initial,
        Func<TAcc, T, TAcc> accumulator,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var acc = initial;
        yield return acc;
        
        await foreach (var item in source.WithCancellation(ct))
        {
            acc = accumulator(acc, item);
            yield return acc;
        }
    }

    #endregion

    #region Filtering Operators

    /// <summary>
    /// Takes first n elements
    /// </summary>
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (count <= 0) yield break;
        
        var taken = 0;
        await foreach (var item in source.WithCancellation(ct))
        {
            yield return item;
            if (++taken >= count) break;
        }
    }

    /// <summary>
    /// Takes while predicate is true
    /// </summary>
    public static async IAsyncEnumerable<T> TakeWhile<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            if (!predicate(item)) break;
            yield return item;
        }
    }

    /// <summary>
    /// Drops first n elements
    /// </summary>
    public static async IAsyncEnumerable<T> Drop<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var dropped = 0;
        await foreach (var item in source.WithCancellation(ct))
        {
            if (dropped < count)
            {
                dropped++;
                continue;
            }
            yield return item;
        }
    }

    /// <summary>
    /// Drops while predicate is true
    /// </summary>
    public static async IAsyncEnumerable<T> DropWhile<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var dropping = true;
        await foreach (var item in source.WithCancellation(ct))
        {
            if (dropping && predicate(item))
                continue;
            
            dropping = false;
            yield return item;
        }
    }

    /// <summary>
    /// Emits only distinct consecutive elements
    /// </summary>
    public static async IAsyncEnumerable<T> DistinctUntilChanged<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : IEquatable<T>
    {
        T? previous = default;
        var isFirst = true;

        await foreach (var item in source.WithCancellation(ct))
        {
            if (isFirst || (previous != null && !previous.Equals(item)) || (previous == null && item != null))
            {
                yield return item;
                previous = item;
                isFirst = false;
            }
        }
    }

    #endregion

    #region Terminal Operators

    /// <summary>
    /// Returns single element
    /// </summary>
    public static async Task<T> Single<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        
        if (!await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Sequence contains no elements");
        
        var result = enumerator.Current;
        
        if (await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Sequence contains more than one element");
        
        return result;
    }

    /// <summary>
    /// Returns single element or default
    /// </summary>
    public static async Task<T?> SingleOrDefault<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        
        if (!await enumerator.MoveNextAsync())
            return default;
        
        var result = enumerator.Current;
        
        if (await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Sequence contains more than one element");
        
        return result;
    }

    /// <summary>
    /// Reduces to single value
    /// </summary>
    public static async Task<T> Reduce<T>(
        this IAsyncEnumerable<T> source,
        Func<T, T, T> accumulator,
        CancellationToken ct = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        
        if (!await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Sequence contains no elements");
        
        var result = enumerator.Current;
        
        while (await enumerator.MoveNextAsync())
        {
            result = accumulator(result, enumerator.Current);
        }
        
        return result;
    }

    /// <summary>
    /// Folds with initial value
    /// </summary>
    public static async Task<TAcc> Fold<T, TAcc>(
        this IAsyncEnumerable<T> source,
        TAcc initial,
        Func<TAcc, T, TAcc> accumulator,
        CancellationToken ct = default)
    {
        var result = initial;
        await foreach (var item in source.WithCancellation(ct))
        {
            result = accumulator(result, item);
        }
        return result;
    }

    /// <summary>
    /// Counts elements
    /// </summary>
    public static async Task<int> Count<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var count = 0;
        await foreach (var _ in source.WithCancellation(ct))
        {
            count++;
        }
        return count;
    }

    #endregion
}