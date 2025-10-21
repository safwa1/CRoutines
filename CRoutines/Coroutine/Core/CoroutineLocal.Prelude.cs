using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Core;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineLocal<T> CoroutineLocalOf<T>() => new();
}