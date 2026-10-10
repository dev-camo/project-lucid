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
    // Original Game.Runtime type 0x02000549.
    [CreateAssetMenu(fileName = "EntityActivationLogicLevelActivateDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicLevelActivateDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicLevelActivateDefinition : EntityActivationLogicDefinition
    {
        [Tooltip("If true, activation will occur on initial level load as well as any subsequent activations, ie on restart or re-enter.")]
        [SerializeField]
        private bool m_activateOnLoad;
        // Original 0x06001cb7/0x06001cb8.
        public override EntityActivationLogicType Type => EntityActivationLogicType.LevelActivate;
        protected override Type ScriptType => typeof(EntityActivationLogicLevelActivate);
        // Original 0x06001cb9.
        public bool ActivateOnLoad => m_activateOnLoad;
        // Original 0x06001cba.
        public EntityActivationLogicLevelActivateDefinition() { }
    }
}
