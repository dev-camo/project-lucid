using System;
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime TimeManipulator (TypeDef 0x020007f4, x86-64 0x5F8450..0x5F87AF) and its
    // <ScaleTime>d__8 iterator (0x020007f5): blends the TimeManager's time scales over a curve.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeManipulator : MonoBehaviour
    {
        private SystemRef<TimeManager> m_timeManagerRef;
        private TimeSetting m_currentSetting;
        private TimeSetting m_blendedSetting;
        private StackableDataHandle m_stackableDataHandle;
        private Coroutine m_scalingCoroutine;
        private Action m_onComplete;

        private void Awake() // 06002e0a
        {
            m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();
            m_currentSetting = TimeSetting.GetDefault();
            m_blendedSetting = TimeSetting.Clone(m_currentSetting);
        }

        // 06002e0b: the override is stacked once; the coroutine handle is not kept, so CleanUp cannot stop the blend.
        public void ApplyTimeScaling(TimeSetting setting, AnimationCurve curve, Action onComplete, bool resetAfter = false)
        {
            m_onComplete = onComplete;
            if (m_stackableDataHandle == null && m_timeManagerRef.IsValid())
                m_stackableDataHandle = m_timeManagerRef.Get().ApplyTimeSetting(m_currentSetting);
            CoroutineUtils.RunCoroutine(ScaleTime(m_currentSetting, setting, curve, resetAfter));
        }

        // 06002e0c, MoveNext 06002e12: one step per fixed update on unscaled time, ending on the curve's last key.
        private IEnumerator ScaleTime(TimeSetting from, TimeSetting to, AnimationCurve curve, bool resetAfter)
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            float elapsedTime = 0f;
            float endTime = curve[curve.length - 1].time;
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            while (elapsedTime <= endTime)
            {
                m_blendedSetting.Lerp(from, to, curve.Evaluate(elapsedTime));
                timeManager.UpdateTimeSetting(m_stackableDataHandle, m_blendedSetting);
                yield return waitForFixedUpdate;
                elapsedTime += Time.fixedUnscaledDeltaTime;
            }
            if (resetAfter)
            {
                CleanUp();
            }
            else
            {
                m_blendedSetting.Lerp(from, to, curve.Evaluate(endTime));
                timeManager.UpdateTimeSetting(m_stackableDataHandle, m_blendedSetting);
            }
            m_onComplete();
        }

        public void CleanUp() // 06002e0d
        {
            if (m_scalingCoroutine != null)
            {
                StopCoroutine(m_scalingCoroutine);
                m_scalingCoroutine = null;
            }
            if (m_stackableDataHandle != null)
            {
                if (m_timeManagerRef.IsNull()) return;
                m_timeManagerRef.Get().RemoveTimeSetting(m_stackableDataHandle);
                m_stackableDataHandle = null;
            }
        }

        private void OnDisable() { CleanUp(); } // 06002e0e

        public TimeManipulator() { } // 06002e0f
    }
}
