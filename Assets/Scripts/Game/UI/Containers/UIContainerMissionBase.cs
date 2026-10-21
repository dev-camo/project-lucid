using Hardlight;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008d8; bodies translated from the 1.10.1 x86-64 binary.
    // Shared mission name and objective image for the mission screens.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UIContainerMissionBase : UIContainer
    {
        public readonly Bindable<string> MissionNameLabel = new Bindable<string>();
        public readonly Bindable<Sprite> MissionObjectiveImage = new Bindable<Sprite>();
        private MissionState m_missionState;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private UIContainerMissionParametersBase m_missionParameters;

        // 06003304 (0x63B840): the string-table handler is removed before it is added, so it is only subscribed once.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_missionParameters = parameters.GetAs<UIContainerMissionParametersBase>();
            SetMissionState();
            StringTable.StringsTableHandlers -= SetMissionState;
            StringTable.StringsTableHandlers += SetMissionState;
        }

        // 06003305 (0x63B9E0): the active context's description replaces the formatted name only when the active
        // context is for the same mission.
        private void SetMissionState()
        {
            MissionState missionState = m_missionParameters.MissionState;
            if (missionState == null)
                return;
            m_missionState = missionState;
            if (m_missionState == null)
                return;
            MissionDefinition definition = m_missionState.Definition;
            IMissionContext activeMissionContext = m_missionManagerRef.Get().ActiveMissionContext;
            MissionDefinition activeDefinition = activeMissionContext?.MissionDefinition;
            MissionNameLabel.Value = activeDefinition == definition
                ? activeMissionContext.Description
                : definition.GetFormattedMissionName();
            MissionObjectiveImage.Value = definition.MissionObjectiveIcon;
        }

        // 06003306
        protected UIContainerMissionBase() { }
    }
}
