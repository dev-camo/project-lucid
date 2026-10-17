using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // Test-only concrete host for the abstract original InputBridge. It records the
    // Action<float> down-listener registrations UISelectableInputListener makes;
    // every other route fails loudly so an unexpected call cannot pass silently.
    public sealed class UISelectableInputListenerBridgeProbe : InputBridge
    {
        public struct Call
        {
            public string Name;
            public InputSupplier Supplier;
            public Action<float> Callback;
            public int JoystickIndex;
        }

        public readonly List<Call> Calls = new List<Call>();

        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) =>
            Calls.Add(new Call { Name = "RegisterOnDown", Supplier = inputSupplier, Callback = callback, JoystickIndex = joystickIndex });

        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) =>
            Calls.Add(new Call { Name = "Unregister", Supplier = inputSupplier, Callback = callback, JoystickIndex = joystickIndex });

        public override void SetInputModule(BaseInputModule inputModule) => throw Unexpected();
        public override bool InputModuleExists() => throw Unexpected();
        public override void SetHardwareCursorVisibility(bool visibility) => throw Unexpected();
        public override bool GetHardwareCursorVisibility() => throw Unexpected();
        public override bool DoesInputTypeRequireSelection(UIInputType inputType) => throw Unexpected();
        public override UIInputType GetLastInputType() => throw Unexpected();
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject) => throw Unexpected();
        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null) => throw Unexpected();
        public override bool IsObjectSelected(GameObject selectedObject) => throw Unexpected();
        public override void RegisterForInputUpdateEvents() => throw Unexpected();
        public override void UnregisterForInputUpdateEvents() => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();

        private static Exception Unexpected() => new NotSupportedException("UISelectableInputListener bridge probe route is outside its fixture");
    }
}
