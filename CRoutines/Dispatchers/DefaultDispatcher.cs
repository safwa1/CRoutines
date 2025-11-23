namespace CRoutines.Dispatchers;

/// <summary>
/// Dispatches to ThreadPool (like Dispatchers.Default)
/// </summary>
public sealed class DefaultDispatcher : ICoroutineDispatcher
{
    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
        => Task.Run(() => work(ct), ct);

    public static DefaultDispatcher Instance { get; } = new();
}