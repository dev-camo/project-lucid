using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a29. Keep serialized members, idle update order,
    // and the supplied release's rotation quirks; this is not an actor controller.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SmoothedTransformRotator : MonoBehaviour
    {
        [Tooltip("X axis = normalised angle difference from target\nY axis = slerp value")]
        [SerializeField] private AnimationCurve m_rotationSlerpCurve;
        [SerializeField, Tooltip("Multiplied by output of rotation slerp curve.")]
        private float m_rotationSlerpMultiplier;
        [SerializeField, Tooltip("If angle difference is less than this value, snap to target rotation.")]
        private float m_minSnapAngle = .01f;
        [SerializeField, Tooltip("If angle difference is greater than this value, snap to target rotation.")]
        private float m_maxSnapAngle = 179.9f;
        [SerializeField, Tooltip("The rate at which the rotation speed returns to 0 (or idle) following an rotational impulse.")]
        private AnimationCurve m_impulseDeceleration;
        [SerializeField] private bool m_idleAutomation;
        [SerializeField, ShowIf("m_idleAutomation", (string)null)]
        private TimeCategory m_timeCategory = TimeCategory.System;
        [ShowIf("m_idleAutomation", (string)null), SerializeField]
        private Vector3 m_idleRotationSpeed = Vector3.zero;
        [ShowIf("m_idleAutomation", (string)null), SerializeField]
        private bool m_pingPongWhenIdle;
        [SerializeField, ShowIf("m_pingPongWhenIdle", (string)null)]
        private float m_pingPongToDegrees;
        [SerializeField, ShowIf("m_pingPongWhenIdle", (string)null)]
        private AnimationCurve m_ppSpeedMultiplierOverExtent;
        protected Transform m_cachedTransform;
        protected bool m_hasCachedTransform;
        private bool m_smoothingEnabled = true;
        protected bool m_rotationOverriden;
        protected bool m_disableRotationUpdate;
        private bool m_pingPongReversalZone;
        private RotationDirection m_rotationDirection;
        private float m_pingPongSpeedMultiplier = 1f;
        private float m_impulseSpeed;
        private TimeManager m_timeManager;
        private bool m_isDestroyed;

        // Original 06003a30..39, ARM64 68fffc..69005c. Accessor declaration order
        // also retains the generated backing field before the readonly data store.
        protected bool IsDestroyed => m_isDestroyed;
        public float MinSnapAngle => m_minSnapAngle;
        public float MaxSnapAngle => m_maxSnapAngle;
        public float MaxSnapCosAngle { get; private set; }
        private readonly StackableData m_stackableData = new StackableData();
        public Transform CachedTransform => m_cachedTransform;
        public bool PingPongWhenIdle => m_pingPongWhenIdle && m_idleAutomation;
        public float PingPongToDegrees => m_pingPongToDegrees;
        public float CurrentImpulseSpeed { get => m_impulseSpeed; set => m_impulseSpeed = value; }

        private enum RotationDirection { Standard = 0, Reversed = 1 }
        public enum OverrideType { RotationSlerpMultiplier = 0 }

        // 06003a3a / 68fb3c.
        protected virtual void Awake() { Initialise(); }
        // 06003a3b / 690064. Cache first, configure overrides, then resolve time.
        private void Initialise()
        {
            m_cachedTransform = transform;
            m_hasCachedTransform = true;
            m_stackableData.SetBaseValue((int)OverrideType.RotationSlerpMultiplier, m_rotationSlerpMultiplier);
            m_stackableData.SetRetrievalOperation<float>((int)OverrideType.RotationSlerpMultiplier,
                StackableData.RetrievalOperation.Latest, 0f);
            MaxSnapCosAngle = Mathf.Cos(m_maxSnapAngle * Mathf.Deg2Rad);
            m_rotationDirection = RotationDirection.Standard;
            if (m_idleAutomation) m_timeManager = ProcessManager.GetSystem<TimeManager>(null, true);
        }
        // 06003a3c / 6901a4.
        private void LateUpdate() { ProcessAutomatedBehaviour(); }
        // 06003a3d / 6904e0 retains the component lookup even before the play gate.
        private void OnValidate() { _ = gameObject; if (Application.isPlaying) Initialise(); }
        // 06003a3e / 68fcf8. Strict positive bounds deliberately snap NaN angles.
        protected Quaternion GetTargetRotation(Quaternion currentRotation, Quaternion targetRotation, float deltaTime)
        {
            if (!m_smoothingEnabled) return targetRotation;
            float angle = Quaternion.Angle(currentRotation, targetRotation);
            if (angle > m_minSnapAngle && angle < m_maxSnapAngle)
            {
                float slerp = m_rotationSlerpCurve.Evaluate(angle == 0f ? 0f : angle / 180f);
                slerp *= m_stackableData.Get<float>((int)OverrideType.RotationSlerpMultiplier, true, 0f);
                return Quaternion.Slerp(currentRotation, targetRotation, slerp * deltaTime);
            }
            return targetRotation;
        }
        // 06003a3f..44 / 69058c..6905cc.
        public void ToggleSmoothing(bool on) { m_smoothingEnabled = on; }
        public void ToggleAutomation(bool on) { m_idleAutomation = on; m_smoothingEnabled = !on; }
        public void SetIdleRotationSpeed(Vector3 speed) { m_idleRotationSpeed = speed; }
        public void OverrideRotation(Quaternion rotation) { m_rotationOverriden = true; SetRotation(rotation); }
        public void StopOverridingRotation() { m_rotationOverriden = false; }
        public void ToggleDisableRotationUpdate(bool value) { m_disableRotationUpdate = value; }
        // 06003a45 / 68ff38. Overrides intentionally still pass this setter.
        protected virtual void SetRotation(Quaternion rotation)
        {
            if (m_isDestroyed || m_disableRotationUpdate || !m_hasCachedTransform) return;
            m_cachedTransform.rotation = rotation;
        }
        // 06003a46 / 6905d4 does not check the cached-transform presence flag.
        private void SetLocalRotation(Quaternion rotation)
        {
            if (m_isDestroyed || m_disableRotationUpdate) return;
            m_cachedTransform.localRotation = rotation;
        }
        // 06003a47 / 68f53c.
        public void UpdateRotation(Quaternion targetRotation, float deltaTime)
        {
            if (m_isDestroyed || m_rotationOverriden || m_disableRotationUpdate) return;
            Quaternion rotation = GetTargetRotation(m_cachedTransform.rotation, targetRotation, deltaTime);
            SetRotation(rotation);
        }
        // 06003a48 / 68f6b4. Preserve up*forward quaternion multiplication order.
        public void UpdateRotation(Quaternion targetUpRotation, float forwardAngle, float deltaTime)
        {
            if (m_isDestroyed || m_rotationOverriden || m_disableRotationUpdate) return;
            Quaternion upRotation = Quaternion.FromToRotation(Vector3.up, m_cachedTransform.up);
            upRotation = GetTargetRotation(upRotation, targetUpRotation, deltaTime);
            Quaternion forwardRotation = Quaternion.AngleAxis(forwardAngle, upRotation * Vector3.up);
            SetRotation(upRotation * forwardRotation);
        }
        // 06003a49 / 6905f4: the native release evaluates smoothing for its side
        // effects, discards the result, and applies the raw Euler target locally.
        public void UpdateLocalRotation(Vector3 targetRotation, float deltaTime)
        {
            if (m_isDestroyed || m_rotationOverriden || m_disableRotationUpdate) return;
            Quaternion current = m_cachedTransform.localRotation;
            Quaternion target = Quaternion.Euler(targetRotation);
            _ = GetTargetRotation(current, target, deltaTime);
            SetLocalRotation(target);
        }
        // 06003a4a / 6901c4. Impulse decay precedes ping-pong, rotation, then extent.
        private void ProcessAutomatedBehaviour()
        {
            if (m_isDestroyed || !m_idleAutomation || m_rotationOverriden || m_disableRotationUpdate) return;
            float deltaTime = m_timeManager.GetDeltaTime(m_timeCategory);
            Vector3 speed = m_idleRotationSpeed * (m_rotationDirection == RotationDirection.Standard ? 1f : -1f);
            float idleMagnitude = m_idleRotationSpeed.magnitude;
            float nextImpulse = 0f;
            if (m_impulseSpeed > idleMagnitude && !Mathf.Approximately(m_impulseSpeed, idleMagnitude))
            {
                float ratio = m_impulseSpeed / idleMagnitude;
                ratio = Mathf.Lerp(ratio, 1f, deltaTime * m_impulseDeceleration.Evaluate(ratio));
                speed *= ratio;
                nextImpulse = speed.magnitude;
            }
            m_impulseSpeed = nextImpulse;
            speed *= m_pingPongSpeedMultiplier;
            Quaternion target = Quaternion.Euler(speed * deltaTime) * m_cachedTransform.rotation;
            PreProcessPingPongRotation(ref target);
            UpdateRotation(target, deltaTime);
            PostProcessPingPongRotation();
        }
        // 06003a4b / 6907b8 uses absolute world orientation, not an initial pose.
        private void PreProcessPingPongRotation(ref Quaternion newTarget)
        {
            if (m_isDestroyed || !m_pingPongWhenIdle) return;
            float angle = Quaternion.Angle(Quaternion.identity, newTarget);
            // Ordered >= matches native b.ge/jae: an unordered NaN extent takes
            // the inside branch, clears a prior latch, and never reads the pose.
            if (angle >= m_pingPongToDegrees)
            {
                if (!m_pingPongReversalZone)
                {
                    m_pingPongReversalZone = true;
                    m_rotationDirection = m_rotationDirection == RotationDirection.Standard
                        ? RotationDirection.Reversed : RotationDirection.Standard;
                    newTarget = m_cachedTransform.rotation;
                }
            }
            else if (m_pingPongReversalZone) m_pingPongReversalZone = false;
        }
        // 06003a4c / 6908e8 preserves division by a zero extent and null-curve gate.
        private void PostProcessPingPongRotation()
        {
            if (m_isDestroyed || !m_pingPongWhenIdle || m_ppSpeedMultiplierOverExtent == null) return;
            float angle = Quaternion.Angle(Quaternion.identity, m_cachedTransform.rotation);
            m_pingPongSpeedMultiplier = m_ppSpeedMultiplierOverExtent.Evaluate(Mathf.Clamp01(angle / m_pingPongToDegrees));
        }
        // 06003a4d / 690a08: positive impulses reverse direction and accumulate.
        public void ApplyRotationImpulse(float impulse)
        {
            m_rotationDirection = impulse > 0f ? RotationDirection.Reversed : RotationDirection.Standard;
            m_impulseSpeed += Mathf.Abs(impulse);
        }
        // 06003a4e / 9a2978 is the genuine fully-shared arbitrary-T native body.
        public StackableDataHandle AddOverrideType<T>(OverrideType overrideType, T value)
        { return m_stackableData.AddOverride((int)overrideType, value); }
        // 06003a4f / 690a28 ignores the category and removes the entire handle.
        public void RemoveOverrideHandle(OverrideType overrideType, StackableDataHandle handle)
        { m_stackableData.RemoveOverrides(handle); }
        // 06003a50 / 690a38.
        private void OnDestroy() { m_isDestroyed = true; }
        // 06003a51 / 68f9ac. Field initializers retain native defaults and null curves.
        public SmoothedTransformRotator() { }
    }
}
