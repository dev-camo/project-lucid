using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x0200007b: constructor-only owner; the generic base
    // BaseInputVectorisedGameInput supplies the callbacks.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputVectorised : BaseInputVectorisedGameInput<GameInputBinding, BindingData>
    {
        public InputVectorised() { } // 0x06000255
    }
}
