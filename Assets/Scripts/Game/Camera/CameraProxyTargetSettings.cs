using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200025f, sequential serializable value type. Camera
    // proxies supply orientation; these original settings do not change movement.
    [Serializable]
    public struct CameraProxyTargetSettings
    {
        [SerializeField] private bool m_boundsEnabled;
        [SerializeField]
        [Tooltip("If character's Y is higher than this, then clamp to this offset.")]
        private float m_upperYBound;
        [Tooltip("If character's Y is lower than this, then clamp to this offset.")]
        [SerializeField] private float m_lowerYBound;
        [SerializeField]
        [Tooltip("Dampening applied when character position is outside of Y bounds. Smaller value will reach target quicker.")]
        private float m_outOfBoundsDampening;
        [SerializeField] private bool m_overrideYSmoothTimeCurve;
        [SerializeField]
        [Tooltip("Y axis = smooth dampening time\nX axis = distance from proxy Y to character Y")]
        [ShowIf("m_overrideYSmoothTimeCurve", (string)null)]
        private AnimationCurve m_ySmoothTimeCurveOverride;
        [SerializeField] private bool m_orientateToCustomProxy;
        [SerializeField] private bool m_orientateToGravityProxy;
        [SerializeField] private bool m_orientateToVisualProxy;
        [SerializeField] private bool m_overrideRelativeOffset;
        [SerializeField, ShowIf("m_overrideRelativeOffset", (string)null)]
        private Vector3 m_relativeOffset;
        [SerializeField] private bool m_limitXAxisAngle;
        [ShowIf("m_limitXAxisAngle", (string)null)]
        [SerializeField, Range(0f, 180f)] private float m_maxXAxisAngle;
        [Tooltip("After a heading override, the max X axis angle will close back in at this rate.")]
        [SerializeField, ShowIf("m_limitXAxisAngle", (string)null)]
        private float m_xAxisAngleLimitSmoothingRate;
        [SerializeField] private int m_priority;

        // Original 06000cf8..06000d03 / ARM 6f0224..6f0280, direct field reads.
        public float UpperYBound => m_upperYBound;
        public float LowerYBound => m_lowerYBound;
        public bool BoundsEnabled => m_boundsEnabled;
        public float OutOfBoundsDampening => m_outOfBoundsDampening;
        public bool OverrideYSmoothTimeCurve => m_overrideYSmoothTimeCurve;
        public AnimationCurve YSmoothTimeCurveOverride => m_ySmoothTimeCurveOverride;
        public bool OverrideRelativeOffset => m_overrideRelativeOffset;
        public Vector3 RelativeOffset => m_relativeOffset;
        public int Priority => m_priority;
        public bool LimitXAxisAngle => m_limitXAxisAngle;
        public float MaxXAxisAngle => m_maxXAxisAngle;
        public float XAxisAngleLimitSmoothingRate => m_xAxisAngleLimitSmoothingRate;

        // 06000d04 / ARM 6f0288. Signed greater-than selects the new settings;
        // ties retain the current value. Even a retained value passes through
        // SetValue; the original operation always continues, with no null guard.
        public static StackableData.OperationAction CameraProxyTargetSettingsPriority(
            StackableData.StackableDataContainer<CameraProxyTargetSettings> stackableDataContainer,
            ref StackableData.ResultCarrier<CameraProxyTargetSettings> result)
        {
            result.SetValue(result.HasValue
                ? (stackableDataContainer.Value.Priority > result.Value.Priority
                    ? stackableDataContainer.Value : result.Value)
                : stackableDataContainer.Value);
            return StackableData.OperationAction.Continue;
        }

        // 06000d05 / ARM 6f0360. Flag precedence is custom, gravity, visual.
        // A selected null Transform still returns true and later dereferences it.
        // The visual path uses the stored CachedTransform, not component.transform.
        private bool TryGetProxy(Character character, out Transform transform)
        {
            if (m_orientateToCustomProxy)
                transform = character.CustomProxy;
            else if (m_orientateToGravityProxy)
                transform = character.GravityProxy;
            else if (m_orientateToVisualProxy)
                transform = character.VisualProxy.CachedTransform;
            else
            {
                transform = null;
                return false;
            }
            return true;
        }

        // 06000d06 / ARM 6efe74: genuine stored actor rotation fallback.
        public Quaternion GetWorldRotation(Character character)
        {
            return TryGetProxy(character, out Transform transform)
                ? transform.rotation : character.WorldRotation;
        }

        // 06000d07 / ARM 6f03e0: UpDirection, not the gravity-derived WorldUp.
        public Vector3 GetWorldUp(Character character)
        {
            return TryGetProxy(character, out Transform transform)
                ? transform.up : character.UpDirection;
        }

        // 06000d08 / ARM 6f04c0: rotate the actor's stored forward by the
        // up-to-proxy-up rotation. The proxy's forward direction is not read.
        public Vector3 GetWorldForward(Character character)
        {
            return TryGetProxy(character, out Transform transform)
                ? Quaternion.FromToRotation(character.UpDirection, transform.up) * character.ForwardDirection
                : character.ForwardDirection;
        }
    }
}
