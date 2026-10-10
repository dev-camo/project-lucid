using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_MovementGroundSpinDashCharge : CharacterAbility_MovementGround<CharacterAbilityDefinition_MovementGroundSpinDashCharge>, IAbilityTrigger
    {
        public bool IsTriggered { get; private set; }
        private float m_speedMin;
        private float m_speedMax;
        private bool m_canBeQueued = true;

        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_speedMin = Definition.LaunchSpeed[0].value;
            m_speedMax = Definition.LaunchSpeed[Definition.LaunchSpeed.length - 1].value;
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            CharacterSpinDashRoll spinDash = m_character.SpinDashRoll;
            bool trigger = brain.SpinDashCharge;
            if (m_canBeQueued && IsTriggered && !trigger && spinDash.ChargeTime > 0f)
                IsTriggered = ProcessQueuedTrigger(spinDash);
            else
            {
                IsTriggered = trigger;
                if (trigger)
                {
                    if (spinDash.ChargeTime == 0f) OnChargeStart();
                    spinDash.AdjustChargeTime(deltaTime);
                }
            }
        }

        // Original06001166 uses the genuine typed air provider and disposes its
        // List enumerator on normal completion and callback faults.
        private void OnChargeStart()
        {
            if (Definition.ResetAirAbilities == null || Definition.ResetAirAbilities.Count <= 0) return;
            foreach (ActorAbilityType abilityType in Definition.ResetAirAbilities)
            {
                if (!m_character.TryGetAbility(abilityType, out CharacterAbility_Air ability)) continue;
                if (ability.AirActive) ability.Reset(true, true);
            }
        }

        private bool ProcessQueuedTrigger(CharacterSpinDashRoll spinDash)
        {
            float time = m_character.GetTotalFixedTime()
                - m_character.GetBrainActionTimestamp(GameAction.CharacterSpinDashCharge);
            float queuedTriggerTime = Definition.QueuedTriggerTime;
            // Both shipping branches reset for unordered comparisons as well.
            if (!(time < queuedTriggerTime)) spinDash.ResetChargeTime();
            return time < queuedTriggerTime;
        }

        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            IsTriggered = false;
        }

        public override bool GetUIText(StringBuilder stringInfoBuilder)
        {
            base.GetUIText(stringInfoBuilder);
            float chargeTime = m_character.SpinDashRoll.ChargeTime;
            stringInfoBuilder.AppendLine(string.Format("LaunchSpeed = {0:F0}", GetLaunchSpeed(chargeTime)));
            return true;
        }

        public float GetLaunchSpeed(float chargeTime) => Definition.LaunchSpeed.Evaluate(chargeTime);
        public float GetLaunchProgress(float chargeTime) => (GetLaunchSpeed(chargeTime) - m_speedMin) / (m_speedMax - m_speedMin);
        public void OnCharge() => m_canBeQueued = false;

        // Original0600116d publishes queue availability before any lookup;
        // velocity and storage prefixes survive faults in later curve/delegate calls.
        public void OnLaunch()
        {
            m_canBeQueued = true;
            float chargeTime = m_character.SpinDashRoll.ChargeTime;
            if (IsTriggered || chargeTime < 0.0001f) return;
            float launchSpeed = GetLaunchSpeed(chargeTime);
            float speed = Mathf.Max(m_character.WorldVelocityMagnitude, launchSpeed);
            m_character.SetWorldVelocity(m_character.ForwardDirection * speed);
            m_character.Storage.SetValue(ActorFSMKeys.SpeedLockCanBeExceeded, Definition.VelocityLockCanBeExceeded);
            AddModifierOverride((int)DirectionModifierType.Speed, speed,
                Definition.VelocityLockTime.Evaluate(speed), Definition.VelocityLockTimeCategory,
                () => m_character.Storage.RemoveValue<bool>(ActorFSMKeys.SpeedLockCanBeExceeded));
            m_character.SpinDashRoll.ResetChargeTime();
            m_character.Storage.SetValue(ActorFSMKeys.RollingActive, true);
        }

        public CharacterAbility_MovementGroundSpinDashCharge() { }
    }
}
