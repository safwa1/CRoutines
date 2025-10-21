using System.Threading.Channels;

namespace CRoutines.Coroutine.Dispatchers;

/// <summary>
/// Single-threaded dispatcher (like Dispatchers.Main or custom limitedParallelism(1))
/// </summary>
public sealed class SingleThreadDispatcher : ICoroutineDispatcher, IDisposable
{
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>();
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();

    private record WorkItem(Func<CancellationToken, Task> Work, CancellationToken Token, TaskCompletionSource Tcs);

    public SingleThreadDispatcher(string name = "CoroutineThread")
    {
        _thread = new Thread(ThreadLoop) { IsBackground = true, Name = name };
        _thread.Start();
    }

    private async void ThreadLoop()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await item.Work(item.Token);
                item.Tcs.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                item.Tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                item.Tcs.TrySetException(ex);
            }
        }
    }

    public Task Dispatch(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Writer.TryWrite(new WorkItem(work, ct, tcs));
        return tcs.Task;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.Complete();
    }
}