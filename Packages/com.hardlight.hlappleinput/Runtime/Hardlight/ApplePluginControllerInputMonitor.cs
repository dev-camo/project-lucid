using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000f. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public sealed class ApplePluginControllerInputMonitor : Hardlight.ActiveBindingsInputMonitor<Hardlight.ActiveApplePluginControllerInputBindings,Hardlight.ApplePluginControllerInputBinding,Hardlight.ApplePluginControllerBindingData,Apple.GameController.Controller.GCControllerInputName,Apple.GameController.Controller.GCControllerInputName>
    {
        [UnityEngine.SerializeField]
        [UnityEngine.Header("Input Processors")]
        private Hardlight.ApplePluginControllerInputButton m_button;
        [UnityEngine.SerializeField]
        private Hardlight.ApplePluginControllerInputAxis m_axis;
        [UnityEngine.SerializeField]
        private Hardlight.ApplePluginControllerInputVectorised m_vectorisedGameInput;
        [UnityEngine.Header("Glyph Map Suppliers")]
        [UnityEngine.SerializeField]
        private Hardlight.ApplePluginControllerGlyphMap m_glyphMapButtonSupplier;
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Specify a different scale variant for a symbol to change the emphasis of the symbol relative to its adjacent text.")]
        [UnityEngine.Header("Glyph Settings")]
        private Apple.GameController.Controller.GCControllerSymbolScale m_glyphSymbolScale = GCControllerSymbolScale.Medium;
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("The rendering mode controls how UIKit uses color information to display an image. iOS and tvOS only.")]
        private Apple.GameController.Controller.GCControllerRenderingMode m_glyphRenderingMode = GCControllerRenderingMode.Automatic;
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("The system font point size to use for the configuration. Higher value results in higher resolution texture returned.")]
        private System.Single m_glyphPointSize = 60f;
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Weight changes the thickness of the lines of the glyph.")]
        private Apple.GameController.Controller.GCControllerSymbolWeight m_glyphSymbolWeight = GCControllerSymbolWeight.Medium;
        [UnityEngine.Tooltip("Use the 'filled in' variant of the glyph where the text is transparent and the button shape is filled in.")]
        [UnityEngine.SerializeField]
        private System.Boolean m_useGlyphFilledStyle = true;
        public const System.String DefaultFileName = "ApplePluginControllerInputMonitor";
        private readonly Hardlight.SourceApplePluginControllerProvider m_sourceProvider = new SourceApplePluginControllerProvider();
        private System.Func<Apple.GameController.Controller.GCControllerInputName,Hardlight.InputType,System.Int32,UnityEngine.Texture2D> m_getGlyphForGCControllerInputNameAndInputType;

        // Original 0x06000023; complete ARM64 and x86-64 bodies retained.
        protected override Hardlight.BaseSourceProvider BaseSourceProvider
        {
            get { return m_sourceProvider; }
        }

        // Original 0x06000024; complete ARM64 and x86-64 bodies retained.
        protected override void InitialiseControlMap(Hardlight.ControlMap controlMap)
        {
            controlMap.Initialise(m_axis);
            controlMap.Initialise(m_button);
            controlMap.Initialise(m_vectorisedGameInput);
        }

        // Original 0x06000025; complete ARM64 and x86-64 bodies retained.
        protected override void InitialiseInputProcessors(Hardlight.IGlyphLookupSystemSetter glyphLookupSystemSetter, System.Collections.Generic.IReadOnlyList<Hardlight.ApplePluginControllerInputBinding> currentInputBindings)
        {
            Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap =
                new Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>>(HardlightEnumComparers.GameInputComparer);
            m_button.Initialise(ref gameInputGlyphMap, m_getGlyphForGCControllerInputNameAndInputType, m_sourceProvider.KeySource, currentInputBindings);
            m_axis.Initialise(ref gameInputGlyphMap, m_getGlyphForGCControllerInputNameAndInputType, m_sourceProvider.AxisSource, currentInputBindings);
            m_vectorisedGameInput.Initialise(currentInputBindings);
            GlyphLookupSystemSetter.SetGameInputGlyphMap(gameInputGlyphMap);
        }

        // Original 0x06000026; complete ARM64 and x86-64 bodies retained.
        protected override void InitialiseGlyphMapSuppliers()
        {
            m_glyphMapButtonSupplier.Initialise();
            m_getGlyphForGCControllerInputNameAndInputType = GetGlyphForKeyAndInputType;
            m_sourceProvider.GlyphSource.SetGlyphRenderSettings(m_glyphSymbolScale, m_glyphRenderingMode,
                m_glyphPointSize, m_glyphSymbolWeight, m_useGlyphFilledStyle);
        }

        // Original 0x06000027; complete ARM64 and x86-64 bodies retained.
        protected override void ShutdownGlyphMapSuppliers()
        {
            m_getGlyphForGCControllerInputNameAndInputType = null;
            m_glyphMapButtonSupplier.Shutdown();
        }

        // Original 0x06000028; complete ARM64 and x86-64 bodies retained.
        protected override void ShutdownInputProcessors()
        {
            m_button.Shutdown();
            m_axis.Shutdown();
            m_vectorisedGameInput.Shutdown();
        }

        // Original 0x06000029; complete ARM64 and x86-64 bodies retained.
        protected override void ShutdownControlMap(Hardlight.ControlMap controlMap)
        {
            controlMap.Shutdown(m_vectorisedGameInput);
            controlMap.Shutdown(m_axis);
            controlMap.Shutdown(m_button);
        }

        // Original 0x0600002a; complete ARM64 and x86-64 bodies retained.
        public override void Update()
        {
            base.Update();
            m_button.Update();
            m_axis.Update();
            m_vectorisedGameInput.Update();
        }

        // Original 0x0600002b; complete ARM64 and x86-64 bodies retained.
        private UnityEngine.Texture2D GetGlyphForKeyAndInputType(Apple.GameController.Controller.GCControllerInputName key, Hardlight.InputType inputType, System.Int32 joystickIndex)
        {
            Texture2D texture;
            if (m_sourceProvider.GlyphSource.TryGetGlyph(key, joystickIndex, out texture))
                return texture;
            return m_glyphMapButtonSupplier.GetGlyphForKeyAndInputType(key, inputType);
        }

        // Original 0x0600002c; complete ARM64 and x86-64 bodies retained.
        public void OnValidate()
        {
        }

        // Original 0x0600002d; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerInputMonitor()
        {
        }

    }
}
