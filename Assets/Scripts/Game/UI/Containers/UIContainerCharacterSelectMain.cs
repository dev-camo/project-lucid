using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200088f; bodies translated from the 1.10.1 x86-64 binary.
    // The main character select screen: one toggle per character, the character skin entry and the continue button.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterSelectMain : UIContainer
    {
        [SerializeField] private SerializableDictionary<CharacterId, UIWidgetToggle> m_characterIdToGameObject = new SerializableDictionary<CharacterId, UIWidgetToggle>(HardlightEnumComparers.CharacterIdComparer);
        [SerializeField] private UIToggleGroup m_charactersToggleGroup;
        [SerializeField] private UIWidgetButton m_continueButton;
        [SerializeField] private InputEvent m_continueInputEvent;
        [SerializeField] private UIContainerOrbCollectableCounter m_collectableRewardCounter;
        [SerializeField] private UIContainerCharacterSelectEquipDreamPower m_equipDreamPowerContainer;
        [Header("Character skins")]
        [SerializeField] private UIModernEvent m_openSkinSelectionEvent;
        [SerializeField] private RequirementGameplayLevelBase[] m_characterSkinUnlockRequirements = Array.Empty<RequirementGameplayLevelBase>();
        [SerializeField] private PromptTrigger m_characterSkinCtaPromptTrigger;
        public readonly Bindable<Color> ZoneBackgroundStartColour = new Bindable<Color>();
        public readonly Bindable<Color> ZoneBackgroundEndColour = new Bindable<Color>();
        public readonly Bindable<bool> IsChallengeMode = new Bindable<bool>();
        public readonly Bindable<bool> SkinsSelectionActive = new Bindable<bool>();
        private App m_app;
        private List<CharacterId> m_characterIds = new List<CharacterId>();
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();

        // 0x06003177: continuing stays disabled until a character is chosen.
        private void Awake()
        {
            m_app = ProcessManager.GetSystem<App>();
            m_continueButton.SetInteractable(false);
            m_continueInputEvent.enabled = false;
            UIContainerUtilities.SetBackgroundGradientBinders(ZoneBackgroundStartColour, ZoneBackgroundEndColour);
            UpdateCharacters();
            if (UIContainerUtilities.TryGetSelectedLevelAndZone(out _, out GameplayLevelCategory zone))
                m_collectableRewardCounter.UpdateValues(zone);
            ControlMapping.OnUpdateLastInputType += OnInputTypeChanged;
            IsChallengeMode.Value = m_missionManagerRef.Get().ActiveMissionContext is MissionContextChallenge;
            SkinsSelectionActive.Value = Requirements.AreMet(m_characterSkinUnlockRequirements);
        }

        // 0x06003178
        private void Start()
        {
            if (!IsAnyNonDefaultCharacterSkinSelected() && SkinsSelectionActive.Value && m_characterSkinCtaPromptTrigger != null)
                m_characterSkinCtaPromptTrigger.TryShow();
        }

        // 0x06003179: a character counts when its current skin is not unlocked by default.
        private bool IsAnyNonDefaultCharacterSkinSelected()
        {
            foreach (var (_, characterDefinition) in m_dataManagerRef.Get().Characters)
            {
                CharacterSkinDefinition skin = characterDefinition.CurrentSkin;
                if (skin == null)
                    continue;
                if (skin.UnlockedByDefault)
                    continue;
                return true;
            }
            return false;
        }

        // 0x0600317a
        private void OnDestroy()
        {
            ControlMapping.OnUpdateLastInputType -= OnInputTypeChanged;
        }

        // 0x0600317b
        public void Action_OnCharacterSelected(CharacterDefinition characterDefinition)
        {
            SetCharacter(characterDefinition);
        }

        // 0x0600317c: hands the current character to the skin selection screen and opens it.
        public void Action_OpenSkinSelection()
        {
            CharacterId characterId = m_app.Storage.GetValue<CharacterId>(AppFSMKeys.CurrentCharacterId, default, true);
            CharacterDefinition characterDefinition = m_dataManagerRef.Get().Characters[characterId];
            m_app.Storage.SetValue<IUIContainerParameters>(AppFSMKeys.UIContainerParameters, new UIContainerCharacterSkinSelection.UIContainerCharacterSkinSelectionParameters(characterDefinition));
            if (m_characterSkinCtaPromptTrigger != null)
                m_characterSkinCtaPromptTrigger.TryAcknowledgeAndDismiss();
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var message = new UIModernMessage(UIModernEventType.OnClick);
            exchange.PublishMessage(in message, in m_openSkinSelectionEvent, MessageBroadcastType.Both);
        }

        // 0x0600317d: remembers the choice for the next visit and for the mission, then enables continuing.
        public void SetCharacter(CharacterDefinition characterDefinition)
        {
            SetPreferredCharacter(characterDefinition.Id);
            m_app.Storage.SetValue(AppFSMKeys.CurrentCharacterId, characterDefinition.Id);
            m_app.Storage.SetValue(AppFSMKeys.MissionCharacterId, characterDefinition.Id);
            m_charactersToggleGroup.AllowSwitchOff = false;
            m_continueButton.SetInteractable(true);
            m_continueInputEvent.enabled = true;
            m_equipDreamPowerContainer.RefreshPowers(characterDefinition.Archetype);
        }

        // 0x0600317e: only the mission's characters are interactable; the preferred one (or the first allowed one
        // when no preference applies) is selected, once.
        private void UpdateCharacters()
        {
            ProcessManager.GetSystem<MissionManager>().GetCharactersForActiveMission(ref m_characterIds);
            CharacterId preferredCharacter = GetPreferredCharacter();
            bool noneSelected = true;
            foreach (var (characterId, characterToggle) in m_characterIdToGameObject)
            {
                bool allowed = m_characterIds.Contains(characterId);
                characterToggle.SetInteractable(allowed);
                if ((preferredCharacter == CharacterId.None || preferredCharacter == characterId) && noneSelected && allowed)
                {
                    TryNavigateToWidget(characterToggle);
                    noneSelected = false;
                }
            }
        }

        // 0x0600317f: the first remembered character that the mission allows.
        private CharacterId GetPreferredCharacter()
        {
            if (!m_app.Storage.TryGetValue(AppFSMKeys.CharacterSelectionPreference, out List<CharacterId> preferences))
                preferences = new List<CharacterId>();
            foreach (CharacterId characterId in preferences)
            {
                if (m_characterIds.Contains(characterId))
                    return characterId;
            }
            return CharacterId.None;
        }

        // 0x06003180: most recent choice first, without duplicates.
        private void SetPreferredCharacter(CharacterId characterId)
        {
            if (!m_app.Storage.TryGetValue(AppFSMKeys.CharacterSelectionPreference, out List<CharacterId> preferences))
                preferences = new List<CharacterId>();
            preferences.Remove(characterId);
            preferences.Insert(0, characterId);
            m_app.Storage.SetValue(AppFSMKeys.CharacterSelectionPreference, preferences);
        }

        // 0x06003181
        private void TryNavigateToWidget(UIWidgetToggle characterToggle)
        {
            characterToggle.SetSelected(true);
            if (m_navigationManagerRef.IsNull())
                return;
            m_navigationManagerRef.Get().Select(characterToggle.WidgetNavigation);
        }

        // 0x06003182
        private void UpdateRewardCounter()
        {
            if (UIContainerUtilities.TryGetSelectedLevelAndZone(out _, out GameplayLevelCategory zone))
                m_collectableRewardCounter.UpdateValues(zone);
        }

        // 0x06003183: switching to a controller selects the first mission character when no toggle is selected.
        private void OnInputTypeChanged(InputType inputType)
        {
            if (inputType == InputType.Mouse || inputType == InputType.Keyboard || inputType == InputType.Touch)
                return;
            bool noneSelected = true;
            foreach (var (_, characterToggle) in m_characterIdToGameObject)
            {
                if (characterToggle.IsSelected())
                {
                    noneSelected = false;
                    break;
                }
            }
            if (!noneSelected)
                return;
            CharacterId firstCharacter = m_characterIds[0];
            foreach (var (characterId, characterToggle) in m_characterIdToGameObject)
            {
                if (characterId == firstCharacter)
                    TryNavigateToWidget(characterToggle);
            }
        }

        // 0x06003184: field initialisers precede the base constructor.
        public UIContainerCharacterSelectMain() { }
    }
}
