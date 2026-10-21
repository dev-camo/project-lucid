using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200088d; bodies translated from the 1.10.1 x86-64 binary.
    // The character select screen: shows the characters the active mission allows and swaps to the chosen one.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerCharacterSelect : UIContainer
    {
        [SerializeField] private UIModernEvent m_continueEvent;
        [SerializeField] private SerializableDictionary<CharacterId, GameObject> m_characterIdToGameObject = new SerializableDictionary<CharacterId, GameObject>(HardlightEnumComparers.CharacterIdComparer);
        [SerializeField] private UIContainerNavigation m_uiContainerNavigation;
        private App m_app;
        private CharacterManager m_characterManager;
        private List<CharacterId> m_characterIds = new List<CharacterId>();

        // 0x0600316a: gameplay control is taken away while the screen is open, and navigation
        // starts on the widget that belongs to the current character.
        private void Start()
        {
            Character character = null;
            m_app = ProcessManager.GetSystem<App>();
            m_characterManager = ProcessManager.GetSystem<CharacterManager>();
            UINavigationManager navigationManager = ProcessManager.GetSystem<UINavigationManager>();
            m_app.SetHasGameplayControl(false);
            UpdateCharacters();
            if (navigationManager.IsActive && m_characterManager.TryGetCurrentCharacter(out character))
            {
                GameObject characterObject = m_characterIdToGameObject[character.IdType];
                foreach (UIWidgetNavigation widget in m_uiContainerNavigation.NavigableWidgets)
                {
                    if (!(widget.gameObject != characterObject))
                        navigationManager.Select(widget);
                }
            }
        }

        // 0x0600316b
        private void OnDestroy()
        {
            m_app.SetHasGameplayControl(true);
        }

        // 0x0600316c
        public void Action_OnCharacterSelected(CharacterDefinition characterDefinition)
        {
            SetCharacter(characterDefinition.Id);
        }

        // 0x0600316d: only swaps when the chosen character is not the current one; the continue event fires either way.
        private void SetCharacter(CharacterId characterId)
        {
            if (!m_characterManager.TryGetCurrentCharacter(out Character character) || character.IdType != characterId)
                m_characterManager.SwapCharacter(characterId, false);
            m_app.AddUniqueUIModernEvent(m_continueEvent);
        }

        // 0x0600316e
        private void UpdateCharacters()
        {
            ProcessManager.GetSystem<MissionManager>().GetCharactersForActiveMission(ref m_characterIds);
            foreach (var (characterId, characterObject) in m_characterIdToGameObject)
                characterObject.SetActive(m_characterIds.Contains(characterId));
        }

        // 0x0600316f: the dictionary and list initialisers precede the base constructor.
        public UIContainerCharacterSelect() { }
    }
}
