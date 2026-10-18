using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000825: a rotator whose accumulated rotation on each
    // axis is held to its limit (0 leaves the axis free). Only the first axis to
    // reach its limit in a step is clamped; the later axes are not accumulated.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformRotatorLimits : TransformRotator
    {
        [Tooltip("Limits to clamp rotations to.")]
        [SerializeField]
        private Vector3 m_limits;
        [Tooltip("Callback when a limit is reached.")]
        [SerializeField]
        private UnityEvent m_onLimitReached = new UnityEvent();
        private Vector3 m_rotationsAccumulated;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_rotationsAccumulated = Vector3.zero;
        }

        // An unordered limit or accumulation counts as reached, and the overshoot is
        // taken back from both the accumulation and this step's rotation.
        private bool ClampToLimit(ref float rotationAccumulated, ref float rotation, float limit)
        {
            rotationAccumulated += rotation;
            if (limit == 0f || Mathf.Abs(rotationAccumulated) < limit)
                return false;
            float overshoot = Mathf.Abs(rotationAccumulated) - limit;
            float correction = rotation < 0f ? -overshoot : overshoot;
            rotationAccumulated -= correction;
            rotation -= correction;
            return true;
        }

        // The event is raised after the rotation for every step that reaches a limit.
        protected override void ApplyRotations(Vector3 rotations)
        {
            bool limitReached = ClampToLimit(ref m_rotationsAccumulated.x, ref rotations.x, m_limits.x)
                || ClampToLimit(ref m_rotationsAccumulated.y, ref rotations.y, m_limits.y)
                || ClampToLimit(ref m_rotationsAccumulated.z, ref rotations.z, m_limits.z);
            base.ApplyRotations(rotations);
            if (limitReached)
                m_onLimitReached.Invoke();
        }
    }
}
