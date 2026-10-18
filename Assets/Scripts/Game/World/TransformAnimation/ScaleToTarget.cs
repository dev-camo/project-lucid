using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ad2: scales between the Awake local scale and the
    // target along the curve, driven by System-category time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ScaleToTarget : MonoBehaviour
    {
        [Tooltip("Animation curve controls the rate and speed of scaling")]
        [SerializeField]
        private AnimationCurve m_curve;
        [SerializeField]
        private Vector3 m_scaleTarget;
        private Vector3 m_scaleOriginal;

        private void Awake()
        {
            m_scaleOriginal = transform.localScale;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            transform.localScale = m_scaleOriginal;
        }

        public void StartScaleToTarget()
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTransform(m_scaleOriginal, m_scaleTarget));
        }

        public void RevertScaleToOriginal()
        {
            StopAllCoroutines();
            StartCoroutine(ScaleTransform(m_scaleTarget, m_scaleOriginal));
        }

        // The curve value is divided by the time of its last key and clamped by the lerp.
        private IEnumerator ScaleTransform(Vector3 start, Vector3 target)
        {
            TimeManager timeManager = ProcessManager.GetSystem<TimeManager>();
            float t = 0f;
            float totalTime = m_curve[m_curve.length - 1].time;
            while (t < totalTime)
            {
                float progress = m_curve.Evaluate(t) / totalTime;
                transform.localScale = Vector3.Lerp(start, target, progress);
                t += timeManager.GetDeltaTime(TimeCategory.System);
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
