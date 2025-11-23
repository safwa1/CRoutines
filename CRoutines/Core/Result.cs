namespace CRoutines.Core;

/// <summary>
/// Represents a result that can be either success or error
/// Similar to Result types in functional programming
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Exception? _error;
    private readonly bool _isSuccess;

    private Result(T? value, Exception? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        _isSuccess = isSuccess;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result<T> Success(T value)
        => new(value, null, true);

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static Result<T> Failure(Exception error)
        => new(default, error ?? throw new ArgumentNullException(nameof(error)), false);

    /// <summary>
    /// Whether the result is successful
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Whether the result is a failure
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the value (throws if failed)
    /// </summary>
    public T Value => _isSuccess 
        ? _value! 
        : throw new InvalidOperationException("Result is a failure", _error);

    /// <summary>
    /// Gets the error (throws if successful)
    /// </summary>
    public Exception Error => !_isSuccess 
        ? _error! 
        : throw new InvalidOperationException("Result is successful");

    /// <summary>
    /// Gets the value or default
    /// </summary>
    public T? ValueOrDefault => _isSuccess ? _value : default;

    /// <summary>
    /// Gets the value or a fallback
    /// </summary>
    public T ValueOr(T fallback) => _isSuccess ? _value! : fallback;

    /// <summary>
    /// Gets the value or computes a fallback
    /// </summary>
    public T ValueOr(Func<Exception, T> fallbackFactory)
        => _isSuccess ? _value! : fallbackFactory(_error!);

    /// <summary>
    /// Maps the value if successful
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
        => _isSuccess 
            ? Result<TResult>.Success(mapper(_value!))
            : Result<TResult>.Failure(_error!);

    /// <summary>
    /// Maps the value if successful (async)
    /// </summary>
    public async Task<Result<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
        => _isSuccess 
            ? Result<TResult>.Success(await mapper(_value!))
            : Result<TResult>.Failure(_error!);

    /// <summary>
    /// Flat maps the result
    /// </summary>
    public Result<TResult> FlatMap<TResult>(Func<T, Result<TResult>> mapper)
        => _isSuccess ? mapper(_value!) : Result<TResult>.Failure(_error!);

    /// <summary>
    /// Flat maps the result (async)
    /// </summary>
    public async Task<Result<TResult>> FlatMapAsync<TResult>(Func<T, Task<Result<TResult>>> mapper)
        => _isSuccess ? await mapper(_value!) : Result<TResult>.Failure(_error!);

    /// <summary>
    /// Executes an action if successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess) action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if successful (async)
    /// </summary>
    public async Task<Result<T>> OnSuccessAsync(Func<T, Task> action)
    {
        if (_isSuccess) await action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if failed
    /// </summary>
    public Result<T> OnFailure(Action<Exception> action)
    {
        if (!_isSuccess) action(_error!);
        return this;
    }

    /// <summary>
    /// Executes an action if failed (async)
    /// </summary>
    public async Task<Result<T>> OnFailureAsync(Func<Exception, Task> action)
    {
        if (!_isSuccess) await action(_error!);
        return this;
    }

    /// <summary>
    /// Matches the result to either success or failure handler
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Exception, TResult> onFailure)
        => _isSuccess ? onSuccess(_value!) : onFailure(_error!);

    /// <summary>
    /// Matches the result to either success or failure handler (async)
    /// </summary>
    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess, 
        Func<Exception, Task<TResult>> onFailure)
        => _isSuccess ? await onSuccess(_value!) : await onFailure(_error!);

    public override string ToString()
        => _isSuccess ? $"Success({_value})" : $"Failure({_error?.Message})";
}

/// <summary>
/// Extension methods for Result
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a task to a Result, catching any exceptions
    /// </summary>
    public static async Task<Result<T>> ToResult<T>(this Task<T> task)
    {
        try
        {
            return Result<T>.Success(await task);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Converts a task to a Result, catching any exceptions
    /// </summary>
    public static async Task<Result<Unit>> ToResult(this Task task)
    {
        try
        {
            await task;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes a function and wraps result in Result
    /// </summary>
    public static Result<T> Try<T>(Func<T> func)
    {
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async function and wraps result in Result
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return Result<T>.Success(await func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }
}

/// <summary>
/// Unit type for void results
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Value = default;
}
