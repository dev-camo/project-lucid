using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000678; methods 06002309..0600230d.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StraightLineProjectile : Projectile
    {
        private Vector3 m_velocity;

        // 06002309: one transform lookup serves both the position read and the
        // rotation write; a target within 1e-5 normalizes to zero velocity.
        public override void UpdateFocus(Vector3 target)
        {
            Transform projectileTransform = transform;
            Vector3 direction = (target - projectileTransform.position).normalized;
            m_velocity = direction * m_definition.Velocity;
            projectileTransform.rotation = Quaternion.LookRotation(direction);
        }

        // 0600230a: moves in world space and reports the same step for collisions.
        protected override Vector3 DoMovement(float adjustedDeltaTime)
        {
            Vector3 movement = m_velocity * adjustedDeltaTime;
            transform.Translate(movement, Space.World);
            return movement;
        }

        protected override bool ShouldIgnoreCollider(Collider collider) => false;

        protected override bool ShouldTeleportToHitPoint() => true;
    }
}
