using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200088e; bodies translated from the 1.10.1 x86-64 binary.
    // The read-only dream power loadout shown on the character select screen.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterSelectEquipDreamPower : UIContainer
    {
        [SerializeField] private UIWidgetImageDreamPowerSlot[] m_slots;
        [SerializeField] private UIWidgetButton m_buttonOverlay;
        [SerializeField] private RectTransform m_controllerGroup;
        [SerializeField] private PromptTrigger m_ctaPromptTrigger;
        public readonly Bindable<bool> Available = new Bindable<bool>();
        public readonly Bindable<bool> PowersEnabled = new Bindable<bool>();
        public readonly Bindable<bool> ShowNewFlag = new Bindable<bool>();
        public readonly Bindable<bool> ShowArchetype = new Bindable<bool>();
        public readonly Bindable<Sprite> ArchetypeImage = new Bindable<Sprite>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<DreamPowerStoreManager> m_dreamPowerStoreManagerRef = ProcessManager.GetSystemRef<DreamPowerStoreManager>();
        private readonly Dictionary<UIWidgetImageDreamPowerSlot, DreamPowerDefinition> m_powersBySlotWidget = new Dictionary<UIWidgetImageDreamPowerSlot, DreamPowerDefinition>();
        private CharacterArchetypeDefinition m_archetypeDefinition;

        // 0x06003170: the loadout is only shown, and the call to action prompt only offered,
        // when the active mission allows dream powers.
        private void Start()
        {
            Available.Value = m_dreamPowerStoreManagerRef.Get().IsAvailable();
            ShowNewFlag.Value = m_saveManagerRef.Get().CurrentSave.DreamPowerStore.IsNewState == DreamPowerStoreIsNewState.IsNew;
            MissionDefinition missionDefinition = m_missionManagerRef.Get().ActiveMissionContext.MissionDefinition;
            if (missionDefinition.EnableDreamPowers)
            {
                PowersEnabled.Value = true;
                m_buttonOverlay.SetInteractable(true);
                m_controllerGroup.gameObject.SetActive(true);
                SetupPowers(missionDefinition.CharacterArchetype);
                if (!IsAnyPowerEquipped() && m_ctaPromptTrigger != null)
                    m_ctaPromptTrigger.TryShow();
            }
            else
            {
                PowersEnabled.Value = false;
                m_buttonOverlay.SetInteractable(false);
                m_controllerGroup.gameObject.SetActive(false);
            }
        }

        // 0x06003171: true when any archetype's saved loadout holds at least one slot.
        private bool IsAnyPowerEquipped()
        {
            foreach (var (characterArchetype, loadout) in m_saveManagerRef.Get().CurrentSave.DreamPowerLoadoutByArchetype)
            {
                if (loadout.SlotsByIndex.Count > 0)
                    return true;
            }
            return false;
        }

        // 0x06003172
        private void OnDestroy()
        {
            if (m_archetypeDefinition != null)
                m_archetypeDefinition.SilhouetteImage.Unload();
        }

        // 0x06003173: slots without a saved power are emptied; a power the archetype may not use is shown disabled.
        private void SetupPowers(CharacterArchetype characterArchetype)
        {
            DataManager dataManager = m_dataManagerRef.Get();
            ShowArchetype.Value = characterArchetype != CharacterArchetype.None;
            if (ShowArchetype.Value)
            {
                m_archetypeDefinition = dataManager.CharacterArchetypes[characterArchetype];
                ArchetypeImage.Value = m_archetypeDefinition.SilhouetteImage.Load();
            }
            SaveDataDreamPowerLoadout loadout = m_saveManagerRef.Get().CurrentSave.GetOrCreateDreamPowerLoadout(characterArchetype);
            for (int i = 0; i < m_slots.Length; i++)
            {
                UIWidgetImageDreamPowerSlot slot = m_slots[i];
                if (loadout.SlotsByIndex.TryGetValue(i, out string dreamPowerGuid))
                {
                    DreamPowerDefinition definition = dataManager.DreamPowerDefinitions[dreamPowerGuid];
                    slot.Equip(definition);
                    if (!m_dreamPowerStoreManagerRef.Get().ArchetypeAllowsPower(characterArchetype, definition))
                        slot.Disable();
                    m_powersBySlotWidget.Add(slot, definition);
                }
                else
                {
                    slot.Unequip();
                }
            }
        }

        // 0x06003174
        public void RefreshPowers(CharacterArchetype characterArchetype)
        {
            if (!PowersEnabled.Value)
                return;
            foreach (var (slot, definition) in m_powersBySlotWidget)
            {
                if (m_dreamPowerStoreManagerRef.Get().ArchetypeAllowsPower(characterArchetype, definition))
                    slot.Enable();
                else
                    slot.Disable();
            }
        }

        // 0x06003175
        private bool ArchetypeAllowsPower(CharacterArchetype characterArchetype, DreamPowerDefinition definition)
        {
            return m_dreamPowerStoreManagerRef.Get().ArchetypeAllowsPower(characterArchetype, definition);
        }

        // 0x06003176: field initialisers precede the base constructor.
        public UIContainerCharacterSelectEquipDreamPower() { }
    }
}
