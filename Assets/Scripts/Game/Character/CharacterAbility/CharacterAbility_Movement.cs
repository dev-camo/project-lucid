using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbility_Movement<T> : CharacterAbility<T> where T : CharacterAbilityDefinition_Movement
    {
        public bool Detach { get; private set; }
        protected bool CanJump { get; private set; }
        public bool CanTurnAround { get; protected set; }
        public bool OrientateToWorldUp { get; private set; }
        public bool TurnAround { get; protected set; }
        public AnimationCurve GripAgainstGravityMultiplierCurve { get; private set; }
        public AnimationCurve GripWithGravityMultiplierCurve { get; private set; }
        public AnimationCurve GripSpeedMultiplierCurve { get; private set; }
        public float SlopeAngle { get; private set; }
        protected CharacterAbilityDefinition_Movement.SlopeMotion m_slopeOverride;

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            UpdateForSlope();
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            UpdateForSlope();
        }

        // Original 06001139: publish defaults before the virtual override lookup.
        private void UpdateForSlope()
        {
            SlopeAngle = ActorMovementUtilities.GetSlopeAngle(m_character);
            float speed = m_character.WorldVelocityMagnitude;
            TurnAround = false;
            Detach = false;
            CanJump = true;
            CanTurnAround = true;
            OrientateToWorldUp = false;
            GripAgainstGravityMultiplierCurve = null;
            GripWithGravityMultiplierCurve = null;
            GripSpeedMultiplierCurve = null;
            if (TryGetSlopeOverride(out m_slopeOverride))
            {
                GravityMultiplier = m_slopeOverride.GravityMultiplier;
                GripAgainstGravityMultiplierCurve = m_slopeOverride.GripAgainstGravityMultiplierCurve;
                GripWithGravityMultiplierCurve = m_slopeOverride.GripWithGravityMultiplierCurve;
                GripSpeedMultiplierCurve = m_slopeOverride.GripSpeedMultiplierCurve;
                CanJump = m_slopeOverride.CanJump;
                CanTurnAround = m_slopeOverride.CanTurnAround;
                OrientateToWorldUp = m_slopeOverride.OrientateToWorldUp;
                CameraTypeOverride = m_slopeOverride.ForwardMotion.GetCameraTypeOverride(speed);
                if (m_character.WorldVelocityMagnitudeSqr < m_slopeOverride.SpeedThreshold * m_slopeOverride.SpeedThreshold)
                {
                    TurnAround = m_slopeOverride.CanTurnAround;
                    Detach = m_slopeOverride.CanDetach;
                }
            }
            else
            {
                CameraTypeOverride = Definition.ForwardMotion.GetCameraTypeOverride(speed);
            }
        }

        // Original 0600113a: collider override precedes constant-direction exclusion.
        protected virtual bool TryGetSlopeOverride(out CharacterAbilityDefinition_Movement.SlopeMotion slopeOverride)
        {
            slopeOverride = null;
            CharacterCollisionData collisionData = m_character.Collider.LinkedCollisionData;
            if (collisionData != null && collisionData.HasMovementOverride)
            {
                if (collisionData.MovementOverride.AbilityOverride == 0 ||
                    collisionData.MovementOverride.AbilityOverride == DefinitionBase.AbilityType)
                {
                    slopeOverride = collisionData.MovementOverride.Motion;
                    if (slopeOverride != null)
                        return true;
                }
            }
            if (m_character.HasConstantDirectionModifier)
                return false;
            if (Definition.SlopeOverrides == null)
                return false;
            foreach (CharacterAbilityDefinition_Movement.SlopeMotion slope in Definition.SlopeOverrides)
            {
                // Both shipped CPUs skip only ordered out-of-range comparisons.
                if (SlopeAngle < slope.AngleMin || SlopeAngle > slope.AngleMax)
                    continue;
                slopeOverride = slope;
                return true;
            }
            return false;
        }

        public virtual void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection, float deltaTime,
            float motionMultiplier = 1f, float effectiveInputMagnitude = 1f,
            AnimationCurve accelerationMultiplier = null, bool applyDeceleration = true)
        {
            forwardSpeed = Mathf.Max(forwardSpeed, 0f);
            CharacterAbilityDefinition.Motion motion = GetForwardMotion();
            UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, deltaTime, motion, motionMultiplier,
                effectiveInputMagnitude, accelerationMultiplier, applyDeceleration);
            m_character.Storage.SetValue(ActorFSMKeys.GroundGripMotion, motion);
        }

        public virtual void UpdateClampedVelocity(ref Vector3 worldVelocity) { }

        protected CharacterAbilityDefinition.Motion GetForwardMotion()
        {
            return m_slopeOverride != null && m_slopeOverride.ForwardMotion != null
                ? m_slopeOverride.ForwardMotion : Definition.ForwardMotion;
        }

        public override CameraRecenterHeadingOverrides GetFreeLookHeadingOverrides()
        {
            if (m_slopeOverride?.FreeLookHeadingOverrides)
                return m_slopeOverride.FreeLookHeadingOverrides;
            return base.GetFreeLookHeadingOverrides();
        }

        protected override void GetDebugExtraInfo(StringBuilder extraInfo)
        {
            base.GetDebugExtraInfo(extraInfo);
            extraInfo.Append(string.Format("Slope Angle = {0} ", SlopeAngle));
        }

        protected CharacterAbility_Movement() { }
    }
}
