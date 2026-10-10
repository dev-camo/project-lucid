using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterVelocityStorage
    {
        private Character m_character;
        private VelocityRestoreParameters m_parameters;
        private Vector3 m_lastVelocity;
        private Vector3 m_storedLocalVelocity;
        private CharacterBuff_VelocityRestore m_velocityRestoreBuff;

        public Vector3 StoredLocalVelocity => m_storedLocalVelocity;
        public void Initialise(VelocityRestoreParameters parameters, Character character)
        {
            m_parameters = parameters;
            m_character = character;
        }
        public void StoreVelocity() => m_storedLocalVelocity = m_character.LocalVelocity;

        // Original 06000474: expire the previous record before clearing its field.
        // The new record is published before the buff handler's AddBuff call.
        public void RestoreStoredVelocity()
        {
            if (m_velocityRestoreBuff != null)
            {
                m_velocityRestoreBuff.Expire();
                m_velocityRestoreBuff = null;
            }
            m_character.BuffHandler.EndAllBuffsOfType<CharacterBuff_VelocityRestore>();
            if (!m_parameters.RestoreIfGreater && m_storedLocalVelocity.sqrMagnitude > m_character.WorldVelocityMagnitudeSqr)
            {
                m_storedLocalVelocity = Vector3.zero;
                return;
            }
            Character character = m_character;
            float durationSeconds = m_parameters.BlendDurationSeconds;
            Vector3 targetComponents = m_storedLocalVelocity;
            Vector3 enteredLocalVelocity = m_character.LocalVelocity;
            VelocityRestoreParameters restoreParameters = m_parameters;
            m_velocityRestoreBuff = new CharacterBuff_VelocityRestore(character, durationSeconds,
                targetComponents, enteredLocalVelocity, restoreParameters);
            m_character.BuffHandler.AddBuff(m_velocityRestoreBuff);
        }

        // Original 06000475 retains the buff reference after dispatching Expire.
        public void Reset()
        {
            m_storedLocalVelocity = Vector3.zero;
            if (m_velocityRestoreBuff != null)
                m_velocityRestoreBuff.Expire();
        }
        public CharacterVelocityStorage() { }
    }
}
