# FlowPilot (ManagedTasks)

Human-friendly orchestration for background tasks: cooperative pause/resume/cancel, priorities, dependencies, scheduling (with repeat), progress, and supervision — all in a small net8.0 library.

## Highlights
- Cooperative tasks: each `FlowTask` receives `(CancellationToken ct, Func<Task> wait)` to pause/resume cleanly.
- Orchestration: priorities, dependencies, scheduling, optional repeating, and a singleton manager (`FlowTaskManager.Shared`).
- Observability: per-task and global events (`TaskStateChangedEvent`/`TaskStateTransitionEvent`), progress reporting, timing, and snapshots.
- Reliability: supervisor jobs with restart/stop/ignore strategies and retry limits.
- Persistence-friendly: `TaskSnapshot` captures state/priority/progress for storage.

## When to use
- Fire-and-forget background work you need to pause/resume/cancel later.
- Pipelines with dependencies (run B after A).
- Scheduled or repeating jobs (pollers, maintenance tasks).
- Centralized progress/state reporting for UI or logging.

## Quick start
```csharp
using ManagedTasks;

var download = FlowTask.NewTask("Download", async (ct, wait) =>
{
    for (int i = 0; i <= 100; i += 10)
    {
        await wait();                 // cooperatively pause if requested
        ct.ThrowIfCancellationRequested();
        await Task.Delay(100, ct);
        Console.WriteLine($"Downloading... {i}%");
    }
})
.Managed()
.WithPriority(TaskPriority.High)
.Schedule(TimeSpan.FromSeconds(5));   // start 5s later

var process = FlowTask.NewTask("Process", async (ct, wait) =>
{
    await Task.Delay(500, ct);
    Console.WriteLine("Processing finished.");
})
.Managed()
.After(download); // run after Download completes

FlowTask.NewTask("FreeTask", async (ct, wait) =>
{
    await wait();
    Console.WriteLine("FreeTask");
    await Task.Delay(1000, ct);
    Console.WriteLine("FreeTask Finished");
})
.WithPriority(TaskPriority.Low)
.After(process)
.Managed();

FlowTaskManager.Shared.TaskChanged += e =>
    Console.WriteLine($"[{e.Timestamp:T}] {e.TaskName} -> {e.NewState} ({e.Progress:0}%){(e.ErrorMessage != null ? " | Error: " + e.ErrorMessage : "")}");

await FlowTaskManager.Shared.RunAllAsync();
```

## Control and observe
```csharp
await FlowTaskManager.Shared.PauseAllAsync();
await FlowTaskManager.Shared.ResumeAllAsync();
await FlowTaskManager.Shared.CancelAsync("Download");
```

Progress binding:
```csharp
var export = FlowTask.NewTask("Export", async (ct, wait) =>
{
    var progress = (IProgress<double>)FlowTaskManager.Shared.Get("Export")!;
    for (int i = 0; i <= 100; i += 5)
    {
        await wait();
        ct.ThrowIfCancellationRequested();
        await Task.Delay(50, ct);
        progress.Report(i);
    }
})
.Managed();

FlowTaskManager.Shared.TaskChanged += e =>
{
    if (e.TaskName == "Export")
        Console.WriteLine($"Export progress: {e.Progress:0}%");
};
```

## Supervision (restarts, retries)
```csharp
using ManagedTasks;

var job = DefaultSupervisorJobBuilder.Create()
    .RestartFailed(maxRetries: 3, retryDelay: TimeSpan.FromSeconds(2))
    .AddTasks(download, process)
    .Build();

await job.StartAllAsync();
```

Strategies (`SupervisionStrategy`):
- `RestartFailed` (default): retry failed tasks up to a limit with delay.
- `StopAll`: cancel siblings when any task fails.
- `Ignore`: do nothing on failure.

## API cheatsheet
- `FlowTask.NewTask(string name, Func<CancellationToken, Func<Task>, Task> action)`
- `Managed()` register in `FlowTaskManager.Shared`
- `WithPriority(TaskPriority priority)`
- `After(params FlowTask[] dependencies)`
- `Schedule(TimeSpan delay, bool repeat = false)`
- `StartAsync()`, `Pause()/PauseAsync()`, `Resume()`, `Cancel()`
- `Subscribe(IObserver<TaskStateChangedEvent>)`
- `Report(double progress)` (0..100)
- `Completion` (awaitable snapshot)
- `FlowTaskManager.Shared`: `RunAllAsync()`, `PauseAllAsync()`, `ResumeAllAsync()`, `CancelAllAsync()`, `CancelAsync(name)`, `TaskChanged` event, `WithAutoCleanup(bool)`

## Project layout
- `Tasks/` task types and manager
- `Events/` task events and transitions
- `Supervision/` supervisor job, builder, strategies
