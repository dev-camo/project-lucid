using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200061f (+ iterator 02000620); native 0x5699F0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class SequencedMaterial : SequencedEffect
    {
        [SerializeField] protected Material m_material;
        [SerializeField] private float m_blendTime = 1f;
        [SerializeField] private float m_blendValueStart;
        [SerializeField] private float m_blendValueEnd = 1f;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        protected float m_blendValueReset;

        // Original060020ce.
        protected abstract void SetBlendValue(float value);

        // Original060020cf: exactly zero (not merely non-positive) blend time applies the end value and completes now.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (m_blendTime == 0f)
            {
                SetBlendValue(m_blendValueEnd);
                m_onComplete();
            }
            else StartCoroutine(WaitForAnimation());
        }

        // Original060020d0 and iterator 02000620 (MoveNext 0x569C00): one fixed step per frame, then the end value.
        private IEnumerator WaitForAnimation()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            float time = 0f;
            while (m_blendTime > time)
            {
                SetBlendValue(Mathf.Lerp(m_blendValueStart, m_blendValueEnd, time / m_blendTime));
                yield return waitForFixedUpdate;
                time += timeManager.GetFixedDeltaTime(m_timeCategory);
            }
            SetBlendValue(m_blendValueEnd);
            m_onComplete();
        }

        // Original060020d1.
        public void ResetProperties() { SetBlendValue(m_blendValueReset); }

        // Implicit original060020d2: blend time and end value of one, then the real SystemRef initialiser, precede the base constructor.
    }
}
