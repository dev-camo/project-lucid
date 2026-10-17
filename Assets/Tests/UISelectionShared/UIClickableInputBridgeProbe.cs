using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only concrete host for the abstract original InputBridge. It answers the hardware
    // cursor query and records selection requests made by UIButton pointer handling; every other
    // route fails loudly so an unexpected call cannot pass silently.
    public sealed class UIClickableInputBridgeProbe : InputBridge
    {
        public bool CursorVisible;
        public readonly List<string> Calls = new List<string>();
        public readonly List<GameObject> SelectedObjects = new List<GameObject>();
        public readonly List<BaseEventData> SelectedEventData = new List<BaseEventData>();

        public override bool GetHardwareCursorVisibility()
        {
            Calls.Add("GetHardwareCursorVisibility");
            return CursorVisible;
        }

        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null)
        {
            Calls.Add("SetSelectedGameObjectWithMemory");
            SelectedObjects.Add(selectableGameObject);
            SelectedEventData.Add(eventData);
        }

        public override void SetInputModule(BaseInputModule inputModule) => throw Unexpected();
        public override bool InputModuleExists() => throw Unexpected();
        public override void SetHardwareCursorVisibility(bool visibility) => throw Unexpected();
        public override bool DoesInputTypeRequireSelection(UIInputType inputType) => throw Unexpected();
        public override UIInputType GetLastInputType() => throw Unexpected();
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject) => throw Unexpected();
        public override bool IsObjectSelected(GameObject selectedObject) => throw Unexpected();
        public override void RegisterForInputUpdateEvents() => throw Unexpected();
        public override void UnregisterForInputUpdateEvents() => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();

        private static Exception Unexpected() => new NotSupportedException("UIClickable input bridge probe route is outside its fixture");
    }
}
