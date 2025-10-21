# CRoutines

A lightweight, pragmatic coroutines and reactive flows toolkit for .NET (net9.0). Inspired by Kotlin Coroutines, adapted to feel natural in C# async/await world.

- Structured concurrency with Job tree and CoroutineScope
- Flexible dispatchers (Default, IO, SingleThread, Unconfined, and UI: WPF, WinForms, WinUI3)
- Fire-and-forget launch and result-returning async (Deferred<T>)
- Context switching via WithContext
- Channels (send/receive) and cold/ hot flows (Flow, MutableSharedFlow, MutableStateFlow) with handy operators
- Utilities: Delay, Retry, Timeout, Select
- Managed tasks: observable background tasks with priority, progress, pause/resume, schedule, dependencies
- Pluggable CoroutineExceptionHandler and SupervisorJob isolation

## Installation

Add the project to your solution or reference the compiled assembly targeting net9.0. No external dependencies.

## Quick start

```csharp
using CRoutines.Coroutine.Extensions;
using static CRoutines.Prelude;

await RunBlocking(async scope =>
{
    // Fire-and-forget
    var job = scope.Launch(async ctx =>
    {
        await Delay(200.Millis);
        Console.WriteLine($"Hello from {ctx.Dispatcher.GetType().Name}");
    });

    // With result (Deferred<T>)
    var deferred = scope.Async(async ctx =>
    {
        await Delay(100.Millis);
        return 42;
    });

    var answer = await deferred.Await();
    Console.WriteLine($"Answer: {answer}");

    await scope.JoinAll();
});
```

## Coroutines and structured concurrency

- CoroutineScope is your entry point. It binds a root Job and a default ICoroutineDispatcher.
- All child jobs are attached to the parent job. Cancelling the parent cancels all children.

Main APIs:

- Coroutines.RunBlocking(Func<CoroutineScope, Task> block, ICoroutineDispatcher? dispatcher = null)
- Coroutines.GlobalScope(ICoroutineDispatcher? dispatcher = null)
- CoroutineScope.Launch(Func<CoroutineContext, Task> block, ICoroutineDispatcher? dispatcher = null, CoroutineStart start = Default)
- CoroutineScope.Async<T>(Func<CoroutineContext, Task<T>> block, ICoroutineDispatcher? dispatcher = null, CoroutineStart start = Default)
- CoroutineScope.WithContext / WithContext<T>
- CoroutineScope.JoinAll([TimeSpan timeout])
- CoroutineScope.Cancel()

CoroutineContext gives you:

- Job Job
- CancellationToken CancellationToken
- ICoroutineDispatcher Dispatcher

### Deferred<T>

- Start(): if created with Lazy start
- Await(): await the result (throws on cancellation/fault)
- Await(TimeSpan timeout): with timeout
- TryGetResult(out T result)
- Cancel(): cancels underlying Job
- GetException(): access inner exception if faulted

### Start modes

- Default: immediately dispatch
- Lazy: return Job/Deferred without starting; call Start()
- Atomic, Undispatched: placeholders for future strategies

## Dispatchers

- DefaultDispatcher: ThreadPool-based
- IODispatcher: optimized for long-running/IO work
- SingleThreadDispatcher: dedicated single thread with internal queue
- UnconfinedDispatcher: runs immediately on the current thread
- WpfDispatcher: wrap Application.Current.Dispatcher
- WinFormsDispatcher: wrap a Control/Form (ISynchronizeInvoke)
- WinUIDispatcher: wrap DispatcherQueue

Example: switch context to a UI thread and back

```csharp
await RunBlocking(async scope =>
{
    var ui = new WpfDispatcher(System.Windows.Application.Current.Dispatcher);

    await scope.WithContext(ui, async ctx =>
    {
        // On UI thread
        // update UI safely here
        await Task.CompletedTask;
    });
});
```

## Exception handling and supervision

- Set CoroutineExceptionHandler.Current to intercept unhandled coroutine exceptions.

```csharp
using CRoutines.Coroutine.Core;
CoroutineExceptionHandler.Current = CoroutineExceptionHandler.Logging();
```

