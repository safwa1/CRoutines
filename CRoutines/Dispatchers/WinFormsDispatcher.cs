namespace CRoutines.Dispatchers;

/// <summary>
/// WinForms UI thread dispatcher
/// Usage: new WinFormsDispatcher(this) where 'this' is a Control or Form
/// </summary>
public sealed class WinFormsDispatcher : ICoroutineDispatcher
{
    private readonly System.ComponentModel.ISynchronizeInvoke _control;

    public WinFormsDispatcher(System.ComponentModel.ISynchronizeInvoke control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromCanceled(ct);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void ExecuteWork()
        {
            async void Execute()
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
            }
            Execute();
        }

        if (_control.InvokeRequired)
            _control.BeginInvoke(new Action(ExecuteWork), null);
        else
            ExecuteWork();

        return tcs.Task;
    }
}