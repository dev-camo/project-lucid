using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original 02000052 is a stripped empty marker. Its inherited interface
    // identity is retained without fabricating missing method declarations.
    public interface IInputMonitor : IBaseInputMonitor { }

    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class InputMonitor :
        ActiveBindingsInputMonitor<ActiveGameInputBindings, GameInputBinding, BindingData, KeyCode, string>,
        IInputMonitor
    {
        [Header("Input Processors")]
        [SerializeField] private InputTouch m_touch;
        [SerializeField] private InputSwipe m_swipe;
        [SerializeField] private InputTilt m_tilt;
        [SerializeField] private InputButton m_button;
        [SerializeField] private InputAxis m_axis;
        [SerializeField] private InputPointer m_pointer;
        [SerializeField] private InputVectorised m_vectorisedGameInput;
        [SerializeField] private InputOnScreen m_onScreenControls;
        [SerializeField]
        [Header("Glyph Map Suppliers")]
        private KeyCodeGlyphMap m_glyphMapButtonSupplier;
        [SerializeField] private StringGlyphMap m_glyphMapAxisSupplier;
        [SerializeField] private DirectionGlyphMap m_glyphMapSwipeSupplier;
        [SerializeField] private FixedBindingsGlyphMap m_glyphMapFixedSupplier;
        [SerializeField] private GameInputGlyphMap m_glyphMapGameInputSupplier;
        public const string DefaultFileName = "InputMonitor";
        private readonly SourceProvider m_sourceProvider = new SourceProvider();
        private bool m_paused;
        private Func<KeyCode, InputType, int, Texture2D> m_getGlyphForKeyCodeInputNameAndInputType;
        private Func<string, InputType, int, Texture2D> m_getGlyphForStringAndInputType;
        private Func<InputSwipe.Direction, InputType, int, Texture2D> m_getGlyphForDirectionAndInputType;
        private Func<FixedBindingsGlyphMap.FixedBindingsData, InputType, int, Texture2D> m_getGlyphForFixedBindingsAndInputType;
        private Func<GameInput, Texture2D> m_getGlyphForGameInput;

        public int TouchCount => m_touch.TouchCount; // Original 0600018d.
        // Original 0600018e retains a final virtual new-slot accessor in shipped
        // metadata. The stripped empty IInputMonitor cannot reproduce that
        // emitted slot through ordinary C# without inventing interface APIs.
        // This source remains unaccepted; original emitted shape is unresolved.
        public ISourceProvider SourceProvider => m_sourceProvider;
        protected override BaseSourceProvider BaseSourceProvider => m_sourceProvider; // Original 0600018f.

        // Original 06000190: axis, button, gesture triple, vectorised order.
        protected override void InitialiseControlMap(ControlMap controlMap)
        {
            controlMap.Initialise(m_axis);
            controlMap.Initialise(m_button);
            controlMap.Initialise(m_swipe, m_touch, m_pointer);
            controlMap.Initialise(m_vectorisedGameInput);
        }

        // Original 06000191 uses the inherited GlyphLookupSystemSetter for its
        // final publication, despite retaining a separate unused setter argument.
        protected override void InitialiseInputProcessors(IGlyphLookupSystemSetter glyphLookupSystemSetter,
            IReadOnlyList<GameInputBinding> currentInputBindings)
        {
            m_tilt.Initialise(m_sourceProvider.GestureSource);
            m_pointer.Initialise(m_sourceProvider.PointerSource, currentInputBindings);
            m_vectorisedGameInput.Initialise(currentInputBindings);

            var gameInputGlyphMap = new Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>>(
                HardlightEnumComparers.GameInputComparer);
            m_button.Initialise(ref gameInputGlyphMap, m_getGlyphForKeyCodeInputNameAndInputType,
                m_sourceProvider.KeySource, currentInputBindings);
            m_axis.Initialise(ref gameInputGlyphMap, m_getGlyphForStringAndInputType,
                m_sourceProvider.AxisSource, currentInputBindings);
            m_swipe.Initialise(ref gameInputGlyphMap, m_getGlyphForDirectionAndInputType,
                m_sourceProvider.GestureSource, currentInputBindings);
            m_touch.Initialise(ref gameInputGlyphMap, m_getGlyphForFixedBindingsAndInputType,
                m_sourceProvider.GestureSource, currentInputBindings);
            m_onScreenControls.Initialise(ref gameInputGlyphMap, m_getGlyphForGameInput,
                m_glyphMapGameInputSupplier.InputType);
            GlyphLookupSystemSetter.SetGameInputGlyphMap(gameInputGlyphMap);
        }

        // Original 06000192 initializes all five maps before creating callbacks.
        protected override void InitialiseGlyphMapSuppliers()
        {
            m_glyphMapButtonSupplier.Initialise();
            m_glyphMapAxisSupplier.Initialise();
            m_glyphMapSwipeSupplier.Initialise();
            m_glyphMapFixedSupplier.Initialise();
            m_glyphMapGameInputSupplier.Initialise();
            m_getGlyphForKeyCodeInputNameAndInputType = GetGlyphForKeyCodeAndInputType;
            m_getGlyphForStringAndInputType = GetGlyphForStringAndInputType;
            m_getGlyphForDirectionAndInputType = GetGlyphForDirectionAndInputType;
            m_getGlyphForFixedBindingsAndInputType = GetGlyphForFixedAndInputType;
            m_getGlyphForGameInput = GetGlyphForGameInput;
        }

        // Original 06000193 does not shut down the on-screen processor.
        protected override void ShutdownInputProcessors()
        {
            m_touch.Shutdown();
            m_swipe.Shutdown();
            m_tilt.Shutdown();
            m_pointer.Shutdown();
            m_button.Shutdown();
            m_axis.Shutdown();
            m_vectorisedGameInput.Shutdown();
        }

        protected override void ShutdownControlMap(ControlMap controlMap) // Original 06000194.
        {
            controlMap.Shutdown(m_axis);
            controlMap.Shutdown(m_button);
            controlMap.Shutdown(m_vectorisedGameInput);
            controlMap.Shutdown(m_swipe, m_touch, m_pointer);
        }

        // Original 06000195 has the same stripped-interface final virtual
        // emitted-slot frontier as SourceProvider. Its real body is one store.
        public void SetPaused(bool paused) => m_paused = paused;

        protected override void ShutdownGlyphMapSuppliers() // Original 06000196.
        {
            m_getGlyphForKeyCodeInputNameAndInputType = null;
            m_getGlyphForStringAndInputType = null;
            m_getGlyphForDirectionAndInputType = null;
            m_getGlyphForFixedBindingsAndInputType = null;
            m_getGlyphForGameInput = null;
            m_glyphMapButtonSupplier.Shutdown();
            m_glyphMapAxisSupplier.Shutdown();
            m_glyphMapSwipeSupplier.Shutdown();
            m_glyphMapFixedSupplier.Shutdown();
            m_glyphMapGameInputSupplier.Shutdown();
        }

        // Original 06000197 pause gates only touch/swipe/tilt. The genuine base
        // and button/axis/pointer/vectorised updates still run in that order.
        public override void Update()
        {
            base.Update();
            m_button.Update();
            m_axis.Update();
            m_pointer.Update();
            m_vectorisedGameInput.Update();
            if (m_paused)
                return;
            m_touch.Update();
            m_swipe.Update();
            m_tilt.Update();
        }

        // Original 06000198..0600019b ignore joystickIndex and delegate to the
        // genuine corresponding glyph map's two-argument API.
        private Texture2D GetGlyphForKeyCodeAndInputType(KeyCode key, InputType inputType, int joystickIndex) =>
            m_glyphMapButtonSupplier.GetGlyphForKeyAndInputType(key, inputType);
        private Texture2D GetGlyphForStringAndInputType(string key, InputType inputType, int joystickIndex) =>
            m_glyphMapAxisSupplier.GetGlyphForKeyAndInputType(key, inputType);
        private Texture2D GetGlyphForDirectionAndInputType(InputSwipe.Direction key, InputType inputType, int joystickIndex) =>
            m_glyphMapSwipeSupplier.GetGlyphForKeyAndInputType(key, inputType);
        private Texture2D GetGlyphForFixedAndInputType(FixedBindingsGlyphMap.FixedBindingsData key, InputType inputType, int joystickIndex) =>
            m_glyphMapFixedSupplier.GetGlyphForKeyAndInputType(key, inputType);
        private Texture2D GetGlyphForGameInput(GameInput gameInput) =>
            m_glyphMapGameInputSupplier.GetGlyphForGameInput(gameInput); // Original 0600019c.

        public void OnValidate() { } // Original 0600019d is a genuine RET body.
        public InputMonitor() : base() { } // Original 0600019e creates SourceProvider before the real base ctor.
    }
}
