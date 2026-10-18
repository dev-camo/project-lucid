using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000822: once the UI is ready, offsets the local
    // position from where it was then by a sine wave per axis.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformOscillator : TimeScaledComponent_SDT
    {
        [Header("Moves an object back and forth based on a Sine wave in any Axis / Speed (Loops)", order = 0)]
        [Space(10)]
        [SerializeField]
        private Vector3 m_amplitude = Vector3.one;
        [SerializeField]
        private Vector3 m_frequency = Vector3.one;
        [SerializeField]
        private Vector3 m_offset = Vector3.zero;
        [SerializeField]
        private bool m_useAbsoluteSine;
        private Vector3 m_timerSeconds = Vector3.zero;
        private Vector3 m_cachedLocalPosition = Vector3.zero;
        private bool m_shouldAnimate;
        private Coroutine m_waitForUICoroutine;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_waitForUICoroutine = CoroutineUtils.WaitForUI(() =>
            {
                m_timerSeconds = Vector3.zero;
                if (transform == null)
                    return;
                m_cachedLocalPosition = transform.localPosition;
                m_shouldAnimate = true;
            });
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CoroutineUtils.StopUtilCoroutine(ref m_waitForUICoroutine);
            if (!m_shouldAnimate)
                return;
            transform.localPosition = m_cachedLocalPosition;
            m_shouldAnimate = false;
        }

        // The timer wraps by a full turn once timer plus phase offset passes 2 pi, but
        // this step's sine still uses the unwrapped phase.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_shouldAnimate)
                return;
            m_timerSeconds += deltaTime * Mathf.PI * m_frequency;
            Vector3 phase = m_timerSeconds + m_offset * Mathf.PI;
            if (phase.x > Mathf.PI * 2f)
                m_timerSeconds.x -= Mathf.PI * 2f;
            if (phase.y > Mathf.PI * 2f)
                m_timerSeconds.y -= Mathf.PI * 2f;
            if (phase.z > Mathf.PI * 2f)
                m_timerSeconds.z -= Mathf.PI * 2f;
            Vector3 sine = new Vector3(Mathf.Sin(phase.x), Mathf.Sin(phase.y), Mathf.Sin(phase.z));
            if (m_useAbsoluteSine)
                sine = sine.Abs();
            Vector3 offset = Vector3.Scale(m_amplitude, sine);
            transform.localPosition = m_cachedLocalPosition + offset;
        }
    }
}
