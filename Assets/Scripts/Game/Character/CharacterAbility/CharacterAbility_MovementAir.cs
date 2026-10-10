using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbility_MovementAir : CharacterAbility_MovementAir<CharacterAbilityDefinition_MovementAir>
    {
        private bool m_resetBrain;

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (m_resetBrain)
            {
                brain.EndAction((GameAction)0x78c13968);
                m_character.BoostStamina.UpdateDeactivation(false);
                m_resetBrain = false;
                return;
            }
            bool hitProjected = m_character.Collider.GroundHit.HitProjected;
            float maxDistanceSquared = m_character.Settings.Collider.GroundCheckMaxDistanceSqr;
            IGraphStorage storage = m_character.Storage;
            bool trackerClose = false;
            if (storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true))
                trackerClose = (m_character.WorldPosition - m_character.Tracker.TrackerPosition).sqrMagnitude < maxDistanceSquared;

            bool canJump = m_character.Storage.GetValue(ActorFSMKeys.CanQueueJump, false, true);
            bool jump = brain.Jump;
            if ((canJump && jump) || m_character.QueuedActions.Jump)
            {
                m_character.QueuedActions.Jump = trackerClose || hitProjected;
                if (!m_character.QueuedActions.Jump) brain.EndAction((GameAction)0x78c13968);
            }
            bool canBoost = m_character.Storage.GetValue(ActorFSMKeys.CanQueueBoost, false, true);
            bool boost = brain.Boost;
            if ((canBoost && boost) || m_character.QueuedActions.Boost)
                m_character.QueuedActions.Boost = trackerClose || hitProjected;
        }

        public void ResetBuffers(bool resetBrain)
        {
            if (resetBrain) m_resetBrain = true;
        }

        public override void UpdateClampedVelocity(ref Vector3 worldVelocity)
        {
            Vector3 up = m_character.WorldUp;
            float downward = Vector3.Dot(worldVelocity, up);
            if (downward < -Definition.TerminalVelocity)
                worldVelocity -= up * (downward + Definition.TerminalVelocity);
        }

        public CharacterAbility_MovementAir() { }
    }
}
