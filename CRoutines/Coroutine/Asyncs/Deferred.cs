using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Core;

namespace CRoutines.Coroutine.Asyncs;

public sealed class Deferred<T>
{
    private readonly Task<T> _task;
    private readonly Job _job;
    private readonly Action? _start;

    internal Deferred(Task<T> task, Job job, Action? start = null)
    {
        _task = task;
        _job = job;
        _start = start;
    }

    public Job Job => _job;
    public bool IsCompleted => _task.IsCompleted;
    public bool IsCancelled => _task.IsCanceled;
    public bool IsFaulted => _task.IsFaulted;

    public void Start() => _start?.Invoke();

    /// <summary>
    /// Await the result (throws if job failed or was cancelled)
    /// </summary>
    public async Task<T> Await() => await _task;
    
    /// <summary>
    /// Await with timeout
    /// </summary>
    public async Task<T> Await(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var delayTask = Task.Delay(System.Threading.Timeout.Infinite, cts.Token);
        
        var completed = await Task.WhenAny(_task, delayTask);
        
        if (completed == _task)
            return await _task;
        
        throw new TimeoutException($"Deferred timed out after {timeout}");
    }

    /// <summary>
    /// Try to get result without throwing
    /// </summary>
    public bool TryGetResult(out T? result)
    {
        if (_task.IsCompletedSuccessfully)
        {
            result = _task.Result;
            return true;
        }
        
        result = default;
        return false;
    }
    
    public TaskAwaiter<T> GetAwaiter() => _task.GetAwaiter();

    public void Cancel() => _job.Cancel();

    /// <summary>
    /// Get exception if job faulted
    /// </summary>
    public Exception? GetException() => _task.Exception?.InnerException;
}