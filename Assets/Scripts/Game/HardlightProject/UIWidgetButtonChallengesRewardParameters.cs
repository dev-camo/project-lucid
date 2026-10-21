using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200099d; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetButtonChallengesRewardParameters : IUIWidgetParameters
    {
        // 0x060037ab..0x060037ac
        public UIChallengeTrackRewardStatus RewardStatus { get; set; }
        public readonly string RewardXP;
        public readonly float HorizontalPosition;
        public readonly bool IsNextReward;
        public readonly bool IsLast;
        public readonly Action OnClick;
        public readonly Action<RectTransform> OnHighlighted;
        public readonly RewardTrackType TrackType;

        // 0x060037ad (0x69A8A0)
        public UIWidgetButtonChallengesRewardParameters(UIChallengeTrackRewardStatus rewardStatus, string rewardXP, float horizontalPosition, bool isNextReward, bool isLast, Action onClick, Action<RectTransform> highlighted, RewardTrackType trackType = RewardTrackType.Adventure)
        {
            RewardStatus = rewardStatus;
            RewardXP = rewardXP;
            HorizontalPosition = horizontalPosition;
            IsNextReward = isNextReward;
            IsLast = isLast;
            OnClick = onClick;
            OnHighlighted = highlighted;
            TrackType = trackType;
        }
    }
}
