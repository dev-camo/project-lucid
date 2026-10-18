using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000ac5. A box collider in front of the character that reports
    // homing targets entering and leaving it to the homing attack ability.
    [RequireComponent(typeof(BoxCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HomingAttack : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The box collider to trigger targets being added to the homing attack ability.")]
        protected BoxCollider m_collider;
        private IHomingAttack m_ability;

        // 06003e1d; the character argument is unused. The box extends forward from the offset.
        public void Initialise(Character character, IHomingAttack ability)
        {
            m_ability = ability;
            SetColliderDimensions(m_ability.TargetZoneSize, m_ability.TargetZoneOffset);
        }

        // 06003e1e; the size is set before the centre, which sits half the depth ahead of the offset.
        public void SetColliderDimensions(Vector3 colliderSize, Vector3 colliderOffset)
        {
            m_collider.size = colliderSize;
            m_collider.center = colliderOffset + Vector3.forward * (colliderSize.z * 0.5f);
        }

        // 06003e1f
        private bool TryGetTarget(Collider c, out HomingTarget target) => c.TryGetComponent(out target);

        // 06003e20
        private void OnTriggerEnter(Collider otherCollider)
        {
            m_ability.OnTriggerEnter(otherCollider);
            if (TryGetTarget(otherCollider, out HomingTarget target) && target.enabled)
                m_ability.AddTarget(target);
        }

        // 06003e21
        private void OnTriggerExit(Collider otherCollider)
        {
            m_ability.OnTriggerExit(otherCollider);
            if (TryGetTarget(otherCollider, out HomingTarget target))
                m_ability.RemoveTarget(target);
        }

        // 06003e22
        public HomingAttack() { }
    }
}
