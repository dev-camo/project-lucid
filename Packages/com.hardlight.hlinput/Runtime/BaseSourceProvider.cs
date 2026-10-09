using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseSourceProvider
    {
        public abstract void Initialise(IBaseControllerProvider baseControllerProvider); // Original06000283.
        public abstract void Shutdown(); // Original06000284.
        public abstract void Update(); // Original06000285.
        protected BaseSourceProvider() { } // Original06000286, genuine Object constructor only.
    }
}
