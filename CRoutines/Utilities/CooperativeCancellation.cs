namespace CRoutines.Utilities;

/// <summary>
/// Utilities for cooperative cancellation in coroutines
/// </summary>
public static class CooperativeCancellation
{
    /// <summary>
    /// Yields execution to allow other coroutines to run (similar to Kotlin's yield())
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to check</param>
    /// <exception cref="OperationCanceledException">Thrown if cancellation is requested</exception>
    public static async Task Yield(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
