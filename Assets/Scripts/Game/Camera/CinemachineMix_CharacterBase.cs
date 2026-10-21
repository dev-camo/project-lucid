using System;
using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000274 (nested VirtualCameraWeighting 0x02000275).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CinemachineMix_CharacterBase : TimeScaledComponent_SDT
    {
        [SerializeField]
        protected CinemachineMixingCamera m_mixingCamera;
        [SerializeField]
        protected VirtualCameraWeighting[] m_cameraWeightings;
        [Min(0f)]
        [Tooltip("Higher value means longer to blend towards target value. Set to 0 for no dampening.")]
        [SerializeField]
        private float m_smoothingDampTimeSeconds;
        protected CharacterManager m_characterManager;
        private float m_currentBlendValue;
        private float m_blendVelocity;

        // 0x06000dbb
        private void Start()
        {
            ProcessManager.GetSystemRef<CharacterManager>().InvokeOnValid(manager => m_characterManager = manager);
        }

        // 0x06000dbc: the shipped base body is empty.
        protected override void InternalUpdate(float deltaTime) { }

        // 0x06000dbd: dampening applies only for a strictly positive smoothing time.
        protected void UpdateBlendWeightings(float targetValue, float deltaTime)
        {
            if (m_smoothingDampTimeSeconds > 0f)
                targetValue = Mathf.SmoothDamp(m_currentBlendValue, targetValue, ref m_blendVelocity,
                    m_smoothingDampTimeSeconds, float.PositiveInfinity, deltaTime);
            m_currentBlendValue = targetValue;
            for (int i = 0; i < m_cameraWeightings.Length; i++)
            {
                VirtualCameraWeighting weighting = m_cameraWeightings[i];
                m_mixingCamera.SetWeight(weighting.VirtualCamera, weighting.ValueToWeightingCurve.Evaluate(m_currentBlendValue));
            }
        }

        // 0x06000dbe
        protected bool IsLive() => CinemachineCore.Instance.IsLive(m_mixingCamera);

        // 0x06000dbf
        protected CinemachineMix_CharacterBase() { }

        // 0x02000275, ctor 0x06000dc1
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class VirtualCameraWeighting
        {
            [Tooltip("X axis = float value\nY axis = camera weighting")]
            public AnimationCurve ValueToWeightingCurve;
            public CinemachineVirtualCameraBase VirtualCamera;

            public VirtualCameraWeighting() { }
        }
    }
}
