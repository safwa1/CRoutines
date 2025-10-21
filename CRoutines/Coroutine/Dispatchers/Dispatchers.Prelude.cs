using CRoutines.Coroutine.Dispatchers;

namespace CRoutines;

public static partial class Prelude
{
    public static class Dispatchers
    {
        public static ICoroutineDispatcher Default => DefaultDispatcher.Instance;
        public static ICoroutineDispatcher IO => IODispatcher.Instance;
        public static ICoroutineDispatcher Main(string name = "CoroutineThread") => new SingleThreadDispatcher(name);
        public static ICoroutineDispatcher Unconfined => UnconfinedDispatcher.Instance;
    }
}