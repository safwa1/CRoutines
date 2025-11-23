using System.Collections.Concurrent;
using System.Diagnostics;

namespace ManagedTasks;

public sealed class SupervisorJob
{
    private readonly List<FlowTask> _children = [];
    private readonly SupervisionStrategy _strategy;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly ConcurrentDictionary<string, int> _retries = new();
    private readonly SemaphoreSlim _completionSemaphore = new(0);
    private int _runningTasks;

    public SupervisorJob(
        IEnumerable<FlowTask> tasks,
        SupervisionStrategy strategy = SupervisionStrategy.RestartFailed,
        int maxRetries = 3,
        TimeSpan? retryDelay = null
    )
    {
        _strategy = strategy;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);

        foreach (var t in tasks)
            Add(t);
    }

    public void Add(FlowTask task)
    {
        lock (_children) _children.Add(task);
        task.StateTransitioned += OnTaskStateTransitioned;
    }

    private async void OnTaskStateTransitioned(TaskStateTransitionEvent evt)
    {
        try
        {
            if (evt.NewState == TaskState.Faulted)
            {
                var failed = _children.FirstOrDefault(t => t.Name == evt.Name);
                if (failed == null) return;

                switch (_strategy)
                {
                    case SupervisionStrategy.StopAll:
                        await CancelAll();
                        DecrementRunning();
                        break;

                    case SupervisionStrategy.RestartFailed:
                        var restarted = await TryRestartAsync(failed);
                        if (!restarted)
                            DecrementRunning();
                        break;
                }
            }
            else if (evt.NewState is TaskState.Completed or TaskState.Canceled)
            {
                DecrementRunning();
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to handle task state transition: {e.Message}", e);
        }
    }

    private async Task<bool> TryRestartAsync(FlowTask failed)
    {
        var count = _retries.AddOrUpdate(failed.Name, 1, (_, v) => v + 1);
        if (count > _maxRetries)
        {
            Debugger.Log(0, "TaskRetry", $"Task {failed.Name} exceeded max retries ({_maxRetries})");
            return false;
        }

        Debugger.Log(0, "TaskRetry", $"Restarting {failed.Name} (attempt {count}/{_maxRetries})");
        await Task.Delay(_retryDelay);

        failed.Reset();
        _ = Task.Run(async () =>
        {
            try
            {
                await failed.StartAsync();
            }
            catch
            {
                // ignored
            }
        });

        return true;
    }

    private void DecrementRunning()
    {
        if (Interlocked.Decrement(ref _runningTasks) == 0)
        {
            _completionSemaphore.Release();
        }
    }

    public Task CancelAll()
    {
        lock (_children)
        {
            foreach (var t in _children)
                t.Cancel();
        }

        return Task.CompletedTask;
    }

    public async Task StartAllAsync()
    {
        lock (_children)
        {
            _runningTasks = _children.Count;
        }

        List<FlowTask> tasks;
        lock (_children)
            tasks = _children.ToList();

        foreach (var t in tasks)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await t.StartAsync();
                }
                catch
                {
                    // ignored
                }
            });
        }

        await _completionSemaphore.WaitAsync();
    }
    
    public static DefaultSupervisorJobBuilder Builder => DefaultSupervisorJobBuilder.Create();
}