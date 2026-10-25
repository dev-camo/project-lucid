using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // Test-only concrete host for the abstract original InputBridge, created
    // with ScriptableObject.CreateInstance. It records every route used by
    // UIRuntimeConfiguration, UIInputTypeVisibilityController and HLSelectable,
    // answers from its public fields, and fails loudly on any other route.
    public sealed class PlayModeInputBridgeProbe : InputBridge
    {
        public UIInputType LastInputType = UIInputType.Mouse;
        public UIInputType SelectionInputType = UIInputType.DefaultController;
        public bool HardwareCursorVisible = true;
        public bool ModuleExists;
        public GameObject SelectedObject;
        public Action BeforeLastInputType;
        public HLInputBridge DeselectionBridge;
        public readonly List<GameObject> DeselectionRequests = new List<GameObject>();
        public readonly List<string> Calls = new List<string>();

        public override void SetInputModule(BaseInputModule inputModule) => Calls.Add("SetInputModule");
        public override void RegisterForInputUpdateEvents() => Calls.Add("RegisterForInputUpdateEvents");
        public override void UnregisterForInputUpdateEvents() => Calls.Add("UnregisterForInputUpdateEvents");

        public override UIInputType GetLastInputType()
        {
            Calls.Add("GetLastInputType");
            BeforeLastInputType?.Invoke();
            return LastInputType;
        }

        public override bool DoesInputTypeRequireSelection(UIInputType inputType)
        {
            Calls.Add("DoesInputTypeRequireSelection " + inputType);
            return inputType == SelectionInputType;
        }

        public override bool GetHardwareCursorVisibility()
        {
            Calls.Add("GetHardwareCursorVisibility");
            return HardwareCursorVisible;
        }

        public override bool InputModuleExists()
        {
            Calls.Add("InputModuleExists");
            return ModuleExists;
        }

        public override bool IsObjectSelected(GameObject selectedObject)
        {
            Calls.Add("IsObjectSelected");
            return selectedObject != null && selectedObject == SelectedObject;
        }

        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null) =>
            Calls.Add("SetSelectedGameObjectWithMemory " + (selectableGameObject == null ? "null" : selectableGameObject.name));

        public override void SetHardwareCursorVisibility(bool visibility) => throw Unexpected();
        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject)
        {
            if (DeselectionBridge == null) throw Unexpected();
            DeselectionRequests.Add(gameObject);
            DeselectionBridge.DeselectObjectIfCurrentlySelected(gameObject);
        }
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();
        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1) => throw Unexpected();

        private static Exception Unexpected() => new NotSupportedException("Play-mode input bridge probe route is outside its fixture");
    }
}
