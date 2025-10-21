namespace CRoutines.Coroutine.Channels;

public interface IReceiveChannel<out T>
{
    IAsyncEnumerable<T> ReceiveAll(CancellationToken ct = default);
}