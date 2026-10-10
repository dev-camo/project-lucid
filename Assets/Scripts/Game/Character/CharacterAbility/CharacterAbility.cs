// Complete original type candidate; genuine Actor/Character, definition, tracking,
// camera, stamina, App and timed-utility graphs remain required and unaccepted.
using System;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbility : ActorAbility
    {
        protected Character m_character;
        public abstract CharacterAbilityDefinition CharacterAbilityDefinition { get; }
        public bool ValidateContactsActive { get; private set; }
        public bool OrientateColliderHit { get; private set; }
        public bool OrientateHeadingToPlane { get; private set; }
        public float GravityMultiplier { get; protected set; }
        public CameraType CameraTypeOverride { get; protected set; }
        public CameraProxyTargetSettings? CameraSettingsOverride { get; private set; }
        public CameraHeadingOverride CameraHeadingOverride { get; private set; }
        protected CameraAbilityDefinition CameraDefinitionOverride { get; set; }
        public ModifierSpeedFunc m_modifierSpeed;
        private bool m_switchAvailableOverride;
        private float m_boostRechargeTime;
        private float m_chaosRechargeTime;
        protected readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();

        public bool SwitchAvailable => CharacterAbilityDefinition.SwitchAvailable || m_switchAvailableOverride;
        public abstract void SetDefinitionOverride();

        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_character = actor as Character;
            if (m_character == null) return;
            m_character.InstantiateAbilityUI(this);
        }

        public override void Close()
        {
            base.Close();
            m_character.DestroyAbilityUI(CharacterAbilityDefinition);
        }

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            GravityMultiplier = CharacterAbilityDefinition.GravityMultiplier;
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            GravityMultiplier = CharacterAbilityDefinition.GravityMultiplier;
        }

        protected override void DoOnPostUpdate(float deltaTime)
        {
            ResetCollisionValues();
            ValidateContactsActive = true;
        }

        public override bool GetUIText(StringBuilder stringInfoBuilder)
        {
            base.GetUIText(stringInfoBuilder);
            float chargeTime = m_character.SpinDashRoll.ChargeTime;
            // Original uses the negative branch: an unordered charge is displayed.
            if (chargeTime < 0.0001f) return false;
            stringInfoBuilder.Append(string.Format("SpinDashCharge = {0:F2}s, ", chargeTime));
            return true;
        }

        public void SetInvulnerability()
        {
            if (CharacterAbilityDefinition.ExitInvulnerability)
                m_character.TryExitInvulnerability();
        }

        // Original unconstrained T. A zero delay does nothing, including omitting
        // completion. Cancel clears the captured handle before invoking completion;
        // expiry removes registration before completion and does not clear the handle.
        protected void AddModifierOverride<T>(int modifierType, T value, float waitSeconds,
            TimeCategory timeCategory, Action onComplete = null)
        {
            StackableDataHandle controlsHandle = null;
            if (waitSeconds == 0f) return;
            controlsHandle = m_character.AddModifierOverride(modifierType, value, CancelModiferOverride);
            TimeScaledUtilities_SDT.DelayFixedSeconds(waitSeconds, timeCategory, () =>
            {
                if (controlsHandle == null) return;
                m_character.RemoveModifierOverrides(controlsHandle);
                onComplete?.Invoke();
            });
            void CancelModiferOverride()
            {
                controlsHandle = null;
                onComplete?.Invoke();
            }
        }

        public override ActorAnimationDefinition GetAnimationDefinition()
        {
            float speed = m_character.WorldVelocityMagnitude;
            return CharacterAbilityDefinition.GetAnimationDefinition(speed);
        }

        public void SetCameraOverrides() => SetCameraOverrides(CameraDefinitionOverride,
            m_character.CustomProxy);

        protected virtual void SetCameraOverrides(CameraAbilityDefinition cameraDefinition,
            Transform targetTransform)
        {
            if (!(cameraDefinition == null) && cameraDefinition.Type != CameraType.None)
            {
                CameraTypeOverride = cameraDefinition.Type;
                CameraSettingsOverride = cameraDefinition.ProxyTargetSettings;
                CameraHeadingOverride = new CameraHeadingOverride
                {
                    Target = targetTransform,
                    Settings = cameraDefinition.HeadingOverrideSettings
                };
            }
            else
            {
                CameraTypeOverride = CharacterAbilityDefinition.CameraTypeOverride;
                CameraSettingsOverride = null;
                CameraHeadingOverride = null;
            }
        }

        public override void RegisterForCollisions()
        {
            ResetCollisionValues();
            ValidateContactsActive = false;
            if (DefinitionBase.CollisionPlanes != null && DefinitionBase.CollisionPlanes.Length != 0)
                m_character.Collider.ValidateContactCallback += ValidateContactCallback;
        }

        public override void UnregisterForCollisions()
        {
            if (DefinitionBase.CollisionPlanes != null && DefinitionBase.CollisionPlanes.Length != 0)
                m_character.Collider.ValidateContactCallback -= ValidateContactCallback;
        }

        private void ValidateContactCallback(ContactPoint contact) => ValidateCollisionContact(contact);
        private void ResetCollisionValues()
        {
            OrientateColliderHit = false;
            OrientateHeadingToPlane = true;
        }

        private void ValidateCollisionContact(ContactPoint contact)
        {
            if (DefinitionBase.CollisionPlanes.Length == 0) return;
            Vector3 position = m_character.WorldPosition;
            // Ray's genuine constructor performs the one observed normalization.
            Ray ray = new Ray(position, contact.point - m_character.WorldPosition);
            float distance = m_character.LateralBoundsThreshold * 2f;
            if (m_character.ActiveTracking.RaycastOnTrack(ray, distance,
                out RaycastHit raycastHit))
                EvaluateRaycastHitAgainstCollisionPlanes(raycastHit);
        }

        private void EvaluateRaycastHitAgainstCollisionPlanes(RaycastHit raycastHit)
        {
            Vector3 contactNormal = raycastHit.normal;
            Vector3 gravityUp = m_character.WorldUp;
            CollisionPlaneDefinition[] planes = DefinitionBase.CollisionPlanes;
            float contactNormalToGravity = Vector3.Dot(contactNormal, gravityUp);
            foreach (CollisionPlaneDefinition plane in planes)
            {
                if (CollisionContactHasGravityOverride(plane, gravityUp, contactNormalToGravity)) return;
                if (!ContactMatchesCollisionPlane(plane, contactNormalToGravity)) continue;
                float normalToForward = Mathf.Abs(Vector3.Dot(contactNormal, m_character.ForwardDirection));
                foreach (CollisionPlaneDefinition.CollisionMotion motion in plane.CollisionOverrides)
                {
                    // Preserve the native rejection branches and their unordered domain.
                    if (normalToForward - 0.0001f > motion.AngleMinCosine) continue;
                    if (normalToForward + 0.0001f <= motion.AngleMaxCosine) continue;
                    if (!motion.OrientateHeadingToPlane) OrientateHeadingToPlane = false;
                    OrientateColliderHit = true;
                    return;
                }
            }
        }

        private bool CollisionContactHasGravityOverride(CollisionPlaneDefinition collisionPlane,
            Vector3 gravityUp, float contactNormalToGravity)
        {
            foreach (CollisionPlaneDefinition.CollisionMotion motion in collisionPlane.CollisionOverrides)
            {
                if (motion.OrientateBodyToGravityThresholdAngle == 0f) continue;
                float cosine = motion.OrientateBodyToGravityThresholdAngleCosine;
                if (cosine > contactNormalToGravity) continue;
                if (Vector3.Dot(gravityUp, m_character.WorldVelocityNormalised) > 1f - cosine) continue;
                return true;
            }
            return false;
        }

        private bool ContactMatchesCollisionPlane(CollisionPlaneDefinition collisionPlane,
            float contactNormalToGravity)
        {
            if (collisionPlane.Normal.y > 0f) return contactNormalToGravity > collisionPlane.AngleCosine;
            if (collisionPlane.Normal.y < 0f) return -contactNormalToGravity > collisionPlane.AngleCosine;
            return Mathf.Sqrt(1f - contactNormalToGravity * contactNormalToGravity) > collisionPlane.AngleCosine;
        }

        public virtual CameraRecenterHeadingOverrides GetFreeLookHeadingOverrides() =>
            CharacterAbilityDefinition.FreeLookHeadingOverrides;

        public void ApplyInputModifier()
        {
            if (m_appRef.Get().Storage.GetValue(AppFSMKeys.InputAbilitySteeringAssistDisabled, false, true)) return;
            CharacterAbilityDefinition.InputModifierLookups modifier = CharacterAbilityDefinition.InputModifier;
            m_character.ApplyInputModifier(modifier.DirectionalAngleLookup);
        }

        protected float GetInputScalingMultiplier(bool forward,
            CharacterAbilityDefinition.ThrottleScaling scaling,
            CharacterAbilityDefinition.ThrottleScaling scalingOverride = null)
        {
            if (forward)
            {
                if (scalingOverride != null && scalingOverride.OverrideForwardScaling)
                    return scalingOverride.ForwardScalingMultiplier;
                if (scaling.OverrideForwardScaling) return scaling.ForwardScalingMultiplier;
                return m_character.Constants.InputForwardScalingMultiplier;
            }
            if (scalingOverride != null && scalingOverride.OverrideBackwardScaling)
                return scalingOverride.BackwardScalingMultiplier;
            if (scaling.OverrideBackwardScaling) return scaling.BackwardScalingMultiplier;
            return m_character.Constants.InputBackwardScalingMultiplier;
        }

        protected float ApplyInputScalingToSpeedClamped(float throttleInput, float speedMin,
            float speedMax, float forwardScalingMultiplier)
        {
            if (throttleInput < 0f) return speedMin;
            if (!(throttleInput < 1f)) return speedMax;
            return (speedMax - speedMin) * (1f - forwardScalingMultiplier) + speedMin;
        }

        protected float ApplyInputScalingToDeceleration(float throttleInput, float deceleration,
            float backwardScalingMultiplier)
        {
            if (throttleInput < 0f)
                return Mathf.Clamp(deceleration - throttleInput * deceleration * backwardScalingMultiplier,
                    0f, (backwardScalingMultiplier + 1f) * deceleration);
            return deceleration;
        }

        protected float ApplyInputScalingClamped(float throttleInput, float value, float multiplier) =>
            Mathf.Clamp(throttleInput * value * multiplier + (1f - multiplier) * value, 0f, value);

        protected virtual void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection,
            float deltaTime, CharacterAbilityDefinition.Motion motion, float motionMultiplier = 1f,
            float effectiveInputMagnitude = 1f, AnimationCurve accelerationCurve = null,
            bool applyDeceleration = true)
        {
            // The shipped native method does not consume motionMultiplier.
            forwardSpeed = motion.ClampSpeed(forwardSpeed);
            float speedMin = motion.SpeedMin;
            float speedMax = motion.SpeedMax;
            float acceleration = motion.Acceleration;
            float deceleration = motion.EvaluateDeceleration(forwardSpeed);
            m_modifierSpeed?.Invoke(ref acceleration, ref speedMin, ref speedMax, ref deceleration);
            float inputMinimum = Mathf.Clamp(forwardSpeed, speedMin, speedMax) * effectiveInputMagnitude;
            if (!motion.ClampToSpeedMin || inputMinimum > speedMin) speedMin = inputMinimum;
            if (accelerationCurve != null)
            {
                float factor;
                if (speedMin > 0.0001f) factor = accelerationCurve.Evaluate(forwardSpeed / speedMin);
                else
                {
                    Keyframe last = accelerationCurve[accelerationCurve.length - 1];
                    // Native reads both time and value from the last key.
                    float lastTime = last.time;
                    factor = last.value;
                }
                acceleration = factor * motion.Acceleration;
            }
            if (effectiveInputMagnitude > 0.0001f && applyDeceleration && forwardSpeed > speedMax + 0.0001f)
            {
                forwardSpeed = Mathf.Max(forwardSpeed - deceleration * deltaTime, speedMax);
                return;
            }
            if (acceleration > 0f)
            {
                if (!(forwardSpeed >= speedMin))
                {
                    forwardSpeed = Mathf.Min(forwardSpeed + acceleration * deltaTime, speedMin);
                    if (motion.ClampToSpeedMin && forwardSpeed < speedMin &&
                        forwardDirection.sqrMagnitude < 0.0001f)
                        forwardDirection = Vector3.forward;
                }
            }
            else if (effectiveInputMagnitude < 0.9999f && applyDeceleration)
                forwardSpeed = Mathf.Max(forwardSpeed + (acceleration - deceleration) * deltaTime, speedMin);
        }

        public void ResetStaminaRecharge()
        {
            m_boostRechargeTime = 0f;
            m_chaosRechargeTime = 0f;
        }
        public void ApplyStaminaRecharge(float deltaTime)
        {
            ApplyBoostRecharge(deltaTime);
            ApplyChaosRecharge(deltaTime);
        }
        private void ApplyBoostRecharge(float deltaTime)
        {
            AnimationCurve recharge = CharacterAbilityDefinition.BoostRechargeRate;
            if (recharge == null || recharge.length < 2) return;
            m_boostRechargeTime += deltaTime;
            float amount = recharge.Evaluate(m_boostRechargeTime) * deltaTime;
            m_character.BoostStamina.AddStamina(amount);
        }
        private void ApplyChaosRecharge(float deltaTime)
        {
            AnimationCurve recharge = CharacterAbilityDefinition.ChaosRechargeRate;
            if (recharge == null || recharge.length < 2 || m_character.ChaosStamina.Active) return;
            m_chaosRechargeTime += deltaTime;
            float amount = recharge.Evaluate(m_chaosRechargeTime) * deltaTime;
            m_character.ChaosStamina.AddStamina(amount);
        }
        public void SetSwitchAvailableOverride(bool switchAvailableOverride) =>
            m_switchAvailableOverride = switchAvailableOverride;
        protected CharacterAbility() { }
        public delegate void ModifierSpeedFunc(ref float acceleration, ref float speedMin,
            ref float speedMax, ref float deceleration);
    }
}
