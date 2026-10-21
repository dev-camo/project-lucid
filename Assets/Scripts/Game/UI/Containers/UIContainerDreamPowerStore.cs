using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200086b with nested 0x0200086c..0x0200086e; bodies translated from the
    // 1.10.1 x86-64 binary. The dream power store: a grid of dream powers, the equip slots and the
    // description, price and action of the selected power.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerDreamPowerStore : UIContainer
    {
        // Original Game.Runtime 0x0200086c: several archetypes that share one icon in the description.
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class CombinedArchetypeIcon
        {
            [SerializeField] private List<CharacterArchetype> m_archetypes;
            [SerializeField] private AssetReferenceAtlasedSprite m_iconReference;

            // 0x060030ca / 0x060030cb
            public ManagedAddressableAsset<Sprite> Icon { get; private set; }

            [NonSerialized]
            public readonly HashSet<CharacterArchetype> ArchetypeSet = new HashSet<CharacterArchetype>();

            // 0x060030cc (0x614260): the icon wrapper is created and the set refilled from the serialized list.
            public void SetUp()
            {
                Icon = new ManagedAddressableAsset<Sprite>(m_iconReference);
                ArchetypeSet.Clear();
                foreach (CharacterArchetype archetype in m_archetypes)
                    ArchetypeSet.Add(archetype);
            }

            // 0x060030cd (0x616410)
            public bool ArchetypesMatch(IEnumerable<CharacterArchetype> archetypes)
            {
                return ArchetypeSet.SetEquals(archetypes);
            }

            // 0x060030ce (0x617060)
            public CombinedArchetypeIcon() { }
        }

        [SerializeField] private bool m_allowEquipping = true;
        [SerializeField] private UIContainerCollectableCounter m_collectableCounter;
        [SerializeField] private UIWidgetToggleDreamPower m_dreamPowerPrefab;
        [Tooltip("An extra prefab to spawn when there are an odd number of dream powers, to keep the row alignment correct.")]
        [SerializeField] private GameObject m_dreamPowerPadderPrefab;
        [SerializeField] private UIContainerDreamPowerStoreBlueCoinCost m_blueCoinCost;
        [SerializeField] private UIToggleGroup m_dreamPowersToggleGroup;
        [SerializeField] private UIWidgetButtonDreamPowerSlot[] m_slotButtons;
        [SerializeField] private RectTransform[] m_dreamPowerRows;
        [SerializeField] private Animation m_dreamPowerRectAnimation;
        [SerializeField] private AnimationClip m_dreamPowerRectBrowseClip;
        [SerializeField] private AnimationClip m_dreamPowerRectSlotClip;
        [HashEnum(null)]
        [SerializeField] private Strings m_counterStringFormat = Strings.FORMAT_X_OF_Y;
        [SerializeField] private HLScrollRectSelection m_scrollRectSelection;
        [HashEnum(null)]
        [SerializeField]
        [Header("Action")]
        private Strings m_actionLockedString;
        [HashEnum(null)]
        [SerializeField] private Strings m_actionEquippableString;
        [HashEnum(null)]
        [SerializeField] private Strings m_actionEquippedString;
        [HashEnum(null)]
        [SerializeField] private Strings m_insufficientCurrencyString;
        [SerializeField]
        [HashEnum(null)]
        private Strings m_actionPurchasableString;
        [Header("Description")]
        [SerializeField]
        [HashEnum(null)]
        private Strings m_lockedByArchetypeString;
        [SerializeField] private RewardUnlockStringPicker m_rewardUnlockStringPicker;
        [SerializeField] private List<CombinedArchetypeIcon> m_combinedArchetypeIcons;

        public readonly Bindable<bool> Available = new Bindable<bool>();
        public readonly Bindable<bool> HasArchetype = new Bindable<bool>();
        public readonly Bindable<bool> LockedDescription = new Bindable<bool>();
        public readonly Bindable<bool> ShowDescriptionArchetype = new Bindable<bool>();
        public readonly Bindable<bool> Browsing = new Bindable<bool>();
        public readonly Bindable<bool> ActionAvailable = new Bindable<bool>();
        public readonly Bindable<Sprite> CharacterArchetypeImage = new Bindable<Sprite>();
        public readonly Bindable<Sprite> CharacterArchetypeSilhouette = new Bindable<Sprite>();
        public readonly Bindable<Sprite> DescriptionArchetypeImage = new Bindable<Sprite>();
        public readonly Bindable<Strings> ActionLabel = new Bindable<Strings>();
        public readonly Bindable<Strings> DreamPowerName = new Bindable<Strings>();
        public readonly Bindable<Strings> DreamPowerDescription = new Bindable<Strings>();
        public readonly Bindable<Strings> DreamPowerLockedDescription = new Bindable<Strings>();
        public readonly Bindable<Sprite> DreamPowerIcon = new Bindable<Sprite>();
        public readonly Bindable<string> CounterFormatString = new Bindable<string>();
        public readonly Bindable<int> TotalAmount = new Bindable<int>();
        public readonly Bindable<int> BoughtAmount = new Bindable<int>();
        public readonly Bindable<bool> Purchasable = new Bindable<bool>();
        public readonly Bindable<string> BlueCoinCost = new Bindable<string>();

        protected CharacterArchetype m_characterArchetype;
        protected DataManager m_dataManager;
        private readonly HashSet<DreamPowerStoreBandType> m_unlockedDreamPowerBands = new HashSet<DreamPowerStoreBandType>();
        private readonly Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower> m_widgetByDefinition = new Dictionary<DreamPowerDefinition, UIWidgetToggleDreamPower>();
        private ManagedAddressableAsset<Sprite> m_lastArchetypeIcon;
        private DreamPowerStoreManager m_dreamPowerStoreManager;
        private MissionManager m_missionManager;
        private SaveManager m_saveManager;
        private ProgressionManager m_progressionManager;
        private UIDreamPowerSlots m_slots;
        private DreamPowerDefinition m_currentSelectedDefinition;
        private UINavigationManager m_uiNavigationManager;

        // 0x060030b0 (0x613FC0): the combined archetype icons build their sets once, after the managers are known.
        private void Awake()
        {
            m_dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>();
            m_dataManager = ProcessManager.GetSystem<DataManager>();
            m_missionManager = ProcessManager.GetSystem<MissionManager>();
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            m_progressionManager = ProcessManager.GetSystem<ProgressionManager>();
            m_uiNavigationManager = ProcessManager.GetSystem<UINavigationManager>();
            foreach (CombinedArchetypeIcon combinedArchetypeIcon in m_combinedArchetypeIcons)
                combinedArchetypeIcon.SetUp();
        }

        // 0x060030b1 (0x6130F0)
        protected virtual void OnEnable()
        {
            CollectableDefinition blueCoinDefinition = m_dataManager.CollectableDefinitions[CollectableType.BlueCoin];
            m_collectableCounter.Setup(new UIContainerCollectableCounterParameters(blueCoinDefinition, false));
            Available.Value = m_dreamPowerStoreManager.IsAvailable();
            Browsing.Value = true;
            ActionAvailable.Value = true;
            SetCharacterArchetype();
            m_slots = new UIDreamPowerSlots(m_characterArchetype, m_slotButtons);
            SetUnlockedBands();
            UpdateTitle();
            SetupDreamPowers();
            UpdateSlots();
            UpdateCounter();
        }

        // 0x060030b2 (0x614460): the archetype is the active mission's, or CharacterArchetype.None (no archetype) outside a mission.
        private void SetCharacterArchetype()
        {
            CharacterArchetype characterArchetype = default;
            if (m_missionManager.HasActiveMission())
                characterArchetype = m_missionManager.ActiveMissionContext.MissionDefinition.CharacterArchetype;
            m_characterArchetype = characterArchetype;
        }

        // 0x060030b3 (0x6146E0): the None band is always open; the others open with their collected reward.
        private void SetUnlockedBands()
        {
            if (m_dreamPowerStoreManager.IsDebugUnlockAll())
            {
                m_unlockedDreamPowerBands.AddRange(EnumUtilities.GetValues<DreamPowerStoreBandType>());
                return;
            }
            SaveDataGame saveGame = m_saveManager.CurrentSave;
            m_unlockedDreamPowerBands.Add(DreamPowerStoreBandType.None);
            foreach (var (bandType, bandDefinition) in m_dataManager.DreamPowerStoreBandDefinitions)
            {
                if (saveGame.TryGetRewardDataByRewardGUID(bandDefinition.GetGUID(), out SaveDataChallengeReward reward) && reward.Collected)
                    m_unlockedDreamPowerBands.Add(bandType);
            }
        }

        // 0x060030b4 (0x614960)
        private void UpdateTitle()
        {
            if (m_characterArchetype != CharacterArchetype.None)
            {
                HasArchetype.Value = true;
                CharacterArchetypeDefinition archetypeDefinition = m_dataManager.CharacterArchetypes[m_characterArchetype];
                CharacterArchetypeImage.Value = archetypeDefinition.AlternateImage.Load();
                CharacterArchetypeSilhouette.Value = archetypeDefinition.SilhouetteImage.Load();
            }
            else
            {
                HasArchetype.Value = false;
            }
        }

        // 0x060030b5 (0x614A60): rebuilds the grid. Powers are dealt across the rows in turn; the last
        // row is padded so that the rows stay aligned. The previously selected power is selected again.
        private void SetupDreamPowers()
        {
            foreach (RectTransform row in m_dreamPowerRows)
            {
                foreach (RectTransform child in row)
                    Destroy(child.gameObject);
            }
            int blueCoinsAvailable = GetBlueCoinsAvailable();
            m_widgetByDefinition.Clear();
            int rowIndex = 0;
            foreach (var (_, definition) in m_dataManager.DreamPowerDefinitions)
            {
                UIWidgetToggleDreamPowerSlotParameters parameters = CreateWidgetParameters(definition, blueCoinsAvailable);
                UIWidgetToggleDreamPower widget = Instantiate(m_dreamPowerPrefab, m_dreamPowerRows[rowIndex]);
                widget.Setup(parameters);
                widget.GroupElement.Group = m_dreamPowersToggleGroup;
                m_widgetByDefinition.Add(definition, widget);
                if (definition == m_dreamPowerStoreManager.LastSelectedDefinition)
                {
                    m_uiNavigationManager.Select(widget.UIWidgetNavigation);
                    widget.GroupElement.Toggle.IsOn = true;
                }
                rowIndex++;
                if (rowIndex >= m_dreamPowerRows.Length)
                    rowIndex = 0;
            }
            while (rowIndex != 0 && rowIndex < m_dreamPowerRows.Length)
            {
                Instantiate(m_dreamPowerPadderPrefab, m_dreamPowerRows[rowIndex]);
                rowIndex++;
            }
        }

        // 0x060030b6 (0x615620): every widget is set up again with its current item state.
        private void RefreshDreamPowers()
        {
            int blueCoinsAvailable = GetBlueCoinsAvailable();
            foreach (var (definition, widget) in m_widgetByDefinition)
                widget.Setup(CreateWidgetParameters(definition, blueCoinsAvailable));
            ReselectCurrentPower();
        }

        // 0x060030b7 (0x6154C0): collected blue coins that have not been spent.
        private int GetBlueCoinsAvailable()
        {
            return m_progressionManager.GetTotalProgress(CollectableType.BlueCoin).Collected - m_saveManager.CurrentSave.BlueCoins.Spent;
        }

        // 0x060030b8 (0x615500): the widget reports its selection back with the item state captured here.
        private UIWidgetToggleDreamPowerSlotParameters CreateWidgetParameters(DreamPowerDefinition definition, int blueCoinsAvailable)
        {
            UIDreamPowerStoreItemState itemState = GetUIItemState(definition, blueCoinsAvailable);
            return new UIWidgetToggleDreamPowerSlotParameters(definition, itemState, Available.Value,
                toggleDefinition => OnDreamPowerChanged(toggleDefinition, itemState));
        }

        // 0x060030b9 (0x615900): a locked band wins; a bought power is bound to its archetype and is
        // equipped when it sits in a slot; an unbought power is Purchasable or Insufficient whatever its
        // archetype.
        private UIDreamPowerStoreItemState GetUIItemState(DreamPowerDefinition definition, int blueCoinsAvailable)
        {
            bool archetypeAllowsPower = m_dreamPowerStoreManager.ArchetypeAllowsPower(m_characterArchetype, definition);
            bool bandUnlocked = m_unlockedDreamPowerBands.Contains(definition.Band);
            bool bought = m_dreamPowerStoreManager.GetDreamPowerStoreItemSaveData(definition).Bought;
            bool canAfford = !bought && definition.PurchaseCost <= blueCoinsAvailable;
            if (!bandUnlocked)
                return UIDreamPowerStoreItemState.LockedByProgression;
            UIDreamPowerStoreItemState itemState = archetypeAllowsPower || !bought
                ? (canAfford ? UIDreamPowerStoreItemState.Purchasable : UIDreamPowerStoreItemState.Insufficient)
                : UIDreamPowerStoreItemState.LockedByArchetype;
            if (bought && archetypeAllowsPower)
            {
                SaveDataDreamPowerLoadout loadout = m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(m_characterArchetype);
                itemState = loadout.EquippedSlot(definition) != -1
                    ? UIDreamPowerStoreItemState.Equipped
                    : UIDreamPowerStoreItemState.Bought;
            }
            return itemState;
        }

        // 0x060030ba (0x615150): puts the saved loadout of the archetype into the slot buttons.
        private void UpdateSlots()
        {
            SaveDataDreamPowerLoadout loadout = m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(m_characterArchetype);
            for (int slotIndex = 0; slotIndex < m_slotButtons.Length; slotIndex++)
            {
                if (loadout.SlotsByIndex.TryGetValue(slotIndex, out string dreamPowerGuid))
                    m_slots.Equip(slotIndex, m_dataManager.DreamPowerDefinitions[dreamPowerGuid]);
            }
        }

        // 0x060030bb (0x6152B0): bought out of all dream powers, shown through the localised format.
        private void UpdateCounter()
        {
            Dictionary<string, DreamPowerDefinition> dreamPowerDefinitions = m_dataManager.DreamPowerDefinitions;
            CounterFormatString.Value = StringTable.GetString(m_counterStringFormat);
            TotalAmount.Value = dreamPowerDefinitions.Count;
            int boughtCount = 0;
            foreach (SaveDataDreamPowerStoreItem storeItem in m_saveManager.CurrentSave.DreamPowerStore.DreamPowers)
            {
                if (storeItem.Bought)
                    boughtCount++;
            }
            BoughtAmount.Value = boughtCount;
        }

        // 0x060030bc (0x613490): a selection updates the price, descriptions and action, and on the next
        // frame the slot buttons and the scroll position. The selection is also remembered in the
        // manager for the next visit.
        protected virtual void OnDreamPowerChanged(DreamPowerDefinition definition, UIDreamPowerStoreItemState itemState)
        {
            bool lockedByArchetype = itemState == UIDreamPowerStoreItemState.LockedByArchetype;
            bool archetypeAllowsPower = m_dreamPowerStoreManager.ArchetypeAllowsPower(m_characterArchetype, definition);
            Purchasable.Value = Available.Value && itemState == UIDreamPowerStoreItemState.Purchasable;
            m_currentSelectedDefinition = definition;
            m_dreamPowerStoreManager.LastSelectedDefinition = m_currentSelectedDefinition;
            m_blueCoinCost.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(definition, itemState));
            BlueCoinCost.Value = definition.PurchaseCost.ToString();
            UpdateAvailableDescription(definition);
            if (itemState == UIDreamPowerStoreItemState.Purchasable)
            {
                LockedDescription.Value = false;
            }
            else
            {
                UpdateLockedStatus(lockedByArchetype || !archetypeAllowsPower,
                    itemState == UIDreamPowerStoreItemState.LockedByProgression,
                    itemState == UIDreamPowerStoreItemState.Insufficient, definition);
            }
            UpdateAction(itemState);
            CoroutineUtils.OnNextFrame(() =>
            {
                SetSlotStatus(itemState);
                if (m_scrollRectSelection != null && m_uiNavigationManager.IsActive && m_currentSelectedDefinition != null)
                {
                    m_scrollRectSelection.Select((RectTransform)m_widgetByDefinition[m_currentSelectedDefinition].transform,
                        true, false, null);
                }
            });
        }

        // 0x060030bd (0x6163C0)
        private void SetSlotStatus(UIDreamPowerStoreItemState itemState)
        {
            foreach (UIWidgetButtonDreamPowerSlot slotButton in m_slotButtons)
                slotButton.SetSlotState(itemState);
        }

        // 0x060030be (0x616200): why the description is replaced by a lock message: price first, then
        // progression, then archetype.
        private void UpdateLockedStatus(bool lockedByArchetype, bool lockedByProgression, bool lockedByCost, DreamPowerDefinition dreamPowerDefinition)
        {
            LockedDescription.Value = lockedByArchetype | lockedByProgression | lockedByCost;
            if (lockedByCost)
            {
                DreamPowerLockedDescription.Value = m_insufficientCurrencyString;
            }
            else if (lockedByProgression)
            {
                m_rewardUnlockStringPicker.UpdateStringFromDreamPower(dreamPowerDefinition);
                DreamPowerLockedDescription.Value = m_rewardUnlockStringPicker.StringID.Value;
            }
            else if (lockedByArchetype)
            {
                DreamPowerLockedDescription.Value = m_lockedByArchetypeString;
            }
        }

        // 0x060030bf (0x615BA0): the previous archetype icon is released first. A power for a single archetype
        // shows that archetype's alternate image; a power for several shows the first combined icon whose
        // set matches. With no requirement the archetype art is hidden.
        private void UpdateAvailableDescription(DreamPowerDefinition definition)
        {
            DreamPowerName.Value = definition.Name;
            DreamPowerDescription.Value = definition.Description;
            DreamPowerIcon.Value = definition.IconAsset.Load();
            if (m_lastArchetypeIcon != null)
                m_lastArchetypeIcon.Unload();
            m_lastArchetypeIcon = null;
            if (m_dreamPowerStoreManager.TryGetRequiredArchetypes(definition, out IReadOnlyCollection<CharacterArchetype> requiredArchetypes))
            {
                ShowDescriptionArchetype.Value = true;
                if (requiredArchetypes.Count == 1)
                {
                    foreach (CharacterArchetype requiredArchetype in requiredArchetypes)
                    {
                        CharacterArchetypeDefinition archetypeDefinition = m_dataManager.CharacterArchetypes[requiredArchetype];
                        DescriptionArchetypeImage.Value = archetypeDefinition.AlternateImage.Load();
                        m_lastArchetypeIcon = archetypeDefinition.AlternateImage;
                        break;
                    }
                }
                else
                {
                    foreach (CombinedArchetypeIcon combinedArchetypeIcon in m_combinedArchetypeIcons)
                    {
                        if (combinedArchetypeIcon.ArchetypesMatch(requiredArchetypes))
                        {
                            DescriptionArchetypeImage.Value = combinedArchetypeIcon.Icon.Load();
                            m_lastArchetypeIcon = combinedArchetypeIcon.Icon;
                            break;
                        }
                    }
                }
            }
            else
            {
                ShowDescriptionArchetype.Value = false;
                DescriptionArchetypeImage.Value = null;
            }
        }

        // 0x060030c0 (0x616300): only a purchasable power has an action of its own; equipping and
        // unequipping additionally need m_allowEquipping. A locked or unaffordable power has none.
        private void UpdateAction(UIDreamPowerStoreItemState itemState)
        {
            Strings actionLabel;
            bool actionAvailable;
            switch (itemState)
            {
                case UIDreamPowerStoreItemState.LockedByProgression:
                case UIDreamPowerStoreItemState.LockedByArchetype:
                    actionLabel = m_actionLockedString;
                    actionAvailable = false;
                    break;
                case UIDreamPowerStoreItemState.Bought:
                    actionLabel = m_actionEquippableString;
                    actionAvailable = m_allowEquipping;
                    break;
                case UIDreamPowerStoreItemState.Purchasable:
                    actionLabel = m_actionPurchasableString;
                    actionAvailable = true;
                    break;
                case UIDreamPowerStoreItemState.Equipped:
                    // The original uses the UNEQUIP literal; m_actionEquippedString is serialized but unused.
                    actionLabel = Strings.MENU_DREAM_POWER_STORE_UNEQUIP;
                    actionAvailable = m_allowEquipping;
                    break;
                default:
                    actionLabel = m_insufficientCurrencyString;
                    actionAvailable = false;
                    break;
            }
            ActionLabel.Value = actionLabel;
            ActionAvailable.Value = actionAvailable;
        }

        // 0x060030c1 (0x616460): buys an unbought power; a bought one is unequipped from its slot or
        // starts the slot selection.
        public void Action_DoAction()
        {
            if (!Available.Value || m_currentSelectedDefinition == null)
                return;
            if (m_dreamPowerStoreManager.GetDreamPowerStoreItemSaveData(m_currentSelectedDefinition).Bought)
            {
                int equippedSlot = m_saveManager.CurrentSave.GetOrCreateDreamPowerLoadout(m_characterArchetype).EquippedSlot(m_currentSelectedDefinition);
                if (equippedSlot != -1)
                    Action_FinishEquip(equippedSlot);
                else
                    StartEquip();
            }
            else
            {
                Purchase();
                RefreshDreamPowers();
            }
        }

        // 0x060030c2 (0x616AC0)
        public void ClearSelectedPower()
        {
            foreach (var (_, widget) in m_widgetByDefinition)
                widget.Action_OnNavigatedAway();
            m_currentSelectedDefinition = null;
            ActionAvailable.Value = false;
        }

        // 0x060030c3 (0x616650): switches to slot selection; navigation starts on the first slot without a power.
        private void StartEquip()
        {
            Browsing.Value = false;
            m_dreamPowerRectAnimation.Play(m_dreamPowerRectSlotClip.name);
            ActionAvailable.Value = false;
            UIWidgetNavigation navigation = m_slotButtons[0].UIWidgetNavigation;
            foreach (UIWidgetButtonDreamPowerSlot slotButton in m_slotButtons)
            {
                if (!slotButton.IsEquipped)
                {
                    navigation = slotButton.UIWidgetNavigation;
                    break;
                }
            }
            m_uiNavigationManager.Select(navigation);
            foreach (UIWidgetButtonDreamPowerSlot slotButton in m_slotButtons)
                slotButton.SetInteractable(true);
            SetAllToggles(false);
        }

        // 0x060030c4 (0x616C90): the power widgets are only navigable while browsing.
        public void SetAllToggles(bool active)
        {
            foreach (var (_, widget) in m_widgetByDefinition)
            {
                if (active)
                    m_uiNavigationManager.RegisterWidget(widget.UIWidgetNavigation);
                else
                    m_uiNavigationManager.DeregisterWidget(widget.UIWidgetNavigation);
            }
            ActionAvailable.Value = active;
        }

        // 0x060030c5 (0x616520): while browsing, the slot toggles the selected power; while choosing a
        // slot, the power is equipped there and browsing resumes.
        public void Action_FinishEquip(int slotIndex)
        {
            if (Browsing.Value)
            {
                m_slots.Toggle(slotIndex, m_currentSelectedDefinition);
            }
            else
            {
                m_slots.Equip(slotIndex, m_currentSelectedDefinition);
                Browsing.Value = true;
                m_dreamPowerRectAnimation.Play(m_dreamPowerRectBrowseClip.name);
                ActionAvailable.Value = true;
            }
            SetAllToggles(true);
            RefreshDreamPowers();
        }

        // 0x060030c6 (0x616F50)
        public void Action_ExitEquip()
        {
            Browsing.Value = true;
            m_dreamPowerRectAnimation.Play(m_dreamPowerRectBrowseClip.name);
            ActionAvailable.Value = true;
            SetAllToggles(true);
            RefreshDreamPowers();
        }

        // 0x060030c7 (0x615820): reruns the selection callbacks of the current power with its new state.
        private void ReselectCurrentPower()
        {
            int blueCoinsAvailable = GetBlueCoinsAvailable();
            UIDreamPowerStoreItemState itemState = GetUIItemState(m_currentSelectedDefinition, blueCoinsAvailable);
            OnDreamPowerChanged(m_currentSelectedDefinition, itemState);
            UIWidgetToggleDreamPower widget = m_widgetByDefinition[m_currentSelectedDefinition];
            widget.Action_OnToggleIsOn(true);
            m_uiNavigationManager.Select(widget.UIWidgetNavigation);
        }

        // 0x060030c8 (0x616750): the purchase is queued as a meta game unlock and the displays are
        // refreshed as if the power were bought but not yet equipped.
        private void Purchase()
        {
            if (m_currentSelectedDefinition == null)
                return;
            m_dreamPowerStoreManager.Purchase(m_currentSelectedDefinition);
            AnalyticsEventCollector.DreamPowerBoughtEvent(m_currentSelectedDefinition);
            List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
            unlocks.Add(new MetaGameUnlockDreamPowerStoreItem(m_currentSelectedDefinition));
            m_collectableCounter.Refresh();
            ActionLabel.Value = m_actionEquippableString;
            ActionAvailable.Value = m_allowEquipping;
            UpdateCounter();
            m_blueCoinCost.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(m_currentSelectedDefinition, UIDreamPowerStoreItemState.Bought));
            RefreshDreamPowers();
        }

        // 0x060030c9 (0x6139E0): m_allowEquipping and the counter format have field defaults; the
        // combined icon list stays null until Unity fills it.
        public UIContainerDreamPowerStore() { }
    }
}
