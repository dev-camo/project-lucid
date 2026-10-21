using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200060f and its iterator <WaitForAnimation>d__10 (02000610);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedAnimation : SequencedEffect
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private Transform m_animationRoot;
        [SerializeField] private bool m_playOnCollector;
        [SerializeField, Min(0.01f)] private float m_lengthMultiplier = 1f;
        private TimeManager m_timeManager;
        private float m_animTime;
        private float m_animLength;

        // 0x06002099 (0x567F60): zero until the clip has a positive length.
        public override float Progress => m_animLength > 0f ? m_animTime / m_animLength : 0f;

        // 0x0600209a (0x567F80): the time manager is looked up on every trigger and the
        // collector root defaults to the animation's own transform.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
            TargetedEffect targetedEffect = (TargetedEffect)data;
            if (m_playOnCollector && m_animationRoot == null)
                m_animationRoot = m_animation.transform;
            StartCoroutine(WaitForAnimation(targetedEffect));
        }

        // 0x0600209b, MoveNext 0x060020a1 (0x568250). Plays the clip at a speed scaled by the length
        // multiplier, samples it by hand each fixed update, then restores the speed.
        private IEnumerator WaitForAnimation(TargetedEffect data)
        {
            m_animation.Play();
            AnimationState animationState = m_animation[m_animation.clip.name];
            m_animLength = animationState.length;
            animationState.speed = animationState.speed / m_lengthMultiplier;
            m_animTime = 0f;
            WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
            while (m_animTime < m_animLength)
            {
                if (m_playOnCollector)
                    m_animationRoot.SetPositionAndRotation(
                        data.Target.position + data.Target.up * data.Offset.y,
                        data.Target.rotation);
                m_animTime += m_timeManager.GetFixedDeltaTime(m_timeCategory) / m_lengthMultiplier;
                animationState.time = m_animTime;
                m_animation.Sample();
                yield return waitForFixedUpdate;
            }
            animationState.speed = animationState.speed * m_lengthMultiplier;
            m_animation.Stop();
            m_onComplete();
        }

        // 0x0600209c (0x5681C0)
        public override void OnReset()
        {
            m_animTime = 0f;
        }

        // 0x0600209d (0x5681E0): the native body is only the tail call of CanValidate.
        private void OnValidate()
        {
            gameObject.CanValidate();
        }
    }
}
