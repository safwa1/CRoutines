using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{

    public static CoroutineScope CoroutineScope => new();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope CoroutineScopeOf(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null) => new(dispatcher, parentJob);
}