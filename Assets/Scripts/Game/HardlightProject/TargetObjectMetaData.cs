using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class TargetObjectMetaData // 0200030f, genuine abstract API.
    {
        public abstract bool IsTargetable { get; } // 060012b7
        public abstract void DoOnTargeted(); // 060012b8
        protected TargetObjectMetaData() { } // 060012b9
    }
}
