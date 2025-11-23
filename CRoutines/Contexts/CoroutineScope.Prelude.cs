using System.Runtime.CompilerServices;
using CRoutines.Contexts;
using CRoutines.Core;
using CRoutines.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{

    public static CoroutineScope CoroutineScope => new();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineScope CoroutineScopeOf(ICoroutineDispatcher? dispatcher = null, Job? parentJob = null) => new(dispatcher, parentJob);
}