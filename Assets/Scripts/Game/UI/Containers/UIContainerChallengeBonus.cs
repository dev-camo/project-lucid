using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000880; bodies translated from the 1.10.1 x86-64 binary.
    // The "complete every challenge" bonus: how much XP it is worth and whether it was earned.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerChallengeBonus : UIContainer
    {
        public readonly Bindable<int> BonusXP = new Bindable<int>();
        public readonly Bindable<int> ChallengeCount = new Bindable<int>();
        public readonly Bindable<bool> Earned = new Bindable<bool>();
        public readonly Bindable<string> Description = new Bindable<string>();
        public readonly Bindable<string> FormattedBonusXP = new Bindable<string>();
        [SerializeField] [HashEnum(typeof(Strings))] private Strings m_descriptionText;
        [Tooltip("Format for XP display. Parameters: {0} XP reward amount.")]
        [SerializeField] [HashEnum(null)] private Strings m_bonusXPFormat = Strings.MENU_TAILS_CHALLENGES_XP;

        // 0x06003127 (0x61C400): the description is formatted with the challenge count and the
        // bonus text with the XP amount.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerChallengeBonusParameters bonusParameters = (UIContainerChallengeBonusParameters)parameters;
            BonusXP.Value = bonusParameters.BonusXP;
            ChallengeCount.Value = bonusParameters.ChallengeCount;
            Earned.Value = bonusParameters.Earned;
            Description.Value = string.Format(StringTable.GetString(m_descriptionText), bonusParameters.ChallengeCount.ToString());
            FormattedBonusXP.Value = string.Format(StringTable.GetString(m_bonusXPFormat), bonusParameters.BonusXP.ToString());
        }

        // 0x06003128
        public UIContainerChallengeBonus() { }

        // Original nested 0x02000881.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class UIContainerChallengeBonusParameters : IUIContainerParameters
        {
            // 0x06003129..0x0600312b
            public int BonusXP { get; }
            public int ChallengeCount { get; }
            public bool Earned { get; }

            // 0x0600312c
            public UIContainerChallengeBonusParameters(int bonusXP, int challengeCount, bool earned)
            {
                BonusXP = bonusXP;
                ChallengeCount = challengeCount;
                Earned = earned;
            }
        }
    }
}
