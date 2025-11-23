namespace CRoutines.Core;

/// <summary>
/// Error boundary pattern for coroutines
/// Prevents errors from propagating beyond a certain scope
/// </summary>
public class ErrorBoundary
{
    private readonly Action<Exception>? _onError;
    private readonly bool _isolateErrors;

    public ErrorBoundary(Action<Exception>? onError = null, bool isolateErrors = true)
    {
        _onError = onError;
        _isolateErrors = isolateErrors;
    }

    /// <summary>
    /// Executes a task within an error boundary
    /// </summary>
    public async Task<T?> Execute<T>(Func<Task<T>> action, T? fallbackValue = default)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            
            if (!_isolateErrors)
                throw;
            
            return fallbackValue;
        }
    }

    /// <summary>
    /// Executes a task within an error boundary (void)
    /// </summary>
    public async Task Execute(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            
            if (!_isolateErrors)
                throw;
        }
    }

    /// <summary>
    /// Executes a sync function within an error boundary
    /// </summary>
    public T? Execute<T>(Func<T> action, T? fallbackValue = default)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
            
            if (!_isolateErrors)
                throw;
            
            return fallbackValue;
        }
    }
}

/// <summary>
/// Error propagation configuration
/// </summary>
public class ErrorPropagationConfig
{
    /// <summary>
    /// Whether to propagate errors to parent scope
    /// </summary>
    public bool PropagateToParent { get; set; } = true;

    /// <summary>
    /// Whether to propagate errors to sibling coroutines
    /// </summary>
    public bool PropagateToSiblings { get; set; } = false;

    /// <summary>
    /// Maximum propagation depth (-1 for unlimited)
    /// </summary>
    public int MaxPropagationDepth { get; set; } = -1;

    /// <summary>
    /// Filter to determine which exceptions should propagate
    /// </summary>
    public Func<Exception, bool>? PropagationFilter { get; set; }

    /// <summary>
    /// Default configuration (propagate to parent only)
    /// </summary>
    public static ErrorPropagationConfig Default => new();

    /// <summary>
    /// Supervisor configuration (don't propagate)
    /// </summary>
    public static ErrorPropagationConfig Supervisor => new()
    {
        PropagateToParent = false,
        PropagateToSiblings = false
    };

    /// <summary>
    /// Isolated configuration (no propagation)
    /// </summary>
    public static ErrorPropagationConfig Isolated => new()
    {
        PropagateToParent = false,
        PropagateToSiblings = false,
        MaxPropagationDepth = 0
    };

    /// <summary>
    /// Checks if an exception should propagate
    /// </summary>
    public bool ShouldPropagate(Exception exception, int currentDepth = 0)
    {
        if (!PropagateToParent && !PropagateToSiblings)
            return false;

        if (MaxPropagationDepth >= 0 && currentDepth >= MaxPropagationDepth)
            return false;

        if (PropagationFilter != null && !PropagationFilter(exception))
            return false;

        return true;
    }
}

/// <summary>
/// Extension methods for error boundaries and propagation
/// </summary>
public static class ErrorBoundaryExtensions
{
    private static readonly AsyncLocal<ErrorBoundary?> _currentBoundary = new();
    private static readonly AsyncLocal<ErrorPropagationConfig?> _currentPropagationConfig = new();

    /// <summary>
    /// Gets the current error boundary
    /// </summary>
    public static ErrorBoundary? CurrentErrorBoundary
    {
        get => _currentBoundary.Value;
        set => _currentBoundary.Value = value;
    }

    /// <summary>
    /// Gets the current error propagation configuration
    /// </summary>
    public static ErrorPropagationConfig CurrentPropagationConfig
    {
        get => _currentPropagationConfig.Value ?? ErrorPropagationConfig.Default;
        set => _currentPropagationConfig.Value = value;
    }

    /// <summary>
    /// Runs code within an error boundary
    /// </summary>
    public static async Task<T> WithErrorBoundary<T>(
        Func<Task<T>> action,
        Action<Exception>? onError = null,
        bool isolateErrors = true,
        T? fallbackValue = default)
    {
        var boundary = new ErrorBoundary(onError, isolateErrors);
        var previous = CurrentErrorBoundary;
        
        try
        {
            CurrentErrorBoundary = boundary;
            var result = await boundary.Execute(action, fallbackValue);
            return result!;
        }
        finally
        {
            CurrentErrorBoundary = previous;
        }
    }

    /// <summary>
    /// Runs code within an error boundary (void)
    /// </summary>
    public static async Task WithErrorBoundary(
        Func<Task> action,
        Action<Exception>? onError = null,
        bool isolateErrors = true)
    {
        var boundary = new ErrorBoundary(onError, isolateErrors);
        var previous = CurrentErrorBoundary;
        
        try
        {
            CurrentErrorBoundary = boundary;
            await boundary.Execute(action);
        }
        finally
        {
            CurrentErrorBoundary = previous;
        }
    }

    /// <summary>
    /// Runs code with custom error propagation configuration
    /// </summary>
    public static async Task<T> WithErrorPropagation<T>(
        Func<Task<T>> action,
        ErrorPropagationConfig config)
    {
        var previous = _currentPropagationConfig.Value;
        
        try
        {
            _currentPropagationConfig.Value = config;
            return await action();
        }
        finally
        {
            _currentPropagationConfig.Value = previous;
        }
    }

    /// <summary>
    /// Runs code with custom error propagation configuration (void)
    /// </summary>
    public static async Task WithErrorPropagation(
        Func<Task> action,
        ErrorPropagationConfig config)
    {
        var previous = _currentPropagationConfig.Value;
        
        try
        {
            _currentPropagationConfig.Value = config;
            await action();
        }
        finally
        {
            _currentPropagationConfig.Value = previous;
        }
    }
}
