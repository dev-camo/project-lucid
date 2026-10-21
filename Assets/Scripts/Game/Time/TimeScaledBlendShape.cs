using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeScaledBlendShape (TypeDef 0x020007f8, x86-64 0x5F9410..0x5F977F): drives blend
    // shape 0 from a weight curve over scaled time, then disables itself at the curve's last key.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledBlendShape : TimeScaledComponent_SDT
    {
        [Tooltip("The skinned mesh renderer to apply blending to."), SerializeField]
        private SkinnedMeshRenderer m_skinnedMeshRenderer;
        [SerializeField, Tooltip("Blend shape weight animation. Range values are between the minimum and the maximum weights defined in the model.")]
        private AnimationCurve m_blendAnim;
        [SerializeField, HideInInspector] private float m_blendLength;
        private float m_time;

        public void Play() // 06002e28
        {
            enabled = true;
            m_time = 0f;
        }

        // 06002e29: leaving the component resets the shape to the curve's first value.
        protected override void OnDisable()
        {
            base.OnDisable();
            m_time = 0f;
            UpdateBlend(0f);
        }

        // 06002e2a: the weight is the curve value times 100.
        public void UpdateBlend(float time)
        {
            if (m_blendAnim == null || m_skinnedMeshRenderer == null) return;
            m_skinnedMeshRenderer.SetBlendShapeWeight(0, m_blendAnim.Evaluate(time) * 100f);
        }

        // 06002e2b: a blend time that is not below the length (or is NaN) stops the component.
        protected override void InternalUpdate(float deltaTime)
        {
            m_time += deltaTime;
            UpdateBlend(m_time);
            if (m_time < m_blendLength) return;
            enabled = false;
        }

        protected override void OnValidate() // 06002e2c
        {
            base.OnValidate();
            if (!gameObject.CanValidate() || m_skinnedMeshRenderer == null || m_blendAnim == null || m_blendAnim.length == 0) return;
            m_blendLength = m_blendAnim[m_blendAnim.length - 1].time;
        }

        public TimeScaledBlendShape() { } // 06002e2d
    }
}
