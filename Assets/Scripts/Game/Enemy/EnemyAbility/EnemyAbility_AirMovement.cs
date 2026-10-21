using Cinemachine.Utility;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000632; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbility_AirMovement : EnemyAbility_Movement<EnemyAbilityDefinition_AirMovement>
    {
        // 06002165 (1.10.1 RVA 0x56E770): a flying enemy drops the velocity component along gravity. With a height
        // restriction it then casts along gravity, ignoring triggers, and when the ground distance is outside the
        // allowed band moves along the cast so that the distance is clamped (a ratio of zero when the ground is
        // within 0.0001 units, which puts the enemy on the hit point).
        public override void UpdateMovement(float deltaTime)
        {
            base.UpdateMovement(deltaTime);
            m_enemy.SetWorldVelocity(m_enemy.WorldVelocity.ProjectOntoPlane(m_enemy.GravityNormalised));
            if (!Definition.RestrictHeight)
            {
                return;
            }

            if (!Physics.Raycast(m_enemy.WorldPosition, m_enemy.GravityNormalised, out RaycastHit hit,
                    Definition.RaycastLength, Definition.RaycastTarget.value, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            float clampedDistance = Mathf.Clamp(hit.distance, Definition.MinHeight, Definition.MaxHeight);
            if (clampedDistance != hit.distance)
            {
                float ratio = hit.distance <= 0.0001f ? 0f : clampedDistance / hit.distance;
                m_enemy.SetWorldPosition(Vector3.LerpUnclamped(hit.point, m_enemy.WorldPosition, ratio));
            }
        }

        // 06002166 (1.10.1 RVA 0x56E9B0)
        public EnemyAbility_AirMovement() { }
    }
}
