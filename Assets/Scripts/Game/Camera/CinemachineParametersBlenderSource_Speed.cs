using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000280.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlenderSource_Speed : CinemachineParametersBlenderSource
    {
        [Tooltip("Velocity magnitude is divided by this to get a normalised value.")]
        [SerializeField]
        private float m_maxSpeed;
        [Tooltip("Multiplied by the local velocity of the character.")]
        [SerializeField]
        private Vector3 m_velocityAxisMultiplier = Vector3.one;
        [Tooltip("If true, speed value will be a sum of the x, y and z local velocity of the character.\nOtherwise magnitude of local velocity will be used.")]
        [SerializeField]
        private bool m_useAbsoluteValues;

        // 0x06000de2: the normalised speed is clamped to [-1, 1] before blending.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_characterManager.TryGetCurrentCharacter(out Character character)) return;
            Vector3 velocity = Vector3.Scale(character.LocalVelocity, m_velocityAxisMultiplier);
            float speed = m_useAbsoluteValues ? velocity.x + velocity.y + velocity.z : velocity.magnitude;
            UpdateBlendValue(Mathf.Clamp(speed / m_maxSpeed, -1f, 1f), deltaTime);
        }

        // 0x06000de3
        public CinemachineParametersBlenderSource_Speed() { }
    }
}
