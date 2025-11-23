using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CRoutines.Channels;

namespace CRoutines.Flows;

/// <summary>
/// Flow builders for integrating with callbacks and events
/// </summary>
public static class FlowBuilders
{
    /// <summary>
    /// Creates a flow from callbacks
    /// Similar to Kotlin's callbackFlow
    /// </summary>
    public static async IAsyncEnumerable<T> CallbackFlow<T>(
        Func<IProducerScope<T>, Action, Task> block,
        int capacity = 16,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = capacity == int.MaxValue
            ? Channel.CreateUnbounded<T>()
            : Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        var awaitCloseCalled = false;
        var awaitCloseTaskSource = new TaskCompletionSource();
        
        void AwaitClose()
        {
            awaitCloseCalled = true;
        }

        var producerScope = new ProducerScope<T>(channel.Writer, ct);
        
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await block(producerScope, AwaitClose);
                
                if (awaitCloseCalled)
                {
                    await awaitCloseTaskSource.Task;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                producerScope.Close(ex);
            }
            finally
            {
                producerScope.CompleteIfNotClosed();
            }
        }, ct);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                yield return item;
            }
        }
        finally
        {
            if (awaitCloseCalled)
            {
                awaitCloseTaskSource.TrySetResult();
            }
            
            await producerTask;
        }
    }

    /// <summary>
    /// Creates a flow that can emit from multiple coroutines
    /// Similar to Kotlin's channelFlow
    /// </summary>
    public static async IAsyncEnumerable<T> ChannelFlow<T>(
        Func<IProducerScope<T>, Task> block,
        int capacity = 16,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = capacity == int.MaxValue
            ? Channel.CreateUnbounded<T>()
            : Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false // Allow multiple writers
            });

        var producerScope = new ProducerScope<T>(channel.Writer, ct);
        
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await block(producerScope);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                producerScope.Close(ex);
            }
            finally
            {
                producerScope.CompleteIfNotClosed();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }

        await producerTask;
    }

    /// <summary>
    /// Creates a flow from an event
    /// </summary>
    public static IAsyncEnumerable<T> FromEvent<T>(
        Action<Action<T>> addHandler,
        Action<Action<T>> removeHandler,
        int capacity = 16)
    {
        return CallbackFlow<T>(async (scope, awaitClose) =>
        {
            void Handler(T value)
            {
                scope.Send(value).Wait();
            }

            addHandler(Handler);
            
            awaitClose();
            
            removeHandler(Handler);
        }, capacity);
    }
}
