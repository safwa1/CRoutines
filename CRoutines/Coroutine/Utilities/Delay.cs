using System.Runtime.CompilerServices;

namespace CRoutines.Coroutine.Utilities;

public static class Delay
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task For(TimeSpan duration, CancellationToken ct = default)
        => Task.Delay(duration, ct);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task For(int milliseconds, CancellationToken ct = default)
        => Task.Delay(milliseconds, ct);
}