using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000315. Input collection, queued/application state,
    // and a temporarily locked raw cache remain distinct. Timestamps track BOTH
    // rising and falling applied edges; this class does not implement a controller.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterBrain
    {
        public bool Enabled { get; private set; } = true;
        private CharacterActionFlags m_actionStateQueued;
        private CharacterActionFlags m_actionStateApplied;
        private CharacterActionFlags m_actionStateRaw;
        private CharacterActionFlags m_ignoreEnableActions;
        private CharacterActionFlags m_actionStateDeferred;
        private bool m_actionStateActivatedThisFrame;
        private readonly Dictionary<GameAction, float> m_actionStateTimestamps =
            new Dictionary<GameAction, float>(HardlightEnumComparers.GameActionComparer);
        private Vector2 m_movementAggregate;
        private Vector2 m_movementState;
        private float m_cacheLockTimestamp;
        private CharacterActionFlags m_actionStateCache;

        public CharacterActionFlags ActionStateQueued { get { return m_actionStateQueued; } }
        public bool Jump { get { return m_actionStateApplied.GetValue(GameAction.CharacterJump); } }
        public bool RailTargeting { get { return m_actionStateApplied.GetValue(GameAction.CharacterRailTarget); } }
        public bool HomingAttackTarget { get { return m_actionStateApplied.GetValue(GameAction.CharacterHomingAttack); } }
        public bool HomingAttackOneShot { get { return m_actionStateApplied.GetValue(GameAction.CharacterHomingAttackOneShot); } }
        public bool HomingAttackLightspeedDash { get { return m_actionStateApplied.GetValue(GameAction.CharacterLightspeedDash); } }
        public bool AirActive { get { return m_actionStateApplied.GetValue(GameAction.CharacterAirActivate); } }
        public bool AirCancel { get { return m_actionStateApplied.GetValue(GameAction.CharacterAirCancel); } }
        public bool Boost { get { return m_actionStateApplied.GetValue(GameAction.CharacterBoost); } }
        public bool BurstAttack { get { return m_actionStateApplied.GetValue(GameAction.CharacterBurstAttack); } }
        public bool AirStompAttack { get { return m_actionStateApplied.GetValue(GameAction.CharacterAirStompAttack); } }
        public bool SpinDashCharge { get { return m_actionStateApplied.GetValue(GameAction.CharacterSpinDashCharge); } }
        public bool Roll { get { return m_actionStateApplied.GetValue(GameAction.CharacterRoll); } }

        protected internal void AddMovement(Vector2 direction, float amount)
        {
            m_movementAggregate += direction * amount;
        }
        public Vector2 GetMovement() { return m_movementState; }
        public void CacheMovement()
        {
            m_movementState = m_movementAggregate;
            if (m_movementState.sqrMagnitude > 1f) m_movementState.Normalize();
            m_movementAggregate = Vector2.zero;
        }
        private void UpdateTimestamps(CharacterActionFlags actions, float timestamp)
        {
            int count = CharacterActionFlags.ActionCount;
            for (int i = 0; i < count; ++i)
            {
                GameAction action = CharacterActionFlags.GetAction(i);
                if (m_actionStateApplied.GetValue(action) != actions.GetValue(action))
                    m_actionStateTimestamps[action] = timestamp;
            }
        }
        public float GetActionTimestamp(GameAction action)
        {
            return m_actionStateTimestamps.GetValueOrDefault(action, 0f);
        }
        public void ApplyActions(CharacterActionFlags actions, float timestamp)
        {
            UpdateTimestamps(actions, timestamp);
            m_actionStateQueued = actions;
            m_actionStateApplied = actions;
        }
        public bool GetAppliedState(GameAction action) { return m_actionStateApplied.GetValue(action); }
        public bool GetRawState(GameAction action, float movementThreshold = 0.5f)
        {
            switch (action)
            {
                case GameAction.CharacterMovementHorizontal: return Mathf.Abs(m_movementState.x) > movementThreshold;
                case GameAction.CharacterMovementVertical: return Mathf.Abs(m_movementState.y) > movementThreshold;
                case GameAction.CharacterMovementRight: return m_movementState.x > movementThreshold;
                case GameAction.CharacterMovementUp: return m_movementState.y > movementThreshold;
                case GameAction.CharacterMovementLeft: return m_movementState.x < -movementThreshold;
                case GameAction.CharacterMovementDown: return m_movementState.y < -movementThreshold;
                default: return m_cacheLockTimestamp > 0f ? m_actionStateCache.GetValue(action) : m_actionStateRaw.GetValue(action);
            }
        }
        public void LockCache(float lockTimestamp)
        {
            m_cacheLockTimestamp = Mathf.Max(m_cacheLockTimestamp, lockTimestamp);
            m_actionStateCache = m_actionStateRaw;
        }
        protected internal void SetState(GameAction action, bool value)
        {
            m_actionStateRaw.SetValue(action, value);
            if (!value)
            {
                m_actionStateQueued.SetValue(action, false);
                m_actionStateDeferred.SetValue(action, false);
            }
            else if (!m_actionStateActivatedThisFrame)
            {
                if (Enabled || m_ignoreEnableActions.GetValue(action)) m_actionStateQueued.SetValue(action, true);
                else m_actionStateDeferred.SetValue(action, true);
            }
        }
        public void EndAction(GameAction action) { SetState(action, false); }
        public void SetEnabled(bool value, float timestamp)
        {
            if (Enabled && !value)
            {
                EndImpulses(timestamp);
                m_actionStateDeferred = m_actionStateQueued;
                Enabled = false;
            }
            else if (!Enabled && value)
            {
                // A disabled-to-enabled transition completes through the original next-frame callback.
                m_actionStateActivatedThisFrame = true;
                CoroutineUtils.OnNextFrame(() => Enabled = true);
            }
        }
        public void EndImpulses(float timestamp)
        {
            SetState(GameAction.CharacterJump, false);
            SetState(GameAction.CharacterAirActivate, false);
            SetState(GameAction.CharacterAirCancel, false);
            SetState(GameAction.CharacterRailTarget, false);
            SetState(GameAction.CharacterHomingAttack, false);
            SetState(GameAction.CharacterLightspeedDash, false);
            SetState(GameAction.CharacterBurstAttack, false);
            SetState(GameAction.CharacterAirStompAttack, false);
            SetState(GameAction.CharacterSpinDashCharge, false);
            Update(timestamp);
        }
        public virtual void Initialise()
        {
            CharacterActionFlags.Initialise();
            m_ignoreEnableActions.SetValue(GameAction.CharacterHomingAttack, true);
            m_ignoreEnableActions.SetValue(GameAction.CharacterHomingAttackOneShot, true);
            m_ignoreEnableActions.SetValue(GameAction.CharacterLightspeedDash, true);
            m_ignoreEnableActions.SetValue(GameAction.CharacterRailTarget, true);
        }
        public void Update(float timestamp)
        {
            CharacterActionFlags actions = m_cacheLockTimestamp > 0f ? m_actionStateCache : m_actionStateQueued;
            if (Enabled && m_actionStateActivatedThisFrame)
            {
                actions = m_actionStateDeferred;
                m_actionStateActivatedThisFrame = false;
            }
            UpdateTimestamps(actions, timestamp);
            m_actionStateQueued = actions;
            m_actionStateApplied = actions;
            if (m_cacheLockTimestamp == 0f || m_cacheLockTimestamp > timestamp) return;
            m_cacheLockTimestamp = 0f;
            m_actionStateCache.Clear();
        }
        public abstract void Close();
        protected CharacterBrain() { }
    }
}
