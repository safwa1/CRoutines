namespace CRoutines.Dispatchers;

/// <summary>
/// WinUI 3 UI thread dispatcher
/// Usage: new WinUIDispatcher(DispatcherQueue.GetForCurrentThread())
/// </summary>
public sealed class WinUIDispatcher : ICoroutineDispatcher
{
    private readonly dynamic _dispatcherQueue;

    public WinUIDispatcher(object dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromCanceled(ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcherQueue.TryEnqueue(new Action(async () =>
        {
            try
            {
                await work(ct);
                tcs.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));

        return tcs.Task;
    }
}