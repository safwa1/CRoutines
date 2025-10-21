using System.Threading.Channels;

namespace CRoutines.Coroutine.Channels;

public sealed class CoroutineChannel<T> : ISendChannel<T>, IReceiveChannel<T>
{
    private readonly Channel<T> _channel;

    private CoroutineChannel(Channel<T> channel) => _channel = channel;

    public static CoroutineChannel<T> CreateUnbounded() 
        => new(Channel.CreateUnbounded<T>());

    public static CoroutineChannel<T> CreateBounded(int capacity)
        => new(Channel.CreateBounded<T>(capacity));

    public static CoroutineChannel<T> CreateRendezvous()
        => new(Channel.CreateBounded<T>(0));

    public ValueTask Send(T value, CancellationToken ct = default) 
        => _channel.Writer.WriteAsync(value, ct);

    public void Close() => _channel.Writer.Complete();

    public IAsyncEnumerable<T> ReceiveAll(CancellationToken ct = default) 
        => _channel.Reader.ReadAllAsync(ct);
}