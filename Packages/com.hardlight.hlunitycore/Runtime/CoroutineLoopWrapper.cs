using System;
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000264. An IEnumerator that yields
    // null while its condition holds, until the timeout budget is spent.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CoroutineLoopWrapper : IEnumerator
    {
        // Original 0x02000265.
        public enum UpdateType
        {
            Delta = 0,
            UnscaledDelta = 1,
            FixedDelta = 2,
            FixedUnscaledDelta = 3
        }

        private readonly Func<float> m_timeUpdate;
        private readonly Func<bool> m_loopCondition;
        private readonly Action m_timeoutCallback;
        private readonly float m_timeoutSeconds;
        private float m_timerSeconds;

        // 0x06000f2e; 0x1B1C050. Clock lambdas <.ctor>b__6_0..3 (0x06000f34..37)
        // follow the case order; the start callback runs last, inside the
        // constructor.
        public CoroutineLoopWrapper(Func<bool> loopCondition, float timeoutSeconds, UpdateType updateType = UpdateType.Delta,
            Action timeoutCallback = null, Action startCallback = null)
        {
            m_loopCondition = loopCondition;
            m_timeoutCallback = timeoutCallback;
            m_timeoutSeconds = timeoutSeconds;
            m_timerSeconds = timeoutSeconds;
            m_timeUpdate = updateType switch
            {
                UpdateType.Delta => () => Time.deltaTime,
                UpdateType.UnscaledDelta => () => Time.unscaledDeltaTime,
                UpdateType.FixedDelta => () => Time.fixedDeltaTime,
                UpdateType.FixedUnscaledDelta => () => Time.fixedUnscaledDeltaTime,
                _ => throw new ArgumentOutOfRangeException(nameof(updateType), updateType, "Provided TimeType is not handled.")
            };
            startCallback?.Invoke();
        }

        // 0x06000f2f; 0x1B1C4A0. The budget is checked before this step's
        // delta is taken, so a NaN budget never times out.
        public bool MoveNext()
        {
            if (m_timerSeconds <= 0f)
            {
                m_timeoutCallback?.Invoke();
                return false;
            }
            m_timerSeconds -= m_timeUpdate();
            return m_loopCondition();
        }

        // 0x06000f30; 0x1B1C510.
        public void Reset()
        {
            m_timerSeconds = m_timeoutSeconds;
        }

        // 0x06000f31; 0x1B1C520.
        object IEnumerator.Current => null;
    }
}
