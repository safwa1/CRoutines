using System.Collections.Concurrent;

namespace CRoutines.Coroutine.Core;

/// <summary>
/// Represents a coroutine job with structured concurrency support
/// </summary>
public class Job
{
    private readonly ConcurrentDictionary<Job, byte> _children = new();
    private int _isCompleted;
    private int _isCancelled;
    private readonly List<Action> _completionHandlers = new();
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _exception;

    public Job? Parent { get; }
    public CancellationTokenSource Cancellation { get; }

    public Job(Job? parent = null)
    {
        Parent = parent;
        Cancellation = new CancellationTokenSource();
        parent?.AttachChild(this);
    }

    public bool IsCancelled => Volatile.Read(ref _isCancelled) == 1;
    public bool IsCompleted => Volatile.Read(ref _isCompleted) == 1;
    public bool IsActive => !IsCompleted && !IsCancelled;
    public bool IsFaulted => _exception != null;
    public Exception? Exception => _exception;

    internal event Action? OnCompleted;

    protected virtual void AttachChild(Job child)
    {
        _children.TryAdd(child, 0);
        child.OnCompleted += () => ChildCompleted(child);
    }

    internal void ChildCompleted(Job child) => _children.TryRemove(child, out _);

    public virtual void Cancel()
    {
        if (Interlocked.CompareExchange(ref _isCancelled, 1, 0) == 0)
        {
            try
            {
                Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            // Cancel all children
            var childrenSnapshot = _children.Keys.ToArray();
            foreach (var child in childrenSnapshot)
            {
                try
                {
                    child.Cancel();
                }
                catch
                {
                    // Continue cancelling other children
                }
            }
            
            Parent?.HandleChildCancellation(this);
            
            // Mark as completed if not already
            if (!IsCompleted)
            {
                _completionSource.TrySetCanceled();
            }
        }
    }

    protected virtual void HandleChildCancellation(Job child)
    {
        // Default: propagate cancellation upward (structured concurrency)
        Cancel();
    }

    protected virtual void HandleChildException(Exception ex)
    {
        // Default: store exception and propagate upward
        _exception = ex;
        Cancel();
    }

    internal void MarkCompleted()
    {
        if (Interlocked.CompareExchange(ref _isCompleted, 1, 0) == 0)
        {
            Parent?.ChildCompleted(this);
            OnCompleted?.Invoke();
            
            lock (_lock)
            {
                foreach (var handler in _completionHandlers)
                {
                    try
                    {
                        handler();
                    }
                    catch
                    {
                        // Ignore handler exceptions
                    }
                }
                _completionHandlers.Clear();
            }

            // Signal completion
            if (IsCancelled)
                _completionSource.TrySetCanceled();
            else if (_exception != null)
                _completionSource.TrySetException(_exception);
            else
                _completionSource.TrySetResult();
        }
    }

    internal void MarkFaulted(Exception exception)
    {
        _exception = exception;
        HandleChildException(exception);
        MarkCompleted();
    }

    public void InvokeOnCompletion(Action handler)
    {
        lock (_lock)
        {
            if (IsCompleted)
            {
                try
                {
                    handler();
                }
                catch
                {
                    // Ignore handler exceptions
                }
            }
            else
            {
                _completionHandlers.Add(handler);
            }
        }
    }

    public IEnumerable<Job> Children => _children.Keys;

    /// <summary>
    /// Suspends coroutine until this job is complete (efficient, no polling)
    /// </summary>
    public Task Join(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.CanBeCanceled)
        {
            return JoinWithCancellation(cancellationToken);
        }
        
        return _completionSource.Task;
    }

    private async Task JoinWithCancellation(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        using var _ = cancellationToken.Register(() => tcs.TrySetCanceled());
        
        var completed = await Task.WhenAny(_completionSource.Task, tcs.Task);
        await completed; // Propagate exceptions/cancellation
    }

    /// <summary>
    /// Join with timeout
    /// </summary>
    public async Task<bool> Join(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await JoinWithCancellation(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false; // Timeout
        }
    }
}