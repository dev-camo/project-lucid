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
    // Original Game.Runtime type 0x0200054b.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "EntityActivationLogicPlatformResetDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicPlatformResetDefinition")]
    public class EntityActivationLogicPlatformResetDefinition : EntityActivationLogicDefinition
    {
        // Original 0x06001cbf.
        public override EntityActivationLogicType Type => EntityActivationLogicType.PlatformReset;
        // Original 0x06001cc0.
        protected override Type ScriptType => typeof(EntityActivationLogicPlatformResetEvent);
        // Original 0x06001cc1.
        public EntityActivationLogicPlatformResetDefinition() { }
    }
}
