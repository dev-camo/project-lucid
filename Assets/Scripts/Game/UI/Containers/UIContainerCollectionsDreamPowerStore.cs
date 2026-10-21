using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000869 and nested 0x0200086a; bodies translated from the 1.10.1 x86-64 binary.
    // The dream power store as a tab of the collections screen.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerCollectionsDreamPowerStore : UIContainerDreamPowerStore
    {
        [SerializeField]
        [Header("Info panel")]
        private UIWidgetButtonImageBase m_infoButton;
        [SerializeField] private GameObject m_infoGlyph;
        public readonly Bindable<Strings> Title = new Bindable<Strings>();

        // 0x060030a5/0x060030a6: ordinary atomic event accessors; the field starts as an empty callback.
        public event Action<Strings> OnSelectionChanged = delegate { };

        // 0x060030a7 (0x613010): the info button and glyph are optional parts of the prefab and are
        // switched off through the Unity null check.
        protected override void OnEnable()
        {
            base.OnEnable();
            Title.Value = Strings.MENU_DREAM_POWER_COLLECTIONS_TITLE;
            if (m_infoButton != null)
                m_infoButton.SetInteractable(false);
            if (m_infoGlyph != null)
                m_infoGlyph.SetActive(false);
            UpdateTitle();
        }

        // 0x060030a8 (0x613360): hides the archetype art without an archetype; otherwise the image loads asynchronously.
        private void UpdateTitle()
        {
            if (m_characterArchetype != CharacterArchetype.None)
            {
                HasArchetype.Value = true;
                m_dataManager.CharacterArchetypes[m_characterArchetype].UIImage.LoadAsync(texture => CharacterArchetypeImage.Value = texture);
            }
            else
            {
                HasArchetype.Value = false;
            }
        }

        // 0x060030a9 (0x613450): reports "Unlock" while the selection can be bought and no prompt otherwise.
        protected override void OnDreamPowerChanged(DreamPowerDefinition definition, UIDreamPowerStoreItemState itemState)
        {
            base.OnDreamPowerChanged(definition, itemState);
            OnSelectionChanged(itemState == UIDreamPowerStoreItemState.Purchasable ? Strings.COMMON_MENU_UNLOCK : Strings.NONE);
        }

        // 0x060030aa (0x613850)
        public void OnNavigatedAway()
        {
            Purchasable.Value = false;
        }

        // 0x060030ab (0x613880): the title binder and the empty selection callback are created before the base constructor.
        public UIContainerCollectionsDreamPowerStore() { }
    }
}
