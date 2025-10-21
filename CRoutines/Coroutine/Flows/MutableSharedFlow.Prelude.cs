using System.Runtime.CompilerServices;
using CRoutines.Coroutine.Flows;

namespace CRoutines;

public static partial class Prelude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableSharedFlow<T> MutableSharedFlowOf<T>() => new();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableStateFlow<T> MutableStateFlowOf<T>(T initial) => new(initial);
}