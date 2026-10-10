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
    // Original Game.Runtime type 0x0200054d.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "EntityActivationLogicWaitForCutsceneDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicWaitForCutsceneDefinition")]
    public class EntityActivationLogicWaitForCutsceneDefinition : EntityActivationLogicDefinition
    {
        // Original 0x06001cc7.
        public override EntityActivationLogicType Type => EntityActivationLogicType.CutsceneWait;
        // Original 0x06001cc8.
        protected override Type ScriptType => typeof(EntityActivationLogicWaitForCutscene);
        // Original 0x06001cc9.
        public EntityActivationLogicWaitForCutsceneDefinition() { }
    }
}
