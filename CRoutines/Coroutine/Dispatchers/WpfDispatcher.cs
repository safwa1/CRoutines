namespace CRoutines.Coroutine.Dispatchers;

/// <summary>
/// WPF UI thread dispatcher
/// Usage: new WpfDispatcher(Application.Current.Dispatcher) or new WpfDispatcher(Dispatcher)
/// </summary>
public sealed class WpfDispatcher : ICoroutineDispatcher
{
    private readonly dynamic _dispatcher;

    public WpfDispatcher(object dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromCanceled(ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.InvokeAsync(new Action(async () =>
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