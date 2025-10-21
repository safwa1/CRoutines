using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CRoutines.Coroutine.Flows;

public static class Flow
{
    public static async IAsyncEnumerable<T> Create<T>(
        Func<IFlowCollector<T>, CancellationToken, Task> block,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<T>();
        var collector = new FlowCollector<T>(channel.Writer);
        
        _ = Task.Run(async () =>
        {
            try
            {
                await block(collector, ct);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
            yield return item;
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