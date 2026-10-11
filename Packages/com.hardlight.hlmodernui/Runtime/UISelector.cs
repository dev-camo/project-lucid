// Preservation reconstruction of complete original Hardlight.UISelector in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UISelector
    {
        public InputBridge InputBridge { get; }
        public UIWidgetNavigation CurrentlySelected { get; private set; }
        private UINavigationConfiguration Configuration { get; }
        private UINavigationManager NavigationManager { get; }

        public UISelector(UINavigationConfiguration configuration, InputBridge inputBridge, UINavigationManager navigationManager)
        {
            Configuration = configuration;
            InputBridge = inputBridge;
            NavigationManager = navigationManager;
        }

        public void Select(UIWidgetNavigation navigableWidget)
        {
            if (!NavigationManager.IsActive) return;
            if (!NavigationManager.CanBeNavigated(navigableWidget)) return;
            DeselectCurrentSelection();
            CurrentlySelected = navigableWidget;
            InputBridge.SetSelectedGameObjectWithMemory(CurrentlySelected.gameObject);
            navigableWidget.OnSelect();
        }

        public void SelectInDirection(Vector3 direction)
        {
            UIWidgetNavigation navigableWidget;
            if (CurrentlySelected != null)
            {
                RectTransform rectTransform = (RectTransform)CurrentlySelected.transform;
                Vector3 localDirection = Quaternion.Inverse(rectTransform.rotation) * direction;
                Vector3 position = CurrentlySelected.transform.TransformPoint(rectTransform.GetPointOnRectEdge(localDirection));
                navigableWidget = NavigationManager.FindWidget(direction, position, CurrentlySelected);
            }
            else
                navigableWidget = NavigationManager.FindWidget(direction, Vector3.zero, null);
            if (navigableWidget == null) return;
            Select(navigableWidget);
        }

        public void DeselectCurrentSelection()
        {
            if (CurrentlySelected == null) return;
            InputBridge.DeselectObjectIfCurrentlySelected(CurrentlySelected.gameObject);
            CurrentlySelected.OnDeselect();
            CurrentlySelected = null;
        }

        public void EnableSystem()
        {
            SubscribeIfValid(Configuration.SelectionLeftInput, OnLeft);
            SubscribeIfValid(Configuration.SelectionRightInput, OnRight);
            SubscribeIfValid(Configuration.SelectionUpInput, OnUp);
            SubscribeIfValid(Configuration.SelectionDownInput, OnDown);
        }

        public void DisableSystem()
        {
            UnsubscribeIfValid(Configuration.SelectionLeftInput, OnLeft);
            UnsubscribeIfValid(Configuration.SelectionRightInput, OnRight);
            UnsubscribeIfValid(Configuration.SelectionUpInput, OnUp);
            UnsubscribeIfValid(Configuration.SelectionDownInput, OnDown);
            if (CurrentlySelected != null) DeselectCurrentSelection();
        }

        private static void SubscribeIfValid(GameInput gameInput, Action<float> callback)
        {
            if ((int)gameInput != 0) ControlMapping.Subscribe(gameInput, callback);
        }

        private static void UnsubscribeIfValid(GameInput gameInput, Action<float> callback)
        {
            if ((int)gameInput != 0) ControlMapping.Unsubscribe(gameInput, callback);
        }

        private void OnLeft(float value) { NavigationManager.SelectInDirection(Vector3.left); }
        private void OnRight(float value) { NavigationManager.SelectInDirection(Vector3.right); }
        private void OnUp(float value) { NavigationManager.SelectInDirection(Vector3.up); }
        private void OnDown(float value) { NavigationManager.SelectInDirection(Vector3.down); }
    }
}
