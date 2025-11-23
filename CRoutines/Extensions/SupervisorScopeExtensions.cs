using CRoutines.Contexts;
using CRoutines.Core;

namespace CRoutines.Extensions;

/// <summary>
/// Supervisor scope builder - failures don't propagate to parent
/// </summary>
public static class SupervisorScopeExtensions
{
    /// <summary>
    /// Creates a supervisor scope where child failures don't cancel siblings or parent
    /// Similar to Kotlin's supervisorScope
    /// </summary>
    public static async Task<T> Execute<T>(
        this CoroutineScope scope,
        Func<CoroutineScope, Task<T>> block)
    {
        // Create a supervisor job
        var supervisorJob = new SupervisorJob(scope.Job);
        var supervisorScope = new CoroutineScope(scope.Dispatcher, supervisorJob);

        try
        {
            return await block(supervisorScope);
        }
        finally
        {
            // Wait for all children to complete
            await supervisorScope.JoinAll();
        }
    }

    /// <summary>
    /// Creates a supervisor scope (void version)
    /// </summary>
    public static async Task Execute(
        this CoroutineScope scope,
        Func<CoroutineScope, Task> block)
    {
        await Execute<object?>(scope, async s =>
        {
            await block(s);
            return null;
        });
    }
}
