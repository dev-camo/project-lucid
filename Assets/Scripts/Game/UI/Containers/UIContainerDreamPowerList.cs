using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008b4; bodies translated from the 1.10.1 x86-64 binary.
    // The three equipped dream powers shown during a mission (for example on the pause screen).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerDreamPowerList : UIContainer
    {
        [SerializeField] private UIWidgetDreamPowerDescription m_powerDescription1;
        [SerializeField] private UIWidgetDreamPowerDescription m_powerDescription2;
        [SerializeField] private UIWidgetDreamPowerDescription m_powerDescription3;
        public readonly Bindable<bool> Visible = new Bindable<bool>();
        private DataManager m_dataManager;
        private SaveManager m_saveManager;
        private MissionManager m_missionManager;
        private DreamPowerStoreManager m_dreamPowerStoreManager;

        // 0x06003241 (0x62F7B0)
        private void Start()
        {
            m_dataManager = ProcessManager.GetSystem<DataManager>();
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            m_missionManager = ProcessManager.GetSystem<MissionManager>();
            m_dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>();
            LoadDreamPowerSlots();
        }

        // 0x06003242 (0x62F8D0): the list is hidden when the store is unavailable, the active mission
        // disallows dream powers or the loadout of the mission's archetype holds no slot. Without an
        // active mission the loadout of the default archetype is used.
        private void LoadDreamPowerSlots()
        {
            CharacterArchetype characterArchetype = default;
            if (!m_dreamPowerStoreManager.IsAvailable())
            {
                Visible.Value = false;
                return;
            }
            if (m_missionManager.HasActiveMission())
            {
                MissionDefinition missionDefinition = m_missionManager.ActiveMissionContext.MissionDefinition;
                if (!missionDefinition.EnableDreamPowers)
                {
                    Visible.Value = false;
                    return;
                }
                characterArchetype = missionDefinition.CharacterArchetype;
            }
            SaveDataDreamPowerLoadout loadout = m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(characterArchetype);
            if (loadout.SlotsByIndex.Count == 0)
            {
                Visible.Value = false;
                return;
            }
            Visible.Value = true;
            PopulateSlot(loadout, 0, m_powerDescription1);
            PopulateSlot(loadout, 1, m_powerDescription2);
            PopulateSlot(loadout, 2, m_powerDescription3);
        }

        // 0x06003243 (0x62FAB0): an empty slot, or a saved power that no longer exists, sets the widget up with a null definition.
        private void PopulateSlot(SaveDataDreamPowerLoadout loadout, int slot, UIWidgetDreamPowerDescription widget)
        {
            DreamPowerDefinition definition = null;
            if (loadout.SlotsByIndex.TryGetValue(slot, out string dreamPowerGuid))
                m_dataManager.DreamPowerDefinitions.TryGetValue(dreamPowerGuid, out definition);
            widget.Setup(new UIWidgetDreamPowerDescriptionParameters(definition));
        }

        // 0x06003244 (0x62FC10)
        public UIContainerDreamPowerList() { }
    }
}
