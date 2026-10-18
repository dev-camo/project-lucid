using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020002b8; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbility_Braking : CharacterAbility_MovementGround<CharacterAbilityDefinition_Braking>, IAbilityTrigger
    {
        // 06001071 (1.10.1 RVA 0x742C80)
        public bool IsTriggered => IsBraking;

        public bool IsBraking { get; private set; }
        private float m_brakeElapsedTime;
        private Vector2 m_bufferedControllerMovement;
        private Vector2 m_bufferedControllerMovementVelocity;

        // 06001074 (1.10.1 RVA 0x742CB0)
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_brakeElapsedTime = 0f;
            m_bufferedControllerMovement = m_character.ControllerMovement;
            IsBraking = EvaluateInput(true);
        }

        // 06001075 (1.10.1 RVA 0x742F80). Leaving through the first branch keeps the
        // elapsed brake time; only the other paths update it.
        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (!brain.Enabled || !CanTurnAround)
            {
                IsBraking = false;
                return;
            }
            float timeSinceAirborne = m_character.GetTotalFixedTime() - m_character.TimeSecondsLastAirborne;
            float landingBufferSeconds = Definition.LandingBufferTimeSeconds;
            if (m_character.RawControllerMovementMagnitude > Definition.MinimumInputMagnitudeSqr)
            {
                m_bufferedControllerMovement = Vector2.SmoothDamp(m_bufferedControllerMovement, m_character.RawControllerMovement,
                    ref m_bufferedControllerMovementVelocity, Definition.BufferedIntentSmoothTimeSeconds,
                    Mathf.Infinity, deltaTime);
            }
            else
            {
                m_bufferedControllerMovement = Vector2.up;
            }
            // Written as >= so that a NaN time (unordered compare) is not treated as landing.
            IsBraking = EvaluateInput(landingBufferSeconds >= timeSinceAirborne);
            if (IsBraking)
                m_brakeElapsedTime += deltaTime;
            else
                m_brakeElapsedTime = 0f;
        }

        // 06001076 (1.10.1 RVA 0x743150)
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            m_brakeElapsedTime = 0f;
            IsBraking = false;
        }

        // 06001077 (1.10.1 RVA 0x743180). Outside braking the forward speed is untouched,
        // and the base ground motion is not consulted at all.
        public override void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection, float deltaTime,
            float motionMultiplier = 1f, float effectiveInputMagnitude = 1f,
            AnimationCurve turnTraitsAccelerationMultiplier = null, bool applyDeceleration = true)
        {
            if (!IsBraking || !applyDeceleration)
                return;
            CharacterMovementUtilities.ProcessDecelerationCurve(ref forwardSpeed, GetForwardMotion(),
                Definition.BrakePowerCurve, m_brakeElapsedTime, deltaTime, motionMultiplier);
        }

        // 06001078 (1.10.1 RVA 0x743240)
        protected override void GetDebugExtraInfo(StringBuilder extraInfo)
        {
            base.GetDebugExtraInfo(extraInfo);
            extraInfo.Append(string.Format("Brake time = {0} ", m_brakeElapsedTime));
        }

        // 06001079 (1.10.1 RVA 0x742D20). Every threshold test is written as the negated
        // >= the native compare uses, so NaN inputs fail the gate like the original.
        private bool EvaluateInput(bool evaluateForwardToIntent = false)
        {
            if (!(m_character.ControllerMovement.sqrMagnitude >= Definition.MinimumInputMagnitudeSqr))
                return false;
            float xzSpeedSqr = m_character.LocalVelocity.xz().sqrMagnitude;
            float xzSpeedThresholdSqr = IsBraking ? Definition.EndAtXZSpeedSqr : Definition.MinimumTriggerXZSpeedSqr;
            if (!(xzSpeedSqr >= xzSpeedThresholdSqr))
                return false;
            if (IsBraking)
                return m_character.GetIntendedToCurrentForwardAngle() >= Definition.ExitIntendedAngle;
            float intendedAngle = evaluateForwardToIntent
                ? m_character.GetIntendedToCurrentForwardAngle()
                : Vector2.Angle(m_bufferedControllerMovement, m_character.RawControllerMovement);
            return intendedAngle >= Definition.MinimumIntendedTriggerAngle;
        }

        // 0600107a (1.10.1 RVA 0x7432F0)
        public CharacterAbility_Braking() { }
    }
}
