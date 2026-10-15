using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only concrete host for the abstract original InputBridge. It answers
    // only the selection query and last input type; lifecycle hooks used by
    // UIRuntimeConfiguration are recorded, and any other route fails loudly.
    public sealed class OwnedInputBridgeProbe : InputBridge
    {
        public UIInputType LastInputType;
        public UIInputType SelectionInputType;
        public readonly List<string> Calls = new List<string>();

        public override bool DoesInputTypeRequireSelection(UIInputType inputType)
        {
            Calls.Add("DoesInputTypeRequireSelection " + inputType);
            return inputType == SelectionInputType;
        }

        public override UIInputType GetLastInputType()
        {
            Calls.Add("GetLastInputType");
            return LastInputType;
        }

        public override void SetInputModule(BaseInputModule inputModule) => Calls.Add("SetInputModule");
        public override void RegisterForInputUpdateEvents() => Calls.Add("RegisterForInputUpdateEvents");
        public override void UnregisterForInputUpdateEvents() => Calls.Add("UnregisterForInputUpdateEvents");

        public override bool InputModuleExists() => throw Unexpected();
        public override void SetHardwareCursorVisibility(bool visibility) => throw Unexpected();
        public override bool GetHardwareCursorVisibility() => throw Unexpected();
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject) => throw Unexpected();
        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null) => throw Unexpected();
        public override bool IsObjectSelected(GameObject selectedObject) => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();

        private static Exception Unexpected() => new NotSupportedException("Owned input bridge probe route is outside its fixture");
    }
}
