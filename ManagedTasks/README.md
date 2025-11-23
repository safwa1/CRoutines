## Managed tasks

Human-friendly, observable background tasks with state machine, progress, priorities, scheduling (including repeat), and dependencies — all coordinated by a singleton manager.

When to use:
- Fire off background work that you want to pause/resume/cancel later
- Chain tasks with dependencies (do B after A)
- Defer or schedule work, optionally repeated
- Show progress and state in UI and log changes centrally

Key types:
- FlowTask — a single managed unit of work
    - Properties: Name, Priority, State, Progress, CreatedAt, Duration
    - States (TaskState): Created, Scheduled, Running, Paused, Completed, Canceled, Faulted
- FlowTaskManager.Shared — the singleton task registry/runner
- TaskPriority — Low, Normal, High, Critical (manager orders by priority)
- TaskSnapshot, TaskStateChangedEvent — persistence and telemetry

FlowTask API (cheatsheet):
- NewTask(string name, Func<CancellationToken, Func<Task>, Task> action)
- Managed() — register in FlowTaskManager.Shared
- WithPriority(TaskPriority priority)
- After(params FlowTask[] dependencies)
- Schedule(TimeSpan delay, bool repeat = false)
- StartAsync() — starts the task (RunAllAsync will call this for all managed tasks)
- Pause() / Resume() / Cancel()
- Subscribe(IObserver<TaskStateChangedEvent>) — observe this task only
- Report(double progress) — implement IProgress<double> to report 0..100
- ToSnapshot() — capture current snapshot

FlowTaskManager API (cheatsheet):
- Add(FlowTask task), Get(string name), IEnumerable<FlowTask> All (ordered by priority)
- RunAllAsync() — starts all registered tasks (respects dependencies and schedule)
- PauseAllAsync(), ResumeAllAsync(), CancelAllAsync(), CancelAsync(name)
- TaskChanged event — observe all tasks globally
- PrintStatus() — writes a simple table to console
- SaveToJson() / LoadFromJson(json) — persist/restore snapshots
- WithAutoCleanup(bool) — automatically removes finished/failed/canceled tasks from registry

Behavior notes:
- Dependencies: a task waits until all its dependencies reach Completed before it starts
- Schedule: if delay > 0, task enters Scheduled then starts after delay
- Repeat: Schedule(delay, repeat: true) re-runs the action with delay spacing until Cancel() is called
- Priority: manager enumerates/starts higher priority tasks first, but does not preempt running ones
- Progress: call Report(x) (0..100). Each change raises TaskChanged (and per-task observers)
- Threading: actions receive (ct, wait) where wait() asynchronously blocks while paused; always pass ct to your awaits

Learn by example (full chain):

```csharp
using CRoutines.ManagedTasks;
using CRoutines.Coroutine.Extensions;

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
.Schedule(5.Second);   // start 5s later

var process = FlowTask.NewTask("Process", async (ct, wait) =>
{
    await Task.Delay(500, ct);
    Console.WriteLine("Processing finished.");
})
.Managed()
.After(download);                      // run after Download completes

FlowTask
    .NewTask("FreeTask", async (ct, wait) =>
    {
        await wait();
        Console.WriteLine("FreeTask");
        await Task.Delay(1000, ct);
        Console.WriteLine("FreeTask Finished");
    })
    .WithPriority(TaskPriority.Low)
    .After(process)
    .Managed();

// Observe everything globally
FlowTaskManager.Shared.TaskChanged += e =>
    Console.WriteLine($"[{e.Timestamp:T}] {e.Name} -> {e.NewState} ({e.Progress:0}%){(e.Error != null ? " | Error: " + e.Error : "")}");

await FlowTaskManager.Shared.RunAllAsync();
```

Pause / resume all and cancel a single task:

```csharp
await FlowTaskManager.Shared.PauseAllAsync();
// ... user resumes later
await FlowTaskManager.Shared.ResumeAllAsync();
// cancel by name
await FlowTaskManager.Shared.CancelAsync("Download");
```

Repeating task (poll every 10s until canceled):

```csharp
var poller = FlowTask.NewTask("PollServer", async (ct, wait) =>
{
    await wait();
    // do work
    await Task.Delay(1000, ct);
    Console.WriteLine("Polled!");
})
.Managed()
.Schedule(10.Second, repeat: true);

// Later: FlowTaskManager.Shared.CancelAsync("PollServer");
```

Report progress and bind to UI:

```csharp
var export = FlowTask.NewTask("Export", async (ct, wait) =>
{
    var progress = (IProgress<double>)FlowTaskManager.Shared.Get("Export")!; // or close over a reference
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
    if (e.Name == "Export")
    {
        // Update UI progress bar safely from your UI thread/dispatcher
        Console.WriteLine($"Export progress: {e.Progress:0}%");
    }
};
```

Persist and restore:

```csharp
// Save current tasks to JSON (snapshots)
var json = FlowTaskManager.Shared.SaveToJson();
File.WriteAllText("tasks.json", json);

// ... later / next run
var json2 = File.ReadAllText("tasks.json");
FlowTaskManager.Shared.LoadFromJson(json2);
// Snapshots restore registrations with names/priorities so you can rebuild planned tasks
```