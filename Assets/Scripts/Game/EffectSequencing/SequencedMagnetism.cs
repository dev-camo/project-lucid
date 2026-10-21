using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200061d (+ iterator 0200061e); native 0x569340.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedMagnetism : SequencedEffect
    {
        [SerializeField] private float m_speed = 10f;
        [SerializeField] private float m_minDistance = 1f;
        [SerializeField] private float m_timeoutSeconds = 2f;
        private TimeManager m_timeManager;

        // Original060020c5: the time manager is re-fetched on every trigger; an inactive host completes at once.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
            TargetedEffect targetedEffect = (TargetedEffect)data;
            if (gameObject.activeInHierarchy) StartCoroutine(MoveToTransform(targetedEffect));
            else m_onComplete();
        }

        // Original060020c6 and iterator 0200061e (MoveNext 0x569590): chase the target's up-offset point with a
        // speed that grows by one per fixed step until close enough, timed out, or the target is destroyed.
        private IEnumerator MoveToTransform(TargetedEffect data)
        {
            bool isComplete = false;
            float timeoutSeconds = m_timeoutSeconds;
            float speed = m_speed;
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            while (!isComplete)
            {
                float fixedDeltaTime = m_timeManager.GetFixedDeltaTime(m_timeCategory);
                Vector3 position = transform.position;
                Vector3 target = data.Target.position + data.Target.up * data.Offset.y;
                position = Vector3.MoveTowards(position, target, speed * fixedDeltaTime);
                transform.position = position;
                timeoutSeconds -= fixedDeltaTime;
                speed += 1f;
                isComplete = (target - position).sqrMagnitude < m_minDistance || timeoutSeconds <= 0f;
                yield return waitForFixedUpdate;
                isComplete |= data.Target == null;
            }
            m_onComplete();
        }

        // Implicit original060020c7: speed 10, minimum distance 1 and timeout 2 seconds precede the base constructor.
    }
}
