namespace CRoutines.Testing;

/// <summary>
/// Controls virtual time for deterministic testing
/// </summary>
public sealed class VirtualTimeController : ITimeProvider
{
    private TimeSpan _currentTime;
    private readonly SortedSet<ScheduledTask> _scheduledTasks = new(new ScheduledTaskComparer());
    private readonly object _lock = new();
    
    public TimeSpan CurrentTime
    {
        get
        {
            lock (_lock)
                return _currentTime;
        }
    }
    
    /// <summary>
    /// Schedule a task to run at a specific time
    /// </summary>
    public void Schedule(TimeSpan when, Func<Task> action)
    {
        lock (_lock)
        {
            _scheduledTasks.Add(new ScheduledTask(when, action));
        }
    }
    
    /// <summary>
    /// Advance time and execute scheduled tasks
    /// </summary>
    public async Task AdvanceTime(TimeSpan duration)
    {
        var targetTime = CurrentTime + duration;
        
        while (true)
        {
            ScheduledTask? taskToExecute = null;
            
            lock (_lock)
            {
                if (_scheduledTasks.Count == 0)
                    break;
                
                var nextTask = _scheduledTasks.Min!;
                if (nextTask.When > targetTime)
                    break;
                
                _scheduledTasks.Remove(nextTask);
                _currentTime = nextTask.When;
                taskToExecute = nextTask;
            }
            
            if (taskToExecute != null)
            {
                await taskToExecute.Action();
            }
        }
        
        lock (_lock)
        {
            _currentTime = targetTime;
        }
    }
    
    /// <summary>
    /// Delay implementation for virtual time
    /// </summary>
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
            return Task.CompletedTask;
        
        var tcs = new TaskCompletionSource();
        var targetTime = CurrentTime + duration;
        
        // Register cancellation
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }
        
        Schedule(targetTime, () =>
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        });
        
        return tcs.Task;
    }
    
    public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
        => Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
    
    /// <summary>
    /// Reset virtual time to zero
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentTime = TimeSpan.Zero;
            _scheduledTasks.Clear();
        }
    }
    
    private record ScheduledTask(TimeSpan When, Func<Task> Action);
    
    private class ScheduledTaskComparer : IComparer<ScheduledTask>
    {
        public int Compare(ScheduledTask? x, ScheduledTask? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            
            var timeCompare = x.When.CompareTo(y.When);
            if (timeCompare != 0) return timeCompare;
            
            // If same time, use reference equality to maintain set uniqueness
            return x.GetHashCode().CompareTo(y.GetHashCode());
        }
    }
}
