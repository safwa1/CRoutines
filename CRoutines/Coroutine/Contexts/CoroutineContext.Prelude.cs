using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Contexts;
using CRoutines.Coroutine.Core;
using CRoutines.Coroutine.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineContext CoroutineContextOf(Job job, ICoroutineDispatcher dispatcher) => new(job, dispatcher);
}