using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000014. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class SourceApplePluginController
        : IApplePluginControllerInputKeySource,
          IBaseInputKeySource<GCControllerInputName>,
          IBaseInputGlyphSource<GCControllerInputName>,
          IApplePluginControllerInputAxisSource,
          IBaseInputAxisSource<GCControllerInputName>,
          IApplePluginControllerGlyphSource
    {
        private readonly IControllerProvider<GCController> m_controllerProvider;
        private GCControllerSymbolScale m_glyphSymbolScale = GCControllerSymbolScale.Medium;
        private GCControllerRenderingMode m_glyphRenderingMode = GCControllerRenderingMode.Automatic;
        private float m_glyphPointSize = 60f;
        private GCControllerSymbolWeight m_glyphSymbolWeight = GCControllerSymbolWeight.Medium;
        private bool m_useGlyphFilledStyle = true;

        public SourceApplePluginController(IControllerProvider<GCController> controllerProvider)
        {
            m_controllerProvider = controllerProvider;
        }

        public bool GetKey(GCControllerInputName inputName, int joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return Mathf.Abs(controllers[joystickIndex].GetInputValue(inputName)) >= 0.25f;
        }

        public bool GetKeyDown(GCControllerInputName inputName, int joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return controllers[joystickIndex].GetButtonDown(inputName, 0.25f);
        }

        public bool GetKeyUp(GCControllerInputName inputName, int joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return false;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return false;
            return controllers[joystickIndex].GetButtonUp(inputName, 0.25f);
        }

        public float GetAxis(GCControllerInputName inputName, int joystickIndex)
        {
            if (joystickIndex < 0 || m_controllerProvider == null)
                return 0f;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            if (joystickIndex >= controllers.Count)
                return 0f;
            return controllers[joystickIndex].GetInputValue(inputName);
        }

        public void Update()
        {
            if (m_controllerProvider == null)
                return;
            IReadOnlyList<GCController> controllers = m_controllerProvider.GetControllers();
            for (int i = 0; i < controllers.Count; ++i)
                controllers[i].Poll();
        }

        public bool TryGetGlyph(GCControllerInputName inputName, int joystickIndex, out Texture2D texture)
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

        public void SetGlyphRenderSettings(GCControllerSymbolScale symbolScale, GCControllerRenderingMode renderingMode,
            float pointSize, GCControllerSymbolWeight symbolWeight, bool useFillStyle)
        {
            m_glyphSymbolScale = symbolScale;
            m_glyphRenderingMode = renderingMode;
            m_glyphPointSize = pointSize;
            m_glyphSymbolWeight = symbolWeight;
            m_useGlyphFilledStyle = useFillStyle;
        }
    }
}
