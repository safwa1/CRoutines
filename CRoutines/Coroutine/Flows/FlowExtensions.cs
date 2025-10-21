using System.Runtime.CompilerServices;

namespace CRoutines.Coroutine.Flows;

public static class FlowExtensions
{
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
}