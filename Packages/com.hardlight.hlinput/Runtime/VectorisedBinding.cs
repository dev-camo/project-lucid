using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLInput.Runtime 0x02000016: complete original local API is three fields
    // and zero methods. No constructor exists in shipped native metadata.
    // Its implicit C# constructor is presently unbound and cannot call the
    // genuine parameterized BaseBinding constructor; no invented body is used.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public partial class VectorisedBinding : BaseBinding
    {
        public List<GameInput> XComponents;
        public List<GameInput> YComponents;
        public List<GameInput> ZComponents;
    }
}
