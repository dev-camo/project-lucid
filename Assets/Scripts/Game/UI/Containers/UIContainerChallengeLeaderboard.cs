using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000883; bodies translated from the 1.10.1 x86-64 binary.
    // The challenge leaderboard entry: the player's score and a button that opens the
    // platform leaderboard through the challenge manager.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerChallengeLeaderboard : UIContainer
    {
        public readonly Bindable<int> Score = new Bindable<int>();
        public readonly Bindable<bool> Available = new Bindable<bool>();
        public readonly Bindable<string> ScoreLabel = new Bindable<string>();
        [SerializeField] private UIWidgetButton m_button;
        [SerializeField] [HashEnum(typeof(Strings))] private Strings m_scoreLabel;

        // 0x06003131 (0x61CBF0)
        private void Awake()
        {
            ScoreLabel.Value = StringTable.GetString(m_scoreLabel);
        }

        // 0x06003132 (0x61CC60)
        public void Action_OnButtonPressed()
        {
            ProcessManager.GetSystem<ChallengeManager>(null, true).ShowLeaderboard();
        }

        // 0x06003133 (0x61CCD0): the button, when assigned, follows the leaderboard's availability.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerChallengeLeaderboardParameters leaderboardParameters = (UIContainerChallengeLeaderboardParameters)parameters;
            Score.Value = leaderboardParameters.Score;
            Available.Value = leaderboardParameters.Available;
            if (m_button != null)
                m_button.SetInteractable(leaderboardParameters.Available);
        }

        // 0x06003134
        public UIContainerChallengeLeaderboard() { }

        // Original nested 0x02000884.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class UIContainerChallengeLeaderboardParameters : IUIContainerParameters
        {
            // 0x06003135..0x06003136
            public int Score { get; }
            public bool Available { get; }

            // 0x06003137
            public UIContainerChallengeLeaderboardParameters(int score, bool available)
            {
                Score = score;
                Available = available;
            }
        }
    }
}
