using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e3; bodies translated from the 1.10.1 x86-64 binary.
    // Lists the rank thresholds of a time-trial mission or a challenge, one text and icon set per rank.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMissionRanks : UIContainer
    {
        public readonly Bindable<bool> Show = new Bindable<bool>();
        public readonly Bindable<bool> ShowTitle = new Bindable<bool>();
        public readonly Bindable<string> SRank = new Bindable<string>();
        public readonly Bindable<Sprite> SRankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> SRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> SRankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<string> ARank = new Bindable<string>();
        public readonly Bindable<Sprite> ARankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> ARankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> ARankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<string> BRank = new Bindable<string>();
        public readonly Bindable<Sprite> BRankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> BRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> BRankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<string> CRank = new Bindable<string>();
        public readonly Bindable<Sprite> CRankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> CRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> CRankIconForeground = new Bindable<Sprite>();
        public readonly Bindable<string> DRank = new Bindable<string>();
        public readonly Bindable<Sprite> DRankIconBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> DRankIconBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> DRankIconForeground = new Bindable<Sprite>();
        private readonly List<ManagedAddressableAsset<Sprite>> m_managedAddressableSprites = new List<ManagedAddressableAsset<Sprite>>();

        // 0600334f (0x63ECB0): releases every icon loaded by SetIcon.
        private void OnDestroy()
        {
            foreach (ManagedAddressableAsset<Sprite> managedAddressableSprite in m_managedAddressableSprites)
                managedAddressableSprite.Unload();
        }

        // 06003350 (0x63EE00): a challenge shows its score thresholds. A time-trial mission shows its rank times,
        // counted down from the total time when it fails on the time limit, and the D rank keeps a time of 0.
        // The title shows only for an instanced sequenced tracker. The remaining missions hide the ranks.
        // The failure flag is read through CompletedMissionOverrides without a null check, as the native body does.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerRanksParameters ranksParameters = parameters.GetAs<UIContainerRanksParameters>();
            MissionDefinition missionDefinition = ranksParameters.MissionDefinition;
            if (ranksParameters.ChallengeDefinition != null)
            {
                ShowChallengeRanks(ranksParameters.ChallengeDefinition);
                return;
            }
            if (missionDefinition == null || missionDefinition.Ranks == null || !missionDefinition.IsTimeTrial())
            {
                Show.Value = false;
                return;
            }
            MissionRankDefinition ranks = missionDefinition.Ranks;
            bool failOnTimeLimitReached = missionDefinition.CompletedMissionOverrides.FailOnTimeLimitReached;
            // Float casts pin the single-precision rounding of the native body.
            float totalTimeSeconds = (float)(missionDefinition.TimeLimitSeconds + missionDefinition.ObjectivesAdditionalTotalTimeSeconds);
            foreach ((RankType rankType, MissionRank rank) in ranks.Ranks)
            {
                float timeSeconds = 0f;
                if (rankType != RankType.D)
                {
                    timeSeconds = rank.TimeInSeconds;
                    if (failOnTimeLimitReached)
                        timeSeconds = (float)(totalTimeSeconds - timeSeconds);
                }
                ShowRank(rank, rankType, MissionStringsUtil.GetMissionTimerFormat(timeSeconds, Strings.MISSION_TIMER_FORMAT_DESCRIPTION));
            }
            if (missionDefinition.IsInstancedSequencedTracker)
                ShowTitle.Value = true;
            Show.Value = true;
        }

        // 06003351 (0x63F290): each challenge rank shows its score.
        private void ShowChallengeRanks(ChallengeDefinition challengeDefinition)
        {
            if (challengeDefinition == null || challengeDefinition.RankDefinition == null)
            {
                Show.Value = false;
                return;
            }
            foreach ((RankType rankType, ChallengeRank rank) in challengeDefinition.RankDefinition.Ranks)
            {
                int score = rank.Score;
                ShowRank(rank, rankType, score.ToString());
            }
            Show.Value = true;
        }

        // 06003352 (0x63F610): the icons of the rank are looked up before the rank type; an unknown type shows nothing.
        private void ShowRank(BaseRank rank, RankType rankType, string formattedValue)
        {
            ManagedAddressableAsset<Sprite> baseIcon = rank.GetBaseAddressable();
            ManagedAddressableAsset<Sprite> backgroundIcon = rank.GetBackgroundAddressable();
            ManagedAddressableAsset<Sprite> foregroundIcon = rank.GetForegroundAddressable();
            switch (rankType)
            {
                case RankType.S:
                    SRank.Value = formattedValue;
                    SetIcon(SRankIconBase, baseIcon);
                    SetIcon(SRankIconBackground, backgroundIcon);
                    SetIcon(SRankIconForeground, foregroundIcon);
                    break;
                case RankType.A:
                    ARank.Value = formattedValue;
                    SetIcon(ARankIconBase, baseIcon);
                    SetIcon(ARankIconBackground, backgroundIcon);
                    SetIcon(ARankIconForeground, foregroundIcon);
                    break;
                case RankType.B:
                    BRank.Value = formattedValue;
                    SetIcon(BRankIconBase, baseIcon);
                    SetIcon(BRankIconBackground, backgroundIcon);
                    SetIcon(BRankIconForeground, foregroundIcon);
                    break;
                case RankType.C:
                    CRank.Value = formattedValue;
                    SetIcon(CRankIconBase, baseIcon);
                    SetIcon(CRankIconBackground, backgroundIcon);
                    SetIcon(CRankIconForeground, foregroundIcon);
                    break;
                case RankType.D:
                    DRank.Value = formattedValue;
                    SetIcon(DRankIconBase, baseIcon);
                    SetIcon(DRankIconBackground, backgroundIcon);
                    SetIcon(DRankIconForeground, foregroundIcon);
                    break;
            }
        }

        // 06003353 (0x63F820): the loaded asset is tracked so OnDestroy can release it.
        private void SetIcon(Bindable<Sprite> bindable, ManagedAddressableAsset<Sprite> addressableAsset)
        {
            bindable.Value = addressableAsset.Load();
            m_managedAddressableSprites.Add(addressableAsset);
        }

        // 06003354
        public UIContainerMissionRanks() { }
    }
}
