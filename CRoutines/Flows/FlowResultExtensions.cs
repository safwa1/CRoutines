using CRoutines.Core;

namespace CRoutines.Flows;

/// <summary>
/// Result-based Flow operators
/// </summary>
public static class FlowResultExtensions
{
    /// <summary>
    /// Maps a flow to Result, catching exceptions
    /// </summary>
    public static async IAsyncEnumerable<Result<T>> CatchToResult<T>(
        this IAsyncEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        
        while (true)
        {
            Result<T> result;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;
                    
                result = Result<T>.Success(enumerator.Current);
            }
            catch (Exception ex)
            {
                result = Result<T>.Failure(ex);
            }
            
            yield return result;
        }
    }

    /// <summary>
    /// Filters only successful results
    /// </summary>
    public static async IAsyncEnumerable<T> OnlySuccess<T>(
        this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var result in source.WithCancellation(ct))
        {
            if (result.IsSuccess)
            {
                yield return result.Value;
            }
        }
    }

    /// <summary>
    /// Filters only failed results
    /// </summary>
    public static async IAsyncEnumerable<Exception> OnlyFailures<T>(
        this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var result in source.WithCancellation(ct))
        {
            if (result.IsFailure)
            {
                yield return result.Error;
            }
        }
    }

    /// <summary>
    /// Unwraps results, throwing on first failure
    /// </summary>
    public static async IAsyncEnumerable<T> Unwrap<T>(
        this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var result in source.WithCancellation(ct))
        {
            yield return result.Value; // Throws if failure
        }
    }

    /// <summary>
    /// Retries with Result return instead of throwing
    /// </summary>
    public static async Task<Result<T>> RetryAsResult<T>(
        this Func<Task<T>> operation,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < maxAttempts - 1)
                {
                    await Task.Delay(100 * (attempt + 1), ct);
                }
            }
        }
        
        return Result<T>.Failure(lastException!);
    }
}
