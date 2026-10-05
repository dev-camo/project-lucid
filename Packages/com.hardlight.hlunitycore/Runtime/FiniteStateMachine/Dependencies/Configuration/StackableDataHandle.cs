using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StackableDataHandle
    {
        // 0x06000138; ARM64 0x1aa2430 only forwards to System.Object's ctor.
        public StackableDataHandle() { }
    }
}
