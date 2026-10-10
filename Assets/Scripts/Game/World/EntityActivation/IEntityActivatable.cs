// Preserves the original shipping declarations and inferred whole managed bodies.
// Compiler-generated identities and exceptional native fault ordering require separate qualification.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000a68.
    public interface IEntityActivatable<T> : IEntityActivatable
    {
        // Original 0x06003bcf/0x06003bd0/0x06003bd1, slots 0/1/2. Genuine abstract contracts, no constraints.
        List<T> EntitiesToEnable { get; }
        List<T> EntitiesToDisable { get; }
        void OnLevelActivated();
    }
}
