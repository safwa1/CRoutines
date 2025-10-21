namespace CRoutines;

public static partial class Prelude
{
    public static Task<T> WithTimeout<T>(TimeSpan timeout, Func<Task<T>> operation)
        => Coroutine.Utilities.Timeout.WithTimeout(timeout, operation);

    public static Task WithTimeout(TimeSpan timeout, Func<Task> operation)
    => Coroutine.Utilities.Timeout.WithTimeout(timeout, operation);
}