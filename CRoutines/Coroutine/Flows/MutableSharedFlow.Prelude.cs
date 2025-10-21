using CRoutines.Coroutine.Flows;

namespace CRoutines;

public static partial class Prelude
{
    public static MutableSharedFlow<T> MutableSharedFlowOf<T>() => new();
    public static MutableStateFlow<T> MutableStateFlowOf<T>(T initial) => new(initial);
}