- SupervisorJob isolates child failures: children do not cancel siblings or parent. Use it when constructing custom scopes or jobs.

## Channels

Simple typed channels built on System.Threading.Channels.

```csharp
using static CRoutines.Prelude;

var chan = BoundedCoroutineChannelOf<int>(16);

// sender
_ = Task.Run(async () =>
{
    for (var i = 0; i < 10; i++)
        await chan.Send(i);
    chan.Close();
});

// receiver
await foreach (var i in chan.ReceiveAll())
    Console.WriteLine(i);
```

APIs:
- CoroutineChannel<T>.CreateUnbounded() OR UnboundedCoroutineChannelOf<T>()
- CoroutineChannel<T>.CreateBounded(int capacity) OR BoundedCoroutineChannelOf<T>(int capacity)
- CoroutineChannel<T>.CreateRendezvous() OR RendezvousCoroutineChannelOf<T>()
- ISendChannel<T>.Send(value)
- IReceiveChannel<T>.ReceiveAll()
- Close()

## Flows

Cold flows, plus hot shared flows.

- Flow.Create<T>(Func<IFlowCollector<T>, CancellationToken, Task> block)
- Flow.Of(params T[] items)
- Operators (extension methods): Map, Filter, FlatMapLatest, Zip, ToList, FirstOrDefault
- MutableSharedFlow<T>: broadcast to subscribers
- MutableStateFlow<T>: holds last value and emits it immediately on subscribe

```csharp
using CRoutines.Coroutine.Flows;
using static CRoutines.Prelude;

var numbers = Flow.Create<int>(async (collector, ct) =>
{
    for (var i = 1; i <= 5; i++)
    {
        await collector.Emit(i, ct);
        await Task.Delay(50, ct);
    }
});

await foreach (var x in numbers.Map(n => n * 2))
    Console.WriteLine(x);

// Shared flow
var shared = MutableSharedFlowOf<string>();
var sub = shared.Subscribe(async s => Console.WriteLine($"got: {s}"));
await shared.Emit("hello");
sub.Dispose();

// State flow
var state = MutableStateFlowOf<int>(0);
var d = state.Subscribe(async v => Console.WriteLine($"state: {v}"));
state.Value = 1; // immediately emits
await state.Update(v => v + 1);
d.Dispose();
```

## Utilities

- Delay.For(TimeSpan|int): thin wrappers over Task.Delay
- Retry.Execute(Func<Task<T>> op, int maxAttempts = 3, TimeSpan? delayBetweenAttempts = null)
- Timeout.WithTimeout(TimeSpan, Func<Task<T>>)
- Select.From(params Func<CancellationToken, Task<T>>[] choices): returns the first completed result and cancels the rest

```csharp
var data = await Retry.Execute(async () => await FetchAsync(), 3, 200.Millis);

var fastest = await Select.From(
    async ct => await DownloadMirrorA(ct),
    async ct => await DownloadMirrorB(ct));

var value = await Timeout.WithTimeout(2.Second, async () => await ComputeAsync());
```

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

## Advanced

- CoroutineLocal<T>: simple AsyncLocal wrapper for coroutine-local data
- Job.Join([timeout]): efficiently wait for a job to complete
- SupervisorJob to isolate failures

## Error handling philosophy

- Exceptions inside coroutines are captured by the corresponding Job, then:
  - Marked faulted (Job.IsFaulted, Job.Exception)
  - Propagated up the Job tree by default (unless using SupervisorJob)
  - Routed to CoroutineExceptionHandler.Current if set

## Threading notes

- Dispatchers define where your coroutine code runs
- SingleThreadDispatcher processes work sequentially on its own dedicated thread
- UI dispatchers marshal back to the UI thread of the given framework

## FAQ

- Is this a replacement for Task/async-await? No, it complements them with structured concurrency and higher-level primitives.
- Do I need to dispose CoroutineScope? Scopes cancel on Dispose(). Prefer using statements or RunBlocking.
- Can I combine with regular Task APIs? Yes. Under the hood, everything is Task-based.

