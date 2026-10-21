using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c1; bodies translated from the 1.10.1 x86-64 binary.
    // A character select toggle showing the character's render, optional companion and name.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetSDTCharacterSelect : UIWidgetToggle
    {
        [SerializeField] private CharacterDefinition m_characterDefinition;
        [SerializeField] private UnityEvent<CharacterDefinition> m_onCharacterSelected;
        [SerializeField] private GameObject m_highlight;
        [SerializeField] private GameObject m_highlightContainer;
        public readonly Bindable<Sprite> CharacterRender = new Bindable<Sprite>();
        public readonly Bindable<Sprite> CompanionRender = new Bindable<Sprite>();
        public readonly Bindable<Strings> CharacterName = new Bindable<Strings>();

        // 0x06003827 (0x69F740): the companion render is optional.
        private void Start()
        {
            CharacterRender.Value = m_characterDefinition.Render.Load();
            if (m_characterDefinition.RenderCompanion.IsValid())
                CompanionRender.Value = m_characterDefinition.RenderCompanion.Load();
            CharacterName.Value = m_characterDefinition.Name;
        }

        // 0x06003828 (0x69F820)
        private void OnDestroy()
        {
            m_characterDefinition.Render.Unload();
            m_characterDefinition.RenderCompanion.Unload();
        }

        // 0x06003829 (0x69F880): the highlight is reparented on the next frame.
        public void Action_OnCharacterSelected(bool isSelected)
        {
            if (!isSelected)
                return;
            m_onCharacterSelected?.Invoke(m_characterDefinition);
            CoroutineUtils.OnNextFrame(() =>
            {
                if (m_highlightContainer == null || m_highlight == null)
                    return;
                m_highlight.transform.SetParent(m_highlightContainer.transform);
            });
        }

        // 0x0600382a (0x69F920)
        public UIWidgetSDTCharacterSelect() { }
    }
}
