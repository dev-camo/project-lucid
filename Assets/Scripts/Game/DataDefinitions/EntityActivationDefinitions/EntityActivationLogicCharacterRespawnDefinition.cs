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
    // Original Game.Runtime type 0x02000546.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "EntityActivationLogicCharacterRespawnDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicCharacterRespawnDefinition")]
    public class EntityActivationLogicCharacterRespawnDefinition : EntityActivationLogicDefinition
    {
        // Original 0x06001ca9.
        public override EntityActivationLogicType Type => EntityActivationLogicType.CharacterRespawn;
        // Original 0x06001caa.
        protected override Type ScriptType => typeof(EntityActivationLogicCharacterRespawn);
        // Original 0x06001cab.
        public EntityActivationLogicCharacterRespawnDefinition() { }
    }
}
