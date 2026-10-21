using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000278. Unlike its siblings it carries no Il2CppSetOption attributes.
    public class CinemachineMix_CharacterSpeed : CinemachineMix_CharacterBase
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

        // 0x06000dc6: the normalised speed is clamped to [-1, 1] before weighting.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!IsLive()) return;
            Vector3 velocity = Vector3.Scale(m_characterManager.GetCurrentCharacterUnsafe().LocalVelocity, m_velocityAxisMultiplier);
            float speed = m_useAbsoluteValues ? velocity.x + velocity.y + velocity.z : velocity.magnitude;
            UpdateBlendWeightings(Mathf.Clamp(speed / m_maxSpeed, -1f, 1f), deltaTime);
        }

        // 0x06000dc7
        public CinemachineMix_CharacterSpeed() { }
    }
}
