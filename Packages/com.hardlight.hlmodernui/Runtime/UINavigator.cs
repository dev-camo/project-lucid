// Preservation reconstruction of complete original Hardlight.UINavigator in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
// GetWidgets returns existing collections; the complete original owner has no iterator child.
// The <= skip guards deliberately preserve unordered NaN branches and first-match ties.
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UINavigator
    {
        private readonly List<UIContainerNavigation> m_exclusiveContainers = new List<UIContainerNavigation>();
        private readonly List<UIWidgetNavigation> m_widgets = new List<UIWidgetNavigation>();

        public UIWidgetNavigation FindWidget(Vector3 direction, Vector3 position, UIWidgetNavigation caller)
        {
            if (caller != null)
            {
                if (!caller.AllowVerticalNavigation) direction.y = 0f;
                if (!caller.AllowHorizontalNavigation) direction.x = 0f;
            }
            UIWidgetNavigation bestWidget = null;
            float bestScore = float.NegativeInfinity;
            foreach (UIWidgetNavigation widget in GetWidgets())
            {
                if (widget == caller) continue;
                if (widget == null) continue;
                if (!CanBeNavigated(widget)) continue;
                RectTransform rectTransform = widget.transform as RectTransform;
                Vector3 centre = rectTransform != null ? (Vector3)rectTransform.rect.center : Vector3.zero;
                Vector3 offset = widget.transform.TransformPoint(centre) - position;
                float dot = Vector3.Dot(direction, offset);
                if (dot <= 0f) continue;
                float score = dot / offset.sqrMagnitude;
                if (score <= bestScore) continue;
                bestScore = score;
                bestWidget = widget;
            }
            return bestWidget;
        }

        public bool CanBeNavigated(UIWidgetNavigation navigableWidget)
        {
            if (navigableWidget == null) return false;
            if (!navigableWidget.gameObject.activeInHierarchy) return false;
            if (!navigableWidget.enabled) return false;
            if (m_exclusiveContainers.TryGetFirst(out UIContainerNavigation container))
                return container.NavigableWidgets.Contains(navigableWidget);
            return true;
        }

        public void RegisterExclusiveContainer(UIContainerNavigation navigableContainer)
        {
            if (navigableContainer.Exclusive) m_exclusiveContainers.AddUnique(navigableContainer);
        }

        public void DeregisterExclusiveContainer(UIContainerNavigation navigableContainer)
        {
            if (m_exclusiveContainers.Count != 0) m_exclusiveContainers.Remove(navigableContainer);
        }

        public void RegisterWidget(UIWidgetNavigation navigableWidget) { m_widgets.AddUnique(navigableWidget); }
        public void DeregisterWidget(UIWidgetNavigation navigableWidget) { m_widgets.Remove(navigableWidget); }

        private IEnumerable<UIWidgetNavigation> GetWidgets()
        {
            if (m_exclusiveContainers.TryGetFirst(out UIContainerNavigation container)) return container.NavigableWidgets;
            return m_widgets;
        }

        public UINavigator() { }
    }
}
