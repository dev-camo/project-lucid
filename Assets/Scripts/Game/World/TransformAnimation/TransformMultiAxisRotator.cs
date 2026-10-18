using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000820: each enabled axis adds its own curve, sampled
    // at that axis' speed, to the Start local euler angles.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformMultiAxisRotator : TimeScaledComponent_SDT
    {
        [Header("Rotates object Per Axis Per Curve")]
        [Space(10)]
        [SerializeField]
        private AnimationSettings m_xSettings;
        [SerializeField]
        private AnimationSettings m_ySettings;
        [SerializeField]
        private AnimationSettings m_zSettings;
        private float m_xTimeSeconds;
        private float m_yTimeSeconds;
        private float m_zTimeSeconds;
        private Vector3 m_initialRotation;
        private Vector3 m_offset;

        private void Start()
        {
            m_xSettings.Initialise();
            m_ySettings.Initialise();
            m_zSettings.Initialise();
            m_initialRotation = transform.localEulerAngles;
        }

        protected override float OnSynchronise(float enableTime)
        {
            float time = base.OnSynchronise(enableTime);
            m_xTimeSeconds = m_xSettings.AnimationSpeed * time;
            m_yTimeSeconds = m_ySettings.AnimationSpeed * time;
            m_zTimeSeconds = m_zSettings.AnimationSpeed * time;
            return time;
        }

        // Every axis time advances; a disabled axis keeps its last offset.
        protected override void InternalUpdate(float deltaTime)
        {
            m_xTimeSeconds += deltaTime * m_xSettings.AnimationSpeed;
            m_yTimeSeconds += deltaTime * m_ySettings.AnimationSpeed;
            m_zTimeSeconds += deltaTime * m_zSettings.AnimationSpeed;
            if (m_xSettings.Enabled)
                m_offset.x = m_xSettings.RotationCurve.Evaluate(m_xTimeSeconds);
            if (m_ySettings.Enabled)
                m_offset.y = m_ySettings.RotationCurve.Evaluate(m_yTimeSeconds);
            if (m_zSettings.Enabled)
                m_offset.z = m_zSettings.RotationCurve.Evaluate(m_zTimeSeconds);
            transform.localEulerAngles = m_initialRotation + m_offset;
        }

        [Serializable]
        private struct AnimationSettings
        {
            [SerializeField]
            private bool m_enableRotation;
            [SerializeField]
            private AnimationCurve m_rotationCuve;
            [SerializeField]
            private float m_animationSpeed;
            [SerializeField]
            private WrapMode m_wrapMode;

            // Only the post-wrap mode is set.
            public void Initialise()
            {
                m_rotationCuve.postWrapMode = m_wrapMode;
            }

            public AnimationCurve RotationCurve => m_rotationCuve;

            public bool Enabled => m_enableRotation;

            public float AnimationSpeed => m_animationSpeed;
        }
    }
}
