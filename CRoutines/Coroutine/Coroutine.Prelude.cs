using CRoutines.Coroutine;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{
    public static Task RunBlocking(
        Func<CoroutineScope, Task> block,
        ICoroutineDispatcher? dispatcher = null) => Coroutines.RunBlocking(block, dispatcher);

    public static CoroutineScope GlobalScope(ICoroutineDispatcher? dispatcher = null)
        => Coroutines.GlobalScope(dispatcher);
}