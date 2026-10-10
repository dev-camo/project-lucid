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
    // Original Game.Runtime type 0x020006d3.
    public struct EntityActivationMessage
    {
        private readonly EntityActivationLogicType Type;
        // Original 0x060024f2; a direct enum-width store, with no additional fields or members.
        public EntityActivationMessage(EntityActivationLogicType type) { Type = type; }
    }
}
