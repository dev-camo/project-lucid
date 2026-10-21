using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008c4; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerLeaderboards : UIContainer
    {
        [Tooltip("Optional leaderboard identifier so when we show the leaderboards we open it at that one.")]
        [SerializeField]
        private LeaderboardIdentifier m_leaderboardIdentifier;
        public readonly Bindable<bool> CanShowChallenge = new Bindable<bool>();
        private readonly SystemRef<TimeTrialLeaderboardManager> m_timeTrialLeaderboardManagerRef = ProcessManager.GetSystemRef<TimeTrialLeaderboardManager>(null, true);

        // 060032a1 (1.10.1 RVA 0x6363D0)
        private void Start()
        {
            ControlMapping.Subscribe(GameInput.UILeaderboards, new Action<float>(ControllerShowLeaderboards), InputTrigger.Down, -1);
            CanShowChallenge.Value = SystemConfiguration.GetConfig<CoreGameConfiguration>().ShowGameCenterChallenges;
        }

        // 060032a2 (1.10.1 RVA 0x6364B0)
        private void OnDestroy()
        {
            ControlMapping.Unsubscribe(GameInput.UILeaderboards, new Action<float>(ControllerShowLeaderboards), -1);
        }

        // 060032a3 (1.10.1 RVA 0x636550): native carries a copy of the ShowLeaderboards body.
        private void ControllerShowLeaderboards(float value = 0f)
        {
            ShowLeaderboards();
        }

        // 060032a4 (1.10.1 RVA 0x6365C0): the two Tails challenge boards are never opened from here.
        public void ShowLeaderboards()
        {
            if (m_leaderboardIdentifier == LeaderboardIdentifier.TailsChallenge_HourlyQA
                || m_leaderboardIdentifier == LeaderboardIdentifier.TailsChallenges_Weekly)
                return;
            m_timeTrialLeaderboardManagerRef.Get().ShowLeaderboard(m_leaderboardIdentifier);
        }

        // 060032a5 (1.10.1 RVA 0x636630)
        public void SetLeaderboard(LeaderboardIdentifier leaderboardIdentifier)
        {
            m_leaderboardIdentifier = leaderboardIdentifier;
        }

        // 060032a6 (1.10.1 RVA 0x636640)
        public void Action_IssueChallenge()
        {
            m_timeTrialLeaderboardManagerRef.Get().IssueChallenge(m_leaderboardIdentifier);
        }

        // 060032a7 (1.10.1 RVA 0x636690)
        public UIContainerLeaderboards()
        {
        }
    }
}
