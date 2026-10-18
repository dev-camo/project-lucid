using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000827: plays the curve once as the local Y angle,
    // with time advanced by the speed, then raises RotationComplete.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformRotatorSingleCurve : TimeScaledComponent_SDT
    {
        [Header("Rotates objects in 'Y' axis only based on AnimCurve value, multiplied by speed. (Not Looping).", order = 0)]
        [Space(10)]
        [SerializeField]
        private AnimationCurve m_rotationCurve;
        [SerializeField]
        private float m_rotationSpeed = 1f;
        [SerializeField]
        private bool m_activateOnEnable;
        public FastAction RotationComplete;
        private float m_timerSeconds;
        private bool m_shouldRotate;
        private Quaternion m_originalRotation;
        private float m_curveLength;
        private bool m_reverseRotation;

        // The curve length is the time of its last key.
        protected override void OnEnable()
        {
            base.OnEnable();
            m_originalRotation = transform.localRotation;
            m_curveLength = m_rotationCurve[m_rotationCurve.length - 1].time;
            if (m_activateOnEnable)
                Activate();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.localRotation = m_originalRotation;
        }

        private void Activate(bool reverseRotation = false)
        {
            m_timerSeconds = 0f;
            m_shouldRotate = true;
            m_reverseRotation = reverseRotation;
        }

        // Completion is decided on the advanced time before the angle is applied,
        // so the step that passes the curve length still applies its angle.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_shouldRotate)
                return;
            m_timerSeconds += deltaTime * m_rotationSpeed;
            bool isComplete = m_timerSeconds > m_curveLength;
            Evaluate();
            if (isComplete)
            {
                RotationComplete.Invoke();
                m_shouldRotate = false;
            }
        }

        private void Evaluate()
        {
            float angle = m_rotationCurve.Evaluate(m_timerSeconds);
            Vector3 eulerAngles = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(eulerAngles.x, m_reverseRotation ? -angle : angle, eulerAngles.z);
        }
    }
}
