using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000760 with nested CheckType 02000761 and the AddCharacterTrigger
    // closure 02000762 (its name follows from the member count before that method). An event trigger
    // that only reacts to the current character's collision collider and only while the conditions
    // hold. Native code inlines the base callbacks the overrides call.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterConditionalTriggerEvent : EventCollisionTrigger
    {
        // 02000761
        public enum CheckType
        {
            All = 0,
            Any = 1
        }

        [Tooltip("Determines if all or any of the checks must be true to trigger event.")]
        [SerializeField]
        private CheckType m_checkType;
        [Tooltip("Conditions to check to trigger event.")]
        [SerializeField]
        private TriggerCondition[] m_conditions;
        [Tooltip("If set, collider will also be ignored on conditions met.")]
        [SerializeField]
        private Collider m_collisionCollider;
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private bool m_isTriggered;

        // 06002945; the collision collider, when set, lets the physics layer ask this trigger
        // whether a contact may be modified.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_collisionCollider == null)
                return;
            m_collisionCollider.hasModifiableContacts = true;
        }

        // 06002946; only the current character's own collision collider can pass.
        protected override bool FilterCollision(Collider other)
        {
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                return false;
            if (other != character.ColliderCollision)
                return false;
            return EvaluateConditions(character, m_conditions, m_checkType);
        }

        // 06002947; a condition counts as met when its answer differs from its invert flag. Any
        // stops at the first met condition and All at the first unmet one; an empty list passes,
        // and so does a check type that is neither, after its first condition.
        public static bool EvaluateConditions(Character character, IEnumerable<TriggerCondition> conditions, CheckType checkType)
        {
            bool result = true;
            foreach (TriggerCondition condition in conditions)
            {
                bool met = condition.Evaluate(character);
                switch (checkType)
                {
                    case CheckType.All:
                        if (!met)
                            return false;
                        break;
                    case CheckType.Any:
                        if (met)
                            return true;
                        break;
                    default:
                        return true;
                }
                result &= met;
            }
            return result;
        }

        // 06002948
        protected override void OnTriggerEnter(Collider other)
        {
            base.OnTriggerEnter(other);
            EvaluateCharacterTrigger(other, true);
        }

        // 06002949; with a collision collider set, staying is driven by the character's fixed update
        // instead (OnCharacterFixedUpdate).
        protected override void OnTriggerStay(Collider other)
        {
            if (m_collisionCollider != null)
                return;
            base.OnTriggerStay(other);
        }

        // 0600294a
        protected override void OnTriggerExit(Collider other)
        {
            base.OnTriggerExit(other);
            EvaluateCharacterTrigger(other, false);
        }

        // 0600294b
        private void EvaluateCharacterTrigger(Collider other, bool addAction)
        {
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                return;
            if (other != character.ColliderCollision)
                return;
            AddCharacterTrigger(character, addAction);
        }

        // 0600294c; subscribes to (or leaves) the character's post-FSM fixed update and, when a
        // collision collider is set, registers its condition check with the track manager.
        private void AddCharacterTrigger(Character character, bool addAction)
        {
            if (m_isTriggered == addAction)
                return;
            m_isTriggered = addAction;
            if (addAction)
                character.OnFixedUpdatePostFSM += OnCharacterFixedUpdate;
            else
                character.OnFixedUpdatePostFSM -= OnCharacterFixedUpdate;
            if (m_collisionCollider != null)
            {
                Dictionary<int, Func<bool>> canModify = character.TrackManager.ColliderCollisionCanModify;
                if (addAction)
                    canModify.Add(m_collisionCollider.GetInstanceID(), () => EvaluateConditions(character, m_conditions, m_checkType));
                else
                    canModify.Remove(m_collisionCollider.GetInstanceID());
            }
        }

        // 0600294d
        private void OnCharacterFixedUpdate(Character character)
        {
            TryTriggerStay(character.ColliderCollision);
        }

        // 0600294e; leaving the scene releases the current character's subscription.
        private void OnDisable()
        {
            if (m_characterManagerRef.IsValid() && m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
                AddCharacterTrigger(character, false);
        }

        // 0600294f
        public CharacterConditionalTriggerEvent() { }
    }
}
