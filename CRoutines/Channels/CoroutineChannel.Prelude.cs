using System.Runtime.CompilerServices;
using CRoutines.Channels;

namespace CRoutines;

public static partial class Prelude
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> UnboundedCoroutineChannelOf<T>() => CoroutineChannel<T>.CreateUnbounded();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> BoundedCoroutineChannelOf<T>(int capacity) => CoroutineChannel<T>.CreateBounded(capacity);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineChannel<T> RendezvousCoroutineChannelOf<T>() => CoroutineChannel<T>.CreateRendezvous();
}