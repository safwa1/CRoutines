using System.Collections.Concurrent;

namespace ManagedTasks;

public sealed class FlowTaskManager : IObserver<TaskStateChangedEvent>
{
    private static readonly Lazy<FlowTaskManager> Instance = new(() => new FlowTaskManager());
    public static FlowTaskManager Shared => Instance.Value;

    private readonly ConcurrentDictionary<string, FlowTask> _tasks = new();
    public event Action<TaskStateChangedEvent>? TaskChanged;
    public event Action<TaskStateTransitionEvent>? TaskTransitioned;

    private bool _autoCleanup = true;

    private FlowTaskManager()
    {
    }

    public IEnumerable<FlowTask> All => _tasks.Values.OrderByDescending(t => t.Priority);

    public bool Add(FlowTask task)
    {
        if (!_tasks.TryAdd(task.Name, task))
            return false;

        // subscribe to state changes
        task.Subscribe(this);
        // subscribe to transitions if available
        task.StateTransitioned += OnTaskTransitioned;

        return true;
    }

    private void OnTaskTransitioned(TaskStateTransitionEvent evt)
    {
        TaskTransitioned?.Invoke(evt);
        if (evt.NewState is TaskState.Completed or TaskState.Canceled or TaskState.Faulted) CleanupCompleted();
    }

    public async Task RunAllAsync()
    {
        var tasks = All.Select(t => t.Completion).ToArray();
        
        // Start all tasks
        foreach (var t in All)
        {
            _ = Task.Run(t.StartAsync);
        }
        
        // Wait for all to complete
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Some tasks may have faulted, but we still want to wait for all
        }
    }

    public Task PauseAllAsync()
    {
        foreach (var t in _tasks.Values) t.Pause();
        return Task.CompletedTask;
    }

    public Task ResumeAllAsync()
    {
        foreach (var t in _tasks.Values) t.Resume();
        return Task.CompletedTask;
    }

    public Task CancelAsync(string name)
    {
        if (_tasks.TryGetValue(name, out var t))
            t.Cancel();
        return Task.CompletedTask;
    }

    public Task PauseAsync(string name)
    {
        if (_tasks.TryGetValue(name, out var t))
            t.Pause();
        return Task.CompletedTask;
    }

    public Task ResumeAsync(string name)
    {
        if (_tasks.TryGetValue(name, out var t))
            t.Resume();
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        foreach (var t in _tasks.Values) t.Cancel();
        return Task.CompletedTask;
    }

    public void Remove(string name)
    {
        if (_tasks.TryRemove(name, out var t))
            t.Cancel();
    }

    public FlowTaskManager WithAutoCleanup(bool autoCleanup)
    {
        _autoCleanup = autoCleanup;
        return this;
    }

    private void CleanupCompleted()
    {
        if (!_autoCleanup) return;
        var completed = _tasks
            .Where(kv => kv.Value.State is TaskState.Completed or TaskState.Canceled or TaskState.Faulted)
            .Select(kv => kv.Key).ToList();
        foreach (var name in completed) _tasks.TryRemove(name, out _);
    }

    public void OnNext(TaskStateChangedEvent evt)
    {
        TaskChanged?.Invoke(evt);
        if (evt.NewState is TaskState.Completed or TaskState.Canceled or TaskState.Faulted) CleanupCompleted();
    }

    public void OnError(Exception error)
    {
        TaskChanged?.Invoke(new TaskStateChangedEvent(
            "FlowTaskManager",
            TaskState.Faulted,
            DateTime.Now,
            0,
            error.Message,
            Exception: error
        ));
    }

    public void OnCompleted()
    {
        TaskChanged?.Invoke(new TaskStateChangedEvent(
            "FlowTaskManager",
            TaskState.Completed,
            DateTime.Now,
            100
        ));
    }

    public static FlowTaskManager AddTask(FlowTask task)
    {
        Shared.Add(task);
        return Shared;
    }

    public static Task RunAllTasksAsync()
    {
        return Shared.RunAllAsync();
    }
}