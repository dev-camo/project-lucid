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
        public MissionScorerComboStage() { }
    }
}
