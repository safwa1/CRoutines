using System.Runtime.CompilerServices;
using CRoutines.Contexts;
using CRoutines.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task RunBlocking(
        Func<CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null) => Coroutines.RunBlocking(block, dispatcher);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope GlobalScope(ICoroutineDispatcher? dispatcher = null)
        => Coroutines.GlobalScope(dispatcher);
}