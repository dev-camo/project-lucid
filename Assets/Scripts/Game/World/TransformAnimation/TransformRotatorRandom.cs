using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000826: every update draws a new random local
    // rotation offset from the Awake local rotation and snaps or smooths to it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TransformRotatorRandom : TimeScaledComponent_SDT
    {
        [Tooltip("Optional target transform else this transform is used.")]
        [SerializeField]
        private Transform m_target;
        [SerializeField]
        [Tooltip("Minimum range of random angle generation.")]
        private Vector3 m_anglesMin;
        [SerializeField]
        [Tooltip("Maximum range of random angle generation.")]
        private Vector3 m_anglesMax;
        [Tooltip("Time it will take to reach the target. 0 will snap to target.")]
        [SerializeField]
        private float m_smoothTime;
        [Tooltip("Optionally clamp the max smooth speed.  0 will leave unclamped.")]
        [SerializeField]
        private float m_maxSpeed;
        private Transform m_transform;
        private Quaternion m_startRotation;
        private Quaternion m_velocity;

        protected override void Awake()
        {
            base.Awake();
            m_transform = m_target != null ? m_target : transform;
            m_startRotation = m_transform.localRotation;
        }

        // Restores the start rotation as a world rotation, although Awake read it locally.
        protected override float OnSynchronise(float enableTime)
        {
            float time = base.OnSynchronise(enableTime);
            m_transform.rotation = m_startRotation;
            return time;
        }

        // The angles are drawn in x, y, z order before the smoothing check, so every
        // update advances the random sequence. A NaN smooth time snaps.
        protected override void InternalUpdate(float deltaTime)
        {
            Quaternion randomRotation = Quaternion.Euler(Random.Range(m_anglesMin.x, m_anglesMax.x),
                Random.Range(m_anglesMin.y, m_anglesMax.y), Random.Range(m_anglesMin.z, m_anglesMax.z));
            Quaternion targetRotation = m_startRotation * randomRotation;
            if (m_smoothTime > 0f)
            {
                float maxSpeed = m_maxSpeed > 0f ? m_maxSpeed : Mathf.Infinity;
                targetRotation = QuaternionExtensions.SmoothDamp(m_transform.localRotation, targetRotation,
                    ref m_velocity, m_smoothTime, maxSpeed, deltaTime);
            }
            m_transform.localRotation = targetRotation;
        }
    }
}
