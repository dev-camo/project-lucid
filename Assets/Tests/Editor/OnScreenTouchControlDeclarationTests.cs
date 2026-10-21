using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // Declaration shape of the restored touch-zone, on-screen button and input handler types, from the DummyDll
    // metadata of the 1.10.1 build: type flags, IL2CPP options in attribute row order, field order and flags,
    // method order and flags, and the compiler-generated nested types whose names follow from the member order.
    public sealed class OnScreenTouchControlDeclarationTests
    {
        private const int PublicClass = 0x00100001;
        private const int AbstractClass = 0x00100081;
        private const int PrivateMethod = 0x0081;
        private const int PublicMethod = 0x0086;
        private const int ProtectedMethod = 0x0084;
        private const int ProtectedOverride = 0x00C4;
        private const int PublicOverride = 0x00C6;
        private const int PublicNewSlot = 0x01C6;
        private const int ProtectedNewSlot = 0x01C4;
        private const int PublicConstructor = 0x1886;
        private const int ProtectedConstructor = 0x1884;
        private const int PrivateField = 0x0001;
        private const int ProtectedField = 0x0004;
        private const int PublicReadonlyField = 0x0026;
        private const int PrivateReadonlyField = 0x0021;
        private const int ProtectedReadonlyField = 0x0024;
        private const int PrivateConstant = 0x8051;

        private static void SerializedRows(Type type, params string[] fields)
        {
            foreach (string field in fields) AttributeRows(type, field, typeof(SerializeField));
        }

        [Test]
        public void OnScreenButtonKeepsItsFieldsHandlersAndIteratorName()
        {
            Type type = typeof(OnScreenButton);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenInputButton)));
            SetOptions(type);
            Fields(type, ("m_button", typeof(UIButton), ProtectedField), ("m_inputToSupply", typeof(GameInput[]), PrivateField),
                ("m_inputOnExit", typeof(GameInput), PrivateField), ("m_inputTrigger", typeof(InputTrigger), PrivateField),
                ("m_buttonGraphic", typeof(GameObject), PrivateField), ("m_showOnInputTypes", typeof(List<InputType>), PrivateField),
                ("m_mouseShowsInEditor", typeof(bool), PrivateField), ("m_controlMapping", typeof(ControlMapping), PrivateField),
                ("InputType", typeof(InputType), PrivateConstant), ("m_heldButtonCoroutine", typeof(Coroutine), PrivateField),
                ("m_buttonHeld", typeof(bool), PrivateField), ("m_onButtonHeldCompleteCallback", typeof(Action), PrivateField));
            SerializedRows(type, "m_button", "m_inputToSupply", "m_inputOnExit", "m_inputTrigger", "m_buttonGraphic", "m_showOnInputTypes", "m_mouseShowsInEditor");
            Assert.That(type.GetField("InputType", Declared).GetRawConstantValue(), Is.EqualTo(-31649603), "Touch");
            Methods(type, ("Reset", PrivateMethod), ("Awake", PrivateMethod), ("OnDestroy", PrivateMethod), ("DisableWhenReady", PublicMethod),
                ("OnButtonHoldBegin", PrivateMethod), ("OnButtonHoldEnd", PrivateMethod), ("OnButtonDown", PrivateMethod), ("OnButtonUp", PrivateMethod),
                ("OnButtonExit", PrivateMethod), ("SupplyHeldButton", PrivateMethod), ("EndButtonHold", PrivateMethod), ("OnInputTypeChanged", PrivateMethod),
                ("IsInteractable", PrivateMethod), (".ctor", PublicConstructor));
            Nested(type, "<SupplyHeldButton>d__21");
        }

        [Test]
        public void OnScreenControlGroupListenerButtonKeepsItsFieldAndClosureName()
        {
            Type type = typeof(OnScreenControlGroupListenerButton);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenControlGroupListener)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_onScreenButton", typeof(OnScreenButton), PrivateField));
            SerializedRows(type, "m_onScreenButton");
            Methods(type, ("Hide", ProtectedOverride), (".ctor", PublicConstructor), ("<>n__0", PrivateMethod));

            Type closure = Nested(type, "<>c__DisplayClass1_0");
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103));
            Fields(closure, ("<>4__this", type, 0x0006), ("completed", typeof(Action), 0x0006));
            Methods(closure, (".ctor", PublicConstructor), ("<Hide>b__0", 0x0083));
        }

        [Test]
        public void OnScreenTouchZoneKeepsItsFieldsAccessorsAndHooks()
        {
            Type type = typeof(OnScreenTouchZone);
            Assert.That((int)type.Attributes, Is.EqualTo(AbstractClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenInputButton)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_validTouchTransform", typeof(RectTransform), ProtectedField), ("m_minimumPinchDistance", typeof(float), PrivateField),
                ("m_touchIgnoreAbilities", typeof(List<ActorAbilityType>), PrivateField), ("m_restrictToSingleTouch", typeof(bool), PrivateField),
                ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), PrivateReadonlyField), ("<MultiTouchEnabled>k__BackingField", typeof(bool), PrivateField),
                ("m_activeTouches", typeof(List<Touch>), PrivateReadonlyField), ("m_changedTouches", typeof(bool), PrivateField),
                ("m_touchPositionPrevious", typeof(Vector2), PrivateField), ("m_isPinching", typeof(bool), PrivateField),
                ("m_initialPinchDistance", typeof(float), PrivateField), ("m_pinchDistance", typeof(float), PrivateField),
                ("m_peakTouches", typeof(int), PrivateField), ("m_pointerWasDown", typeof(bool), PrivateField));
            SerializedRows(type, "m_validTouchTransform", "m_restrictToSingleTouch");
            AttributeRows(type, "m_minimumPinchDistance", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_touchIgnoreAbilities", typeof(SerializeField), typeof(TooltipAttribute));
            Assert.That(Tooltip(type, "m_minimumPinchDistance"), Is.EqualTo("During multi-touch functionality, pinch distance between two points will be clamped to this as a minimum"));
            Assert.That(Tooltip(type, "m_touchIgnoreAbilities"), Is.EqualTo("If any specified abilities are active on the character then ignore touch input while in use."));
            const int protectedAccessor = 0x0884;
            Methods(type, ("get_IsPinching", protectedAccessor), ("get_InitialPinchDistance", protectedAccessor), ("get_PinchDistance", protectedAccessor),
                ("get_PointerWasDown", protectedAccessor), ("get_PointerIsDown", 0x0886), ("get_MultiTouchEnabled", protectedAccessor),
                ("set_MultiTouchEnabled", protectedAccessor), ("Initialise", PublicNewSlot), ("Shutdown", PublicNewSlot), ("OnTouchRelease", ProtectedNewSlot),
                ("ProcessTouch", ProtectedNewSlot), ("TouchPositionValid", PublicMethod), ("GetScoreForTouchPosition", ProtectedNewSlot),
                ("SupplyHeldGameInput", 0x05C4), ("SupplyPinchInput", ProtectedNewSlot), ("OnInput", 0x05C4), ("IsActive", PublicNewSlot),
                ("IsSingleTouch", ProtectedNewSlot), ("CanAcceptNewTouch", PublicMethod), ("SetMultiTouchEnabled", PublicMethod),
                ("ClearAllTouches", PublicMethod), ("IsTrackingTouch", PublicMethod), ("StopTrackingTouch", PublicMethod), ("StartTrackingTouch", PublicMethod),
                ("UpdateTouch", PublicMethod), ("TryGetTouchIndex", PrivateMethod), ("TouchValidForCharacter", ProtectedMethod),
                ("SupplyTouchData", PublicMethod), ("SupplySingleTouch", PrivateMethod), (".ctor", ProtectedConstructor));
            Assert.That(type.GetMethod("TouchPositionValid").GetParameters().Select(p => (p.Name, p.IsOut)).ToArray(), Is.EqualTo(new[] { ("position", false), ("score", true) }));
            Assert.That(type.GetMethod("ProcessTouch", Declared).GetParameters().Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "touchPosition", "touchDelta", "pinchDistance", "initialPinchDistance" }));
            Assert.That(type.GetMethod("SupplyHeldGameInput", Declared).GetParameters().Single().Name, Is.EqualTo("resultantMovement"));
            Assert.That(type.GetMethod("OnInput", Declared).GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "touchPosition", "touchDelta" }));
            Assert.That(type.GetMethod("SupplyPinchInput", Declared).GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "pinchDistance", "initialPinchDistance" }));
        }

        [Test]
        public void OnScreenControlZoneKeepsItsAxisFieldsAndHooks()
        {
            Type type = typeof(OnScreenControlZone);
            Assert.That((int)type.Attributes, Is.EqualTo(AbstractClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenTouchZone)));
            SetOptions(type, 1, 2);
            Fields(type, ("m_axisLeft", typeof(GameInput), PrivateField), ("m_axisRight", typeof(GameInput), PrivateField), ("m_axisUp", typeof(GameInput), PrivateField),
                ("m_axisDown", typeof(GameInput), PrivateField), ("m_onPress", typeof(GameInput), PrivateField), ("m_showOnInputTypes", typeof(List<InputType>), ProtectedField),
                ("m_provideAbsoluteInputValues", typeof(bool), PrivateField), ("m_controllerInput", typeof(Vector2), ProtectedField),
                ("m_controlMapping", typeof(ControlMapping), PrivateField),
                ("m_modifierLookups", typeof(SerializableDictionary<InputModifierType, List<InputModifier>>), PrivateField),
                ("m_saveManagerRef", typeof(SystemRef<SaveManager>), ProtectedReadonlyField), ("m_inputSystemRef", typeof(SystemRef<InputSystem>), PrivateReadonlyField),
                ("m_dataManagerRef", typeof(SystemRef<DataManager>), PrivateReadonlyField));
            AttributeRows(type, "m_axisLeft", typeof(SerializeField), typeof(HashEnumAttribute));
            AttributeRows(type, "m_axisRight", typeof(SerializeField), typeof(HashEnumAttribute));
            AttributeRows(type, "m_axisUp", typeof(HashEnumAttribute), typeof(SerializeField));
            AttributeRows(type, "m_axisDown", typeof(SerializeField), typeof(HashEnumAttribute));
            AttributeRows(type, "m_onPress", typeof(HashEnumAttribute), typeof(SerializeField));
            SerializedRows(type, "m_showOnInputTypes", "m_provideAbsoluteInputValues");
            Methods(type, ("Initialise", PublicOverride), ("Shutdown", PublicOverride), ("OnTouchRelease", ProtectedOverride), ("ProcessTouch", ProtectedOverride),
                ("OnPressValueValid", PrivateMethod), ("SupplyHeldGameInput", ProtectedOverride), ("OnInputTypeChanged", PublicNewSlot),
                ("GetInputModifierType", PrivateMethod), ("TryGetModifiers", PrivateMethod), ("ApplyModifiers", ProtectedMethod),
                ("ResetAxisModifiers", PrivateMethod), (".ctor", ProtectedConstructor));
            Assert.That(type.GetMethod("SupplyHeldGameInput", Declared).GetParameters().Single().Name, Is.EqualTo("movement"));
            Assert.That(type.GetMethod("ApplyModifiers", Declared).GetParameters().Single().ParameterType, Is.EqualTo(typeof(Vector2).MakeByRefType()));
        }

        [Test]
        public void OnScreenControlInputHandlerKeepsItsFieldsAndEmptyDebugMethods()
        {
            Type type = typeof(OnScreenControlInputHandler);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_controlZones", typeof(OnScreenTouchZone[]), PrivateField), ("m_graphicsToToggle", typeof(GameObject[]), PrivateField),
                ("m_uiInputTypesWhichToggleOn", typeof(List<UIInputType>), PrivateField), ("m_mouseShowsInEditor", typeof(bool), PrivateField),
                ("MaxAssumedTouchCount", typeof(int), PrivateConstant), ("m_currentTouches", typeof(List<Touch>), PrivateReadonlyField),
                ("m_inputMonitor", typeof(InputMonitor), PrivateField), ("m_firstUpdateComplete", typeof(bool), PrivateField),
                ("m_isValidInputType", typeof(bool), PrivateField), ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), PrivateReadonlyField));
            SerializedRows(type, "m_controlZones", "m_graphicsToToggle", "m_uiInputTypesWhichToggleOn", "m_mouseShowsInEditor");
            Assert.That(type.GetField("MaxAssumedTouchCount", Declared).GetRawConstantValue(), Is.EqualTo(5));
            Methods(type, ("OnEnable", PrivateMethod), ("OnDisable", PrivateMethod), ("Update", PrivateMethod), ("GetFingerId", PrivateMethod),
                ("OnInputTypeChanged", PrivateMethod), ("SetupDebugButtons", PrivateMethod), ("DestroyDebugButtons", PrivateMethod),
                ("AddDebugTouch", PrivateMethod), (".ctor", PublicConstructor));
        }

        [Test]
        public void OnScreenJoystickDebugKeepsItsConditionalMembers()
        {
            Type type = typeof(OnScreenJoystickDebug);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            SetOptions(type, 2, 1);
            Fields(type, ("m_verticalDeadZoneInner", typeof(Image), PrivateField), ("m_horizontalDeadZoneInner", typeof(Image), PrivateField),
                ("m_liveZone", typeof(Image), PrivateField), ("m_inputTypeIsTouch", typeof(bool), PrivateField), ("m_characterInfoShowing", typeof(bool), PrivateField));
            SerializedRows(type, "m_verticalDeadZoneInner", "m_horizontalDeadZoneInner", "m_liveZone");
            Methods(type, ("OnEnable", ProtectedMethod), ("ToggleCharacterInfoShowing", PrivateMethod), ("ToggleIsInputTouch", PublicMethod),
                ("ToggleDeadZones", PrivateMethod), ("OnDisable", ProtectedMethod), (".ctor", PublicConstructor));
            foreach (string name in new[] { "OnEnable", "ToggleIsInputTouch", "ToggleDeadZones", "OnDisable" })
            {
                ConditionalAttribute condition = type.GetMethod(name, Declared).GetCustomAttribute<ConditionalAttribute>();
                Assert.That(condition?.ConditionString, Is.EqualTo("BUILD_DEVELOPMENT"), name);
            }
            Assert.That(type.GetMethod("ToggleCharacterInfoShowing", Declared).GetCustomAttribute<ConditionalAttribute>(), Is.Null);
        }

        [Test]
        public void OnScreenTargetingButtonKeepsItsBindableAndClosureName()
        {
            Type type = typeof(OnScreenTargetingButton);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            SetOptions(type, 1, 2);
            Fields(type, ("m_validAbilityTypeGroup", typeof(CharacterAbilityTypeGroup), PrivateField), ("IsTargetingActive", typeof(Bindable<bool>), PublicReadonlyField));
            SerializedRows(type, "m_validAbilityTypeGroup");
            Methods(type, ("OnEnable", PrivateMethod), ("SetTargetingActive", PublicMethod), (".ctor", PublicConstructor));

            Type closure = Nested(type, "<>c__DisplayClass3_0");
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103));
            Fields(closure, ("abilityType", typeof(ActorAbilityType), 0x0006));
            Methods(closure, (".ctor", PublicConstructor), ("<SetTargetingActive>b__0", 0x0083));
        }

        [Test]
        public void OnScreenTertiaryButtonKeepsItsFieldsAndMethodOrder()
        {
            Type type = typeof(OnScreenTertiaryButton);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            SetOptions(type, 2, 1);
            Fields(type, ("m_airStompIcon", typeof(GameObject), PrivateField), ("m_spinDashIcon", typeof(GameObject), PrivateField),
                ("m_uiContainerOnScreenControls", typeof(UIContainerOnScreenControls), PrivateField),
                ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), PrivateReadonlyField), ("m_playerCharacter", typeof(Character), PrivateField),
                ("m_airStompAttackAvailable", typeof(bool), PrivateField), ("m_spinDashAvailable", typeof(bool), PrivateField),
                ("m_lastScreenControlsGroup", typeof(ScreenControlsGroup), PrivateField));
            SerializedRows(type, "m_airStompIcon", "m_spinDashIcon", "m_uiContainerOnScreenControls");
            Methods(type, ("Awake", PrivateMethod), ("OnEnable", PrivateMethod), ("RefreshIcons", PrivateMethod), ("SetIconsActive", PrivateMethod),
                ("OnDestroy", PrivateMethod), ("OnCharacterChange", PrivateMethod), ("Update", PrivateMethod), ("DoUpdate", PrivateMethod),
                ("OnValidate", PrivateMethod), (".ctor", PublicConstructor));
        }
    }
}
