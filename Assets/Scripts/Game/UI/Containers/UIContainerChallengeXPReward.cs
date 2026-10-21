using System.Collections;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200088a; bodies translated from the 1.10.1 x86-64 binary.
    // Shows a reward track's experience bar and the next reward on it. The member order
    // below is the original's: the compiler-generated iterator names (d__33, d__36)
    // come from each method's position in the type.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerChallengeXPReward : UIContainer
    {
        [SerializeField] private RewardTrackType m_rewardTrackType;
        [SerializeField] private UIWidgetChallengesSliderMini m_xpSlider;
        [HashEnum(null)] [SerializeField] private Strings m_xpFormatString;
        [SerializeField] private Animation m_animation;
        [SerializeField] private AnimationClip m_unlockAnimation;
        [SerializeField] private float m_xpAnimateTickSeconds = 0.1f;
        [SerializeField] private PromptTrigger m_ctaPromptTrigger;
        public readonly Bindable<Texture> RewardTexture = new Bindable<Texture>();
        public readonly Bindable<Sprite> RewardSprite = new Bindable<Sprite>();
        public readonly Bindable<bool> HasReward = new Bindable<bool>();
        public readonly Bindable<bool> IsUnlocked = new Bindable<bool>();
        public readonly Bindable<float> SpentXP = new Bindable<float>();
        public readonly Bindable<string> SpentXPString = new Bindable<string>();
        public readonly Bindable<float> AvailableXP = new Bindable<float>();
        public readonly Bindable<string> AvailableXPString = new Bindable<string>();
        public readonly Bindable<float> TotalXP = new Bindable<float>();
        public readonly Bindable<string> TotalXPString = new Bindable<string>();
        public readonly Bindable<string> FormattedString = new Bindable<string>();
        public readonly Bindable<bool> RewardIsCollectable = new Bindable<bool>();
        public readonly Bindable<Strings> RewardCollectableType = new Bindable<Strings>();
        public readonly Bindable<Color> TrackGlowColour = new Bindable<Color>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);
        private ChallengeReward m_challengeReward;
        private bool m_unlockAnimationMidpointReached;

        // 0x0600314f (0x61F750)
        private void Start()
        {
            RefreshXP();
        }

        // 0x06003150 (0x61FA80): fills the slider and counts the label up to the spent XP.
        private void SetupSlider(int spentXP, int totalXP)
        {
            if (m_xpSlider == null)
                return;
            m_xpSlider.Setup(new UIWidgetChallengesSliderMiniParameters(spentXP, totalXP));
            m_xpSlider.AnimateNewReward();
            StartCoroutine(AnimateLabel(spentXP));
        }

        // 0x06003151 (0x61FC40): the previous reward's image is released before the new one loads.
        private void SetupNextReward(ChallengeReward challengeReward)
        {
            UnloadCachedChallengeReward();
            m_challengeReward = challengeReward;
            HasReward.Value = true;
            RewardCollectableType.Value = challengeReward.GetRewardName();
            challengeReward.LoadImage(SetTexture, SetSprite);
        }

        // 0x06003152 (0x61FDF0)
        private void OnDestroy()
        {
            UnloadCachedChallengeReward();
        }

        // 0x06003153 (0x61FDA0)
        private void UnloadCachedChallengeReward()
        {
            if (m_challengeReward == null)
                return;
            m_challengeReward.UnloadImage();
        }

        // 0x06003154 (0x61FE40); the native code inlines it at its call sites.
        private void SetupNoReward()
        {
            HasReward.Value = false;
            SetupSlider(0, 100);
        }

        // 0x06003155 (0x61FE80)
        private void SetTexture(Texture texture)
        {
            RewardTexture.Value = texture;
        }

        // 0x06003156 (0x61FEA0)
        private void SetSprite(Sprite sprite)
        {
            RewardSprite.Value = sprite;
        }

        // 0x06003157 (0x61FBC0, iterator MoveNext 0x6207D0): waits for any running animation,
        // then plays it while the spent XP counts up one point per tick until it reaches the target.
        private IEnumerator AnimateLabel(int target)
        {
            if (m_animation == null)
                yield break;
            while (m_animation.isPlaying)
                yield return null;
            UpdateBindableNumberString(SpentXP, SpentXPString, 0f);
            m_animation.Play();
            while (target > SpentXP.Value)
            {
                yield return new WaitForSeconds(m_xpAnimateTickSeconds);
                UpdateBindableNumberString(SpentXP, SpentXPString, SpentXP.Value + 1f);
            }
            m_animation.Stop();
        }

        // 0x06003158 (0x61FEE0)
        private void UpdateBindableNumberString(Bindable<float> floatBindable, Bindable<string> stringBindable, float number)
        {
            floatBindable.Value = number;
            stringBindable.Value = number.ToString("F0");
        }

        // 0x06003159 (0x61F760): the track's unlock state decides what is shown. The unknown-state
        // message formats the track state object itself, not its unlock state, as in the original.
        public void RefreshXP()
        {
            TrackGlowColour.Value = m_dataManagerRef.Get().RewardTrackDefinitions[m_rewardTrackType].RewardGlowColour;
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            ChallengeRewardTrackState trackState = challengeManager.RewardTrackStates[m_rewardTrackType];
            IsUnlocked.Value = trackState.UnlockState >= SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
            bool rewardIsCollectable = false;
            switch (trackState.UnlockState)
            {
                case SaveDataChallengeRewardTrack.FeatureUnlockState.Seen:
                    rewardIsCollectable = ShowProgressTowardsNextReward(trackState, challengeManager);
                    if (m_ctaPromptTrigger != null)
                        m_ctaPromptTrigger.TryShow();
                    break;
                case SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked:
                    StartCoroutine(ShowUnlockAnimation());
                    break;
                case SaveDataChallengeRewardTrack.FeatureUnlockState.Locked:
                    SetupNoReward();
                    break;
                default:
                    HLOutput.LogError(string.Format("Reward track {0} is in unknown state: {1}", m_rewardTrackType.GetString(), trackState));
                    SetupNoReward();
                    break;
            }
            RewardIsCollectable.Value = rewardIsCollectable;
        }

        // 0x0600315a (0x61FF70, iterator MoveNext 0x620B20): plays the unlock animation until it ends or
        // UnlockMidAnimation is signalled, then marks the track as seen.
        private IEnumerator ShowUnlockAnimation()
        {
            // The native body tests m_animation twice; the second test guards both the Play and the wait loop.
            if (m_animation != null)
            {
                m_animation.Play(m_unlockAnimation.name);
                while (m_animation.isPlaying && !m_unlockAnimationMidpointReached)
                    yield return null;
            }
            m_challengeManagerRef.Get().SetRewardTrackState(m_rewardTrackType, SaveDataChallengeRewardTrack.FeatureUnlockState.Seen);
            RefreshXP();
        }

        // 0x0600315b (0x61FFE0): shows the XP between the previous and the next reward; returns
        // whether that next reward can already be collected.
        private bool ShowProgressTowardsNextReward(ChallengeRewardTrackState trackState, ChallengeManager challengeManager)
        {
            UpdateBindableNumberString(SpentXP, SpentXPString, trackState.SpentXP);
            UpdateBindableNumberString(AvailableXP, AvailableXPString, challengeManager.GetCurrentXP());
            ChallengeReward nextReward;
            if (trackState.TryGetNextCollectableReward(out ChallengeReward nextCollectable))
            {
                RewardIsCollectable.Value = true;
                nextReward = nextCollectable;
            }
            else
            {
                nextReward = null;
                if (trackState.NextReward != null)
                    nextReward = trackState.NextReward;
            }
            FormattedString.Value = StringTable.GetString(m_xpFormatString);
            if (nextReward != null)
            {
                int nextThreshold = nextReward.XPThreshold;
                int previousThreshold = 0;
                if (trackState.TryGetPreviousReward(nextReward, out ChallengeReward previousReward))
                    previousThreshold = previousReward.XPThreshold;
                int rewardXP = nextThreshold - previousThreshold;
                UpdateBindableNumberString(TotalXP, TotalXPString, rewardXP);
                int progress = trackState.SpentXP - previousThreshold + challengeManager.GetCurrentXP();
                SetupSlider(progress < rewardXP ? progress : rewardXP, rewardXP);
                SetupNextReward(nextReward);
                return rewardXP <= progress;
            }
            SetupNoReward();
            return false;
        }

        // 0x0600315c (0x620340): signalled from the unlock animation's midpoint.
        public void UnlockMidAnimation()
        {
            m_unlockAnimationMidpointReached = true;
        }

        // 0x0600315d
        public UIContainerChallengeXPReward() { }
    }
}
