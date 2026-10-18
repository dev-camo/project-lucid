using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000824: rotates the target (or this transform) in
    // local space by the speed in use times the multiplier, every time-scaled update.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformRotator : TimeScaledComponent_SDT
    {
        [Header("Rotates objects in any axis at a given speed (degrees p/s) (Loops)", order = 0)]
        [Space(10)]
        [Tooltip("Optional target transform else this transform is used.")]
        [SerializeField]
        private Transform m_targetTransform;
        [SerializeField]
        private bool m_randomiseRotationSpeedOnAwake;
        [SerializeField]
        [HideIf("m_randomiseRotationSpeedOnAwake", null)]
        private Vector3 m_rotationSpeed = Vector3.zero;
        [SerializeField]
        [ShowIf("m_randomiseRotationSpeedOnAwake", null)]
        private Vector3 m_rotationSpeedMin = Vector3.zero;
        [ShowIf("m_randomiseRotationSpeedOnAwake", null)]
        [SerializeField]
        private Vector3 m_rotationSpeedMax = Vector3.zero;
        private Transform m_transform;
        private Vector3 m_rotationSpeedInUse;
        private Quaternion m_startRotation;
        private float m_multiplier = 1f;

        public Vector3 RotationSpeed
        {
            get => m_rotationSpeedInUse;
            set => m_rotationSpeedInUse = value;
        }

        // The start rotation is the world rotation; each axis speed is drawn in x, y, z order.
        protected override void Awake()
        {
            base.Awake();
            m_transform = m_targetTransform != null ? m_targetTransform : transform;
            m_startRotation = m_transform.rotation;
            if (m_randomiseRotationSpeedOnAwake)
                m_rotationSpeedInUse = new Vector3(Random.Range(m_rotationSpeedMin.x, m_rotationSpeedMax.x),
                    Random.Range(m_rotationSpeedMin.y, m_rotationSpeedMax.y),
                    Random.Range(m_rotationSpeedMin.z, m_rotationSpeedMax.z));
            else
                m_rotationSpeedInUse = m_rotationSpeed;
        }

        // Restarts from the start rotation and applies the whole synchronised time at once.
        protected override float OnSynchronise(float enableTime)
        {
            float time = base.OnSynchronise(enableTime);
            ResetRotation();
            Rotate(time);
            return time;
        }

        protected override void InternalUpdate(float deltaTime)
        {
            Rotate(deltaTime);
        }

        public void ResetRotation()
        {
            m_transform.rotation = m_startRotation;
        }

        private void Rotate(float deltaTime)
        {
            ApplyRotations(m_rotationSpeedInUse * m_multiplier * deltaTime);
        }

        protected virtual void ApplyRotations(Vector3 rotations)
        {
            m_transform.Rotate(rotations);
        }

        public void SetRotationSpeedMultiplier(float multiplier)
        {
            m_multiplier = multiplier;
        }
    }
}
