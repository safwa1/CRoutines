namespace CRoutines.Coroutine.Channels;

public interface ISendChannel<in T>
{
    ValueTask Send(T value, CancellationToken ct = default);
    void Close();
}