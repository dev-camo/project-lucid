using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only InputBridge that answers the last input type the case sets and does nothing else.
    public sealed class OnScreenControlInputBridgeProbe : InputBridge
    {
        public UIInputType LastInputType;

        public override UIInputType GetLastInputType() => LastInputType;

        public override void SetInputModule(BaseInputModule inputModule) { }
        public override bool InputModuleExists() => false;
        public override void SetHardwareCursorVisibility(bool visibility) { }
        public override bool GetHardwareCursorVisibility() => false;
        public override bool DoesInputTypeRequireSelection(UIInputType inputType) => false;
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject) { }
        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null) { }
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
