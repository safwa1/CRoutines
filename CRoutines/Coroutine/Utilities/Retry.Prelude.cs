using System.Runtime.CompilerServices;
using RetryType = CRoutines.Coroutine.Utilities.Retry;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> Retry<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
        => RetryType.Execute(operation, maxAttempts, delayBetweenAttempts);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Retry(
        Func<Task> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
        => RetryType.Execute(operation, maxAttempts, delayBetweenAttempts);
}