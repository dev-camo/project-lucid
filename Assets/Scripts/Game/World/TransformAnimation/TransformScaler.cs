using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200082a: moves the local scale between the minimum and
    // maximum on a cosine wave per axis, starting at the maximum.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TransformScaler : TimeScaledComponent_SDT
    {
        [SerializeField]
        [Space(10)]
        [Header("Scales an object back and forth based on a Sine wave in any Axis / Speed (Loops).", order = 0)]
        private Vector3 m_frequency = Vector3.one;
        [SerializeField]
        private Vector3 m_offset = Vector3.zero;
        [SerializeField]
        private Vector3 m_minScale = Vector3.one;
        [SerializeField]
        private Vector3 m_maxScale = Vector3.one;
        private Vector3 m_timerSeconds = Vector3.zero;
        private Vector3 m_originalScale = Vector3.zero;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_timerSeconds = Vector3.zero;
            m_originalScale = transform.localScale;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.localScale = m_originalScale;
        }

        // The timer wraps by a full turn once timer plus phase offset passes 2 pi, but
        // this step's cosine still uses the unwrapped phase.
        protected override void InternalUpdate(float deltaTime)
        {
            m_timerSeconds += deltaTime * Mathf.PI * m_frequency;
            Vector3 phase = m_timerSeconds + m_offset * Mathf.PI;
            if (phase.x > Mathf.PI * 2f)
                m_timerSeconds.x -= Mathf.PI * 2f;
            if (phase.y > Mathf.PI * 2f)
                m_timerSeconds.y -= Mathf.PI * 2f;
            if (phase.z > Mathf.PI * 2f)
                m_timerSeconds.z -= Mathf.PI * 2f;
            Vector3 blend = (Vector3.one + new Vector3(Mathf.Cos(phase.x), Mathf.Cos(phase.y), Mathf.Cos(phase.z))) * 0.5f;
            // Built before reading transform, matching the native order.
            Vector3 scale = new Vector3(Mathf.Lerp(m_minScale.x, m_maxScale.x, blend.x),
                Mathf.Lerp(m_minScale.y, m_maxScale.y, blend.y), Mathf.Lerp(m_minScale.z, m_maxScale.z, blend.z));
            transform.localScale = scale;
        }
    }
}
