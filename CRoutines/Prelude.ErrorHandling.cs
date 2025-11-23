using System.Runtime.CompilerServices;
using CRoutines.Core;

namespace CRoutines;

public static partial class Prelude
{
    // Error Boundary helpers (new features)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorBoundary errorBoundary(Action<Exception>? onError = null, bool isolateErrors = true)
        => new(onError, isolateErrors);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorPropagationConfig supervisorPropagation()
        => ErrorPropagationConfig.Supervisor;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorPropagationConfig isolatedPropagation()
        => ErrorPropagationConfig.Isolated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorPropagationConfig defaultPropagation()
        => ErrorPropagationConfig.Default;

    // Exception Handler helpers (new features)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineExceptionHandler loggingHandler()
        => CoroutineExceptionHandler.Logging();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineExceptionHandler customHandler(Action<Exception> handler)
        => new(handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineExceptionHandler filteredHandler<TException>(Action<TException> handler)
        where TException : Exception
        => CoroutineExceptionHandler.Filtered(handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CoroutineExceptionHandler combineHandlers(params CoroutineExceptionHandler[] handlers)
        => CoroutineExceptionHandler.Combine(handlers);
}
