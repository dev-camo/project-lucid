using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "HLInputBridge", menuName = "Hardlight/UI/HLInputBridge")]
    public class HLInputBridge : InputBridge
    {
        private HLInputModule m_inputModule;
        private bool m_listenersRegistered;

        // Original HLUnityUI.Runtime 06000300..06000308.
        public override void SetInputModule(BaseInputModule inputModule)
        {
            m_inputModule = inputModule as HLInputModule;
        }

        public override bool InputModuleExists()
        {
            return m_inputModule != null;
        }

        public override void SetHardwareCursorVisibility(bool visibility)
        {
            m_inputModule.SetHardwareCursorVisibility(visibility);
        }

        public override bool GetHardwareCursorVisibility()
        {
            return m_inputModule.GetHardwareCursorVisibility();
        }

        public override bool DoesInputTypeRequireSelection(UIInputType uiInputType)
        {
            InputType inputType = (InputType)uiInputType;
            return Enum.IsDefined(typeof(InputType), inputType) && m_inputModule.DoesInputTypeRequireSelection(inputType);
        }

        public override UIInputType GetLastInputType()
        {
            if (Enum.IsDefined(typeof(UIInputType), (UIInputType)ControlMapping.LastInputType))
                return (UIInputType)ControlMapping.LastInputType;
            return UIInputType.Unsupported;
        }

        public override void DeselectObjectIfCurrentlySelected(GameObject gameObject)
        {
            m_inputModule.DeselectObjectIfCurrentlySelected(gameObject);
        }

        public override void SetSelectedGameObjectWithMemory(GameObject selectableGameObject = null, BaseEventData eventData = null)
        {
            m_inputModule.SetSelectedGameObjectWithMemory(selectableGameObject, eventData);
        }

        public override bool IsObjectSelected(GameObject selectedObject)
        {
            return m_inputModule.IsObjectSelected(selectedObject);
        }

        // The shipping bodies read this guard but never change it.
        public override void RegisterForInputUpdateEvents()
        {
            if (m_listenersRegistered) return;
            ControlMapping.OnUpdateLastInputType += OnUpdateLastInputTypeBridge;
            HLInputModule.OnShutdown += InternalOnShutdown;
        }

        public override void UnregisterForInputUpdateEvents()
        {
            if (!m_listenersRegistered) return;
            ControlMapping.OnUpdateLastInputType -= OnUpdateLastInputTypeBridge;
        }

        private void OnUpdateLastInputTypeBridge(InputType inputType)
        {
            UIInputType uiInputType = (UIInputType)inputType;
            if (!Enum.IsDefined(typeof(UIInputType), uiInputType))
                uiInputType = UIInputType.Unsupported;
            OnUpdateLastInputType.Invoke(uiInputType);
        }

        private static void InternalOnShutdown()
        {
            HLInputModule.OnShutdown -= InternalOnShutdown;
            if (OnShutdown != null) OnShutdown.Invoke();
        }

        // Failed as-casts are dereferenced before entering ControlMapping.
        public override void RegisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1)
        {
            ControlMapping.Subscribe((inputSupplier as HLInputSupplier).Input, callback, joystickIndex);
        }

        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<Vector2> callback, int joystickIndex = -1)
        {
            ControlMapping.Unsubscribe((inputSupplier as HLInputSupplier).Input, callback, joystickIndex);
        }

        public override void RegisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1)
        {
            ControlMapping.Subscribe((inputSupplier as HLInputSupplier).Input, callback, joystickIndex);
        }

        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<List<Vector2>> callback, int joystickIndex = -1)
        {
            ControlMapping.Unsubscribe((inputSupplier as HLInputSupplier).Input, callback, joystickIndex);
        }

        public override void RegisterInputListenerOnDown(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1)
        {
            ControlMapping.Subscribe((inputSupplier as HLInputSupplier).Input, callback, InputTrigger.Down, joystickIndex);
        }

        public override void RegisterInputListenerOnHeld(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1)
        {
            ControlMapping.Subscribe((inputSupplier as HLInputSupplier).Input, callback, InputTrigger.Held, joystickIndex);
        }

        public override void RegisterInputListenerOnUp(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1)
        {
            ControlMapping.Subscribe((inputSupplier as HLInputSupplier).Input, callback, InputTrigger.Up, joystickIndex);
        }

        public override void UnregisterInputListener(InputSupplier inputSupplier, Action<float> callback, int joystickIndex = -1)
        {
            ControlMapping.Unsubscribe((inputSupplier as HLInputSupplier).Input, callback, joystickIndex);
        }

        // Original 06000315: implicit public constructor only calls InputBridge.
    }
}
