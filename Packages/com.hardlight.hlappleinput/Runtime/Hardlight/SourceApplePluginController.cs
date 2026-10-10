using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000014. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public sealed class SourceApplePluginController : Hardlight.IApplePluginControllerInputKeySource, Hardlight.IBaseInputKeySource<Apple.GameController.Controller.GCControllerInputName>, Hardlight.IBaseInputGlyphSource<Apple.GameController.Controller.GCControllerInputName>, Hardlight.IApplePluginControllerInputAxisSource, Hardlight.IBaseInputAxisSource<Apple.GameController.Controller.GCControllerInputName>, Hardlight.IApplePluginControllerGlyphSource
    {
        private readonly Hardlight.IControllerProvider<Apple.GameController.Controller.GCController> m_controllerProvider;
        private Apple.GameController.Controller.GCControllerSymbolScale m_glyphSymbolScale = GCControllerSymbolScale.Medium;
        private Apple.GameController.Controller.GCControllerRenderingMode m_glyphRenderingMode = GCControllerRenderingMode.Automatic;
        private System.Single m_glyphPointSize = 60f;
        private Apple.GameController.Controller.GCControllerSymbolWeight m_glyphSymbolWeight = GCControllerSymbolWeight.Medium;
        private System.Boolean m_useGlyphFilledStyle = true;

        // Original 0x06000037; complete ARM64 and x86-64 bodies retained.
        public SourceApplePluginController(Hardlight.IControllerProvider<Apple.GameController.Controller.GCController> controllerProvider)
        {
            m_controllerProvider = controllerProvider;
        }

        // Original 0x06000038; complete ARM64 and x86-64 bodies retained.
        public System.Boolean GetKey(Apple.GameController.Controller.GCControllerInputName inputName, System.Int32 joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return Mathf.Abs(controllers[joystickIndex].GetInputValue(inputName)) >= 0.25f;
        }

        // Original 0x06000039; complete ARM64 and x86-64 bodies retained.
        public System.Boolean GetKeyDown(Apple.GameController.Controller.GCControllerInputName inputName, System.Int32 joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return controllers[joystickIndex].GetButtonDown(inputName, 0.25f);
        }

        // Original 0x0600003a; complete ARM64 and x86-64 bodies retained.
        public System.Boolean GetKeyUp(Apple.GameController.Controller.GCControllerInputName inputName, System.Int32 joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return controllers[joystickIndex].GetButtonUp(inputName, 0.25f);
        }

        // Original 0x0600003b; complete ARM64 and x86-64 bodies retained.
        public System.Single GetAxis(Apple.GameController.Controller.GCControllerInputName inputName, System.Int32 joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return 0f;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return 0f;
            return controllers[joystickIndex].GetInputValue(inputName);
        }

        // Original 0x0600003c; complete ARM64 and x86-64 bodies retained.
        public void Update()
        {
            if (m_controllerProvider == null)
                return;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            for (int i = 0; i < controllers.Count; ++i)
                controllers[i].Poll();
        }

        // Original 0x0600003d; complete ARM64 and x86-64 bodies retained.
        public System.Boolean TryGetGlyph(Apple.GameController.Controller.GCControllerInputName inputName, System.Int32 joystickIndex, out UnityEngine.Texture2D texture)
        {
            texture = null;
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            GCController controller = controllers[joystickIndex];
            // The four discrete D-pad glyph requests share the horizontal D-pad symbol.
            if (inputName == GCControllerInputName.DpadLeft || inputName == GCControllerInputName.DpadRight ||
                inputName == GCControllerInputName.DpadUp || inputName == GCControllerInputName.DpadDown)
                inputName = GCControllerInputName.DpadHorizontal;
            texture = controller.GetSymbolForInputName(inputName, m_glyphSymbolScale, m_glyphRenderingMode,
                m_glyphPointSize, m_glyphSymbolWeight, m_useGlyphFilledStyle);
            return texture != null;
        }

        // Original 0x0600003e; complete ARM64 and x86-64 bodies retained.
        public void SetGlyphRenderSettings(Apple.GameController.Controller.GCControllerSymbolScale symbolScale, Apple.GameController.Controller.GCControllerRenderingMode renderingMode, System.Single pointSize, Apple.GameController.Controller.GCControllerSymbolWeight symbolWeight, System.Boolean useFillStyle)
        {
            m_glyphSymbolScale = symbolScale;
            m_glyphRenderingMode = renderingMode;
            m_glyphPointSize = pointSize;
            m_glyphSymbolWeight = symbolWeight;
            m_useGlyphFilledStyle = useFillStyle;
        }

    }
}
