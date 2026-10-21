using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000890; bodies translated from the 1.10.1 x86-64 binary.
    // Lists a character's skins as toggles and applies the chosen one.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerCharacterSkinSelection : UIContainer
    {
        [SerializeField] private Transform m_toggleParent;
        [SerializeField] private UIWidgetToggle m_skinTogglePrefab;
        [SerializeField] private UIToggleGroup m_skinToggleGroup;
        [SerializeField] private Animation m_skinSelectedAnimation;
        [SerializeField] private UnityEvent m_onSelectCharacter;
        public readonly Bindable<Sprite> Render = new Bindable<Sprite>();
        public readonly Bindable<Strings> SkinName = new Bindable<Strings>();
        public readonly Bindable<bool> IsLocked = new Bindable<bool>();
        public readonly Bindable<Color> ZoneBackgroundStartColour = new Bindable<Color>();
        public readonly Bindable<Color> ZoneBackgroundEndColour = new Bindable<Color>();
        private CharacterSkinDefinition m_currentSkin;
        private CharacterDefinition m_characterDefinition;
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();

        // 0x06003185: rebuilds the toggle list; the toggle of the character's current skin is selected
        // (and focused when navigation is active) and becomes the displayed skin.
        public override void Setup(IUIContainerParameters parameters)
        {
            m_characterDefinition = parameters.GetAs<UIContainerCharacterSkinSelectionParameters>().CharacterDefinition;
            for (int i = 0; i < m_toggleParent.childCount; i++)
                Object.Destroy(m_toggleParent.GetChild(i).gameObject);
            UINavigationManager navigationManager = ProcessManager.GetSystem<UINavigationManager>();
            foreach (CharacterSkinDefinition skin in m_characterDefinition.CharacterSkins)
            {
                UIWidgetToggle toggle = Object.Instantiate(m_skinTogglePrefab, m_toggleParent);
                toggle.Setup(new UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters(skin, SetCharacterSkin, m_skinToggleGroup));
                bool isCurrentSkin = skin == m_characterDefinition.CurrentSkin;
                toggle.SetSelected(isCurrentSkin);
                if (isCurrentSkin)
                {
                    if (navigationManager.IsActive)
                        navigationManager.Select(toggle.WidgetNavigation);
                    m_currentSkin = skin;
                }
            }
            UIContainerUtilities.SetBackgroundGradientBinders(ZoneBackgroundStartColour, ZoneBackgroundEndColour);
            UpdateCharacterBinders();
        }

        // 0x06003186
        public void SetCharacterSkin(CharacterSkinDefinition skin)
        {
            if (skin == m_currentSkin)
                return;
            UnloadRender();
            m_currentSkin = skin;
            AttemptSetCharacterSkin();
            UpdateCharacterBinders();
        }

        // 0x06003187: a locked skin is shown as locked and is not applied.
        private void AttemptSetCharacterSkin()
        {
            SaveDataCharacterSkin skinData = m_saveManagerRef.Get().CurrentSave.GetOrCreateSkinData(m_currentSkin.GetGUID());
            if (skinData.Unlocked)
            {
                IsLocked.Value = false;
            }
            else
            {
                bool unlockedByDefault = m_currentSkin.UnlockedByDefault;
                IsLocked.Value = !unlockedByDefault;
                if (!unlockedByDefault)
                    return;
            }
            if (m_characterDefinition.SetCharacterSkin(m_currentSkin))
            {
                if (m_onSelectCharacter != null)
                    m_onSelectCharacter.Invoke();
                m_skinSelectedAnimation.Play();
            }
        }

        // 0x06003188
        private void UpdateCharacterBinders()
        {
            Render.Value = m_currentSkin.Thumbnail.Load();
            SkinName.Value = m_currentSkin.Name;
        }

        // 0x06003189
        private void UnloadRender()
        {
            if (m_currentSkin == null)
                return;
            m_currentSkin.Thumbnail.Unload();
        }

        // 0x0600318a
        private void OnDestroy()
        {
            if (m_currentSkin == null)
                return;
            m_currentSkin.Thumbnail.Unload();
        }

        // 0x0600318b: the bindable and system reference initialisers precede the base constructor.
        public UIContainerCharacterSkinSelection() { }

        // Original Game.Runtime 0x02000891.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public sealed class UIContainerCharacterSkinSelectionParameters : IUIContainerParameters
        {
            public readonly CharacterDefinition CharacterDefinition;

            // 0x0600318c
            public UIContainerCharacterSkinSelectionParameters(CharacterDefinition characterDefinition)
            {
                CharacterDefinition = characterDefinition;
            }
        }
    }
}
