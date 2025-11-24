using CRoutines.Asyncs;
using CRoutines.Contexts;
using CRoutines.Core;
using CRoutines.Dispatchers;
using CRoutines.Utilities;

namespace CRoutines.Testing;

/// <summary>
/// Coroutine scope for testing with virtual time support
/// Wraps a CoroutineScope and provides test-specific functionality
/// </summary>
public sealed class TestCoroutineScope : IDisposable
{
    private readonly CoroutineScope _scope;
    private readonly TestDispatcher _dispatcher;
    private readonly VirtualTimeController _timeController;
    private readonly ITimeProvider _originalTimeProvider;
    private int _activeJobs;
    
    public TestCoroutineScope()
    {
        _dispatcher = new TestDispatcher();
        _scope = new CoroutineScope(_dispatcher);
        _timeController = new VirtualTimeController();
        
        // Save original and set virtual time
        _originalTimeProvider = Delay.TimeProvider;
        Delay.TimeProvider = _timeController;
        
        // Subscribe to job events
        _scope.JobStarted += OnJobStarted;
        _scope.JobCompleted += OnJobCompleted;
    }
    
    /// <summary>
    /// The underlying scope
    /// </summary>
    public CoroutineScope Scope => _scope;
    
    /// <summary>
    /// The test dispatcher
    /// </summary>
    public TestDispatcher Dispatcher => _dispatcher;
    
    /// <summary>
    /// Current virtual time
    /// </summary>
    public TimeSpan CurrentTime => _timeController.CurrentTime;
    
    /// <summary>
    /// Launch a fire-and-forget coroutine
    /// </summary>
    public Job Launch(
        Func<CoroutineContext, Task> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default)
        => _scope.Launch(block, dispatcher, start);
    
    /// <summary>
    /// Launch with result
    /// </summary>
    public Deferred<T> Async<T>(
        Func<CoroutineContext, Task<T>> block,
        ICoroutineDispatcher? dispatcher = null,
        CoroutineStart start = CoroutineStart.Default)
        => _scope.Async(block, dispatcher, start);
    
    /// <summary>
    /// Advance virtual time by specified duration
    /// </summary>
    public async Task AdvanceTimeBy(TimeSpan duration)
    {
        await _timeController.AdvanceTime(duration);
        await _dispatcher.Yield(); // Process scheduled tasks
    }
    
    /// <summary>
    /// Advance to a specific time
    /// </summary>
    public async Task AdvanceTimeTo(TimeSpan target)
    {
        var duration = target - CurrentTime;
        if (duration > TimeSpan.Zero)
            await AdvanceTimeBy(duration);
    }
    
    /// <summary>
    /// Run until all coroutines complete or timeout
    /// </summary>
    public async Task<bool> RunUntilIdle(TimeSpan? timeout = null)
    {
        var maxTime = timeout ?? TimeSpan.FromSeconds(10);
        var deadline = DateTime.UtcNow + maxTime;
        var lastActiveCount = -1;
        var stuckCount = 0;
        
        while (!IsIdle && DateTime.UtcNow < deadline)
        {
            // Check if we're making progress
            var currentActiveCount = _activeJobs;
            if (currentActiveCount == lastActiveCount && currentActiveCount > 0)
            {
                stuckCount++;
                if (stuckCount > 100) // Stuck for 100 iterations
                {
                    // Force advance virtual time to unstick
                    await AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
                    stuckCount = 0;
                }
            }
            else
            {
                stuckCount = 0;
            }
            lastActiveCount = currentActiveCount;
            
            // Advance time in small steps
            await AdvanceTimeBy(TimeSpan.FromMilliseconds(10));
            
            // Yield dispatcher
            await _dispatcher.Yield();
            
            // Small delay to prevent tight loop
            await Task.Delay(1);
        }
        
        return IsIdle;
    }
    
    /// <summary>
    /// Check if scope is idle (no active jobs)
    /// </summary>
    public bool IsIdle => _activeJobs == 0 && !_dispatcher.HasPendingTasks;
    
    /// <summary>
    /// Cancel all jobs
    /// </summary>
    public void Cancel() => _scope.Cancel();
    
    /// <summary>
    /// Join all jobs
    /// </summary>
    public Task JoinAll(CancellationToken cancellationToken = default)
        => _scope.JoinAll(cancellationToken);
    
    private void OnJobStarted()
    {
        Interlocked.Increment(ref _activeJobs);
    }
    
    private void OnJobCompleted()
    {
        Interlocked.Decrement(ref _activeJobs);
    }
    
    /// <summary>
    /// Dispose and restore original time provider
    /// </summary>
    public void Dispose()
    {
        // Unsubscribe from events
        _scope.JobStarted -= OnJobStarted;
        _scope.JobCompleted -= OnJobCompleted;
        
        // Restore original time provider
        Delay.TimeProvider = _originalTimeProvider;
        
        // Dispose scope
        _scope.Dispose();
        
        // Clear dispatcher
        _dispatcher.Clear();
    }
}
