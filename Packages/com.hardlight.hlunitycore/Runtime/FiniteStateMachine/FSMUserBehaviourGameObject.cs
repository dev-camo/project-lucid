using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMUserBehaviourGameObject : FSMUserBehaviour, IGraphUser
    {
        // HLUnityCore.Runtime.dll:0x060004e4; arm64 0x1ad1da8.
        public IGraphStorage Storage { get; } = new FSMStorage();
        // Original token 0x060004e5; arm64 0x1ad1db0. No null fallback.
        public void DestroyUser() { Storage.Clear(); }
        // Original token 0x060004e6; arm64 0x1ad1e6c: return this unchanged.
        protected override IGraphUser AcquireUserInstance() => this;
        // Original token 0x060004e7; arm64 0x1ad1e70. Allocate original
        // five-entry history storage before the base flag initializers.
        public FSMUserBehaviourGameObject() { }
    }
}
