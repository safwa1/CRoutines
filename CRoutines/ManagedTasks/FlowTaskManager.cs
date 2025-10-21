using System.Collections.Concurrent;

namespace CRoutines.ManagedTasks;

public sealed class FlowTaskManager : IObserver<TaskStateChangedEvent>
{
    private static readonly Lazy<FlowTaskManager> Instance = new(() => new FlowTaskManager());
    public static FlowTaskManager Shared => Instance.Value;

    private readonly ConcurrentDictionary<string, FlowTask> _tasks = new();
    public event Action<TaskStateChangedEvent>? TaskChanged;

    private bool _autoCleanup = true;

    private FlowTaskManager()
    {
    }
    
    public IEnumerable<FlowTask> All => _tasks.Values.OrderByDescending(t => t.Priority);

    public bool Add(FlowTask task)
    {
        if (!_tasks.TryAdd(task.Name, task))
            return false;

        task.Subscribe(this);
        return true;
    }

    public async Task RunAllAsync()
    {
        var tasks = All.Select(t => t.StartAsync());
        await Task.WhenAll(tasks);
    }

    // Pause/Resume/Cancel without freezing UI
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
        Console.WriteLine($"[TaskManager Error] {error.Message}");
    }

    public void OnCompleted()
    {
        Console.WriteLine("TaskManager finished receiving events.");
    }

    public void PrintStatus()
    {
        Console.WriteLine($"\n--- Tasks ---");
        foreach (var t in All)
            Console.WriteLine(
                $"{t.Name,-20} | {t.State,-10} | {t.Progress,6:0.0}% | {t.Priority,-8} | {(t.Duration?.TotalSeconds ?? 0):0.0}s");
        Console.WriteLine();
    }
}