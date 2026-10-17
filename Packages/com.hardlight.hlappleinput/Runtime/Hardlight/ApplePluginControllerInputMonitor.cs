using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000f. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class ApplePluginControllerInputMonitor
        : ActiveBindingsInputMonitor<
            ActiveApplePluginControllerInputBindings,
            ApplePluginControllerInputBinding,
            ApplePluginControllerBindingData,
            GCControllerInputName,
            GCControllerInputName>
    {
        [SerializeField]
        [Header("Input Processors")]
        private ApplePluginControllerInputButton m_button;
        [SerializeField]
        private ApplePluginControllerInputAxis m_axis;
        [SerializeField]
        private ApplePluginControllerInputVectorised m_vectorisedGameInput;
        [Header("Glyph Map Suppliers")]
        [SerializeField]
        private ApplePluginControllerGlyphMap m_glyphMapButtonSupplier;
        [SerializeField]
        [Tooltip("Specify a different scale variant for a symbol to change the emphasis of the symbol relative to its adjacent text.")]
        [Header("Glyph Settings")]
        private GCControllerSymbolScale m_glyphSymbolScale = GCControllerSymbolScale.Medium;
        [SerializeField]
        [Tooltip("The rendering mode controls how UIKit uses color information to display an image. iOS and tvOS only.")]
        private GCControllerRenderingMode m_glyphRenderingMode = GCControllerRenderingMode.Automatic;
        [SerializeField]
        [Tooltip("The system font point size to use for the configuration. Higher value results in higher resolution texture returned.")]
        private float m_glyphPointSize = 60f;
        [SerializeField]
        [Tooltip("Weight changes the thickness of the lines of the glyph.")]
        private GCControllerSymbolWeight m_glyphSymbolWeight = GCControllerSymbolWeight.Medium;
        [Tooltip("Use the 'filled in' variant of the glyph where the text is transparent and the button shape is filled in.")]
        [SerializeField]
        private bool m_useGlyphFilledStyle = true;
        public const string DefaultFileName = "ApplePluginControllerInputMonitor";
        private readonly SourceApplePluginControllerProvider m_sourceProvider =
            new SourceApplePluginControllerProvider();
        private Func<GCControllerInputName, InputType, int, Texture2D> m_getGlyphForGCControllerInputNameAndInputType;

        protected override BaseSourceProvider BaseSourceProvider
        {
            get { return m_sourceProvider; }
        }

        protected override void InitialiseControlMap(ControlMap controlMap)
        {
            controlMap.Initialise(m_axis);
            controlMap.Initialise(m_button);
            controlMap.Initialise(m_vectorisedGameInput);
        }

        protected override void InitialiseInputProcessors(IGlyphLookupSystemSetter glyphLookupSystemSetter,
            IReadOnlyList<ApplePluginControllerInputBinding> currentInputBindings)
        {
            Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap =
                new Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>>(
                    HardlightEnumComparers.GameInputComparer);
            m_button.Initialise(ref gameInputGlyphMap, m_getGlyphForGCControllerInputNameAndInputType,
                m_sourceProvider.KeySource, currentInputBindings);
            m_axis.Initialise(ref gameInputGlyphMap, m_getGlyphForGCControllerInputNameAndInputType,
                m_sourceProvider.AxisSource, currentInputBindings);
            m_vectorisedGameInput.Initialise(currentInputBindings);
            GlyphLookupSystemSetter.SetGameInputGlyphMap(gameInputGlyphMap);
        }

        protected override void InitialiseGlyphMapSuppliers()
        {
            m_glyphMapButtonSupplier.Initialise();
            m_getGlyphForGCControllerInputNameAndInputType = GetGlyphForKeyAndInputType;
            m_sourceProvider.GlyphSource.SetGlyphRenderSettings(m_glyphSymbolScale, m_glyphRenderingMode,
                m_glyphPointSize, m_glyphSymbolWeight, m_useGlyphFilledStyle);
        }

        protected override void ShutdownGlyphMapSuppliers()
        {
            m_getGlyphForGCControllerInputNameAndInputType = null;
            m_glyphMapButtonSupplier.Shutdown();
        }

        protected override void ShutdownInputProcessors()
        {
            m_button.Shutdown();
            m_axis.Shutdown();
            m_vectorisedGameInput.Shutdown();
        }

        protected override void ShutdownControlMap(ControlMap controlMap)
        {
            controlMap.Shutdown(m_vectorisedGameInput);
            controlMap.Shutdown(m_axis);
            controlMap.Shutdown(m_button);
        }

        public override void Update()
        {
            base.Update();
            m_button.Update();
            m_axis.Update();
            m_vectorisedGameInput.Update();
        }

        private Texture2D GetGlyphForKeyAndInputType(GCControllerInputName key, InputType inputType, int joystickIndex)
        {
            Texture2D texture;
            if (m_sourceProvider.GlyphSource.TryGetGlyph(key, joystickIndex, out texture))
                return texture;
            return m_glyphMapButtonSupplier.GetGlyphForKeyAndInputType(key, inputType);
        }

        public void OnValidate()
        {
        }

        public ApplePluginControllerInputMonitor()
        {
        }
    }
}
