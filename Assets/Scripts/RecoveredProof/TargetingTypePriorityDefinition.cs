using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "TargetingTypePriorityDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TargetingTypePriorityDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class TargetingTypePriorityDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.HomingTargetType m_targetType;

        [UnityEngine.Tooltip("Targets with a higher priority will be chosen over ones with a lower priority.")]
        [UnityEngine.SerializeField]
        private System.Int32 m_priority;

        // Original Game.Runtime 0x06001f5a, ARM 0x531fb4.
        public HardlightProject.HomingTargetType TargetType => m_targetType;

        // Original Game.Runtime 0x06001f5b, ARM 0x531fbc.
        public System.Int32 Priority => m_priority;

        // Original Game.Runtime 0x06001f5c, ARM 0x531fc4.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
