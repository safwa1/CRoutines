namespace CRoutines.Flows;

public interface ISharedFlow<out T>
{
    IDisposable Subscribe(Func<T, Task> collector);
}