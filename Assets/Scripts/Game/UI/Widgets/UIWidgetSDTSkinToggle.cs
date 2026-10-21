using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c7; bodies translated from the 1.10.1 x86-64 binary.
    // A character skin toggle on the skin selection screen.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetSDTSkinToggle : UIWidgetToggle
    {
        [SerializeField] private UITransitionAnimation m_uiTransitionAnimation;
        public readonly Bindable<Sprite> Thumbnail = new Bindable<Sprite>();
        public readonly Bindable<bool> IsLocked = new Bindable<bool>();
        public readonly Bindable<bool> IsNew = new Bindable<bool>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private UIWidgetSDTSkinToggleParameters m_parameters;

        // 0x06003858: does not run the base toggle setup; the thumbnail is retained until OnDestroy.
        public override void Setup(IUIWidgetParameters parameters)
        {
            m_parameters = parameters.GetAs<UIWidgetSDTSkinToggleParameters>();
            SetToggleGroup(m_parameters.ToggleGroup);
            Thumbnail.Value = m_parameters.CharacterSkinDefinition.Thumbnail.Load();
            UpdateBinders();
        }

        // 0x06003859: a skin counts as new once it is unlocked, unseen and not unlocked by default.
        private void UpdateBinders()
        {
            if (m_parameters == null)
                return;
            CharacterSkinDefinition skin = m_parameters.CharacterSkinDefinition;
            SaveDataCharacterSkin skinData = m_saveManagerRef.Get().CurrentSave.GetOrCreateSkinData(skin.GetGUID());
            bool locked = !skinData.Unlocked && !skin.UnlockedByDefault;
            IsLocked.Value = locked;
            IsNew.Value = skinData.Unlocked && !skinData.UnlockSeen && !skin.UnlockedByDefault;
            bool selected = m_parameters.ToggleGroup.ActiveToggles().Contains(m_toggle.GroupElement);
            m_uiTransitionAnimation.QueueTransitionToState(selected ? UITransitionState.Selected : locked ? UITransitionState.Disabled : UITransitionState.Normal);
        }

        // 0x0600385a
        public void Action_OnSkinSelected()
        {
            if (m_parameters != null)
                m_parameters.SetCharacterSkin(m_parameters.CharacterSkinDefinition);
            UpdateBinders();
        }

        // 0x0600385b: the flag is ignored, the body matches the parameterless overload.
        public void Action_OnSkinSelected(bool isSelected)
        {
            if (m_parameters != null)
                m_parameters.SetCharacterSkin(m_parameters.CharacterSkinDefinition);
            UpdateBinders();
        }

        // 0x0600385c
        private void SetCharacterSkin()
        {
            if (m_parameters != null)
                m_parameters.SetCharacterSkin(m_parameters.CharacterSkinDefinition);
            UpdateBinders();
        }

        // 0x0600385d
        private void OnDestroy()
        {
            if (m_parameters != null)
                m_parameters.CharacterSkinDefinition.Thumbnail.Unload();
        }

        // 0x0600385e: field initialisers precede the base constructor.
        public UIWidgetSDTSkinToggle() { }

        // Original Game.Runtime 0x020009c8.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class UIWidgetSDTSkinToggleParameters : IUIWidgetParameters
        {
            public readonly CharacterSkinDefinition CharacterSkinDefinition;
            public readonly Action<CharacterSkinDefinition> SetCharacterSkin;
            public readonly UIToggleGroup ToggleGroup;

            // 0x0600385f
            public UIWidgetSDTSkinToggleParameters(CharacterSkinDefinition characterSkinDefinition, Action<CharacterSkinDefinition> setCharacterSkin, UIToggleGroup toggleGroup)
            {
                CharacterSkinDefinition = characterSkinDefinition;
                SetCharacterSkin = setCharacterSkin;
                ToggleGroup = toggleGroup;
            }
        }
    }
}
