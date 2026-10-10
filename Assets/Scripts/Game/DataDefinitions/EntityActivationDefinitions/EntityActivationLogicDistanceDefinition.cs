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
    // Original Game.Runtime type 0x02000548.
    [CreateAssetMenu(fileName = "EntityActivationLogicDistanceDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationLogicDistanceDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationLogicDistanceDefinition : EntityActivationLogicDefinition
    {
        // Original 0x06001cb1/0x06001cb2.
        public override EntityActivationLogicType Type => EntityActivationLogicType.Distance;
        protected override Type ScriptType => typeof(EntityActivationLogicDistance);
        // Original 0x06001cb3/0x06001cb4; the generated backing field precedes m_distance in original field order.
        public float DistanceSqr { get; private set; }
        [SerializeField]
        private float m_distance = 10f;
        // Original 0x06001cb5: only Awake fills the cached square.
        private void Awake() => DistanceSqr = m_distance * m_distance;
        // Original 0x06001cb6; m_distance is assigned before the base constructor.
        public EntityActivationLogicDistanceDefinition() { }
    }
}
