using CRoutines.Coroutine.Asyncs;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines.Coroutine.Contexts;

public sealed class CoroutineScope : IDisposable
{
    private readonly Job _job;
    private readonly ICoroutineDispatcher _dispatcher;
    private bool _disposed;
    public CoroutineScope(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null)
    {
        _dispatcher = dispatcher ?? DefaultDispatcher.Instance;
        _job = parentJob ?? new Job();
    }

    public Job Job => _job;
    public ICoroutineDispatcher Dispatcher => _dispatcher;
    public CoroutineContext CoroutineContext => new(_job, _dispatcher);

    /// <summary>
    /// Launch a fire-and-forget coroutine (like Kotlin's launch)
    /// </summary>
    public Job Launch(
        Func<CoroutineContext, Task> block, 
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default)
    {
        var actualDispatcher = dispatcher ?? _dispatcher;
        var child = new Job(_job);
        
        if (start == CoroutineStart.Lazy)
        {
            // Lazy start - return job without executing
            return child;
        }

        _ = actualDispatcher.Dispatch(async ct =>
        {
            try
            {
                await block(new CoroutineContext(child, actualDispatcher));
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, just complete
            }
            catch (Exception ex)
            {
                child.MarkFaulted(ex);
                HandleException(ex, child);
                return;
            }
            
            child.MarkCompleted();
        }, child.Cancellation.Token);

        return child;
    }

    /// <summary>
    /// Launch with result (like Kotlin's async)
    /// </summary>
    public Deferred<T> Async<T>(
        Func<CoroutineContext, Task<T>> block, 
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default)
    {
        var actualDispatcher = dispatcher ?? _dispatcher;
        var child = new Job(_job);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (start == CoroutineStart.Lazy)
        {
            return new Deferred<T>(tcs.Task, child, () => StartAsync());
        }

        StartAsync();
        return new Deferred<T>(tcs.Task, child);

        void StartAsync()
        {
            _ = actualDispatcher.Dispatch(async ct =>
            {
                try
                {
                    var result = await block(new CoroutineContext(child, actualDispatcher));
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    child.MarkFaulted(ex);
                    HandleException(ex, child);
                    return;
                }
                
                child.MarkCompleted();
            }, child.Cancellation.Token);
        }
    }

    /// <summary>
    /// Switch context temporarily (like withContext)
    /// </summary>
    public async Task<T> WithContext<T>(ICoroutineDispatcher dispatcher, Func<CoroutineContext, Task<T>> block)
    {
        var child = new Job(_job);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        await dispatcher.Dispatch(async ct =>
        {
            try
            {
                #if DEBUG
                Console.WriteLine(dispatcher.ToString());
                #endif
                var result = await block(new CoroutineContext(child, dispatcher));
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                child.MarkCompleted();
            }
        }, child.Cancellation.Token);
        
        return await tcs.Task;
    }

    public Task WithContext(ICoroutineDispatcher dispatcher, Func<CoroutineContext, Task> block)
        => WithContext<object?>(dispatcher, async ctx => { await block(ctx); return null; });

    private void HandleException(Exception ex, Job job)
    {
        CoroutineExceptionHandler.Current?.Handle(ex);
    }

    public void Cancel()
    {
        if (!_disposed)
            _job.Cancel();
    }

    /// <summary>
    /// Efficiently wait for all child jobs to complete (no polling)
    /// </summary>
    public async Task JoinAll(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (true)
        {
            var activeChildren = _job.Children.Where(c => c.IsActive).ToList();
            
            if (activeChildren.Count == 0)
                break;

            // Wait for any child to complete
            var joinTasks = activeChildren.Select(c => c.Join(cancellationToken)).ToArray();
            
            try
            {
                await Task.WhenAny(joinTasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Individual job failures are handled by exception handler
                // Continue waiting for other jobs
            }
        }
    }

    // public async Task JoinAll(CancellationToken cancellationToken = default)
    // {
    //     ThrowIfDisposed();
    //     
    //     var children = _job.Children.ToList();
    //     
    //     if (children.Count == 0)
    //         return;
    //
    //     await Task.WhenAll(children.Select(c => c.Join(cancellationToken)));
    // }

    /// <summary>
    /// Join all with timeout
    /// </summary>
    public async Task<bool> JoinAll(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await JoinAll(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false; // Timeout
        }
    }
    
    
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CoroutineScope));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        Cancel();
    }
}