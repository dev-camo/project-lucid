using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000277.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineMix_CharacterLateral : CinemachineMix_CharacterBase
    {
        [Tooltip("When controller input is lifted, that value will be kept as the blend weighting for a smoother transition.")]
        [SerializeField]
        private bool m_preserveLastInput = true;
        [Tooltip("Divided by the lateral movement value to give a normalised value.")]
        [SerializeField]
        private float m_maxLateralSpeed;
        [Tooltip("Uses direct input values between -1 and 1 rather than calculated lateral movement.")]
        [SerializeField]
        private bool m_useDirectControllerInput;

        // 0x06000dc4: the base InternalUpdate is not called; an exact zero is skipped while preserving input.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!IsLive()) return;
            Character character = m_characterManager.GetCurrentCharacterUnsafe();
            float lateral = m_useDirectControllerInput
                ? character.ControllerMovement.x / m_maxLateralSpeed
                : character.Storage.GetValue(ActorFSMKeys.GroundInputDirection, default(Vector3), true).x / m_maxLateralSpeed;
            if (m_preserveLastInput && lateral == 0f) return;
            UpdateBlendWeightings(lateral, deltaTime);
        }

        // 0x06000dc5
        public CinemachineMix_CharacterLateral() { }
    }
}
