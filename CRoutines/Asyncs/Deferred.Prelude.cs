using System.Runtime.CompilerServices;
using CRoutines.Asyncs;
using CRoutines.Core;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Deferred<T> DeferredOf<T>(Task<T> task, Job job, Action? start = null) => new(task, job, start);
}