## License

MIT License


---

## Practical examples

Below are concise, copy‑pasteable examples adapted to CRoutines to help you get productive fast.

> Tip: Namespaces you will commonly use:
> - CRoutines.Coroutine, CRoutines.Coroutine.Contexts, CRoutines.Coroutine.Dispatchers
> - CRoutines.Coroutine.Utilities, CRoutines.Coroutine.Channels, CRoutines.Coroutine.Flows
> - CRoutines.Coroutine.Core (for SupervisorJob, CoroutineExceptionHandler, CoroutineLocal)

### 1) Basic: Launch, Async (Deferred<T>), Lazy start

```csharp
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Extensions;
using static CRoutines.Prelude;

await RunBlocking(async scope =>
{
    Console.WriteLine("Starting coroutines...");

    // Fire-and-forget
    var job1 = scope.Launch(async ctx =>
    {
        await Delay(1.Second, ctx.CancellationToken);
        Console.WriteLine("Job 1 completed!");
    });

    // With result (Deferred<T>)
    var deferred = scope.Async(async ctx =>
    {
        await Delay(500.Millis, ctx.CancellationToken);
        return 42;
    });

    Console.WriteLine("Doing other work...");
    var result = await deferred.Await();
    Console.WriteLine($"Result: {result}");

    // Lazy start
    var lazy = scope.Async(async ctx =>
    {
        Console.WriteLine("Starting lazy computation...");
        await Delay(500, ctx.CancellationToken);
        return "Lazy result!";
    }, start: CoroutineStart.Lazy);

    Console.WriteLine("Deferred created, not started yet");
    await Delay(200);
    lazy.Start();
    Console.WriteLine(await lazy.Await());

    await job1.Join();
});
```

### 2) Dispatchers and WithContext

```csharp
using CRoutines.Coroutine.Dispatchers;
using static CRoutines.Prelude;

// Default (ThreadPool)
await RunBlocking(async scope =>
{
    scope.Launch(async ctx =>
    {
        Console.WriteLine($"Default thread: {Environment.CurrentManagedThreadId}");
        await Delay(250, ctx.CancellationToken);
    });
});

// IO dispatcher
using var ioScope = CoroutineScopeOf(IODispatcher.Instance);
var ioJob = ioScope.Launch(async ctx =>
{
    // Simulate IO
    await Delay(100, ctx.CancellationToken);
});
await ioJob.Join();

// Single-threaded dispatcher
using var single = new SingleThreadDispatcher("MyThread");
using var singleScope = CoroutineScopeOf(single);
singleScope.Launch(async ctx =>
{
    Console.WriteLine($"Job 1 on thread: {Environment.CurrentManagedThreadId}");
    await Delay(50, ctx.CancellationToken);
});
await singleScope.JoinAll();

// WithContext switch
await RunBlocking(async scope =>
{
    var value = await scope.WithContext(IODispatcher.Instance, async ctx =>
    {
        await Delay(100, ctx.CancellationToken);
        return "from IO";
    });
    Console.WriteLine(value);
});
```

### 3) Cancellation, Join/JoinAll, timeouts and tokens

```csharp
using CRoutines.Coroutine.Extensions;
using static CRoutines.Prelude;

await RunBlocking(async scope =>
{
    var job = scope.Launch(async ctx =>
    {
        for (int i = 0; i < 10; i++)
        {
            if (ctx.CancellationToken.IsCancellationRequested) return;
            Console.WriteLine($"Working {i}");
            await Delay(200, ctx.CancellationToken);
        }
    });

    await Delay(600);
    job.Cancel();
    Console.WriteLine("Cancellation requested");
    await job.Join();
});

// Join with timeout
await RunBlocking(async scope =>
{
    var longJob = scope.Launch(async ctx =>
    {
        await Delay(5000, ctx.CancellationToken);
    });
    var completed = await longJob.Join(1.Second);
    Console.WriteLine(completed ? "Completed in time" : "Timeout");
    longJob.Cancel();
});

// Join with CancellationToken
await RunBlocking(async scope =>
{
    var job = scope.Launch(async ctx => await Delay(3000, ctx.CancellationToken));
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(500);
    try { await job.Join(cts.Token); }
    catch (OperationCanceledException) { Console.WriteLine("Join cancelled"); }
    job.Cancel();
});

// Efficient JoinAll and timeout
await RunBlocking(async scope =>
{
    for (int i = 0; i < 5; i++)
        scope.Launch(async ctx => await Delay(200 + i * 150, ctx.CancellationToken));

    var allInTime = await scope.JoinAll(1.Second);
    if (!allInTime) scope.Cancel();
});
```

