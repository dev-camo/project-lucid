using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original02000619: complete abstract provider8 plus original PlayEffect closure2.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SequencedEffect : MonoBehaviour
    {
        [SerializeField] protected TimeCategory m_timeCategory = TimeCategory.Effects;
        [SerializeField] protected float m_delay;
        [SerializeField]
        [Tooltip("Only trigger if any of the below input types are active. If list is empty then this check is ignored.")]
        private InputType[] m_triggerOnlyForInputTypes;
        [SerializeField]
        [Tooltip("If ticked, then the effect will only trigger if NONE of the input types in the above list are active.")]
        private bool m_inputTypesExclusive;
        protected Action m_onComplete;
        private Coroutine m_delayCoroutine;
        // Original060020b2/read-only backingfield040015a4 remains zero; no original setter.
        public virtual float Progress { get; }

        // Original060020b3 and natural closure060020ba/bb. Both backends send NaN delay through the immediate branch.
        public void PlayEffect(in IEffectData data, Action onComplete)
        {
            IEffectData effectData = null;
            m_onComplete = onComplete;
            float initialDelay = m_delay;
            IEffectData inputData = data;
            if (initialDelay > 0f)
            {
                effectData = inputData;
                CancelCoroutine();
                float delay = m_delay;
                TimeCategory timeCategory = m_timeCategory;
                m_delayCoroutine = TimeScaledUtilities_SDT.DelayFixedSeconds(delay, timeCategory, () => TryTrigger(effectData));
            }
            else TryTrigger(inputData);
        }
        // Original060020b4: rejection invokes the saved callback without clearing it.
        private void TryTrigger(IEffectData data)
        {
            if (CanTrigger()) OnEffectTriggered(data);
            else m_onComplete?.Invoke();
        }
        // Original060020b5: capture the array, read the real control mapping input type on each iteration.
        private bool CanTrigger()
        {
            InputType[] inputTypes = m_triggerOnlyForInputTypes;
            if (inputTypes == null || inputTypes.Length == 0) return true;
            foreach (InputType inputType in inputTypes)
                if (inputType == ControlMapping.LastInputType) return !m_inputTypesExclusive;
            return m_inputTypesExclusive;
        }
        // Original060020b6; real original extension and private Coroutine field, no replacement host.
        public void CancelCoroutine() { this.SafeStopCoroutine(ref m_delayCoroutine); }
        // Original060020b7: genuine native RET hook, not an invented fallback.
        protected virtual void OnEffectTriggered(IEffectData data) { }
        // Original060020b8: genuine native RET hook.
        public virtual void OnReset() { }
        // Implicit protected060020b9: Effects0x88b13d4f initializer precedes MonoBehaviour constructor.
    }
}
