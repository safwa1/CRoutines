using System.Threading.Channels;

namespace CRoutines.Flows;

internal sealed class FlowCollector<T> : IFlowCollector<T>
{
    private readonly ChannelWriter<T> _writer;
    public FlowCollector(ChannelWriter<T> writer) => _writer = writer;
    public ValueTask Emit(T value, CancellationToken ct = default) => _writer.WriteAsync(value, ct);
}