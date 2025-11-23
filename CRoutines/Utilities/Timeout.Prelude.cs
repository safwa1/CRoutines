using System.Runtime.CompilerServices;
using Timeout = CRoutines.Utilities.Timeout;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> WithTimeout<T>(TimeSpan timeout, Func<Task<T>> operation)
        => Timeout.WithTimeout(timeout, operation);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WithTimeout(TimeSpan timeout, Func<Task> operation)
    => Timeout.WithTimeout(timeout, operation);
}