namespace CRoutines.Channels;

public interface ISendChannel<in T>
{
    ValueTask Send(T value, CancellationToken ct = default);
    void Close();
}