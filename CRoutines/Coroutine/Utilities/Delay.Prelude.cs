using System.Runtime.CompilerServices;
using D = CRoutines.Coroutine.Utilities.Delay;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Delay(TimeSpan duration, CancellationToken ct = default)
        => D.For(duration, ct);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task Delay(int milliseconds, CancellationToken ct = default)
        => D.For(milliseconds, ct);
}