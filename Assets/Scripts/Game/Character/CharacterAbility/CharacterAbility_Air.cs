using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020002b3; all 47 whole native bodies retained.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_Air : CharacterAbility<CharacterAbilityDefinition_Air>, IVelocityStore
    {
        private const float CollisionVelocityNormal = 5f;
        public bool AirActive { get; private set; }
        public Vector3 Velocity => m_velocityWorld;
        public Vector3 BlendProgress { get; private set; }
        public CharacterVelocityStorage VelocityStorage { get; } = new CharacterVelocityStorage();
        protected readonly List<CharacterAbilityDefinition_Air.AirComponent> m_airComponents = new List<CharacterAbilityDefinition_Air.AirComponent>();
        protected float m_totalTimeSeconds;
        protected float m_elapsedTimeSeconds;
        private float m_staminaTimeSeconds;
        private float m_elapsedTimeInAirSeconds;
        private Vector3 m_velocityWorld;
        private Vector3 m_velocityLocalStart;
        private float m_timeSinceActive;
        private bool m_brainWasActive;
        private bool m_hasBeenActive;
        private int m_activeCount;
        private float[] m_blendTimes;
        private bool m_isRecharging;
        private bool m_raycastHit;
        private readonly CharacterAbilityDefinition.Motion m_motion = new CharacterAbilityDefinition.Motion();

        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_airComponents.Clear();
            SetupAirComponents();
            CharacterVelocityStorage storage = VelocityStorage;
            storage.Initialise(Definition.VelocityStorageParameters, m_character);
            CacheMotionScalingOverride();
            SetStaminaFromFSMValues();
        }

        protected virtual void SetupAirComponents()
        {
            m_airComponents.AddRange(Definition.Components);
            m_blendTimes = new float[m_airComponents.Count];
            ResetBlendTimes();
            m_totalTimeSeconds = CalculateTotalTimeSeconds();
            m_staminaTimeSeconds = CalculateStaminaTimeSeconds();
        }

        protected virtual float CalculateTotalTimeSeconds()
        {
            float time = 0f;
            foreach (CharacterAbilityDefinition_Air.AirComponent component in m_airComponents)
            {
                AnimationCurve curve = component.SpeedCurve;
                time = Mathf.Max(curve[curve.length - 1].time, time);
            }
            return time;
        }

        private float CalculateStaminaTimeSeconds()
        {
            float time = 0f;
            foreach (CharacterAbilityDefinition_Air.AirComponent component in m_airComponents)
            {
                if (component.Direction.y <= 0f) continue;
                AnimationCurve curve = component.SpeedCurve;
                int length = curve.length;
                for (int i = 0; i < length; ++i)
                {
                    Keyframe key = curve[i];
                    if (key.value <= 0f) break;
                    time = Mathf.Max(key.time, time);
                }
            }
            return time;
        }

        private bool UpdateCanBeActivated(CharacterBrain brain)
        {
            if (Definition.RemainActiveUntilInterrupted && AirActive) return true;
            if (IsSecondaryAndPrimaryActive()) return false;
            if (!AirActive && Definition.InterruptFromProjectedGroundHit && m_character.Collider.GroundHit.HitProjected) return false;
            if (m_character.IsExitingLevel()) return false;
            if (!m_character.AreControlsEnabled()) return false;
            if (m_character.HomingPool.IsTargetActive()) return false;
            if (!IsControlSingle() && m_character.HasHoverData()) return false;
            if (Definition.DeactivateOnDirectionModifier && m_character.HasAnyDirectionModifier()) return false;
            if (m_character.Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true)) return false;
            switch (Definition.Input)
            {
                case AbilityInputType.None: return m_elapsedTimeSeconds < m_totalTimeSeconds;
                case AbilityInputType.Continuous:
                    if (!IsImpulseActive(brain)) return false;
                    return m_elapsedTimeSeconds < m_totalTimeSeconds;
                case AbilityInputType.Single:
                case AbilityInputType.SingleToggle:
                    if (!IsImpulseActive(brain) && m_elapsedTimeSeconds <= 0f) return false;
                    return m_elapsedTimeSeconds < m_totalTimeSeconds;
                case AbilityInputType.UntilRelease: return IsImpulseActive(brain);
                default: return false;
            }
        }

        public bool IsSecondaryAndPrimaryActive()
        {
            if (Definition.IsPrimary) return false;
            CharacterAbility_Air primary = m_character.Storage.GetValue<CharacterAbility_Air>(ActorFSMKeys.AirAbilityActive, null, true);
            return primary != null && primary != this && primary.AirActive;
        }

        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            ResetBlendTimes();
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            m_elapsedTimeInAirSeconds += deltaTime;
            if (!UpdateCanBeActivated(brain))
            {
                ResetBlendTimes();
                SetAirActive(false);
                return;
            }
            if (AirActive && Definition.RemainActiveUntilInterrupted) return;
            if (Definition.Input == AbilityInputType.UntilRelease)
            {
                if (!AirActive && m_activeCount >= Definition.ActivationCount) SetAirActive(false);
                else SetAirActive(IsImpulseActive(brain));
                return;
            }
            if (Definition.Input == AbilityInputType.SingleToggle && m_elapsedTimeSeconds > 0f)
            {
                if (m_brainWasActive && !IsBrainImpulseActive(brain)) m_brainWasActive = false;
                else if (!m_brainWasActive && IsBrainImpulseActive(brain))
                {
                    SetAirActive(false);
                    return;
                }
            }
            SetAirActive(m_elapsedTimeSeconds < m_totalTimeSeconds);
        }

        public void UpdateVelocity(float deltaTime)
        {
            CalculateVelocity(ref m_velocityWorld, ref m_velocityLocalStart, ref m_elapsedTimeSeconds, deltaTime, m_totalTimeSeconds);
        }

        private void CalculateVelocity(ref Vector3 velocityWorld, ref Vector3 startVelocityLocal,
            ref float elapsedTime, float deltaTime, float totalTimeSeconds)
        {
            Vector3 acceleration = Definition.Acceleration;
            if (elapsedTime == 0f)
            {
                velocityWorld = m_character.WorldVelocity;
                startVelocityLocal = Vector3.zero;
                if (Definition.IsAdditive)
                {
                    startVelocityLocal = m_character.LocalVelocity;
                    if (startVelocityLocal.x < 0f) startVelocityLocal.x = 0f;
                    if (startVelocityLocal.y < 0f) startVelocityLocal.y = 0f;
                    if (startVelocityLocal.z < 0f) startVelocityLocal.z = 0f;
                }
            }
            ScaleVelocityFromInput(ref startVelocityLocal.z, deltaTime);
            if (Definition.IsAdditive) velocityWorld += m_character.Gravity * deltaTime;
            elapsedTime += deltaTime;
            Vector3 localVelocity = startVelocityLocal;
            int count = m_airComponents.Count;
            for (int i = 0; i < count; ++i)
            {
                CharacterAbilityDefinition_Air.AirComponent component = m_airComponents[i];
                if (SetComponentBlendTime(component, deltaTime, ref m_blendTimes[i], elapsedTime))
                {
                    float speed = EvaluateSpeedCurve(component, elapsedTime, totalTimeSeconds);
                    localVelocity += component.Direction * speed;
                }
            }
            if (Definition.MaintainGravitySpeed) localVelocity = MaintainGravitySpeed(localVelocity);
            localVelocity = Vector3.Scale(Definition.ResultingLocalVelocityMultiplier, localVelocity);
            Vector3 currentVelocityLocal = m_character.WorldToLocalRotation * velocityWorld;
            localVelocity.x = AccelerateTowardsTarget(currentVelocityLocal.x, localVelocity.x, acceleration.x, deltaTime);
            localVelocity.y = AccelerateTowardsTarget(currentVelocityLocal.y, localVelocity.y, acceleration.y, deltaTime);
            localVelocity.z = AccelerateTowardsTarget(currentVelocityLocal.z, localVelocity.z, acceleration.z, deltaTime);
            velocityWorld = m_character.WorldRotation * localVelocity;
        }

        private static float AccelerateTowardsTarget(float value, float target, float acceleration, float deltaTime)
        {
            if (acceleration == 0f) return target;
            if (value < target) return Mathf.Min(value + acceleration * deltaTime, target);
            if (value > target) return Mathf.Max(value - acceleration * deltaTime, target);
            return target;
        }

        private void ScaleVelocityFromInput(ref float forwardSpeed, float deltaTime)
        {
            if (!Definition.UseMotionScaling) return;
            Vector3 input = m_character.WorldToLocalRotation * m_character.CameraForwardOnCharacterPlane;
            float movement = input.z * m_character.ControllerMovement.y - input.x * m_character.ControllerMovement.x;
            float speed = movement < 0f ? Definition.MotionScaling.SpeedMin : Definition.MotionScaling.SpeedMax;
            m_motion.SpeedMin = speed;
            m_motion.SpeedMax = speed;
            UpdateForwardSpeed(ref forwardSpeed, deltaTime, m_motion, movement);
        }

        private void UpdateForwardSpeed(ref float forwardSpeed, float deltaTime, CharacterAbilityDefinition.Motion motion,
            float controllerMovement, CharacterAbilityDefinition.Motion motionOverride = null, float motionMultiplier = 1f)
        {
            CharacterAbilityDefinition.ThrottleScaling scaling = motion.Scaling;
            float forwardScaling;
            float backwardScaling;
            if (motionOverride != null)
            {
                motion = motionOverride;
                CharacterAbilityDefinition.ThrottleScaling overrideScaling = motionOverride.Scaling;
                forwardScaling = overrideScaling != null && overrideScaling.OverrideForwardScaling
                    ? overrideScaling.ForwardScalingMultiplier
                    : scaling.OverrideForwardScaling ? scaling.ForwardScalingMultiplier : m_character.Constants.InputForwardScalingMultiplier;
                backwardScaling = overrideScaling != null && overrideScaling.OverrideBackwardScaling
                    ? overrideScaling.BackwardScalingMultiplier
                    : scaling.OverrideBackwardScaling ? scaling.BackwardScalingMultiplier : m_character.Constants.InputBackwardScalingMultiplier;
            }
            else
            {
                forwardScaling = scaling.OverrideForwardScaling ? scaling.ForwardScalingMultiplier : m_character.Constants.InputForwardScalingMultiplier;
                backwardScaling = scaling.OverrideBackwardScaling ? scaling.BackwardScalingMultiplier : m_character.Constants.InputBackwardScalingMultiplier;
            }
            float deceleration = motion.EvaluateDeceleration(forwardSpeed);
            float minSpeed = Mathf.Clamp((1f - forwardScaling) * motion.SpeedMin + motion.SpeedMin * controllerMovement * forwardScaling,
                0f, motion.SpeedMin) * motionMultiplier;
            float maxSpeed;
            if (controllerMovement < 0f)
            {
                deceleration = Mathf.Clamp(deceleration - deceleration * controllerMovement * backwardScaling,
                    0f, (backwardScaling + 1f) * deceleration);
                maxSpeed = minSpeed;
            }
            else
                maxSpeed = controllerMovement < 1f ? minSpeed + (1f - forwardScaling) * (motion.SpeedMax - minSpeed) : motion.SpeedMax;
            // Original native arithmetic applies this multiplier after minSpeed was already multiplied.
            maxSpeed *= motionMultiplier;
            forwardSpeed = motion.ClampSpeed(forwardSpeed);
            if (forwardSpeed > maxSpeed) forwardSpeed = Mathf.Max(forwardSpeed - deceleration * deltaTime, maxSpeed);
            else if (controllerMovement < 0f) forwardSpeed = Mathf.Max(forwardSpeed - deceleration * deltaTime, Mathf.Min(minSpeed, 0f));
            else if (forwardSpeed < minSpeed)
            {
                float acceleration = Mathf.Clamp((1f - forwardScaling) * motion.Acceleration + motion.Acceleration * controllerMovement * forwardScaling,
                    0f, motion.Acceleration);
                forwardSpeed = Mathf.Min(forwardSpeed + acceleration * deltaTime, minSpeed);
            }
        }

        private void CacheMotionScalingOverride()
        {
            CharacterAbilityDefinition.Motion motion = Definition.MotionScaling;
            m_motion.SpeedMin = motion.SpeedMin;
            m_motion.SpeedMax = motion.SpeedMax;
            m_motion.Acceleration = motion.Acceleration;
            m_motion.DecelerationCurve = motion.DecelerationCurve;
            m_motion.Scaling = motion.Scaling;
        }

        protected virtual float EvaluateSpeedCurve(CharacterAbilityDefinition_Air.AirComponent airComponent,
            float elapsedTimeSeconds, float totalTime)
        {
            return airComponent.SpeedCurve.Evaluate(elapsedTimeSeconds - airComponent.StartTime);
        }

        private void ResetBlendTimes()
        {
            for (int i = 0; i < m_airComponents.Count; ++i) m_blendTimes[i] = 0f;
        }

        private bool SetComponentBlendTime(CharacterAbilityDefinition_Air.AirComponent component, float deltaTime,
            ref float blendTime, float elapsedTime)
        {
            float t = 0f;
            if (component.StartTime < elapsedTime)
            {
                blendTime += deltaTime;
                t = component.BlendCurve.Evaluate(blendTime);
            }
            Vector3 blend = BlendProgress;
            UpdateBlend(component, component.Direction.x, t, ref blend.x);
            UpdateBlend(component, component.Direction.y, t, ref blend.y);
            UpdateBlend(component, component.Direction.z, t, ref blend.z);
            BlendProgress = blend;
            return t > 0f;
        }

        private void UpdateBlend(CharacterAbilityDefinition_Air.AirComponent component, float distance, float t, ref float blendT)
        {
            if (Mathf.Abs(distance) > 0.0001f) blendT = t;
        }

        private Vector3 MaintainGravitySpeed(Vector3 localVelocity)
        {
            Vector3 worldVelocity = m_character.WorldRotation * localVelocity;
            Vector3 newGravityVelocity = Vector3.Project(worldVelocity, m_character.Gravity);
            Vector3 oldGravityVelocity = Vector3.Project(m_character.WorldVelocity, m_character.Gravity);
            return m_character.WorldToLocalRotation * (worldVelocity - newGravityVelocity + oldGravityVelocity);
        }

        public virtual void Reset(bool interrupted, bool resetActiveCount)
        {
            m_elapsedTimeSeconds = interrupted ? m_totalTimeSeconds : 0f;
            SetAirActive(false);
            m_velocityWorld = Vector3.zero;
            if (resetActiveCount) m_activeCount = 0;
            m_character.ReportAirTime(Mathf.RoundToInt(m_elapsedTimeInAirSeconds * 1000f));
            m_elapsedTimeInAirSeconds = 0f;
        }

        public void Recharge(bool isExit)
        {
            if (!Definition.CanRecharge) return;
            m_elapsedTimeSeconds = 0.0001f;
            m_character.Storage.SetValue(ActorFSMKeys.AirAbilityBlendVelocity, m_character.LocalVelocity);
            if (!m_isRecharging)
            {
                m_isRecharging = true;
                if (Definition.RechargePFX != 0) m_character.PFXController.PlayActorPFX(Definition.RechargePFX);
            }
            if (isExit)
            {
                ResetBlendTimes();
                m_isRecharging = false;
            }
        }

        public void SetWorldVelocity(Vector3 worldVelocity, float deltaTime)
        {
            Vector3 position = m_character.WorldPosition;
            Vector3 direction = m_character.ForwardDirection;
            Ray ray = new Ray(position, direction);
            float distance = m_character.LateralBoundsThreshold + worldVelocity.magnitude * deltaTime;
            bool priorHit = m_raycastHit;
            LayerMask mask = m_character.CollisionMask;
            m_raycastHit = Physics.Raycast(ray, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore);
            if (priorHit && m_raycastHit)
            {
                float dot = Vector3.Dot(worldVelocity, hit.normal);
                worldVelocity -= hit.normal * (dot + CollisionVelocityNormal);
            }
            m_character.SetWorldVelocity(worldVelocity);
        }

        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            SetAirActive(false);
        }

        public override bool GetUIText(StringBuilder stringInfoBuilder)
        {
            bool result = base.GetUIText(stringInfoBuilder);
            if (AirActive && Definition.UIType != 0)
            {
                stringInfoBuilder.AppendLine(string.Format("Stamina = {0:F0}", GetStaminaRemaining() * 100f));
                result = true;
            }
            return result;
        }

        public float GetStaminaRemaining() => m_elapsedTimeSeconds < m_staminaTimeSeconds
            ? (m_staminaTimeSeconds - m_elapsedTimeSeconds) / m_staminaTimeSeconds : 0f;
        protected float GetProgress() => m_elapsedTimeSeconds < m_totalTimeSeconds ? m_elapsedTimeSeconds / m_totalTimeSeconds : 1f;

        protected override void GetDebugExtraInfo(StringBuilder extraInfo)
        {
            base.GetDebugExtraInfo(extraInfo);
            extraInfo.Append(string.Format("Stamina = {0}, Time = {1:F2} / {2:F2} ", GetStaminaRemaining(), m_elapsedTimeSeconds, m_totalTimeSeconds));
        }

        private bool IsControlSingle() => Definition.Input == AbilityInputType.Single || Definition.Input == AbilityInputType.SingleToggle;
        protected virtual bool IsControlContinuous() => Definition.Input == AbilityInputType.Continuous && !m_character.SaveDataSettings.AirAbilityHold;

        private bool IsImpulseActive(CharacterBrain brain)
        {
            if (IsBrainImpulseActive(brain)) return true;
            if (!AirActive || Definition.TrackDistanceForceContinue == 0f) return false;
            if (m_character.ActiveTracking.IsOnGround()) return true;
            Ray ray = new Ray(m_character.WorldPosition, m_character.WorldVelocityNormalised);
            float distance = Definition.TrackDistanceForceContinue;
            return m_character.ActiveTracking.RaycastOnTrack(ray, distance, out RaycastHit hit);
        }

        protected virtual bool IsBrainImpulseActive(CharacterBrain brain)
        {
            return AirActive && IsControlContinuous() ? !brain.AirActive : brain.AirActive;
        }
        protected bool IsBrainImpulseCancel(CharacterBrain brain) => AirActive && brain.AirCancel;
        protected virtual void EndBrainImpulse() => m_character.EndAirActiveImpulse();

        protected virtual void SetAirActive(bool active)
        {
            if (AirActive != active)
            {
                AirActive = active;
                m_brainWasActive = active;
                if (IsControlContinuous() || (!AirActive && IsControlSingle())) EndBrainImpulse();
                if (AirActive)
                {
                    m_hasBeenActive = true;
                    m_timeSinceActive = m_character.GetTotalFixedTime();
                    m_raycastHit = false;
                    m_activeCount++;
                }
                else if (m_activeCount < Definition.ActivationCount) m_elapsedTimeSeconds = 0f;
            }
            SetValues();
        }

        private void SetValues()
        {
            m_character.Storage.SetValue(ActorFSMKeys.AirAbilityElapsedTime, m_elapsedTimeSeconds);
            m_character.Storage.SetValue(ActorFSMKeys.AirAbilityActiveCount, m_activeCount);
        }

        private void SetStaminaFromFSMValues()
        {
            if (m_character.Storage.TryGetValue(ActorFSMKeys.AirAbilityElapsedTime, out m_elapsedTimeSeconds)
                && m_character.Storage.TryGetValue(ActorFSMKeys.AirAbilityActiveCount, out m_activeCount)
                && (m_elapsedTimeSeconds != 0f || m_activeCount != 0))
            {
                m_elapsedTimeSeconds = Definition.PostSwitchCanActivate ? Definition.PostSwitchStamina : m_totalTimeSeconds;
                m_activeCount = Definition.ActivationCount;
            }
        }

        public bool WasRecentlyActive(float withinTimeSeconds) => !AirActive && m_hasBeenActive
            && m_timeSinceActive >= m_character.GetTotalFixedTime() - withinTimeSeconds;
        protected virtual float GetBlendMultiplier(CharacterAbilityDefinition_Air.AirComponent airComponent) => 1f;

        public void Interrupt()
        {
            if (!AirActive) return;
            Reset(true, false);
            if (!IsControlSingle() && m_character.HasHoverData()) return;
            if (Definition.IsPrimary) EndBrainImpulse();
        }

        public override ActorAnimationDefinition GetAnimationDefinition()
        {
            if (m_activeCount < Definition.AnimationActivationOverrides.Count)
            {
                ActorAnimationDefinition animation = Definition.AnimationActivationOverrides[m_activeCount];
                if (animation != null) return animation;
            }
            float speed = m_character.WorldVelocityMagnitude;
            return CharacterAbilityDefinition.GetAnimationDefinition(speed);
        }

        public CharacterAbility_Air() { }
    }
}
