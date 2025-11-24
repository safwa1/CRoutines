using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CRoutines.Flows;

public static class Flow
{
    public static async IAsyncEnumerable<T> Create<T>(
        Func<IFlowCollector<T>, CancellationToken, Task> block,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        var collector = new FlowCollector<T>(channel.Writer);
        
        // Use TaskCompletionSource for better control and avoid unnecessary Task.Run overhead
        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Start the producer directly without Task.Run - this is more efficient
        var producerTask = ProduceAsync(block, collector, channel.Writer, ct, completionSource);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
            yield return item;
            
        // Ensure producer completed
        await producerTask;
    }

    private static async Task ProduceAsync<T>(
        Func<IFlowCollector<T>, CancellationToken, Task> block,
        IFlowCollector<T> collector,
        ChannelWriter<T> writer,
        CancellationToken ct,
        TaskCompletionSource<object?> completionSource)
    {
        try
        {
            await block(collector, ct);
            completionSource.TrySetResult(null);
        }
        catch (Exception ex)
        {
            completionSource.TrySetException(ex);
            throw;
        }
        finally
        {
            writer.Complete();
        }
    }

    public static async IAsyncEnumerable<T> Of<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}