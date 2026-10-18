using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 020001d1. Records where a jump starts and ends.
    // The retail build compiles out its diagnostic output (the velocity log and the
    // System.Math use after the end position is stored); only state changes remain.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterJumpTrackerTrigger : CharacterTrigger
    {
        private Character m_character;
        private bool m_hasStartedJump;
        private bool m_jumpComplete;
        private bool m_hasLoggedVelocity;
        private Vector3 m_startPosition;
        private Vector3 m_startVelocity;
        private Vector3 m_endPosition;

        // 060009c4
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            m_character = character;
        }

        // 060009c5; the start state is sampled on the first tick, completion needs
        // ground contact and no jumping flag.
        private void FixedUpdate()
        {
            if (m_jumpComplete || m_character == null) return;
            if (!m_hasStartedJump)
            {
                m_hasStartedJump = m_character.Storage.GetValue<bool>(ActorFSMKeys.IsJumping, false, true);
                m_startPosition = m_character.WorldPosition;
                m_startVelocity = m_character.WorldVelocity;
                return;
            }
            if (!m_hasLoggedVelocity) m_hasLoggedVelocity = true;
            m_jumpComplete = m_character.ActiveTracking.IsOnGround() &&
                !m_character.Storage.GetValue<bool>(ActorFSMKeys.IsJumping, false, true);
            if (m_jumpComplete) m_endPosition = m_character.WorldPosition;
        }

        // 060009c6
        public CharacterJumpTrackerTrigger() { }
    }
}
