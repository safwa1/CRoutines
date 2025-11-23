using System.Threading.Channels;
using CRoutines.Contexts;

namespace CRoutines.Channels;

/// <summary>
/// Channel builders for creating channels with coroutine scopes
/// </summary>
public static class ChannelBuilders
{
    /// <summary>
    /// Creates a channel and starts a coroutine to produce values
    /// Similar to Kotlin's produce
    /// </summary>
    public static CoroutineChannel<T> Produce<T>(
        this CoroutineScope scope,
        int capacity = 16,
        Func<IProducerScope<T>, Task>? block = null)
    {
        // Create the channel using CoroutineChannel's static factory
        var coroutineChannel = capacity == int.MaxValue
            ? CoroutineChannel<T>.CreateUnbounded()
            : CoroutineChannel<T>.CreateBounded(capacity);

        if (block != null)
        {
            // Launch a coroutine to produce values
            scope.Launch(async ctx =>
            {
                var channel = capacity == int.MaxValue
                    ? Channel.CreateUnbounded<T>()
                    : Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait
                    });

                var producerScope = new ProducerScope<T>(channel.Writer, ctx.CancellationToken);
                
                try
                {
                    await block(producerScope);
                }
                catch (Exception ex)
                {
                    producerScope.Close(ex);
                    throw;
                }
                finally
                {
                    producerScope.CompleteIfNotClosed();
                }

                // Transfer all values from internal channel to return channel
                await foreach (var item in channel.Reader.ReadAllAsync(ctx.CancellationToken))
                {
                    await coroutineChannel.Send(item, ctx.CancellationToken);
                }
                
                coroutineChannel.Close();
            });
        }

        return coroutineChannel;
    }

    /// <summary>
    /// Creates an unbounded channel producer
    /// </summary>
    public static CoroutineChannel<T> ProduceUnbounded<T>(
        this CoroutineScope scope,
        Func<IProducerScope<T>, Task> block)
    {
        return Produce(scope, int.MaxValue, block);
    }

    /// <summary>
    /// Creates a simple producer returning the channel directly
    /// </summary>
    public static CoroutineChannel<T> ProduceSimple<T>(
        this CoroutineScope scope,
        Func<CoroutineContext, IAsyncEnumerable<T>> block,
        int capacity = 16)
    {
        var coroutineChannel = capacity == int.MaxValue
            ? CoroutineChannel<T>.CreateUnbounded()
            : CoroutineChannel<T>.CreateBounded(capacity);

        scope.Launch(async ctx =>
        {
            try
            {
                await foreach (var item in block(ctx).WithCancellation(ctx.CancellationToken))
                {
                    await coroutineChannel.Send(item, ctx.CancellationToken);
                }
            }
            finally
            {
                coroutineChannel.Close();
            }
        });

        return coroutineChannel;
    }
}
