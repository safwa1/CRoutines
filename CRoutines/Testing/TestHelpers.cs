namespace CRoutines.Testing;

/// <summary>
/// Helper methods for testing coroutines
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Run a test with virtual time
    /// </summary>
    public static async Task RunTest(Func<TestCoroutineScope, Task> test, TimeSpan? timeout = null)
    {
        using var scope = new TestCoroutineScope();
        
        try
        {
            await test(scope);
            await scope.RunUntilIdle(timeout);
        }
        finally
        {
            scope.Cancel();
        }
    }
    
    /// <summary>
    /// Run a test with timeout and result
    /// </summary>
    public static async Task<T> RunTest<T>(
        Func<TestCoroutineScope, Task<T>> test,
        TimeSpan? timeout = null)
    {
        using var scope = new TestCoroutineScope();
        
        try
        {
            var result = await test(scope);
            await scope.RunUntilIdle(timeout);
            return result;
        }
        finally
        {
            scope.Cancel();
        }
    }
    
    /// <summary>
    /// Assert that a coroutine completes within virtual time
    /// </summary>
    public static async Task AssertCompletes(
        TestCoroutineScope scope,
        TimeSpan duration,
        Func<Task> action)
    {
        var startTime = scope.CurrentTime;
        var task = action();
        await scope.AdvanceTimeBy(duration);
        
        if (!task.IsCompleted)
            throw new TestAssertionException($"Operation did not complete within {duration}");
        
        await task; // Propagate exceptions
    }
    
    /// <summary>
    /// Assert that a coroutine does NOT complete within virtual time
    /// </summary>
    public static async Task AssertDoesNotComplete(
        TestCoroutineScope scope,
        TimeSpan duration,
        Func<Task> action)
    {
        var task = action();
        await scope.AdvanceTimeBy(duration);
        
        if (task.IsCompleted)
            throw new TestAssertionException($"Operation completed unexpectedly within {duration}");
    }
    
    /// <summary>
    /// Assert that the scope is idle
    /// </summary>
    public static void AssertIdle(TestCoroutineScope scope)
    {
        if (!scope.IsIdle)
            throw new TestAssertionException("Scope is not idle");
    }
    
    /// <summary>
    /// Assert that the scope is not idle
    /// </summary>
    public static void AssertNotIdle(TestCoroutineScope scope)
    {
        if (scope.IsIdle)
            throw new TestAssertionException("Scope is idle");
    }
}

/// <summary>
/// Exception thrown by test assertions
/// </summary>
public class TestAssertionException : Exception
{
    public TestAssertionException(string message) : base(message) { }
    public TestAssertionException(string message, Exception innerException) 
        : base(message, innerException) { }
}
