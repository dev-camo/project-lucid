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
    // Original Game.Runtime type 0x0200054a.
    [CreateAssetMenu(fileName = "EntityActivationLogicMissionActivateDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicMissionActivateDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicMissionActivateDefinition : EntityActivationLogicDefinition
    {
        [SerializeField]
        private bool m_deactivates;
        // Original 0x06001cbb/0x06001cbc.
        public override EntityActivationLogicType Type => EntityActivationLogicType.MissionActivate;
        protected override Type ScriptType => typeof(EntityActivationLogicMissionActivate);
        // Original 0x06001cbd.
        public bool Deactivates => m_deactivates;
        // Original 0x06001cbe.
        public EntityActivationLogicMissionActivateDefinition() { }
    }
}
