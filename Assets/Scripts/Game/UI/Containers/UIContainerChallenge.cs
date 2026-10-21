using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200087e; bodies translated from the 1.10.1 x86-64 binary.
    // Details of one daily challenge: score and time bests, XP reward, the characters or
    // archetype it is restricted to, and the S rank bonus.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerChallenge : UIContainer
    {
        [SerializeField] private UIWidgetCharacterHeadsSpawner m_characterHeadsSpawner;
        [SerializeField] private UIWidgetArchetypeImageSpawner m_archetypeSpawner;
        [HashEnum(null)] [SerializeField] private Strings m_bestTimeLabel;
        [SerializeField] [HashEnum(null)] private Strings m_bestScoreLabel;
        [SerializeField] private bool m_useXpPostfix;
        [Tooltip("Format for XP display. Parameters: {0} XP reward amount.")]
        [SerializeField] [HashEnum(null)] [ShowIf("m_useXpPostfix", (string)null)]
        private Strings m_xpFormat = Strings.MENU_TAILS_CHALLENGES_XP;
        [SerializeField] private bool m_useXpPostfixForRank;
        [SerializeField] [ShowIf("m_useXpPostfixForRank", (string)null)]
        [Tooltip("Format for rank XP display. Parameters: {0} XP reward amount.")]
        [HashEnum(null)] private Strings m_xpFormatForRank = Strings.MENU_TAILS_CHALLENGES_XP;
        public readonly Bindable<string> Description = new Bindable<string>();
        public readonly Bindable<bool> Completed = new Bindable<bool>();
        public readonly Bindable<int> XPReward = new Bindable<int>();
        public readonly Bindable<bool> IsTimed = new Bindable<bool>();
        public readonly Bindable<bool> IsScored = new Bindable<bool>();
        public readonly Bindable<int> HighScore = new Bindable<int>();
        public readonly Bindable<string> BestTimeString = new Bindable<string>();
        public readonly Bindable<Sprite> MissionIcon = new Bindable<Sprite>();
        public readonly Bindable<string> BestTimeLabel = new Bindable<string>();
        public readonly Bindable<string> BestScoreLabel = new Bindable<string>();
        public readonly Bindable<string> FormattedXP = new Bindable<string>();
        public readonly Bindable<bool> ShowRank = new Bindable<bool>();
        public readonly Bindable<string> FormattedRankXP = new Bindable<string>();
        public readonly Bindable<Sprite> RankBase = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RankLetter = new Bindable<Sprite>();
        public readonly Bindable<bool> SRankAchieved = new Bindable<bool>();
        private ChallengeState m_challengeState;
        private readonly List<ManagedAddressableAsset<Sprite>> m_managedAddressableSprites = new List<ManagedAddressableAsset<Sprite>>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);

        // 0x0600311e (0x61B1E0): the labels are localised, then the container follows the
        // given challenge's score updates, unfollowing the previous one.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            BestScoreLabel.Value = StringTable.GetString(m_bestScoreLabel);
            BestTimeLabel.Value = StringTable.GetString(m_bestTimeLabel);
            UIContainerChallengeParameters challengeParameters = (UIContainerChallengeParameters)parameters;
            if (m_challengeState != null)
                m_challengeState.OnUpdateScore -= OnChallengeStateUpdate;
            m_challengeState = challengeParameters.ChallengeState;
            if (m_challengeState != null)
            {
                OnChallengeStateUpdate(m_challengeState);
                m_challengeState.OnUpdateScore += OnChallengeStateUpdate;
            }
        }

        // 0x0600311f (0x61B460): updates for any other challenge than the shown one are ignored.
        // The rank is judged by the best score; a never-played challenge hides the rank.
        public void OnChallengeStateUpdate(ChallengeState challengeState)
        {
            if (m_challengeState != challengeState)
                return;
            ChallengeDefinition challengeDefinition = challengeState.ChallengeDefinition;
            IsTimed.Value = challengeDefinition.MissionDefinition.IsTimed;
            IsScored.Value = challengeDefinition.MissionDefinition.IsScored;
            Completed.Value = m_challengeState.BestTimeSeconds > 0f;
            XPReward.Value = challengeDefinition.XPReward;
            HighScore.Value = m_challengeState.BestScore;
            BestTimeString.Value = MissionStringsUtil.GetMissionTimerFormat(m_challengeState.BestTimeSeconds);
            MissionIcon.Value = challengeDefinition.MissionDefinition.MissionObjectiveIcon;
            Description.Value = challengeDefinition.GetDescription();
            FormattedXP.Value = GetFormattedXpString(challengeDefinition.XPReward);
            if (m_characterHeadsSpawner != null)
            {
                if (TryGetArchetype(m_challengeState.ChallengeSeededData.CharacterList, out CharacterArchetype archetype))
                {
                    m_characterHeadsSpawner.ClearAllCharacterHeads();
                    m_archetypeSpawner.Spawn(archetype);
                }
                else
                {
                    m_archetypeSpawner.Spawn(CharacterArchetype.None);
                    m_characterHeadsSpawner.UpdateCharacterHeads(m_challengeState.ChallengeSeededData.CharacterList);
                }
            }
            ShowRank.Value = false;
            if (!m_challengeState.Played)
                return;
            ChallengeRankDefinition rankDefinition = challengeDefinition.RankDefinition;
            if (rankDefinition == null)
                return;
            ShowRank.Value = rankDefinition.TryGetRankForScore(m_challengeState.BestScore, out ChallengeRank rank);
            if (!ShowRank.Value)
                return;
            SetIcon(RankBase, rank.GetBaseAddressable());
            SetIcon(RankBackground, rank.GetBackgroundAddressable());
            SetIcon(RankLetter, rank.GetForegroundAddressable());
            FormattedRankXP.Value = GetFormattedXpString(rankDefinition.SRankBonus, true);
            SRankAchieved.Value = rank.RankType == RankType.S;
        }

        // 0x06003120 (0x61B8D0): the XP number, wrapped in the configured format when enabled.
        private string GetFormattedXpString(int xpValue, bool forRank = false)
        {
            string text = xpValue.ToString();
            if (forRank ? m_useXpPostfixForRank : m_useXpPostfix)
                text = string.Format(StringTable.GetString(forRank ? m_xpFormatForRank : m_xpFormat), text);
            return text;
        }

        // 0x06003121 (0x61B960): true only when every character of a non-empty list shares one
        // archetype; the first character's archetype is reported even on a mismatch.
        private bool TryGetArchetype(IReadOnlyList<CharacterId> characterIds, out CharacterArchetype archetype)
        {
            archetype = CharacterArchetype.None;
            if (characterIds.Count == 0)
                return false;
            DataManager dataManager = m_dataManagerRef.Get();
            archetype = dataManager.Characters[characterIds[0]].Archetype;
            for (int i = 1; i < characterIds.Count; i++)
            {
                if (dataManager.Characters[characterIds[i]].Archetype != archetype)
                    return false;
            }
            return true;
        }

        // 0x06003122 (0x61BC00): the loaded asset is tracked so OnDestroy can release it.
        private void SetIcon(Bindable<Sprite> bindable, ManagedAddressableAsset<Sprite> addressableAsset)
        {
            bindable.Value = addressableAsset.Load();
            m_managedAddressableSprites.Add(addressableAsset);
        }

        // 0x06003123 (0x61BCE0)
        private void OnDestroy()
        {
            if (m_challengeState != null)
                m_challengeState.OnUpdateScore -= OnChallengeStateUpdate;
            foreach (ManagedAddressableAsset<Sprite> managedAddressableSprite in m_managedAddressableSprites)
                managedAddressableSprite.Unload();
            m_managedAddressableSprites.Clear();
        }

        // 0x06003124
        public UIContainerChallenge() { }

        // Original nested 0x0200087f.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public sealed class UIContainerChallengeParameters : IUIContainerParameters
        {
            // 0x06003125
            public ChallengeState ChallengeState { get; }

            // 0x06003126
            public UIContainerChallengeParameters(ChallengeState challengeState)
            {
                ChallengeState = challengeState;
            }
        }
    }
}
