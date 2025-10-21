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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope globalScope(ICoroutineDispatcher? dispatcher = null)
        => GlobalScope(dispatcher);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope scope(
        ICoroutineDispatcher? dispatcher = null,
        Job? parentJob = null)
        => new(dispatcher, parentJob);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineLocal<T> coroutineLocalOf<T>() => CoroutineLocalOf<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineContext coroutineContextOf(Job job, ICoroutineDispatcher dispatcher) =>
        CoroutineContextOf(job, dispatcher);

    public static CoroutineScope coroutineScope => CoroutineScope;

    public static Func<Func<CoroutineContext, Task>, ICoroutineDispatcher?, CoroutineStart, Job> launch
        => coroutineScope.Launch;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Job launchOn(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task> block,
        Job? parentJob = null,
        CoroutineStart start = CoroutineStart.Default
    )
    {
        var scope = coroutineScopeOf(dispatcher, parentJob);
        return scope.Launch(block, dispatcher, start);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task withContext(
        ICoroutineDispatcher dispatcher,
        Func<CoroutineContext, Task> block,
        Job? parentJob = null
    )
    {
        var scope = coroutineScopeOf(dispatcher, parentJob);
        return scope.WithContext(dispatcher, block);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task withContext(
        ICoroutineDispatcher dispatcher,
        CoroutineScope scope,
        Func<CoroutineContext, Task> block,
        Job? parentJob = null
    )
    {
        return scope.WithContext(dispatcher, block);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope coroutineScopeOf(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null) =>
        CoroutineScopeOf(dispatcher, parentJob);

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
        => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

    extension(int time)
    {
        public TimeSpan millis
            => TimeSpan.FromMilliseconds(time);

        public TimeSpan second
            => TimeSpan.FromSeconds(time);

        public TimeSpan minute
            => TimeSpan.FromMinutes(time);

        public TimeSpan hour
            => TimeSpan.FromHours(time);
    }
}