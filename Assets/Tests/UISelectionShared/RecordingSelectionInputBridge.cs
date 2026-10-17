using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests.UISelectionShared
{
    // Test-only InputBridge for the UISelectable and UISelectableController
    // fixtures. The selection, cursor and input-type routes answer from public
    // fields and record each call with its arguments; input listener routes are
    // outside these fixtures and fail loudly. OwnedInputBridgeProbe is not
    // reused because its selection and cursor routes must keep failing loudly
    // for the UIRuntimeConfiguration fixtures.
    public sealed class RecordingSelectionInputBridge : InputBridge
    {
        public UIInputType LastInputType;
        public UIInputType SelectionInputType;
        public bool CursorVisible;
        public bool ModuleExists;
        public bool ObjectSelected;
        public readonly List<string> Calls = new List<string>();
        public readonly List<GameObject> GameObjects = new List<GameObject>();
        public readonly List<BaseEventData> EventData = new List<BaseEventData>();

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

        public override void SetHardwareCursorVisibility(bool visibility) => Calls.Add("SetHardwareCursorVisibility " + visibility);

        public override bool GetHardwareCursorVisibility()
        {
            Calls.Add("GetHardwareCursorVisibility");
            return CursorVisible;
        }

        public override bool InputModuleExists()
        {
            Calls.Add("InputModuleExists");
            return ModuleExists;
        }

        public override bool IsObjectSelected(GameObject selectedObject)
        {
            Calls.Add("IsObjectSelected");
            GameObjects.Add(selectedObject);
            return ObjectSelected;
        }

        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject)
        {
            Calls.Add("DeselectObjectIfCurrentlySelected");
            GameObjects.Add(gameObject);
        }

        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null)
        {
            Calls.Add("SetSelectedGameObjectWithMemory");
            GameObjects.Add(selectableGameObject);
            EventData.Add(eventData);
        }

        public override void SetInputModule(BaseInputModule inputModule) => throw Unexpected();
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

        private static Exception Unexpected() => new NotSupportedException("Recording selection bridge route is outside its fixture");
    }
}
