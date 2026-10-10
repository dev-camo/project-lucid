using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    public abstract class InputBridge : ScriptableObject
    {
        public static FastAction<UIInputType> OnUpdateLastInputType;
        public static FastAction OnShutdown;

        // Original 06000318..0600032a are genuine zero-pointer abstract declarations.
        public abstract void SetInputModule(BaseInputModule inputModule);
        public abstract bool InputModuleExists();
        public abstract void SetHardwareCursorVisibility(bool visibility);
        public abstract bool GetHardwareCursorVisibility();
        public abstract bool DoesInputTypeRequireSelection(UIInputType inputType);
        public abstract UIInputType GetLastInputType();
        public abstract void DeselectObjectIfCurrentlySelected(GameObject gameObject);
        public abstract void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null);
        public abstract bool IsObjectSelected(GameObject selectedObject);
        public abstract void RegisterForInputUpdateEvents();
        public abstract void UnregisterForInputUpdateEvents();
        public abstract void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1);
        public abstract void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1);
        public abstract void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1);
        public abstract void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1);
        public abstract void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1);
        public abstract void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1);
        public abstract void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1);
        public abstract void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1);

        // Original 0600032b only invokes ScriptableObject's constructor; static actions stay uninitialised.
        protected InputBridge() { }
    }
}
