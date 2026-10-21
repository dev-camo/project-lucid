using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009bc; bodies translated from the 1.10.1 x86-64 binary.
    // An ornament thumbnail: locked or unlocked art, new marker and the selected outline.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetOrnamentToggle : UIWidgetToggle
    {
        [SerializeField] private Image m_backgroundImage;
        [SerializeField] private Sprite m_backgroundSpriteLocked;
        [SerializeField] private Sprite m_backgroundSpriteUnlocked;
        [SerializeField] private RawImage m_ornamentImage;
        [SerializeField] private Color m_ornamentImageLockedColour;
        [SerializeField] private Image m_borderImage;
        [SerializeField] private Sprite m_borderSpriteLocked;
        [SerializeField] private Sprite m_borderSpriteUnlocked;
        [SerializeField] private Image m_selectedOutline;
        [SerializeField] private UIToggleGroupElement m_groupElement;
        [SerializeField] private GameObject m_newUnlockIcon;
        [SerializeField] private HLAudioSfxComponent m_sfxComponent;
        private Action<RectTransform> m_onHover;
        private bool m_isNew;
        private OrnamentDefinition m_definition;

        // 0x06003817 (0x69EE60): UIWidgetToggle.Setup is not called. A toggle reused for another
        // ornament is first reset to unselected and interactable. A default-selected toggle runs
        // the selected path (the native body inlines OnToggleValueChanged without the sound).
        public override void Setup(IUIWidgetParameters parameters)
        {
            UIWidgetOrnamentToggleParameters toggleParameters = parameters.GetAs<UIWidgetOrnamentToggleParameters>();
            if (m_definition != null && m_definition != toggleParameters.Definition)
            {
                m_selectedOutline.enabled = false;
                m_toggle.Interactable = true;
                SetSelectedWithoutNotify(false);
            }
            m_definition = toggleParameters.Definition;
            m_onHover = toggleParameters.HoverCallback;
            m_ornamentImage.texture = m_definition.ThumbnailAsset.Load();
            m_ornamentImage.color = toggleParameters.Available ? Color.white : m_ornamentImageLockedColour;
            m_backgroundImage.sprite = toggleParameters.Available ? m_backgroundSpriteUnlocked : m_backgroundSpriteLocked;
            m_borderImage.sprite = toggleParameters.Available ? m_borderSpriteUnlocked : m_borderSpriteLocked;
            Label.Value = toggleParameters.Label;
            m_isNew = toggleParameters.Available && toggleParameters.IsNew;
            m_newUnlockIcon.SetActive(m_isNew);
            m_toggle.OnValueChanged.RemoveAllListeners();
            m_toggle.OnValueChanged.AddListener(selected => OnToggleValueChanged(selected, toggleParameters));
            m_groupElement.Group = toggleParameters.ToggleGroup;
            m_selectedOutline.enabled = false;
            if (toggleParameters.DefaultSelected)
            {
                OnToggleValueChanged(true, toggleParameters, true);
                SetSelectedWithoutNotify(true);
            }
        }

        // 0x06003818 (0x69F1B0): selecting reports the ornament, outlines it, locks the toggle
        // and clears the new marker; the select sound is skipped for the default selection.
        private void OnToggleValueChanged(bool selected, UIWidgetOrnamentToggleParameters toggleParameters, bool isDefaultSelected = false)
        {
            if (selected)
            {
                toggleParameters.SelectCallback(toggleParameters.Definition, toggleParameters.Available, m_isNew);
                m_selectedOutline.enabled = true;
                m_toggle.Interactable = false;
                toggleParameters.IsNew = false;
                m_isNew = false;
                m_newUnlockIcon.SetActive(false);
                if (!isDefaultSelected)
                    m_sfxComponent.PlayOneShotAtSource();
            }
            else
            {
                m_selectedOutline.enabled = false;
                m_toggle.Interactable = true;
            }
        }

        // 0x06003819 (0x69F290)
        public void Action_OnSelected()
        {
            m_onHover((RectTransform)transform);
        }

        // 0x0600381a (0x69F300)
        private void OnDestroy()
        {
            m_definition.ThumbnailAsset.Unload();
        }

        // 0x0600381b (0x69F350)
        public UIWidgetOrnamentToggle() { }
    }
}
