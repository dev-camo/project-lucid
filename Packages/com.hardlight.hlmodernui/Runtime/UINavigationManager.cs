// Preservation reconstruction of original HLModernUI.Runtime.dll Hardlight.UINavigationManager.
// All 23 original methods are represented. Exact C# local names, grouping and inlined-helper spelling
// are inferred, not recovered text; unchecked native null/array fault behavior is not C# parity.
// Native Select/SelectInDirection inline the genuine UIHighlighter.Highlight checks before Move.
// RegisterExclusiveContainer registers first, deselects on an empty widget array, and otherwise
// selects element zero only when no child is already selected; genuine provider algorithms remain held.
// Shutdown removes only the input-type callback. It does not invent registry/unsubscription cleanup.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UINavigationManager : ISystem
    {
        private UIHighlighter UIHighlighter { get; }
        private UINavigator UINavigator { get; }
        private UISelector UISelector { get; }

        private readonly UIRuntimeConfiguration m_uiRuntimeConfig;
        private bool m_isActive;
        private Action<bool> m_onActiveValueChanged;

        public bool IsActive
        {
            get { return m_isActive; }
            private set
            {
                m_isActive = value;
                m_onActiveValueChanged?.Invoke(value);
            }
        }

        public UINavigationManager()
        {
            UINavigationConfiguration configuration = SystemConfiguration.GetConfig<UINavigationConfiguration>();
            UIHighlighter = new UIHighlighter(configuration.DefaultHighlightPrefab, this);
            UINavigator = new UINavigator();
            m_uiRuntimeConfig = ProcessManager.GetSystem<UIRuntimeConfiguration>();
            UISelector = new UISelector(configuration, m_uiRuntimeConfig.InputBridge, this);
            InputBridge.OnUpdateLastInputType += OnUpdateLastInputType;
            OnUpdateLastInputType(m_uiRuntimeConfig.InputBridge.GetLastInputType());
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, OnShutdown);
        }

        private void OnShutdown(object context = null)
        {
            InputBridge.OnUpdateLastInputType -= OnUpdateLastInputType;
        }

        private void OnUpdateLastInputType(UIInputType lastInputType)
        {
            if (UISelector.InputBridge.DoesInputTypeRequireSelection(lastInputType))
                Enable();
            else
                Disable();
        }

        public void Select(UIWidgetNavigation navigableWidget)
        {
            if (!IsActive)
                return;
            UISelector.Select(navigableWidget);
            UIHighlighter.Highlight(navigableWidget);
        }

        private void Deselect()
        {
            UISelector.DeselectCurrentSelection();
            UIHighlighter.Destroy();
        }

        public void SelectInDirection(Vector3 direction)
        {
            if (!IsActive)
                return;
            UISelector.SelectInDirection(direction);
            UIHighlighter.Highlight(UISelector.CurrentlySelected);
        }

        public void RegisterExclusiveContainer(UIContainerNavigation navigableContainer)
        {
            UINavigator.RegisterExclusiveContainer(navigableContainer);
            if (navigableContainer.NavigableWidgets.Length == 0)
            {
                Deselect();
                return;
            }
            if (!navigableContainer.IsAnySelected())
                Select(navigableContainer.NavigableWidgets[0]);
        }

        public void DeregisterExclusiveContainer(UIContainerNavigation navigableContainer)
        {
            UINavigator.DeregisterExclusiveContainer(navigableContainer);
        }

        public void RegisterWidget(UIWidgetNavigation navigableWidget)
        {
            UINavigator.RegisterWidget(navigableWidget);
        }

        public void DeregisterWidget(UIWidgetNavigation navigableWidget)
        {
            if (UISelector.CurrentlySelected == navigableWidget)
                Deselect();
            UINavigator.DeregisterWidget(navigableWidget);
        }

        public UIWidgetNavigation FindWidget(Vector3 direction, Vector3 position, UIWidgetNavigation caller)
        {
            return UINavigator.FindWidget(direction, position, caller);
        }

        public void RegisterOnActiveChangedEvent(Action<bool> callback, bool invokeImmediately)
        {
            if (invokeImmediately)
                callback(IsActive);
            m_onActiveValueChanged += callback;
        }

        public void UnregisterOnActiveChangedEvent(Action<bool> callback)
        {
            m_onActiveValueChanged -= callback;
        }

        public UIWidgetNavigation GetCurrentlySelected()
        {
            return IsActive ? UISelector.CurrentlySelected : null;
        }

        public void Enable()
        {
            if (IsActive)
                Disable();
            IsActive = true;
            UISelector.EnableSystem();
        }

        public void Disable()
        {
            IsActive = false;
            UIHighlighter.Destroy();
            UISelector.DisableSystem();
        }

        public bool CanBeHighlighted()
        {
            return m_uiRuntimeConfig.InputBridge.DoesInputTypeRequireSelection(
                m_uiRuntimeConfig.InputBridge.GetLastInputType());
        }

        public bool CanBeNavigated(UIWidgetNavigation navigableWidget)
        {
            return UINavigator.CanBeNavigated(navigableWidget);
        }
    }
}