Supervision and exceptions:

```csharp
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Contexts;
using static CRoutines.Prelude;

CoroutineExceptionHandler.Current = new CoroutineExceptionHandler(ex =>
    Console.WriteLine($"[Global] {ex.Message}"));

await RunBlocking(async scope =>
{
    var supervisor = new SupervisorJob();
    var childScope = CoroutineScopeOf(parentJob: supervisor);

    // Child 1 fails
    childScope.Launch(async ctx =>
    {
        await Delay(200, ctx.CancellationToken);
        throw new InvalidOperationException("boom");
    });

    // Child 2 continues
    childScope.Launch(async ctx =>
    {
        await Delay(500, ctx.CancellationToken);
        Console.WriteLine("Child 2 completed");
    });

    await Delay(800);
});

// Non-cascading async: one Deferred fails, another still succeeds
await RunBlocking(async scope =>
{
    var d1 = scope.Async<string>(async ctx =>
    {
        await Delay(100, ctx.CancellationToken);
        throw new Exception("Deferred 1 failed!");
    });

    var d2 = scope.Async(async ctx =>
    {
        await Delay(200, ctx.CancellationToken);
        return "OK";
    });

    await Delay(300);
    Console.WriteLine($"d1 faulted: {d1.IsFaulted}, d2 completed: {d2.IsCompleted}");
    if (d2.TryGetResult(out var ok)) Console.WriteLine(ok);
});
```

### 4) Channels

```csharp
using static CRoutines.Prelude;

var channel = BoundedCoroutineChannelOf<int>(2);
await RunBlocking(async scope =>
{
    // Producer
    scope.Launch(async ctx =>
    {
        for (int i = 0; i < 5; i++)
        {
            await channel.Send(i, ctx.CancellationToken);
            Console.WriteLine($"Sent {i}");
        }
        channel.Close();
    });

    // Consumer
    scope.Launch(async ctx =>
    {
        await foreach (var item in channel.ReceiveAll(ctx.CancellationToken))
            Console.WriteLine($"Received {item}");
    });
});
```

### 5) Flow (cold streams) and operators

```csharp
using CRoutines.Coroutine.Flows;
using static CRoutines.Prelude;

var numbers = Flow.Create<int>(async (collector, ct) =>
{
    for (int i = 1; i <= 5; i++)
    {
        await collector.Emit(i, ct);
        await Delay(50, ct);
    }
});

await foreach (var item in numbers.Filter(x => x % 2 == 0).Map(x => x * x))
    Console.WriteLine(item);

// Zip and collect
var a = Flow.Of(1, 2, 3);
var b = Flow.Of("A", "B", "C");
await foreach (var (i, s) in a.Zip(b))
    Console.WriteLine($"{i}:{s}");

var list = await a.Map(x => x * 10).ToList();
```

### 6) SharedFlow and StateFlow (hot streams)

```csharp
using static CRoutines.Prelude;

var shared = MutableSharedFlowOf<string>();
var sub1 = shared.Subscribe(async v => { Console.WriteLine($"S1: {v}"); await Task.CompletedTask; });
var sub2 = shared.Subscribe(async v => { Console.WriteLine($"S2: {v}"); await Task.CompletedTask; });

await shared.Emit("Event 1");
sub1.Dispose();
await shared.Emit("Event 2");
sub2.Dispose();

var state = MutableStateFlowOf<int>(0);
var sub = state.Subscribe(async v => { Console.WriteLine($"State: {v}"); await Task.CompletedTask; });
state.Value = 1;
await state.Update(v => v + 1);
sub.Dispose();
```

