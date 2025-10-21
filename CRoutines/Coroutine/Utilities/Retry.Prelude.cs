

using CRoutines.Coroutine.Utilities;

namespace CRoutines;

public static partial class Prelude
{
    public static Task<T> Execute<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
        => Retry.Execute(operation, maxAttempts, delayBetweenAttempts);

    public static Task Execute(
        Func<Task> operation,
        int maxAttempts = 3,
        TimeSpan? delayBetweenAttempts = null)
        => Retry.Execute(operation, maxAttempts, delayBetweenAttempts);
}