using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Asyncs;
using CRoutines.Coroutine.Channels;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Dispatchers;
using CRoutines.Coroutine.Flows;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task runBlocking(
        Func<CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null) => RunBlocking(block, dispatcher);

    public static async Task runScopedBlocking(
        Func<CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null
    )
    {
        var scope = new CoroutineScope(dispatcher);
        try
        {
            await block(scope);
            await scope.JoinAll();
        }
        catch (Exception ex)
        {
            CoroutineExceptionHandler.Current?.Handle(ex);
            throw;
        }
        finally
        {
            scope.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope globalScope(ICoroutineDispatcher? dispatcher = null)
        => GlobalScope(dispatcher);

    public static CoroutineScope coroutineScope => CoroutineScope;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope coroutineScopeOf(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null) =>
        CoroutineScopeOf(dispatcher, parentJob);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineLocal<T> coroutineLocalOf<T>() => CoroutineLocalOf<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineContext coroutineContextOf(Job job, ICoroutineDispatcher dispatcher) =>
        CoroutineContextOf(job, dispatcher);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Job launch(
        Func<CoroutineContext, Task> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default
    ) => GlobalScope(dispatcher).Launch(block, dispatcher, start);

    /// <summary>
    /// Launches a coroutine in the global scope, optionally using the specified dispatcher and start mode.
    /// </summary>
    /// <param name="block">
    /// A function representing the coroutine body. It receives a <see cref="CoroutineContext"/> 
    /// and the <see cref="CoroutineScope"/> it runs in, and returns a <see cref="Task"/> representing 
    /// the asynchronous work.
    /// </param>
    /// <param name="dispatcher">
    /// An optional <see cref="ICoroutineDispatcher"/> to control the thread or execution context of the coroutine. 
    /// If <c>null</c>, the default global dispatcher is used.
    /// </param>
    /// <param name="start">
    /// A <see cref="CoroutineStart"/> value that determines how the coroutine is started. 
    /// For example, <see cref="CoroutineStart.Default"/> starts it immediately, while other options 
    /// may allow lazy or atomic starts.
    /// </param>
    /// <returns>
    /// A <see cref="Job"/> representing the coroutine. This can be used to monitor its state, cancel it, or 
    /// attach completion handlers.
    /// </returns>
    /// <remarks>
    /// This method launches a coroutine that is tied to the global scope, meaning it is not 
    /// bound to any parent job and will live until completion or cancellation. Use this for top-level coroutines 
    /// that are independent of structured concurrency.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Job launch(
        Func<CoroutineContext, CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default)
    {
        var scope = GlobalScope(dispatcher);
        return scope.Launch(async ctx => await block(ctx, scope), dispatcher, start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void cancelGlobalScope(ICoroutineDispatcher? dispatcher = null)
    {
        GlobalScope(dispatcher).Cancel();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Job launchOn(
        CoroutineScope scope,
        Func<CoroutineContext, Task> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default
    )
    {
        return scope.Launch(block, dispatcher, start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Job launchOn(
        CoroutineScope scope,
        Func<CoroutineContext, CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default
    )
    {
        return scope.Launch(async ctx => await block(ctx, scope), dispatcher, start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void cleanScopes()
    {
        CoroutineScopeCache.ClearAll();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task withUnscopedContext(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task> block,
        Job? parentJob = null)
    {
        var child = new Job(parentJob);
        var tcs = new TaskCompletionSource();

#if DEBUG
        log(dispatcher.ToString());
#endif
        await dispatcher.Dispatch(async ct =>
        {
            try
            {
                await block(new CoroutineContext(child, dispatcher));
                tcs.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                child.MarkCompleted();
            }
        }, child.Cancellation.Token);

        await tcs.Task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<T> withUnscopedContext<T>(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task<T>> block,
        Job? parentJob = null)
    {
        var child = new Job(parentJob);
        var tcs = new TaskCompletionSource<T>();

#if DEBUG
        log(dispatcher.ToString());
#endif
        await dispatcher.Dispatch(async ct =>
        {
            try
            {
                var result = await block(new CoroutineContext(child, dispatcher));
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                child.MarkCompleted();
            }
        }, child.Cancellation.Token);

        return await tcs.Task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task withContext(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task> block
    )
    {
        var scope = GlobalScope(dispatcher);
        return scope.WithContext(dispatcher, block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> withContext<T>(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task<T>> block
    )
    {
        var scope = GlobalScope(dispatcher);
        return scope.WithContext(dispatcher, block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task withContext(
        ICoroutineDispatcher dispatcher,
        CoroutineScope scope,
        Func<CoroutineContext, Task> block
    )
    {
        return scope.WithContext(dispatcher, block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> withContext<T>(
        ICoroutineDispatcher dispatcher,
        CoroutineScope scope,
        Func<CoroutineContext, Task<T>> block
    )
    {
        return scope.WithContext(dispatcher, block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task delay(TimeSpan duration, CancellationToken token = default)
        => Delay(duration, token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task delay(int ms, CancellationToken token = default)
        => Delay(ms, token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> withTimeout<T>(TimeSpan timeout, Func<Task<T>> op)
        => WithTimeout(timeout, op);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task withTimeout(TimeSpan timeout, Func<Task> op)
        => WithTimeout(timeout, op);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> retry<T>(Func<Task<T>> op, int attempts = 3, TimeSpan? delay = null)
        => Retry(op, attempts, delay);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task retry(Func<Task> op, int attempts = 3, TimeSpan? delay = null)
        => Retry(op, attempts, delay);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableSharedFlow<T> sharedFlowOf<T>() => new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableStateFlow<T> stateFlowOf<T>(T initial) => new(initial);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> flowOf<T>(
        Func<IFlowCollector<T>, CancellationToken, Task> block,
        CancellationToken ct = default
    ) => FlowOf(block, ct);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> flowOf<T>(params T[] items) => FlowOf(items);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Deferred<T> deferredOf<T>(Task<T> task, Job job, Action? start = null) =>
        DeferredOf<T>(task, job, start);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> unboundedChannelOf<T>()
        => UnboundedCoroutineChannelOf<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> boundedChannelOf<T>(int capacity)
        => BoundedCoroutineChannelOf<T>(capacity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> rendezvousChannelOf<T>()
        => RendezvousCoroutineChannelOf<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void log(object? msg)
        => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [T{Environment.CurrentManagedThreadId:D3}] {msg}");
}