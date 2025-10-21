using CRoutines.Coroutine.Channels;

namespace CRoutines;

public static partial class Prelude
{
    public static CoroutineChannel<T> UnboundedCoroutineChannelOf<T>() => CoroutineChannel<T>.CreateUnbounded();
    public static CoroutineChannel<T> BoundedCoroutineChannelOf<T>(int capacity) => CoroutineChannel<T>.CreateBounded(capacity);
    public static CoroutineChannel<T> RendezvousCoroutineChannelOf<T>() => CoroutineChannel<T>.CreateRendezvous();
}