namespace CRoutines.Core;

/// <summary>
/// Represents the state of a Job in its lifecycle
/// </summary>
public enum JobState
{
    /// <summary>
    /// Job is created but not yet started or still running
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed = 1,
    
    /// <summary>
    /// Job was cancelled
    /// </summary>
    Cancelled = 2,
    
    /// <summary>
    /// Job failed with an exception
    /// </summary>
    Faulted = 3
}
