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
    // Original Game.Runtime type 0x0200054c.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "EntityActivationLogicTimeDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicTimeDefinition")]
    public class EntityActivationLogicTimeDefinition : EntityActivationLogicDefinition
    {
        [SerializeField]
        [Tooltip("Time until activated.")]
        private float m_time = 5f;
        [SerializeField]
        [Tooltip("Time category for activation to be applied in.")]
        private TimeCategory m_timeCategory = TimeCategory.Environment;
        // Original 0x06001cc2/0x06001cc3.
        public override EntityActivationLogicType Type => EntityActivationLogicType.Time;
        protected override Type ScriptType => typeof(EntityActivationLogicTime);
        // Original 0x06001cc4/0x06001cc5.
        public float Time => m_time;
        public TimeCategory TimeCategory => m_timeCategory;
        // Original 0x06001cc6: 5 seconds and Environment are assigned before the base constructor.
        public EntityActivationLogicTimeDefinition() { }
    }
}
