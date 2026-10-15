using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class MissionScorerComboStage
    {
        [SerializeField, Tooltip("How long the combo lasts while un-extended.")]
        private float m_comboTimeLimit = 5f;
        [SerializeField, Tooltip("Multiply any points added on top of the basic combo multiplier.")]
        private float m_comboMultiplier = 1f;
        [SerializeField, Tooltip("The biggest combo multiplier available.")]
        private float m_maxCombo = 2f;
        [SerializeField, Tooltip("Rate to drain the combo multiplier.")]
        private float m_drainPerSecond;
        public float ComboTimeLimit => m_comboTimeLimit;
        public float ComboMultiplier => m_comboMultiplier;
        public float MaxCombo => m_maxCombo;
        public float DrainPerSecond => m_drainPerSecond;
        // Original Game.Runtime 0x02000710 .ctor 0x06002789 (x86-64 0x5c07e0): one movsd
        // of the packed pair 0x40A00000/0x3F800000 (5f, 1f) to +0x10/+0x14, then
        // 0x40000000 (2f) to +0x18; +0x1c stays zero. Getters 0x06002785..88 read
        // +0x10, +0x14, +0x18 and +0x1c in that order.
        public MissionScorerComboStage() { }
    }
}
