using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020002b7; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_AirStompAttack : CharacterAbility_Air
    {
        private CharacterAbilityDefinition_AirStompAttack m_airStompDefinition;

        // 06001069 (1.10.1 RVA 0x742580). The cast is guarded: a different definition
        // type leaves any previous reference in place instead of clearing it.
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            if (Definition is CharacterAbilityDefinition_AirStompAttack airStompDefinition)
                m_airStompDefinition = airStompDefinition;
        }

        // 0600106a (1.10.1 RVA 0x742640)
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_character.QueuedActions.ResetBuffers();
        }

        // 0600106b (1.10.1 RVA 0x7426D0). Bitwise OR keeps the eager brain getter calls.
        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (!AirActive)
                return;
            m_character.QueuedActions.Jump |= brain.Jump;
            m_character.QueuedActions.Boost |= brain.Boost;
            TryRotateDownhillOfCollision(out float _);
        }

        // 0600106c (1.10.1 RVA 0x742770). Returns true when the character is
        // oriented downhill, or is already facing it; boostSpeed then holds the
        // slope setting's speed. Both slope maths helpers are the UnityEngine
        // bodies the native build inlines (Vector3.Angle, ProjectOnPlane, normalized).
        private bool TryRotateDownhillOfCollision(out float boostSpeed)
        {
            boostSpeed = 0f;
            RaycastColliderHit hit = m_character.Collider.GroundHit;
            if (!hit.Hit && !hit.HitProjected)
                return false;
            if (!hit.Hit)
                m_character.SetWorldPosition(hit.HitPosition());
            Vector3 normal = hit.HitNormal();
            Vector3 gravity = m_character.GravityNormalised;
            float slopeAngle = Vector3.Angle(normal, -gravity);
            Vector3 downhill = Vector3.ProjectOnPlane(gravity, normal).normalized;
            m_airStompDefinition.GetSettingsForSlope(slopeAngle, out bool orientDownhill, out boostSpeed);
            if (orientDownhill)
            {
                m_character.SetWorldRotation(Quaternion.LookRotation(downhill, normal));
                return true;
            }
            if (Vector3.Dot(downhill, m_character.ForwardDirection) > 0f)
                return true;
            boostSpeed = 0f;
            return false;
        }

        // 0600106d (1.10.1 RVA 0x742BB0)
        protected override bool IsBrainImpulseActive(CharacterBrain brain)
        {
            if (AirActive && IsControlContinuous())
                return !brain.AirStompAttack;
            return brain.AirStompAttack;
        }

        // 0600106e (1.10.1 RVA 0x742C00)
        protected override void EndBrainImpulse()
        {
            m_character.EndAirStompAttackImpulse();
        }

        // 0600106f (1.10.1 RVA 0x742C10)
        public override void Reset(bool interrupted, bool resetActiveCount)
        {
            base.Reset(interrupted, resetActiveCount);
            if (!interrupted)
                return;
            m_character.TriggerBurstAttackAir(true);
            if (TryRotateDownhillOfCollision(out float boostSpeed))
                m_character.SetLocalVelocity(new Vector3(0f, 0f, boostSpeed));
        }

        // 06001070 (1.10.1 RVA 0x742C70)
        public CharacterAbility_AirStompAttack() { }
    }
}
