using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200061b and its iterator <MoveToTransform>d__7 (0200061c);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedFollower : SequencedEffect
    {
        [Tooltip("Local offset from the target transform."), SerializeField]
        private Vector3 m_offsetLocal = Vector3.zero;
        [Tooltip("If static, offset is in global space else transform rotation is applied."), SerializeField]
        private bool m_offsetStatic = true;
        [Tooltip("Time it takes to move from the current location to the target."), SerializeField]
        private float m_duration = 1f;
        [Tooltip("Whether to also apply scaling."), SerializeField]
        private bool m_scalingApplied;
        [SerializeField, ShowIf("m_scalingApplied", null), Tooltip("Target scaling to lerp to.")]
        private Vector3 m_scalingTarget = Vector3.one;
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();

        // 0x060020bc (0x568C60): an inactive object completes immediately.
        protected override void OnEffectTriggered(IEffectData data)
        {
            TargetedEffect targetedEffect = (TargetedEffect)data;
            if (gameObject.activeInHierarchy)
                StartCoroutine(MoveToTransform(targetedEffect));
            else
                m_onComplete();
        }

        // 0x060020bd, MoveNext 0x060020c1 (0x568F40). The elapsed fraction feeds both
        // lerps; a non-static offset follows the target's rotation every step.
        private IEnumerator MoveToTransform(TargetedEffect data)
        {
            TimeManager timeManager = m_timeManagerRef.GetSafe();
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            float time = m_duration;
            Vector3 localOffsetStart = transform.position - data.Target.position;
            Vector3 localOffsetEnd = data.Target.rotation * m_offsetLocal;
            Vector3 scalingStart = transform.localScale;
            Vector3 scalingEnd = m_scalingTarget;
            while (time > 0f)
            {
                float deltaTime = timeManager.GetFixedDeltaTime(m_timeCategory);
                time = Mathf.Max(time - deltaTime, 0f);
                float t = 1f - time / m_duration;
                if (!m_offsetStatic)
                    localOffsetEnd = data.Target.rotation * m_offsetLocal;
                Vector3 offset = Vector3.Lerp(localOffsetStart, localOffsetEnd, t);
                transform.position = data.Target.position + offset;
                if (m_scalingApplied)
                    transform.localScale = Vector3.Lerp(scalingStart, scalingEnd, t);
                yield return waitForFixedUpdate;
            }
            m_onComplete();
        }
    }
}
