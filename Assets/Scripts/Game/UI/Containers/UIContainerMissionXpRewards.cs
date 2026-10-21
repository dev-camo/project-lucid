using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008eb; bodies translated from the 1.10.1 x86-64 binary.
    // The XP breakdown of the results screen: XP from the rings collected, from clearing the mission, and the running total.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerMissionXpRewards : UIContainer
    {
        [SerializeField] private UIAnimatingCounter m_ringXPAnimatingCounter;
        [SerializeField] private UIAnimatingCounter m_missionXPAnimatingCounter;
        public readonly Bindable<bool> EarnedXPFromRings = new Bindable<bool>();
        public readonly Bindable<bool> EarnedXPFromMission = new Bindable<bool>();
        public readonly Bindable<string> RingsCollected = new Bindable<string>();
        public readonly Bindable<string> RingXPEarned = new Bindable<string>();
        public readonly Bindable<string> MissionXPEarned = new Bindable<string>();
        public readonly Bindable<string> XPTotal = new Bindable<string>();
        private readonly SystemRef<MissionManager> m_missionManageRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);

        // 0x06003383 (0x669640)
        private void Awake()
        {
            Refresh();
        }

        // 0x06003384 (0x669650): nothing is shown unless the ring collectable has a state. The XP strings are "+{0}" followed by the
        // localised XP label.
        private void Refresh()
        {
            MissionManager missionManager = m_missionManageRef.Get();
            IMissionContext missionContext = missionManager.ActiveMissionContext;
            if (!ProcessManager.GetSystem<CollectableManager>(null, true).TryGetCollectableState(CollectableType.Ring, out CollectableState ringState))
                return;
            string xpFormat = string.Concat("+{0}", StringTable.GetString(Strings.CHALLENGES_LABEL_XP));
            EarnedXPFromRings.Value = missionContext.RingXPEarned > 0;
            RingsCollected.Value = string.Format("x{0}", ringState.Collected);
            m_ringXPAnimatingCounter.SetUp(0, missionContext.RingXPEarned, xpFormat);
            RingXPEarned.Value = string.Format(xpFormat, missionContext.RingXPEarned);
            if (missionManager.ActiveMissionContext is MissionContextRegular regularContext && regularContext.MissionClearXPEarned > 0)
            {
                EarnedXPFromMission.Value = true;
                MissionXPEarned.Value = string.Format(xpFormat, regularContext.MissionClearXPEarned);
                m_missionXPAnimatingCounter.SetUp(0, regularContext.MissionClearXPEarned, xpFormat);
            }
            XPTotal.Value = string.Format(xpFormat, m_saveManagerRef.Get().CurrentSave.SaveDataChallenges.TotalXPEarned);
        }

        // 0x06003385
        public UIContainerMissionXpRewards() { }
    }
}
