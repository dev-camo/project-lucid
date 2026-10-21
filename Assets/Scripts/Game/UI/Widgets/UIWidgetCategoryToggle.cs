using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009a6; bodies translated from the 1.10.1 x86-64 binary.
    // An ornament category tab: selected or unselected art, the collected count and whether the
    // category holds an unlocked, collected ornament that has not been seen yet.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetCategoryToggle : UIWidgetToggle
    {
        [SerializeField] private Sprite m_selectedBackground;
        [SerializeField] private Sprite m_unselectedBackground;
        [SerializeField] private Sprite m_selectedIcon;
        [SerializeField] private Sprite m_unselectedIcon;
        public readonly Bindable<Sprite> IsSelectedBackground = new Bindable<Sprite>();
        public readonly Bindable<Sprite> IsSelectedIcon = new Bindable<Sprite>();
        public readonly Bindable<bool> IsComplete = new Bindable<bool>();
        public readonly Bindable<bool> HasNewUnlock = new Bindable<bool>();
        public readonly Bindable<int> Collected = new Bindable<int>();
        public readonly Bindable<int> Total = new Bindable<int>();
        private bool m_hasAnyNew;
        private OrnamentCategory m_category;
        private OrnamentManager m_ornamentManager;

        // 0x060037cf: the UIWidgetToggle setup is not called.
        public override void Setup(IUIWidgetParameters parameters)
        {
            UIWidgetCategoryToggleParameters data = parameters.GetAs<UIWidgetCategoryToggleParameters>();
            IsSelectedBackground.Value = data.IsSelected ? m_selectedBackground : m_unselectedBackground;
            IsSelectedIcon.Value = data.IsSelected ? m_selectedIcon : m_unselectedIcon;
            Collected.Value = data.Collected;
            Total.Value = data.Total;
            IsComplete.Value = data.Collected == data.Total;
            m_ornamentManager = ProcessManager.GetSystem<OrnamentManager>(null, true);
            m_category = data.Category;
            m_hasAnyNew = true;
            HasAnyNew(m_category);
        }

        // 0x060037d0
        public override void Action_OnValueChanged(bool selected)
        {
            base.Action_OnValueChanged(selected);
            IsSelectedBackground.Value = selected ? m_selectedBackground : m_unselectedBackground;
            IsSelectedIcon.Value = selected ? m_selectedIcon : m_unselectedIcon;
        }

        // 0x060037d1: for this tab's category, while a new unlock is still assumed, rescans the
        // ornaments for one that is available (unlocked and collected) but not yet seen.
        public void HasAnyNew(OrnamentCategory categoryChanged)
        {
            if (m_category != categoryChanged || !m_hasAnyNew)
                return;
            m_hasAnyNew = false;
            foreach (var (_, status) in m_ornamentManager.OrnamentCollections[categoryChanged])
            {
                if (status.IsAvailable() && !status.Seen)
                {
                    m_hasAnyNew = true;
                    break;
                }
            }
            HasNewUnlock.Value = m_hasAnyNew;
        }

        // 0x060037d2
        public UIWidgetCategoryToggle() { }
    }
}
