using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008f3; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerOnScreenControls : UIContainer
    {
        public readonly Bindable<bool> DynamicStick = new Bindable<bool>();
        public readonly Bindable<bool> CameraSnapBehindCharacterButtonVisible = new Bindable<bool>();
        public readonly Bindable<bool> TertiaryButtonLeftVisible = new Bindable<bool>();
        public readonly Bindable<bool> TertiaryButtonRightVisible = new Bindable<bool>();
        private SystemRef<MessageManager> m_messageManagerRef;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private Character m_playerCharacter;
        private ScreenControlGroupMessageData m_lastControlGroupMessageData;

        // 060033b4 (1.10.1 RVA 0x66BE80): announces the Default group once with an empty completion.
        private void Awake()
        {
            m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange);
            PublishToggleScreenGroupMessage(ScreenControlsGroup.Default, () => { });
        }

        // 060033b5 (RVA 0x66C150): the tertiary buttons follow the air-stomp button mapping, and only
        // show for a character with either of the two abilities checked below.
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            SaveDataSettings settings = saveManager.GetSaveDataSettings();
            DynamicStick.Value = settings.DynamicMovementStick;
            CameraSnapBehindCharacterButtonVisible.Value = settings.CameraSnapBehindCharacterButtonVisible;
            ButtonMappingType mapping = settings.AirStompAttackMapping;
            bool hasAirStompAttack = m_playerCharacter != null
                && m_playerCharacter.HasAbilityType(ActorAbilityType.Character_AirStompAttack);
            bool hasSpinDashCharge = m_playerCharacter != null
                && m_playerCharacter.HasAbilityType(ActorAbilityType.Character_SpinDashCharge);
            bool hasTertiaryAbility = hasAirStompAttack || hasSpinDashCharge;
            TertiaryButtonLeftVisible.Value = (mapping == ButtonMappingType.All || mapping == ButtonMappingType.Primary)
                && hasTertiaryAbility;
            TertiaryButtonRightVisible.Value = (mapping == ButtonMappingType.All || mapping == ButtonMappingType.Secondary)
                && hasTertiaryAbility;
        }

        // 060033b6 (RVA 0x66C2B0)
        private void OnDestroy()
        {
            UnsubscribeCharacterMessages();
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
        }

        // 060033b7 (RVA 0x66C4A0): None means Default; an in-air request is dropped mid-teleport.
        private void OnRequestScreenControlGroup(in ScreenControlsRequestMessage data, Action completed)
        {
            if (data.OnScreenControlsGroup == ScreenControlsGroup.None)
            {
                PublishToggleScreenGroupMessage(ScreenControlsGroup.Default, completed);
                return;
            }
            if (m_playerCharacter != null && data.OnScreenControlsGroup == ScreenControlsGroup.InAir
                && m_playerCharacter.TeleportIsInProgress())
                return;
            PublishToggleScreenGroupMessage(data.OnScreenControlsGroup, completed);
        }

        // 060033b8 (RVA 0x66C550)
        private void OnCharacterChange(Character character)
        {
            UnsubscribeCharacterMessages();
            m_playerCharacter = character;
            m_messageManagerRef.Get().ComponentMessagesWithCompletion
                .SubscribeToMessage<ScreenControlsRequestMessage>(m_playerCharacter, OnRequestScreenControlGroup);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 060033b9 (RVA 0x66C080)
        private void PublishToggleScreenGroupMessage(ScreenControlsGroup groupToEnable, Action completed)
        {
            MessageManager messageManager = m_messageManagerRef.Get();
            m_lastControlGroupMessageData = new ScreenControlGroupMessageData { GroupToEnable = groupToEnable };
            messageManager.ComponentMessagesWithCompletion
                .PublishMessage<ScreenControlGroupMessageData>(this, m_lastControlGroupMessageData, completed, -1);
        }

        // 060033ba (RVA 0x66C380)
        private void UnsubscribeCharacterMessages()
        {
            if (m_playerCharacter == null || m_messageManagerRef.IsNull()) return;
            m_messageManagerRef.Get().ComponentMessagesWithCompletion
                .UnsubscribeFromMessage<ScreenControlsRequestMessage>(m_playerCharacter, OnRequestScreenControlGroup);
        }

        // 060033bb (RVA 0x66C6D0)
        public ScreenControlGroupMessageData GetLastControlGroupMessageData()
        {
            return m_lastControlGroupMessageData;
        }

        // 060033bc (RVA 0x66C6E0)
        public UIContainerOnScreenControls()
        {
        }
    }
}
