using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x0200007b: complete constructor-only owner;
    // its genuine full generic base and callbacks are existing Input44 reuse.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputVectorised : BaseInputVectorisedGameInput<GameInputBinding, BindingData>
    {
        public InputVectorised() { } // 0x06000255
    }
}
