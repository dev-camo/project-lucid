using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only InputBridge for a real UINavigationManager without an event system: it answers whether the input type requires
    // selection as the case sets it, and records the objects the navigation selects and deselects instead of reaching an EventSystem.
    public sealed class DreamPowerStoreSelectionBridgeProbe : InputBridge
    {
        public bool RequiresSelection;
        public readonly List<GameObject> Selected = new List<GameObject>();
        public readonly List<GameObject> Deselected = new List<GameObject>();

        public override UIInputType GetLastInputType() => default(UIInputType);
        public override bool DoesInputTypeRequireSelection(UIInputType inputType) => RequiresSelection;
        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null) => Selected.Add(selectableGameObject);
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject) => Deselected.Add(gameObject);

        public override void SetInputModule(BaseInputModule inputModule) { }
        public override bool InputModuleExists() => false;
        public override void SetHardwareCursorVisibility(bool visibility) { }
        public override bool GetHardwareCursorVisibility() => false;
        public override bool IsObjectSelected(GameObject selectedObject) => false;
        public override void RegisterForInputUpdateEvents() { }
        public override void UnregisterForInputUpdateEvents() { }
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) { }
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) { }
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) { }
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) { }
        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) { }
        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) { }
        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) { }
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) { }
    }
}
