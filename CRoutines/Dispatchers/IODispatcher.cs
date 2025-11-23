namespace CRoutines.Dispatchers;

/// <summary>
/// IO-optimized dispatcher (like Dispatchers.IO)
/// </summary>
public sealed class IODispatcher : ICoroutineDispatcher
{
    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
        => Task.Factory.StartNew(() => work(ct), ct, 
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    public static IODispatcher Instance { get; } = new();
}