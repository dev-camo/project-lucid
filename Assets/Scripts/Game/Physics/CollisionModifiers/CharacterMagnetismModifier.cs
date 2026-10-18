using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000743. Pulls the character towards the trigger
    // along the frame captured on entry (travel direction projected onto the
    // trigger's plane, its right vector and its up vector).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterMagnetismModifier : CharacterCollisionModifier
    {
        [SerializeField]
        [Tooltip("Magnetism acceleration by distance on forward from origin.")]
        private AnimationCurve m_magnetismByDistanceForward = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField]
        [Tooltip("Magnetism acceleration by distance on lateral from origin.")]
        private AnimationCurve m_magnetismByDistanceLateral = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField]
        [Tooltip("Magnetism acceleration by distance on normal from origin.")]
        private AnimationCurve m_magnetismByDistanceOnNormal = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Scriptable object for the abilities valid for magnetism to act on the character.")]
        [SerializeField]
        private CharacterAbilityTypeGroup m_abilityTypeGroup;
        private Vector3 m_worldUp;
        private Vector3 m_worldForward;
        private Vector3 m_worldRight;

        // 060028de
        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetCharacter(other, out Character character)) return;
            m_worldUp = transform.up;
            m_worldForward = Vector3.ProjectOnPlane(character.WorldVelocityNormalised, m_worldUp).normalized;
            m_worldRight = Vector3.Cross(m_worldUp, m_worldForward);
        }

        // 060028df; accelerates against the offset from the trigger, one curve per axis.
        private void OnTriggerStay(Collider other)
        {
            if (!TryGetCharacter(other, out Character character)) return;
            if (m_abilityTypeGroup != null &&
                !ActorAbilityUtilities.AnyAbilityInUse(character, m_abilityTypeGroup.Abilities)) return;
            float deltaTime = Time.fixedDeltaTime;
            Vector3 offset = character.WorldPosition - transform.position;
            float forwardMagnitude = m_magnetismByDistanceForward.Evaluate(Vector3.Dot(m_worldForward, offset));
            float lateralMagnitude = -m_magnetismByDistanceLateral.Evaluate(Vector3.Dot(m_worldRight, offset));
            float normalMagnitude = m_magnetismByDistanceOnNormal.Evaluate(Vector3.Dot(m_worldUp, offset));
            Vector3 acceleration = lateralMagnitude * m_worldRight - forwardMagnitude * m_worldForward -
                normalMagnitude * m_worldUp;
            character.SetWorldVelocity(character.WorldVelocity + acceleration * deltaTime);
        }

        // 060028e0; the three curves are initialised before the base constructor.
        public CharacterMagnetismModifier() { }
    }
}
