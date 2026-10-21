using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000857; bodies translated from the 1.10.1 x86-64 binary.
    // Counts the experience counter up from the last value the player saw to the current one, firing the highest
    // threshold effect the gained amount reaches.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class XPCounterBufferedIncrease : MonoBehaviour
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class EffectThresholds
        {
            public int Threshold;
            public UnityEvent OnThresholdHit;
            public float CounterAnimationSeconds;
        }

        [SerializeField] private UIAnimatingCounter m_uiAnimatingCounter;
        [SerializeField] private List<EffectThresholds> m_effectThresholds;
        [SerializeField] private bool m_isOnChallengesOverview;
        private static readonly SystemRef<ChallengeManager> s_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);
        private static readonly SystemRef<App> s_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private Coroutine m_waitForBonusAcknowledgementCoroutine;

        // 0x0600305b (0x60EE20): on the challenges overview, bonus experience that has not been acknowledged yet keeps the
        // counter at the last seen value until it is; otherwise the counter animates straight away.
        private void OnEnable()
        {
            ChallengeManager challengeManager = s_challengeManagerRef.Get();
            if (m_isOnChallengesOverview && challengeManager.BonusXPUnacknowledged > 0)
            {
                int currentXP = challengeManager.GetCurrentXP();
                int lastSeenXP = s_appRef.Get().Storage.GetValue(AppFSMKeys.LastSeenXPValue, currentXP, false);
                m_uiAnimatingCounter.SetUp(lastSeenXP, lastSeenXP);
                CoroutineUtils.StopUtilCoroutine(ref m_waitForBonusAcknowledgementCoroutine);
                m_waitForBonusAcknowledgementCoroutine = CoroutineUtils.RunCoroutine(WaitForBonusAcknowledgement());
            }
            else
            {
                Trigger();
            }
        }

        // 0x0600305c (0x60F590)
        private void OnDisable()
        {
            CoroutineUtils.StopUtilCoroutine(ref m_waitForBonusAcknowledgementCoroutine);
        }

        // 0x0600305d (0x60F110): the experience gained since it was last seen is stored as seen. The last threshold (in list
        // order) that the gain reaches fires its event and sets the count-up time. Without a gain the counter just shows the
        // current value.
        private void Trigger()
        {
            int currentXP = s_challengeManagerRef.Get().GetCurrentXP();
            IGraphStorage storage = s_appRef.Get().Storage;
            int lastSeenXP = storage.GetValue(AppFSMKeys.LastSeenXPValue, currentXP, false);
            int gainedXP = currentXP - lastSeenXP;
            storage.SetValue(AppFSMKeys.LastSeenXPValue, currentXP);
            EffectThresholds effect = null;
            if (gainedXP > 0 && m_effectThresholds != null)
            {
                foreach (EffectThresholds candidate in m_effectThresholds)
                {
                    if (gainedXP < candidate.Threshold)
                        break;
                    effect = candidate;
                }
                if (effect != null && effect.OnThresholdHit != null)
                    effect.OnThresholdHit.Invoke();
            }
            if (m_uiAnimatingCounter != null)
            {
                if (gainedXP > 0)
                {
                    m_uiAnimatingCounter.SetUp(lastSeenXP, currentXP, null, effect != null ? effect.CounterAnimationSeconds : 0f);
                    m_uiAnimatingCounter.StartAnimating();
                }
                else
                {
                    m_uiAnimatingCounter.SetUp(currentXP, currentXP);
                }
            }
        }

        // 0x0600305e (0x60F0A0), MoveNext 0x60F6C0: waits one frame at a time until the bonus experience is acknowledged.
        private IEnumerator WaitForBonusAcknowledgement()
        {
            ChallengeManager challengeManager = s_challengeManagerRef.Get();
            while (challengeManager.BonusXPUnacknowledged > 0)
                yield return null;
            Trigger();
        }

        // 0x0600305f (0x60F5C0)
        public XPCounterBufferedIncrease() { }
    }
}
