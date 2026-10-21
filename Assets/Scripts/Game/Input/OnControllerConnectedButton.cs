using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HardlightProject
{
    // Original Game.Runtime 020006b2; bodies translated from the 1.10.1 x86-64 binary, 0x58BA50..0x58C380.
    // Presses its game input once at start when the Apple controller provider is in use; connecting
    // controllers only update the input type.
    public class OnControllerConnectedButton : MonoBehaviour, IInputButton
    {
        // 060023eb..060023f0: compiler-generated accessors.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;

        private GameInput m_inputToSupply = GameInput.ControllerConnected;
        private ControlMapping m_controlMapping;
        private InputType m_inputType;
        private readonly InputTrigger m_inputTrigger = InputTrigger.Down;
        private HLInputModule m_hlInputModule;
        private ApplePluginControllerProvider m_applePluginControllerProvider;
        private bool m_systemEnabled;

        // 060023f1: the input module must already be an HLInputModule (a plain cast, not "as");
        // any other controller provider leaves the button inert.
        private void Start()
        {
            m_controlMapping = ProcessManager.GetSystem<InputSystem>().ControlMapping;
            m_controlMapping.RegisterButtonProvider(this);
            m_hlInputModule = (HLInputModule)EventSystem.current.currentInputModule;
            if (m_hlInputModule.BaseControllerProvider is ApplePluginControllerProvider provider)
            {
                m_systemEnabled = true;
                m_applePluginControllerProvider = provider;
                GCControllerService.ControllerConnected += SetActiveControllerType;
                TryGetActiveControllerType();
                if (m_inputType != InputType.Unsupported)
                {
                    SimulateButtonPress();
                }
            }
        }

        // 060023f2: a Siri Remote never changes the input type.
        private void SetActiveControllerType(object sender, ControllerConnectedEventArgs args)
        {
            if (args.Controller.Handle.GetControllerType() != GCControllerType.SiriRemote)
            {
                m_inputType = ConvertGCControllerTypeToInputType(args.Controller.Handle.GetControllerType());
            }
        }

        // 060023f3: the last non-Siri-Remote controller in the provider list decides the input type.
        private void TryGetActiveControllerType()
        {
            if (!m_systemEnabled)
            {
                return;
            }
            IReadOnlyList<GCController> controllers = m_applePluginControllerProvider.GetControllers();
            for (int i = controllers.Count - 1; i >= 0; i--)
            {
                GCControllerType controllerType = controllers[i].Handle.GetControllerType();
                if (controllerType == GCControllerType.SiriRemote)
                {
                    continue;
                }
                m_inputType = ConvertGCControllerTypeToInputType(controllerType);
                return;
            }
        }

        // 060023f4
        private void SimulateButtonPress()
        {
            if (!m_systemEnabled)
            {
                return;
            }
            switch (m_inputTrigger)
            {
                case InputTrigger.Held:
                    ButtonHandlers?.Invoke(0, m_inputToSupply, 1f, m_inputType);
                    break;
                case InputTrigger.Down:
                    ButtonDownHandlers?.Invoke(0, m_inputToSupply, 1f, m_inputType);
                    break;
                case InputTrigger.Up:
                    ButtonUpHandlers?.Invoke(0, m_inputToSupply, 1f, m_inputType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 060023f5: native table 0x29A79B4; everything outside the four mapped types is Unsupported.
        private InputType ConvertGCControllerTypeToInputType(GCControllerType gcControllerType)
        {
            switch (gcControllerType)
            {
                case GCControllerType.Unknown:
                    return InputType.MfiController;
                case GCControllerType.XboxOne:
                    return InputType.XboneController;
                case GCControllerType.DualShock:
                    return InputType.PS4Controller;
                case GCControllerType.DualSense:
                    return InputType.PS5Controller;
                default:
                    return InputType.Unsupported;
            }
        }

        // 060023f6
        private void OnDestroy()
        {
            m_controlMapping.UnregisterButtonProvider(this);
            if (m_systemEnabled)
            {
                GCControllerService.ControllerConnected -= SetActiveControllerType;
                m_systemEnabled = false;
            }
        }
    }
}
