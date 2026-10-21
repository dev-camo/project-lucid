using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0200062b; native 0x56AF10.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedUnityEvent : SequencedEffect, ISequencedEffectWithCallback
    {
        [SerializeField] private UnityEvent<ISequencedEffectWithCallback> m_unityEvent;
        [SerializeField] private float m_lengthOverrideSeconds;
        private int m_sequenceWaitCount;

        // Original060020ff: listeners may hold the effect open through OnSequenceStart/OnSequenceEnd.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_sequenceWaitCount = 0;
            m_unityEvent.Invoke(this);
            if (m_sequenceWaitCount > 0) return;
            OnEffectComplete(m_onComplete);
        }

        // Original06002100: a positive override delays completion in fixed-step time.
        private void OnEffectComplete(Action onComplete)
        {
            if (m_lengthOverrideSeconds > 0f) TimeScaledUtilities_SDT.DelayFixedSeconds(m_lengthOverrideSeconds, m_timeCategory, onComplete);
            else onComplete?.Invoke();
        }

        // Original06002101.
        public void OnSequenceStart() { m_sequenceWaitCount++; }

        // Original06002102: the effect completes when the last waiter ends.
        public void OnSequenceEnd()
        {
            if (--m_sequenceWaitCount > 0) return;
            OnEffectComplete(m_onComplete);
        }

        // Original06002103: the caller's own end hook completes this effect.
        public void BeginSequence(ISequencedEffectWithCallback callback)
        {
            callback.OnSequenceStart();
            OnEffectComplete(callback.OnSequenceEnd);
        }

        // Implicit original06002104.
    }
}
