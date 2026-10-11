// Preservation reconstruction of complete original Hardlight.UIHighlighter in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIHighlighter
    {
        private UINavigationManager NavigationManager { get; }
        private UIHighlightDefinition DefaultHighlight { get; }
        private UIHighlight ActiveHighlight { get; set; }

        public UIHighlighter(UIHighlightDefinition defaultHighlight, UINavigationManager navigationManager)
        {
            NavigationManager = navigationManager;
            DefaultHighlight = defaultHighlight;
            NavigationManager = navigationManager;
        }

        public void Highlight(UIWidgetNavigation navigableWidget)
        {
            if (!NavigationManager.CanBeNavigated(navigableWidget)) return;
            if (!NavigationManager.CanBeHighlighted()) return;
            Move(navigableWidget);
        }

        public void Destroy()
        {
            if (ActiveHighlight == null) return;
            Object.Destroy(ActiveHighlight.gameObject);
            ActiveHighlight = null;
        }

        private void Move(UIWidgetNavigation navigableWidget)
        {
            EnsureCorrectHighlight(navigableWidget);
            if (ActiveHighlight == null) return;
            ActiveHighlight.gameObject.SetActive(navigableWidget.ShowHighlight);
            ActiveHighlight.transform.SetParent(navigableWidget.GetHighlightParent(), false);
            if (navigableWidget.ShowHighlight) ActiveHighlight.OnShowHighlight();
        }

        private void EnsureCorrectHighlight(UIWidgetNavigation navigableWidget)
        {
            bool hasHighlight = ActiveHighlight != null;
            UIHighlightDefinition definition = navigableWidget.Highlighter == null ? DefaultHighlight : navigableWidget.Highlighter;
            if (hasHighlight)
            {
                if (ActiveHighlight.Definition == definition) return;
                Object.Destroy(ActiveHighlight.gameObject);
            }
            ActiveHighlight = Object.Instantiate(definition.Prefab, navigableWidget.GetHighlightParent());
        }
    }
}
