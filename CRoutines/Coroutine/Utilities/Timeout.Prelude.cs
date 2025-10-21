using System.Runtime.CompilerServices;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> WithTimeout<T>(TimeSpan timeout, Func<Task<T>> operation)
        => Coroutine.Utilities.Timeout.WithTimeout(timeout, operation);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WithTimeout(TimeSpan timeout, Func<Task> operation)
    => Coroutine.Utilities.Timeout.WithTimeout(timeout, operation);
}