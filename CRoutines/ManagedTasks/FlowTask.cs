using System.Diagnostics;

namespace CRoutines.ManagedTasks;

public sealed class FlowTask : IObservable<TaskStateChangedEvent>, IProgress<double>
{
    private readonly Func<CancellationToken, Func<Task>, Task> _action;
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private readonly List<IObserver<TaskStateChangedEvent>> _observers = new();
    private readonly Stopwatch _stopwatch = new();

    private CancellationTokenSource _cts = new();
    private bool _isRepeating;
    private TimeSpan _delay;
    private readonly List<FlowTask> _dependencies = new();

    public string Name { get; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public TaskState State { get; private set; } = TaskState.Created;
    public double Progress { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public TimeSpan? Duration { get; private set; }

    private FlowTask(string name, Func<CancellationToken, Func<Task>, Task> action)
    {
        Name = name;
        _action = action;
        Notify(TaskState.Created);
    }

    public static FlowTask NewTask(string name, Func<CancellationToken, Func<Task>, Task> action)
    {
        return new FlowTask(name, action);
    }

    public FlowTask Managed()
    {
        FlowTaskManager.Shared.Add(this);
        return this;
    }
    
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

    public async Task StartAsync()
    {
        if (State is TaskState.Running or TaskState.Completed)
            return;
        
        if (_dependencies.Count > 0)
        {
            foreach (var dep in _dependencies)
            {
                while (dep.State != TaskState.Completed)
                    await Task.Delay(100);
            }
        }
        
        if (_delay > TimeSpan.Zero)
        {
            SetState(TaskState.Scheduled);
            await Task.Delay(_delay);
        }

        _cts = new CancellationTokenSource();
        SetState(TaskState.Running);
        _stopwatch.Restart();

        try
        {
            do
            {
                await _action(_cts.Token, WaitIfPausedAsync);
                if (!_cts.IsCancellationRequested)
                    SetState(TaskState.Completed);

                _stopwatch.Stop();
                Duration = _stopwatch.Elapsed;

                if (_isRepeating && !_cts.IsCancellationRequested)
                    await Task.Delay(_delay);
            }
            while (_isRepeating && !_cts.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            SetState(TaskState.Canceled);
        }
        catch (Exception ex)
        {
            SetState(TaskState.Faulted, ex.Message);
        }
    }

    public void Pause()
    {
        if (State != TaskState.Running) return;
        _pauseEvent.Reset();
        SetState(TaskState.Paused);
    }

    public void Resume()
    {
        if (State != TaskState.Paused) return;
        _pauseEvent.Set();
        SetState(TaskState.Running);
    }

    public void Cancel()
    {
        if (State is TaskState.Canceled or TaskState.Completed) return;
        _cts.Cancel();
        _pauseEvent.Set();
        SetState(TaskState.Canceled);
    }

    private Task WaitIfPausedAsync()
    {
        _pauseEvent.Wait();
        return Task.CompletedTask;
    }

    private void SetState(TaskState newState, string? error = null)
    {
        State = newState;
        Notify(newState, error);
    }

    private void Notify(TaskState state, string? error = null)
    {
        var evt = new TaskStateChangedEvent(Name, state, DateTime.Now, Progress, error, Duration);
        foreach (var obs in _observers.ToArray())
            obs.OnNext(evt);
    }

    public void Report(double value)
    {
        Progress = Math.Clamp(value, 0, 100);
        Notify(State);
    }

    public IDisposable Subscribe(IObserver<TaskStateChangedEvent> observer)
    {
        _observers.Add(observer);
        observer.OnNext(new TaskStateChangedEvent(Name, State, DateTime.Now, Progress, null, Duration));
        return new Unsubscriber(_observers, observer);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<TaskStateChangedEvent>> _list;
        private readonly IObserver<TaskStateChangedEvent> _observer;
        public Unsubscriber(List<IObserver<TaskStateChangedEvent>> list, IObserver<TaskStateChangedEvent> observer)
        {
            _list = list;
            _observer = observer;
        }
        public void Dispose() => _list.Remove(_observer);
    }
    
    public TaskSnapshot ToSnapshot() => new(Name, State, Priority, CreatedAt, Duration, Progress);
}