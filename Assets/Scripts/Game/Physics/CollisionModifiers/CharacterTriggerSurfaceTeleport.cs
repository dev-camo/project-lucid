using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000745. While the current character's collision collider is
    // inside this trigger, DoTeleport moves the character onto the surface of the trigger's
    // collider that lies along the local direction.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public class CharacterTriggerSurfaceTeleport : CharacterCollisionModifier
    {
        [SerializeField]
        private Vector3 m_localDirection = Vector3.forward;
        [SerializeField]
        private Collider m_collider;
        [Unity.Collections.ReadOnly]
        [SerializeField]
        private float m_rayDistance;
        private bool m_colliderActive;

        // 060028e8
        private void Reset() => m_collider = GetComponent<Collider>();

        // 060028e9; the ray is long enough to cross the whole collider.
        private void OnValidate()
        {
            if (gameObject.CanValidate())
                m_rayDistance = m_collider.bounds.size.magnitude;
        }

        // 060028ea
        private void OnTriggerEnter(Collider other)
        {
            if (TryGetCharacter(other, out Character _))
                m_colliderActive = true;
        }

        // 060028eb; casts out along the character's rotated direction, then back from the far
        // point against the collider, so the hit is the collider face on the far side from the
        // character (the first face the reverse ray meets).
        public void DoTeleport()
        {
            if (!m_colliderActive || !TryGetCharacter(out Character character))
                return;
            Ray ray = new Ray(character.WorldPosition, character.WorldRotation * m_localDirection);
            Ray reverseRay = new Ray(ray.GetPoint(m_rayDistance), -ray.direction);
            if (m_collider.Raycast(reverseRay, out RaycastHit hit, float.MaxValue))
                character.SetWorldPosition(hit.point);
        }

        // 060028ec
        private void OnTriggerExit(Collider other)
        {
            if (TryGetCharacter(other, out Character _))
                m_colliderActive = false;
        }

        // 060028ed
        public CharacterTriggerSurfaceTeleport() { }
    }
}
