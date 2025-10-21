using System.Collections.Concurrent;
using System.Text.Json;

namespace CRoutines.ManagedTasks;

public sealed class FlowTaskManager : IObserver<TaskStateChangedEvent>
{
    private static readonly Lazy<FlowTaskManager> Instance = new();
    public static FlowTaskManager Shared => Instance.Value;

    private readonly ConcurrentDictionary<string, FlowTask> _tasks = new();
    public event Action<TaskStateChangedEvent>? TaskChanged;
    
    private bool _autoCleanup = true;

    private FlowTaskManager() { }

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
        var tasks = All
            .OrderByDescending(t => t.Priority)
            .Select(t => t.StartAsync());

        await Task.WhenAll(tasks);
    }

    public FlowTask? Get(string name) =>
        _tasks.GetValueOrDefault(name);

    public async Task CancelAsync(string name)
    {
        if (_tasks.TryGetValue(name, out var t))
            await Task.Run(() => t.Cancel());
    }

    public async Task PauseAllAsync()
    {
        await Task.Run(() =>
        {
            foreach (var t in _tasks.Values) t.Pause();
        });
    }

    public async Task ResumeAllAsync()
    {
        await Task.Run(() =>
        {
            foreach (var t in _tasks.Values) t.Resume();
        });
    }

    public async Task CancelAllAsync()
    {
        await Task.Run(() =>
        {
            foreach (var t in _tasks.Values) t.Cancel();
        });
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
            .Select(kv => kv.Key)
            .ToList();

        foreach (var name in completed)
            _tasks.TryRemove(name, out _);
    }
    
    public string SaveToJson()
    {
        var snapshots = _tasks.Values.Select(t => t.ToSnapshot()).ToList();
        return JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true });
    }

    public void LoadFromJson(string json)
    {
        var snapshots = JsonSerializer.Deserialize<List<TaskSnapshot>>(json) ?? [];
        foreach (var snap in snapshots)
        {
            var dummy = FlowTask.NewTask(snap.Name, async (_, _) => await Task.CompletedTask)
                .WithPriority(snap.Priority);
            Add(dummy);
        }
    }
    
    public void OnNext(TaskStateChangedEvent evt)
    {
        TaskChanged?.Invoke(evt);

        if (evt.NewState is TaskState.Completed or TaskState.Canceled or TaskState.Faulted)
            CleanupCompleted();
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
            Console.WriteLine($"{t.Name,-20} | {t.State,-10} | {t.Progress,6:0.0}% | {t.Priority,-8} | {(t.Duration?.TotalSeconds ?? 0):0.0}s");
        Console.WriteLine();
    }
}