### 7) Utilities: Timeout, Retry, Select

```csharp
using CRoutines.Coroutine.Extensions;
using static CRoutines.Prelude;

try
{
    var value = await WithTimeout(1.Second, async () =>
    {
        await Delay(1500);
        return 123;
    });
}
catch (TimeoutException)
{
    Console.WriteLine("Operation timed out");
}

var result = await Retry(async () =>
{
    // throw until it succeeds
    return "Success";
}, maxAttempts: 5, delayBetweenAttempts: 200.Millis);

var first = await Select.From(
    async ct => { await Task.Delay(1000, ct); return "Slow"; },
    async ct => { await Task.Delay(200, ct); return "Fast"; }
);
```

### 8) CoroutineLocal

```csharp
using static CRoutines.Prelude;

var userId = CoroutineLocalOf<string>();
await RunBlocking(async scope =>
{
    userId.Value = "User123";

    scope.Launch(async ctx =>
    {
        Console.WriteLine($"Job 1 sees: {userId.Value}");
        await Delay(100, ctx.CancellationToken);
    });

    scope.Launch(async ctx =>
    {
        userId.Value = "User456";
        Console.WriteLine($"Job 2 sees: {userId.Value}");
        await Delay(100, ctx.CancellationToken);
    });

    await scope.JoinAll();
    Console.WriteLine($"Main sees: {userId.Value}");
});
```

### 9) UI thread integration (WPF, WinForms, WinUI 3)

Use the provided dispatchers to marshal back to UI thread:

```csharp
// WPF
using CRoutines.Coroutine.Dispatchers;
var wpf = new WpfDispatcher(System.Windows.Application.Current.Dispatcher);

// WinForms
var winForms = new WinFormsDispatcher(this); // 'this' is a Control/Form implementing ISynchronizeInvoke

// WinUI 3
var winui = new WinUIDispatcher(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
```

Switch to UI thread safely:

```csharp
using static CRoutines.Prelude;

await RunBlocking(async scope =>
{
    await scope.WithContext(wpf, async ctx =>
    {
        // Update UI here
        await Task.CompletedTask;
    });
});
```

### 10) Real‑world snippets

API client with Retry + Timeout:

```csharp
using CRoutines.Coroutine.Extensions;
using static CRoutines.Prelude;

public sealed class ApiClient
{
    public async Task<string> FetchAsync(string endpoint)
    {
        return await Retry(async () =>
        {
            return await WithTimeout(5.Second, async () =>
            {
                await Delay(500); // simulate
                return $"Data from {endpoint}";
            });
        }, maxAttempts: 3, delayBetweenAttempts: 500.Millis);
    }
}
```

Background sync using multiple Deferreds and state flow:

```csharp
using static CRoutines.Prelude;

var status = MutableStateFlowOf<string>("Idle");
await RunBlocking(async scope =>
{
    status.Subscribe(v => { Console.WriteLine($"Status: {v}"); return Task.CompletedTask; });

    var a = scope.Async(async _ => { await Delay(400); return true; });
    var b = scope.Async(async _ => { await Delay(300); return true; });
    var c = scope.Async(async _ => { await Delay(200); return true; });

    await a.Await(); await b.Await(); await c.Await();
    status.Value = "Complete";
});
```

Chat room with MutableSharedFlow:

```csharp
using static CRoutines.Prelude

public record ChatMessage(string User, string Text, DateTime Timestamp);
var messages = MutableSharedFlowOf<ChatMessage>();
var sub = messages.Subscribe(async m => { Console.WriteLine($"{m.User}: {m.Text}"); await Task.CompletedTask; });
await messages.Emit(new ChatMessage("Alice", "Hello!", DateTime.Now));
sub.Dispose();
```

---

These examples are intentionally short and mirror the concepts covered earlier. For deeper explanations, see the sections above (Coroutines, Dispatchers, Channels, Flows, Utilities, Managed tasks, and Advanced).