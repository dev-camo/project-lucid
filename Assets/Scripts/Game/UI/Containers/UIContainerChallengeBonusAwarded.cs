using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000882; bodies translated from the 1.10.1 x86-64 binary.
    // Announces the XP bonus granted for finishing the period's challenges. The bonus is
    // acknowledged when the container is destroyed, if it received the challenge manager.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerChallengeBonusAwarded : UIContainer
    {
        public readonly Bindable<Strings> XPRewardTitle = new Bindable<Strings>();
        public readonly Bindable<int> BonusXPAwarded = new Bindable<int>();
        public readonly Bindable<int> TotalXPBefore = new Bindable<int>();
        public readonly Bindable<int> TotalXPAfter = new Bindable<int>();
        public readonly Bindable<string> BonusXPString = new Bindable<string>();
        public readonly Bindable<Sprite> RewardIcon = new Bindable<Sprite>();
        [HashEnum(null)] [SerializeField] private Strings m_xpStringFormat = Strings.MENU_TAILS_CHALLENGES_XP;
        [SerializeField] [HashEnum(null)] private Strings m_titleChallengesComplete;
        [SerializeField] private Sprite m_iconChallenges;
        private ChallengeManager m_challengeManager;

        // 0x0600312d (0x61C750)
        private void OnEnable()
        {
            ProcessManager.GetSystemRef<ChallengeManager>(null, true).InvokeOnValid(OnChallengeManagerValid);
        }

        // 0x0600312e (0x61C830): the total before the bonus is the current total less the bonus.
        private void OnChallengeManagerValid(ChallengeManager challengeManager)
        {
            m_challengeManager = challengeManager;
            BonusXPAwarded.Value = challengeManager.PeriodBonusXP;
            TotalXPAfter.Value = challengeManager.GetCurrentXP();
            TotalXPBefore.Value = TotalXPAfter - BonusXPAwarded;
            BonusXPString.Value = string.Format(StringTable.GetString(m_xpStringFormat), BonusXPAwarded.Value.ToString());
            XPRewardTitle.Value = m_titleChallengesComplete;
            RewardIcon.Value = m_iconChallenges;
        }

        // 0x0600312f (0x61C9B0)
        private void OnDestroy()
        {
            if (m_challengeManager != null)
                m_challengeManager.AcknowledgeBonusXP();
            m_challengeManager = null;
        }

        // 0x06003130
        public UIContainerChallengeBonusAwarded() { }
    }
}
