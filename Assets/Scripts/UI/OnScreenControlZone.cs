using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000973; bodies translated from the 1.10.1 x86-64 binary.
    // A touch zone that turns the touch movement into four directional game inputs, shaped by the input
    // modifiers for the saved control stick sensitivity.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class OnScreenControlZone : OnScreenTouchZone
    {
        [SerializeField]
        [HashEnum(typeof(GameInput))]
        private GameInput m_axisLeft = GameInput.Left;
        [SerializeField]
        [HashEnum(typeof(GameInput))]
        private GameInput m_axisRight = GameInput.Right;
        [HashEnum(typeof(GameInput))]
        [SerializeField]
        private GameInput m_axisUp = GameInput.Up;
        [SerializeField]
        [HashEnum(typeof(GameInput))]
        private GameInput m_axisDown = GameInput.Down;
        [HashEnum(typeof(GameInput))]
        [SerializeField]
        private GameInput m_onPress;
        [SerializeField] protected List<InputType> m_showOnInputTypes = new List<InputType>();
        [SerializeField] private bool m_provideAbsoluteInputValues;
        protected Vector2 m_controllerInput;
        private ControlMapping m_controlMapping;
        private SerializableDictionary<InputModifierType, List<InputModifier>> m_modifierLookups =
            new SerializableDictionary<InputModifierType, List<InputModifier>>(HardlightEnumComparers.InputModifierTypeComparer);
        protected readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<InputSystem> m_inputSystemRef = ProcessManager.GetSystemRef<InputSystem>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();

        // 06003676 (1.10.1 RVA 0x68F1D0): registers as a button provider and takes the input modifiers from the
        // input global constants.
        public override void Initialise()
        {
            base.Initialise();
            m_controllerInput = Vector2.zero;
            m_controlMapping = m_inputSystemRef.Get().ControlMapping;
            if (m_controlMapping != null) m_controlMapping.RegisterButtonProvider(this);
            GlobalConstantDefinition_Input inputConstants = m_dataManagerRef.Get().GlobalConstantDefinitions[GlobalConstantType.Input] as GlobalConstantDefinition_Input;
            m_modifierLookups = inputConstants != null ? inputConstants.CompositeMovementModifierLookups : null;
        }

        // 06003677 (RVA 0x690970)
        public override void Shutdown(bool usingValidInputType)
        {
            base.Shutdown(usingValidInputType);
            if (m_controlMapping != null) m_controlMapping.UnregisterButtonProvider(this);
        }

        // 06003678 (RVA 0x690290): the press ends when the last touch leaves, so the press input is released.
        protected override void OnTouchRelease()
        {
            base.OnTouchRelease();
            if (PointerWasDown)
            {
                m_controllerInput = Vector2.zero;
                ResetAxisModifiers();
                if (OnPressValueValid()) OnButtonUp(m_onPress, 1f, InputType.Touch);
            }
        }

        // 06003679 (RVA 0x68F850): the first touch also presses the press input.
        protected override void ProcessTouch(Vector2 touchPosition, Vector2 touchDelta, float pinchDistance, float initialPinchDistance)
        {
            if (!PointerWasDown && OnPressValueValid()) OnButtonDown(m_onPress, 1f, InputType.Touch);
            base.ProcessTouch(touchPosition, touchDelta, pinchDistance, initialPinchDistance);
        }

        // 0600367a (RVA 0x690CA0)
        private bool OnPressValueValid()
        {
            return m_onPress != (GameInput)0 && m_onPress != GameInput.None;
        }

        // 0600367b (RVA 0x690D40): one held input per axis, left or down for a movement that is not positive.
        protected override void SupplyHeldGameInput(Vector2 movement)
        {
            ApplyModifiers(ref movement);
            OnButtonHeld(movement.x > 0f ? m_axisRight : m_axisLeft,
                m_provideAbsoluteInputValues ? Mathf.Abs(movement.x) : movement.x, InputType.Touch);
            OnButtonHeld(movement.y > 0f ? m_axisUp : m_axisDown,
                m_provideAbsoluteInputValues ? Mathf.Abs(movement.y) : movement.y, InputType.Touch);
        }

        // 0600367c (RVA 0x690FF0)
        public virtual void OnInputTypeChanged(InputType inputType)
        {
        }

        // 0600367d (RVA 0x691000)
        private InputModifierType GetInputModifierType()
        {
            return m_saveManagerRef.Get().GetSaveDataSettings().ControlStickSensitivity;
        }

        // 0600367e (RVA 0x691050)
        private bool TryGetModifiers(out List<InputModifier> modifiers)
        {
            return m_modifierLookups.TryGetValue(GetInputModifierType(), out modifiers);
        }

        // 0600367f (RVA 0x690E00): every modifier of the saved sensitivity is applied in turn.
        protected void ApplyModifiers(ref Vector2 movementValue)
        {
            if (TryGetModifiers(out List<InputModifier> modifiers))
            {
                foreach (InputModifier modifier in modifiers)
                    movementValue = modifier.Modify(movementValue, Time.deltaTime);
            }
        }

        // 06003680 (RVA 0x690AF0)
        private void ResetAxisModifiers()
        {
            if (TryGetModifiers(out List<InputModifier> modifiers))
            {
                foreach (InputModifier modifier in modifiers)
                    modifier.Reset();
            }
        }

        // 06003681 (RVA 0x6906B0)
        protected OnScreenControlZone()
        {
        }
    }
}
