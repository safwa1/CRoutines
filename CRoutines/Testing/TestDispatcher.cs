using CRoutines.Dispatchers;

namespace CRoutines.Testing;

/// <summary>
/// Test dispatcher that executes tasks immediately and synchronously
/// Provides deterministic execution for testing
/// </summary>
public sealed class TestDispatcher : ICoroutineDispatcher
{
    private readonly Queue<Func<Task>> _queue = new();
    private bool _isDispatching;
    private readonly object _lock = new();
    
    public Task Dispatch(Func<CancellationToken, Task> block, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_isDispatching)
            {
                // Recursive dispatch - queue it
                var tcs = new TaskCompletionSource();
                _queue.Enqueue(async () =>
                {
                    try
                    {
                        await block(ct);
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                return tcs.Task;
            }
        }
        
        // Execute immediately
        return ExecuteImmediate(block, ct);
    }
    
    private async Task ExecuteImmediate(Func<CancellationToken, Task> block, CancellationToken ct)
    {
        lock (_lock)
        {
            _isDispatching = true;
        }
        
        try
        {
            await block(ct);
            
            // Process queued tasks
            await ProcessQueue();
        }
        finally
        {
            lock (_lock)
            {
                _isDispatching = false;
            }
        }
    }
    
    /// <summary>
    /// Process all queued tasks
    /// </summary>
    private async Task ProcessQueue()
    {
        while (true)
        {
            Func<Task>? action = null;
            
            lock (_lock)
            {
                if (_queue.Count == 0)
                    break;
                
                action = _queue.Dequeue();
            }
            
            if (action != null)
            {
                await action();
            }
        }
    }
    
    /// <summary>
    /// Ensures all queued tasks are executed
    /// </summary>
    public async Task Yield()
    {
        await ProcessQueue();
    }
    
    /// <summary>
    /// Check if there are pending tasks
    /// </summary>
    public bool HasPendingTasks
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count > 0;
            }
        }
    }
    
    /// <summary>
    /// Clear all pending tasks
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
}
