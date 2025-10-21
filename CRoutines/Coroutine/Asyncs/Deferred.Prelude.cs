using CRoutines.Coroutine.Asyncs;
using CRoutines.Coroutine.Core;

namespace CRoutines;

public static partial class Prelude
{
    public static Deferred<T> DeferredOf<T>(Task<T> task, Job job, Action? start = null) =>
        new Deferred<T>(task, job, start);
}