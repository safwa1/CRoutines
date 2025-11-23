using System.Runtime.CompilerServices;
using CRoutines.Contexts;
using CRoutines.Core;
using CRoutines.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineContext CoroutineContextOf(Job job, ICoroutineDispatcher dispatcher) => new(job, dispatcher);
}