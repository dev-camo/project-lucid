using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200099c; bodies translated from the 1.10.1 x86-64 binary.
    // One reward on a challenge reward track: its action label, thumbnail and zone, and the
    // collect, ready and hidden animations that follow the reward's status.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIWidgetButtonChallengeReward : UIInteractable
    {
        [HashEnum(null)] [SerializeField] private Strings m_nextRewardString;
        [HashEnum(null)] [SerializeField] private Strings m_readyToCollectString;
        [SerializeField] private UIWidgetNavigation m_widgetNavigation;
        [SerializeField] private UITransitionAnimation m_transition;
        [SerializeField] private AnimationClip m_collectedAnimationClip;
        [FormerlySerializedAs("m_collectedMusicAnimationClip")]
        [SerializeField] private AnimationClip m_collectedAnimationClipWithMask;
        [SerializeField] private AnimationClip m_collectedAnimationClipPlayable;
        [SerializeField] private AnimationClip m_readyToCollectAnimationClip;
        [SerializeField] private AnimationClip m_hiddenAnimationClip;
        [Tooltip("Offsets the computed position so the separator icon matches the threshold of the slider.")]
        [SerializeField] private float m_offsetBy = 10f;
        public readonly Bindable<Strings> Action = new Bindable<Strings>();
        public readonly Bindable<Strings> Name = new Bindable<Strings>();
        public readonly Bindable<string> RewardXP = new Bindable<string>();
        public readonly Bindable<Texture> Thumbnail = new Bindable<Texture>();
        public readonly Bindable<Sprite> ThumbnailSprite = new Bindable<Sprite>();
        public readonly Bindable<bool> HasAction = new Bindable<bool>();
        public readonly Bindable<bool> ButtonInteractable = new Bindable<bool>();
        public readonly Bindable<Color> GlowColour = new Bindable<Color>();
        public readonly Bindable<Strings> ZoneName = new Bindable<Strings>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private UIWidgetButtonChallengesRewardParameters m_parameters;

        // 0x0600379d..0x0600379e
        public UIWidgetNavigation WidgetNavigation => m_widgetNavigation;
        public UIChallengeTrackRewardStatus RewardStatus => m_parameters.RewardStatus;

        // 0x0600379f (0x699DD0): after refreshing, the button is placed along the track. Half of its
        // width is taken off the track position, and the last button gets no extra offset.
        public void Setup(IUIWidgetParameters parameters)
        {
            m_parameters = parameters.GetAs<UIWidgetButtonChallengesRewardParameters>();
            Refresh(m_parameters.RewardStatus);
            RectTransform rectTransform = (RectTransform)transform;
            float offset = m_parameters.IsLast ? 0f : m_offsetBy;
            float horizontalPosition = m_parameters.HorizontalPosition;
            float x = rectTransform.sizeDelta.x * -0.5f + horizontalPosition + offset;
            Vector2 anchoredPosition = rectTransform.anchoredPosition;
            anchoredPosition.x = x;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        // 0x060037a0 (0x69A290)
        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_parameters.RewardStatus.Reward.UnloadImage();
        }

        // 0x060037a1 (0x69A2D0)
        public void Action_OnButtonClick()
        {
            m_parameters.OnClick();
        }

        // 0x060037a2 (0x69A2F0)
        public void SetThumbnail(Texture thumbnail)
        {
            Thumbnail.Value = thumbnail;
        }

        // 0x060037a3 (0x69A310)
        public void SetThumbnailSprite(Sprite thumbnail)
        {
            ThumbnailSprite.Value = thumbnail;
        }

        // 0x060037a4 (0x69A330): music tracks and playable levels have their own collect animations.
        public void Collect(UIChallengeTrackRewardStatus trackRewardStatus)
        {
            AnimationClip animationClip = m_collectedAnimationClip;
            if (trackRewardStatus.Reward is ChallengeRewardMusicTrack)
                animationClip = m_collectedAnimationClipWithMask;
            else if (trackRewardStatus.Reward is ChallengeRewardGameplayLevel)
                animationClip = m_collectedAnimationClipPlayable;
            m_transition.QueueCustomAnimation(animationClip);
        }

        // 0x060037a5 (0x69A3F0): the next reward is labelled as such, a collectable one invites collecting.
        private Strings GetAction()
        {
            if (m_parameters.IsNextReward)
                return m_nextRewardString;
            return m_parameters.RewardStatus.IsReadyToCollect() ? m_readyToCollectString : Strings.NONE;
        }

        // 0x060037a6 (0x69A430)
        public bool IsReadyToCollect()
        {
            return m_parameters.RewardStatus.IsReadyToCollect();
        }

        // 0x060037a7 (0x69A460)
        public void Action_OnHighlighted()
        {
            m_parameters.OnHighlighted((RectTransform)transform);
        }

        // 0x060037a8 (0x69A4D0)
        public void Highlight()
        {
            m_parameters.OnHighlighted((RectTransform)transform);
        }

        // 0x060037a9 (0x699EE0): shows the reward, then queues the animation for its status: collect,
        // ready or hidden. A playable level also shows the zone it opens.
        public void Refresh(UIChallengeTrackRewardStatus rewardStatus)
        {
            m_parameters.RewardStatus = rewardStatus;
            Strings action = GetAction();
            bool collected = rewardStatus.Unlocked && rewardStatus.Collected;
            bool readyToCollect = rewardStatus.IsReadyToCollect();
            HasAction.Value = action != Strings.NONE;
            Action.Value = action;
            Name.Value = rewardStatus.Reward.GetRewardName();
            RewardXP.Value = m_parameters.RewardXP;
            Interactable = readyToCollect;
            ButtonInteractable.Value = readyToCollect;
            GlowColour.Value = m_dataManagerRef.Get().RewardTrackDefinitions[m_parameters.TrackType].RewardGlowColour;
            rewardStatus.Reward.LoadImage(SetThumbnail, SetThumbnailSprite);
            if (rewardStatus.Reward is ChallengeRewardGameplayLevel gameplayLevelReward)
                ZoneName.Value = gameplayLevelReward.GetZoneName();
            if (collected)
                Collect(m_parameters.RewardStatus);
            else
                m_transition.QueueCustomAnimation(rewardStatus.Unlocked ? m_readyToCollectAnimationClip : m_hiddenAnimationClip);
        }

        // 0x060037aa
        public UIWidgetButtonChallengeReward() { }
    }
}
