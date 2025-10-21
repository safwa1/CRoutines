namespace CRoutines.ManagedTasks;

public enum TaskState
{
    Created,
    Scheduled,
    Running,
    Paused,
    Canceled,
    Completed,
    Faulted
}