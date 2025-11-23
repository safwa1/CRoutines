namespace CRoutines.Core;

public sealed class CoroutineLocal<T>
{
    private static readonly AsyncLocal<T?> Local = new();

    public T? Value
    {
        get => Local.Value;
        set => Local.Value = value;
    }
}