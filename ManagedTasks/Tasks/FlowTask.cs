using System.Diagnostics;
using System.Threading.Channels;

namespace ManagedTasks;

public sealed class FlowTask : IObservable<TaskStateChangedEvent>, IProgress<double>, IDisposable
{
    private readonly Func<CancellationToken, Func<Task>, Task> _action;
    private readonly List<IObserver<TaskStateChangedEvent>> _observers = new();
    private readonly Stopwatch _stopwatch = new();

    private CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private bool _isRepeating;
    private TimeSpan _delay = TimeSpan.Zero;
    private readonly List<FlowTask> _dependencies = new();
    private Action<Exception>? _onFailed;
    private bool _disposed;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    // Dispatcher channel for non-blocking notifications
    private readonly Channel<TaskStateChangedEvent> _notifyChannel =
        Channel.CreateUnbounded<TaskStateChangedEvent>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

    private readonly Task _dispatcherTask;
    private readonly CancellationTokenSource _dispatcherCts = new();

    private TaskCompletionSource<TaskSnapshot> _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Name { get; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public TaskState State { get; private set; } = TaskState.Created;
    public double Progress { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>
    /// The last exception that caused a fault, if any.
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <summary>
    /// A task that completes when this FlowTask finishes (Completed/Canceled/Faulted).
    /// The task result is a snapshot taken at completion.
    /// </summary>
    public Task<TaskSnapshot> Completion => _completionSource.Task;

    /// <summary>
    /// Raised when a state transition happens (previous -> new).
    /// </summary>
    public event Action<TaskStateTransitionEvent>? StateTransitioned;

    public FlowTask(string name, Func<CancellationToken, Func<Task>, Task> action)
    {
        Name = name;
        _action = action;
        Notify(TaskState.Created);

        // start dispatcher
        _dispatcherTask = Task.Run(NotifyDispatcherLoop);
    }

    private async Task NotifyDispatcherLoop()
    {
        var reader = _notifyChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_dispatcherCts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var evt))
                {
                    // snapshot observers to avoid holding lock while calling them
                    List<IObserver<TaskStateChangedEvent>> observersSnapshot;
#if NET9_0_OR_GREATER
                    _lock.Enter();
                    try
                    {
                        observersSnapshot = new List<IObserver<TaskStateChangedEvent>>(_observers);
                    }
                    finally
                    {
                        _lock.Exit();
                    }
#else
                    lock (_lock) { observersSnapshot = new List<IObserver<TaskStateChangedEvent>>(_observers); }
#endif

                    foreach (var obs in observersSnapshot)
                    {
                        try
                        {
                            obs.OnNext(evt);
                            if (evt.NewState == TaskState.Faulted && evt.Exception != null) obs.OnError(evt.Exception);
                            else if (evt.NewState is TaskState.Completed or TaskState.Canceled) obs.OnCompleted();
                        }
                        catch
                        {
                            // swallow observer exceptions - observers should manage their own errors
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* dispatcher stopping */
        }
    }

    public static FlowTask NewTask(string name, Func<CancellationToken, Func<Task>, Task> action)
        => new(name, action);

    public FlowTask WithPriority(TaskPriority priority)
    {
        Priority = priority;
        return this;
    }

    public FlowTask After(params FlowTask[] dependencies)
    {
        _dependencies.AddRange(dependencies);
        return this;
    }

    public FlowTask Schedule(TimeSpan delay, bool repeat = false)
    {
        _delay = delay;
        _isRepeating = repeat;
        return this;
    }

    public FlowTask Managed()
    {
        FlowTaskManager.AddTask(this);
        return this;
    }

    public FlowTask OnFailed(Action<Exception> handler)
    {
        _onFailed = handler;
        return this;
    }

    public async Task StartAsync()
    {
        if (State is TaskState.Running or TaskState.Completed or TaskState.Canceled or TaskState.Faulted or TaskState.Paused) return;

        // Refresh CTS and dispose previous instance to avoid leaks across restarts.
        var previousCts = _cts;
        _cts = new CancellationTokenSource();
        previousCts.Dispose();
        var cts = _cts;

        // Wait for dependencies using their Completion Task -> avoids polling
        if (_dependencies.Count > 0)
        {
            var depTasks = _dependencies.Select(d => d.Completion).ToArray();
            try
            {
                await Task.WhenAll(depTasks).ConfigureAwait(false);
            }
            catch
            {
                // ignore dependency exceptions here - dependencies may fault/cancel; Start anyway or decide policy
            }
        }

        if (State is TaskState.Canceled or TaskState.Faulted || cts.IsCancellationRequested) return;

        if (_delay > TimeSpan.Zero)
        {
            SetState(TaskState.Scheduled);
            try
            {
                await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SetState(TaskState.Canceled);
                return;
            }
        }

        if (State is TaskState.Canceled or TaskState.Faulted || cts.IsCancellationRequested) return;

        SetState(TaskState.Running);
        _stopwatch.Restart();

        try
        {
            while (!cts.IsCancellationRequested)
            {
                await _action(cts.Token, WaitIfPausedAsync).ConfigureAwait(false);

                if (_isRepeating && !cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // cancellation handled below
                    }
                    continue;
                }

                _stopwatch.Stop();
                Duration = _stopwatch.Elapsed;
                if (!cts.IsCancellationRequested)
                    SetState(TaskState.Completed);
                return;
            }

            _stopwatch.Stop();
            Duration = _stopwatch.Elapsed;
            SetState(TaskState.Canceled);
        }
        catch (OperationCanceledException)
        {
            _stopwatch.Stop();
            Duration = _stopwatch.Elapsed;
            SetState(TaskState.Canceled);
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            Duration = _stopwatch.Elapsed;
            LastException = ex;
            SetState(TaskState.Faulted, ex);
            
            // Call OnFailed handler
            try
            {
                _onFailed?.Invoke(ex);
            }
            catch
            {
                // swallow handler exceptions
            }
        }
        finally
        {
            // Set completion source if not already
            var snapshot = ToSnapshot();
            _completionSource.TrySetResult(snapshot);
        }
    }

    public void Pause()
    {
        if (State != TaskState.Running) return;
        _pauseSemaphore.Wait(); // block async tasks at WaitIfPausedAsync
        SetState(TaskState.Paused);
    }

    public async Task PauseAsync()
    {
        if (State != TaskState.Running) return;
        await _pauseSemaphore.WaitAsync().ConfigureAwait(false);
        SetState(TaskState.Paused);
    }

    public void Resume()
    {
        if (State != TaskState.Paused) return;
        _pauseSemaphore.Release();
        SetState(TaskState.Running);
    }

    public void Cancel()
    {
        if (State is TaskState.Canceled or TaskState.Completed) return;
        _cts.Cancel();
        if (State == TaskState.Paused) Resume();
        SetState(TaskState.Canceled);
    }

    private async Task WaitIfPausedAsync()
    {
        await _pauseSemaphore.WaitAsync().ConfigureAwait(false);
        _pauseSemaphore.Release();
    }

    private void SetState(TaskState newState, Exception? exception = null)
    {
        var previous = State;
        State = newState;

        if (exception != null) LastException = exception;

        // raise transition event
        var transition = new TaskStateTransitionEvent(Name, previous, newState, DateTime.Now, exception);
        try
        {
            StateTransitioned?.Invoke(transition);
        }
        catch
        {
            /* swallow */
        }

        Notify(newState, exception);
        
        if (newState is TaskState.Completed or TaskState.Canceled or TaskState.Faulted)
        {
            var snapshot = ToSnapshot();
            _completionSource.TrySetResult(snapshot);
        }
    }

    private void Notify(TaskState state, Exception? exception = null)
    {
        var evt = new TaskStateChangedEvent(Name, state, DateTime.Now, Progress, exception?.Message, exception,
            Duration);
        // enqueue to dispatcher channel
        _notifyChannel.Writer.TryWrite(evt);
    }

    public void Report(double value)
    {
        Progress = Math.Clamp(value, 0, 100);
        Notify(State);
    }

    public IDisposable Subscribe(IObserver<TaskStateChangedEvent> observer)
    {
#if NET9_0_OR_GREATER
        _lock.Enter();
        try
        {
            _observers.Add(observer);
        }
        finally
        {
            _lock.Exit();
        }
#else
        lock (_lock) { _observers.Add(observer); }
#endif
        // send initial state immediately (still via channel to be consistent)
        var initial = new TaskStateChangedEvent(Name, State, DateTime.Now, Progress, null, null, Duration);
        _notifyChannel.Writer.TryWrite(initial);
        return new Unsubscriber(_observers, observer, _lock);
    }

    private sealed class Unsubscriber : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _lock;
#else
        private readonly object _lock;
#endif
        private readonly List<IObserver<TaskStateChangedEvent>> _list;
        private readonly IObserver<TaskStateChangedEvent> _observer;

#if NET9_0_OR_GREATER
        public Unsubscriber(List<IObserver<TaskStateChangedEvent>> list, IObserver<TaskStateChangedEvent> observer,
            Lock lockObject)
        {
            _list = list;
            _observer = observer;
            _lock = lockObject;
        }
#else
        public Unsubscriber(List<IObserver<TaskStateChangedEvent>> list, IObserver<TaskStateChangedEvent> observer, object lockObject)
        {
            _list = list;
            _observer = observer;
            _lock = lockObject;
        }
#endif

        public void Dispose()
        {
#if NET9_0_OR_GREATER
            _lock.Enter();
            try
            {
                _list.Remove(_observer);
            }
            finally
            {
                _lock.Exit();
            }
#else
            lock (_lock) { _list.Remove(_observer); }
#endif
        }
    }

    public void Reset()
    {
        if (State == TaskState.Running)
            Cancel();

        Progress = 0;
        Duration = null;
        LastException = null;

        // Dispose old CTS properly
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _completionSource = new TaskCompletionSource<TaskSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetState(TaskState.Created);
    }

    public TaskSnapshot ToSnapshot() => new(Name, State, Priority, CreatedAt, Duration, Progress);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Cancel if still running
        if (State == TaskState.Running)
        {
            Cancel();
        }

        // Stop dispatcher
        _dispatcherCts.Cancel();
        _notifyChannel.Writer.Complete();

        try
        {
            _dispatcherTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore
        }

        // Dispose resources
        _cts.Dispose();
        _dispatcherCts.Dispose();
        _pauseSemaphore.Dispose();

        // Clear observers
#if NET9_0_OR_GREATER
        _lock.Enter();
        try
        {
            _observers.Clear();
        }
        finally
        {
            _lock.Exit();
        }
#else
        lock (_lock) { _observers.Clear(); }
#endif
    }
}
