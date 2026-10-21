using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000885; bodies translated from the 1.10.1 x86-64 binary.
    // The results panel shown after a challenge: formatted time and score against the previous best, the XP awarded and the rank earned.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerChallengeResults : UIContainer
    {
        public readonly Bindable<bool> IsScored = new Bindable<bool>();
        public readonly Bindable<bool> IsTimed = new Bindable<bool>();
        public readonly Bindable<bool> IsXPAwarded = new Bindable<bool>();
        public readonly Bindable<string> XPAwardedString = new Bindable<string>();
        public readonly Bindable<string> XPAwardedLongString = new Bindable<string>();
        public readonly Bindable<string> PreviousBestScoreString = new Bindable<string>();
        public readonly Bindable<string> ScoreString = new Bindable<string>();
        public readonly Bindable<string> PreviousBestTimeString = new Bindable<string>();
        public readonly Bindable<string> TimeString = new Bindable<string>();
        public readonly Bindable<bool> IsNewRecord = new Bindable<bool>();
        public readonly Bindable<string> ChallengeTitle = new Bindable<string>();
        public readonly Bindable<bool> ShowRanks = new Bindable<bool>();
        public readonly Bindable<Sprite> RankBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankBadge = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankLetter = new Bindable<Sprite>();
        [SerializeField]
        [Tooltip("If ticked, if the current time/score are a new record, then the 'previous' best time/score will also show the current time/score.")]
        private bool m_showBestInsteadOfPrevious = true;
        [SerializeField]
        [Tooltip("Formatted string for XP display, where {0} is the amount of XP the challenge was worth.")]
        private Strings m_xpStringFormat = Strings.MENU_TAILS_CHALLENGES_XP;
        [SerializeField]
        [Tooltip("Formatted string for longer XP display, where {0} is the amount of XP the challenge was worth.")]
        private Strings m_xpStringFormatLong = Strings.CHALLENGES_RESULT_XP_DESCRIPTION;
        [SerializeField]
        [Tooltip("Formatted string for the challenge time. {0} = minutes, {1} = seconds, {2} = centiseconds.")]
        private Strings m_missionTimerFormat = Strings.CHALLENGE_BEST_TIME_FORMAT;
        [Tooltip("Formatted string for title. {0} is the challenge number.")]
        [SerializeField]
        private Strings m_challengeTitleFormat = Strings.CHALLENGES_RESULT_TITLE_CHALLENGE;
        private readonly List<ManagedAddressableAsset<Sprite>> m_managedAddressableSprites = new List<ManagedAddressableAsset<Sprite>>();

        // 0x06003138 (0x61CF10): a time or score is a new record when there is no previous best (a previous best time of
        // zero or less) or when it beats it. The previous best score is scored from the previous best time alone, without
        // the bonus score the current run carries.
        public override void Setup(IUIContainerParameters parameters)
        {
            UIContainerChallengeResultsParameters resultsParameters = parameters.GetAs<UIContainerChallengeResultsParameters>();
            ChallengeDefinition challengeDefinition = resultsParameters.ChallengeDefinition;
            float previousBestTimeSeconds = resultsParameters.PreviousBestTimeSeconds;
            IsScored.Value = resultsParameters.IsScored;
            IsTimed.Value = resultsParameters.IsTimed;
            string xpFormat = StringTable.GetString(m_xpStringFormat);
            IsXPAwarded.Value = !(previousBestTimeSeconds > 0f);
            string xp = challengeDefinition.XPReward.ToString();
            XPAwardedString.Value = IsXPAwarded.Value ? string.Format(xpFormat, xp) : string.Empty;
            string xpFormatLong = StringTable.GetString(m_xpStringFormatLong);
            XPAwardedLongString.Value = IsXPAwarded.Value ? string.Format(xpFormatLong, xp) : string.Empty;
            ChallengeTitle.Value = string.Format(StringTable.GetString(m_challengeTitleFormat), resultsParameters.ChallengeIndex.ToString());

            bool isNewTimeRecord = true;
            if (previousBestTimeSeconds > 0f)
                isNewTimeRecord = previousBestTimeSeconds > resultsParameters.TimeSeconds;
            TimeString.Value = MissionStringsUtil.GetMissionTimerFormat(resultsParameters.TimeSeconds, m_missionTimerFormat);
            if (isNewTimeRecord && m_showBestInsteadOfPrevious)
                PreviousBestTimeString.Value = TimeString.Value;
            else
                PreviousBestTimeString.Value = MissionStringsUtil.GetMissionTimerFormat(previousBestTimeSeconds, m_missionTimerFormat);

            int previousBestScore;
            int score;
            bool isNewScoreRecord;
            if (previousBestTimeSeconds > 0f)
            {
                previousBestScore = challengeDefinition.CalculateScore(previousBestTimeSeconds);
                score = challengeDefinition.CalculateScore(resultsParameters.TimeSeconds) + resultsParameters.Score;
                isNewScoreRecord = score > previousBestScore;
            }
            else
            {
                previousBestScore = 0;
                score = challengeDefinition.CalculateScore(resultsParameters.TimeSeconds) + resultsParameters.Score;
                isNewScoreRecord = true;
            }
            ScoreString.Value = score.ToString();
            if (isNewScoreRecord && m_showBestInsteadOfPrevious)
                PreviousBestScoreString.Value = ScoreString.Value;
            else
                PreviousBestScoreString.Value = previousBestScore.ToString();
            IsNewRecord.Value = isNewTimeRecord | isNewScoreRecord;

            ChallengeRankDefinition rankDefinition = challengeDefinition.RankDefinition;
            if (rankDefinition == null)
            {
                ShowRanks.Value = false;
                return;
            }
            ShowRanks.Value = rankDefinition.TryGetRankForScore(score, out ChallengeRank rank);
            if (ShowRanks.Value)
            {
                SetIcon(RankBase, rank.GetBaseAddressable());
                SetIcon(RankBadge, rank.GetBackgroundAddressable());
                SetIcon(RankLetter, rank.GetForegroundAddressable());
            }
        }

        // 0x06003139 (0x61D420): the loaded asset is tracked so OnDisable can release it.
        private void SetIcon(Bindable<Sprite> bindable, ManagedAddressableAsset<Sprite> addressableAsset)
        {
            bindable.Value = addressableAsset.Load();
            m_managedAddressableSprites.Add(addressableAsset);
        }

        // 0x0600313a (0x61D500)
        private void OnDisable()
        {
            foreach (ManagedAddressableAsset<Sprite> managedAddressableSprite in m_managedAddressableSprites)
                managedAddressableSprite.Unload();
            m_managedAddressableSprites.Clear();
        }

        // 0x0600313b
        public UIContainerChallengeResults() { }

        // Original Game.Runtime 0x02000886
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class UIContainerChallengeResultsParameters : IUIContainerParameters
        {
            public ChallengeDefinition ChallengeDefinition;
            public float PreviousBestTimeSeconds;
            public bool IsTimed;
            public float TimeSeconds;
            public bool IsScored;
            public int Score;
            public int ChallengeIndex;
        }
    }
}
