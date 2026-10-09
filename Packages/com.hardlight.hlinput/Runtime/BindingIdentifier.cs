using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BindingIdentifier
    {
        public string Identifier;
        public InputType InputType;
        public BindingIdentifier() { } // HLInput.Runtime 06000117: Object constructor only, null/zero retained.
    }
}
