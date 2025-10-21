namespace CRoutines.Coroutine.Utilities;

public static class Delay
{
    public static Task For(TimeSpan duration, CancellationToken ct = default)
        => Task.Delay(duration, ct);

    public static Task For(int milliseconds, CancellationToken ct = default)
        => Task.Delay(milliseconds, ct);
}