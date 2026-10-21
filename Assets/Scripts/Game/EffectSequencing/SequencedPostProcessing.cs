using System.Collections;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace HardlightProject
{
    // Original Game.Runtime 02000626 (+ iterator 02000627); native 0x56A610.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedPostProcessing : SequencedEffect
    {
        [SerializeField] private AnimationCurve m_curve;
        [SerializeField] private Volume m_postProcessVolume;
        private TimeManager m_timeManager;

        // Original060020ed: the coroutine runs on the shared coroutine host, not on this behaviour.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
            CoroutineUtils.RunCoroutine(ScalePostProcessWeight());
        }

        // Original060020ee and iterator 02000627 (MoveNext 0x56A7A0): drive the volume weight along the curve, then
        // leave the final key's value in place. The curve is sampled before each (scaled) delta step.
        private IEnumerator ScalePostProcessWeight()
        {
            float elapsedTime = 0f;
            Keyframe finalFrame = m_curve[m_curve.length - 1];
            float endTime = finalFrame.time;
            while (elapsedTime <= endTime)
            {
                m_postProcessVolume.weight = m_curve.Evaluate(elapsedTime);
                yield return null;
                elapsedTime += m_timeManager.GetDeltaTime(m_timeCategory);
            }
            m_postProcessVolume.weight = finalFrame.value;
            m_onComplete();
        }

        // Implicit original060020ef.
    }
}
