using System.Collections.Concurrent;

namespace CRoutines.Core;

/// <summary>
/// Represents a coroutine job with structured concurrency support
/// </summary>
public class Job
{
    private readonly ConcurrentDictionary<Job, byte> _children = new();
    private int _state = (int)JobState.Active; // Single atomic state
    private readonly List<Action> _completionHandlers = new();
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? _exception;
    private string? _cancellationReason;

    public Job? Parent { get; }
    public CancellationTokenSource Cancellation { get; }

    public Job(Job? parent = null)
    {
        Parent = parent;
        Cancellation = new CancellationTokenSource();
        parent?.AttachChild(this);
    }

    /// <summary>
    /// Current state of the job
    /// </summary>
    public JobState State => (JobState)Volatile.Read(ref _state);
    
    /// <summary>
    /// Whether the job was cancelled
    /// </summary>
    public bool IsCancelled => State == JobState.Cancelled;
    
    /// <summary>
    /// Whether the job completed (successfully, cancelled, or faulted)
    /// </summary>
    public bool IsCompleted => State != JobState.Active;
    
    /// <summary>
    /// Whether the job is still active (not completed, cancelled, or faulted)
    /// </summary>
    public bool IsActive => State == JobState.Active;
    
    /// <summary>
    /// Whether the job failed with an exception
    /// </summary>
    public bool IsFaulted => State == JobState.Faulted;
    
    /// <summary>
    /// The exception that caused the job to fault, if any
    /// </summary>
    public Exception? Exception => _exception;
    
    /// <summary>
    /// The reason for cancellation, if cancelled
    /// </summary>
    public string? CancellationReason => _cancellationReason;

    internal event Action? OnCompleted;

    protected virtual void AttachChild(Job child)
    {
        _children.TryAdd(child, 0);
        child.OnCompleted += () => ChildCompleted(child);
    }

    internal void ChildCompleted(Job child) => _children.TryRemove(child, out _);

    /// <summary>
    /// Cancels this job and all its children
    /// </summary>
    public virtual void Cancel() => Cancel(null);
    
    /// <summary>
    /// Cancels this job with a specific reason
    /// </summary>
    /// <param name="reason">The reason for cancellation</param>
    public virtual void Cancel(string? reason)
    {
        // Try to transition from Active to Cancelled
        if (Interlocked.CompareExchange(ref _state, (int)JobState.Cancelled, (int)JobState.Active) == (int)JobState.Active)
        {
            _cancellationReason = reason;
            
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
                    child.Cancel(reason);
                }
                catch
                {
                    // Continue cancelling other children
                }
            }
            
            Parent?.HandleChildCancellation(this);
            
            // Complete the task
            CompleteTask();
        }
    }
    
    /// <summary>
    /// Cancels the job and waits for it to complete
    /// </summary>
    public async Task CancelAndJoin(TimeSpan? timeout = null)
    {
        Cancel();
        if (timeout.HasValue)
        {
            await Join(timeout.Value);
        }
        else
        {
            await Join();
        }
    }

    protected virtual void HandleChildCancellation(Job child)
    {
        // Default: propagate cancellation upward (structured concurrency)
        Cancel($"Child job cancelled: {child.CancellationReason}");
    }

    protected virtual void HandleChildException(Exception ex)
    {
        // Default: store exception and mark as faulted
        _exception = ex;
        MarkFaulted(ex);
    }

    internal void MarkCompleted()
    {
        // Try to transition from Active to Completed
        if (Interlocked.CompareExchange(ref _state, (int)JobState.Completed, (int)JobState.Active) == (int)JobState.Active)
        {
            CompleteTask();
        }
    }

    internal void MarkFaulted(Exception exception)
    {
        _exception = exception;
        
        // Try to transition from Active to Faulted
        if (Interlocked.CompareExchange(ref _state, (int)JobState.Faulted, (int)JobState.Active) == (int)JobState.Active)
        {
            Parent?.HandleChildException(exception);
            CompleteTask();
        }
    }
    
    /// <summary>
    /// Ensures the job is still active, throws if cancelled
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown if the job is not active</exception>
    public void EnsureActive()
    {
        if (!IsActive)
        {
            throw new OperationCanceledException(
                _cancellationReason ?? "Job is not active",
                Cancellation.Token);
        }
    }

    private void CompleteTask()
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

        // Signal completion based on state
        var currentState = State;
        if (currentState == JobState.Cancelled)
            _completionSource.TrySetCanceled();
        else if (currentState == JobState.Faulted && _exception != null)
            _completionSource.TrySetException(_exception);
        else
            _completionSource.TrySetResult();
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