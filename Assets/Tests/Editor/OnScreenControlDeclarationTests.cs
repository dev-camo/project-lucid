using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // Declaration facts of the on-screen control types of Game.Runtime, against the 1.10.1 DummyDll metadata:
    // type flags, Il2CppSetOption rows in metadata order, field and method order with their flags, serialized
    // attribute rows, and the compiler-generated names that follow from member order (state machines d__N and
    // the <>c closure b__N_0 carry the member ordinal).
    public sealed class OnScreenControlDeclarationTests
    {
        private const int PrivateField = 1;
        private const int PublicReadonlyField = 0x26;
        private const int PrivateReadonlyField = 0x21;
        private const int PrivateMethod = 0x81;
        private const int PublicMethod = 0x86;
        private const int ProtectedMethod = 0x84;
        private const int ProtectedOverride = 0xc4;
        private const int ProtectedNewVirtual = 0x1c4;
        private const int PublicConstructor = 0x1886;
        private const int ProtectedConstructor = 0x1884;
        private const int Accessor = 0x9e6;

        // 02000976. Abstract, implements IInputButton, with the three compiler-generated event fields of the interface.
        [Test]
        public void OnScreenInputButtonKeepsItsInterfaceEventsAndIteratorName()
        {
            Type type = typeof(OnScreenInputButton);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100081));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(IInputButton) }));
            SetOptions(type, 2, 1);
            Fields(type, ("ButtonHandlers", typeof(ButtonHandler.OnHeld), PrivateField), ("ButtonDownHandlers", typeof(ButtonHandler.OnDown), PrivateField),
                ("ButtonUpHandlers", typeof(ButtonHandler.OnUp), PrivateField));
            foreach (string field in new[] { "ButtonHandlers", "ButtonDownHandlers", "ButtonUpHandlers" })
                AttributeRows(type, field, typeof(CompilerGeneratedAttribute));
            Methods(type, ("add_ButtonHandlers", Accessor), ("remove_ButtonHandlers", Accessor), ("add_ButtonDownHandlers", Accessor),
                ("remove_ButtonDownHandlers", Accessor), ("add_ButtonUpHandlers", Accessor), ("remove_ButtonUpHandlers", Accessor),
                ("OnButtonDown", ProtectedMethod), ("OnButtonHeld", ProtectedMethod), ("OnButtonUp", ProtectedMethod),
                ("SupplyGameInputSequence", ProtectedMethod), ("SupplyGameInputCoroutine", PrivateMethod), (".ctor", ProtectedConstructor));

            Type iterator = Nested(type, "<SupplyGameInputCoroutine>d__13");
            Assert.That(type.GetMethod("SupplyGameInputCoroutine", Declared).GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType, Is.EqualTo(iterator));
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Fields(iterator, ("<>1__state", typeof(int), PrivateField), ("<>2__current", typeof(object), PrivateField),
                ("<>4__this", type, 6), ("gameInput", typeof(GameInput), 6), ("inputType", typeof(InputType), 6));
        }

        // 0200096b
        [Test]
        public void OnScreenBurstAttackButtonKeepsItsBindableFields()
        {
            Type type = typeof(OnScreenBurstAttackButton);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 1, 2);
            Fields(type, ("CanTrigger", typeof(Bindable<bool>), PublicReadonlyField), ("IsChaosControl", typeof(Bindable<bool>), PublicReadonlyField),
                ("IsActive", typeof(Bindable<bool>), PublicReadonlyField));
            Methods(type, ("SetTargetingActive", PublicMethod), (".ctor", PublicConstructor));
        }

        // 0200096e. m_wasInteractable is declared and never used.
        [Test]
        public void OnScreenCameraSnapButtonKeepsItsFieldsAndHandlerOrder()
        {
            Type type = typeof(OnScreenCameraSnapButton);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_button", typeof(UIButton), PrivateField), ("m_cameraManagerRef", typeof(SystemRef<CinemachineCameraManager>), PrivateField),
                ("m_wasInteractable", typeof(bool), PrivateField));
            AttributeRows(type, "m_button", typeof(SerializeField));
            AttributeRows(type, "m_cameraManagerRef");
            Methods(type, ("OnEnable", PrivateMethod), ("OnCameraManagerReady", PrivateMethod), ("OnDisable", PrivateMethod),
                ("OnCameraTypeChanged", PrivateMethod), (".ctor", PublicConstructor));
        }

        // 0200096f. The three labels are private literals; OnGUI keeps its Conditional attribute.
        [Test]
        public void OnScreenControlDebuggingKeepsItsConstantsAndConditionalGui()
        {
            Type type = typeof(OnScreenControlDebugging);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("DebugMenuPath", typeof(string), 0x8051), ("HideInfoLabel", typeof(string), 0x8051), ("ShowInfoLabel", typeof(string), 0x8051),
                ("m_touchControls", typeof(UserEditableUIComponentController[]), PrivateField), ("m_guiSkin", typeof(GUISkin), PrivateField),
                ("m_stringBuilder", typeof(StringBuilder), PrivateField), ("m_debugActive", typeof(bool), PrivateField));
            Assert.That(type.GetField("DebugMenuPath", Declared).GetRawConstantValue(), Is.EqualTo("Input"));
            Assert.That(type.GetField("HideInfoLabel", Declared).GetRawConstantValue(), Is.EqualTo("Hide Touch control info"));
            Assert.That(type.GetField("ShowInfoLabel", Declared).GetRawConstantValue(), Is.EqualTo("Show Touch control info"));
            AttributeRows(type, "m_touchControls", typeof(SerializeField));
            AttributeRows(type, "m_guiSkin", typeof(SerializeField));
            Methods(type, ("Awake", PrivateMethod), ("OnDestroy", PrivateMethod), ("OnGUI", PrivateMethod), ("ToggleDebugText", PrivateMethod),
                ("ShowCurrentTouchPositionInfo", PrivateMethod), (".ctor", PublicConstructor));
            Assert.That(type.GetMethod("OnGUI", Declared).GetCustomAttribute<ConditionalAttribute>().ConditionString, Is.EqualTo("BUILD_DEVELOPMENT"));
        }

        // 02000836
        [Test]
        public void OnScreenControlGroupListenerKeepsItsSerializedFieldsAndVirtualHooks()
        {
            Type type = typeof(OnScreenControlGroupListener);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_controlGroups", typeof(List<ScreenControlsGroup>), PrivateField), ("m_subscribeToMessagesOnComponent", typeof(MonoBehaviour), PrivateField),
                ("m_uiContainerOnScreenControls", typeof(UIContainerOnScreenControls), PrivateField), ("m_onShowEvent", typeof(UnityEvent), PrivateField),
                ("m_onHideEvent", typeof(UnityEvent), PrivateField), ("m_messageManagerRef", typeof(SystemRef<MessageManager>), PrivateField));
            AttributeRows(type, "m_controlGroups", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_subscribeToMessagesOnComponent", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_uiContainerOnScreenControls", typeof(SerializeField));
            AttributeRows(type, "m_onShowEvent", typeof(SerializeField));
            AttributeRows(type, "m_onHideEvent", typeof(SerializeField));
            AttributeRows(type, "m_messageManagerRef");
            Assert.That(Tooltip(type, "m_controlGroups"), Is.EqualTo("UI element GameObject will be enabled/disabled if any of these groups are toggled on/off."));
            Assert.That(Tooltip(type, "m_subscribeToMessagesOnComponent"), Is.EqualTo("Script that will be publishing screen control group messages."));
            Methods(type, ("Awake", PrivateMethod), ("OnDestroy", PrivateMethod), ("SubscribeToMessages", PrivateMethod),
                ("OnToggleScreenControlGroup", ProtectedMethod), ("OnEnable", PrivateMethod), ("UpdateState", PrivateMethod),
                ("Show", ProtectedNewVirtual), ("Hide", ProtectedNewVirtual), (".ctor", PublicConstructor));
            ParameterInfo completed = type.GetMethod("UpdateState", Declared).GetParameters()[1];
            Assert.That(completed.IsOptional && completed.HasDefaultValue && completed.DefaultValue == null, Is.True, "completed = null");
            Assert.That(type.GetMethod("OnToggleScreenControlGroup", Declared).GetParameters()[0].IsIn, Is.True);
        }

        // 020008f3. The static lambda of Awake is the <>c member b__8_0 because eight fields precede Awake.
        [Test]
        public void UIContainerOnScreenControlsKeepsItsFieldsAndClosureName()
        {
            Type type = typeof(UIContainerOnScreenControls);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100101));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UIContainer)));
            SetOptions(type, 1, 2);
            Fields(type, ("DynamicStick", typeof(Bindable<bool>), PublicReadonlyField), ("CameraSnapBehindCharacterButtonVisible", typeof(Bindable<bool>), PublicReadonlyField),
                ("TertiaryButtonLeftVisible", typeof(Bindable<bool>), PublicReadonlyField), ("TertiaryButtonRightVisible", typeof(Bindable<bool>), PublicReadonlyField),
                ("m_messageManagerRef", typeof(SystemRef<MessageManager>), PrivateField), ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), PrivateField),
                ("m_playerCharacter", typeof(Character), PrivateField), ("m_lastControlGroupMessageData", typeof(ScreenControlGroupMessageData), PrivateField));
            Methods(type, ("Awake", PrivateMethod), ("OnSaveManagerValid", PrivateMethod), ("OnDestroy", PrivateMethod), ("OnRequestScreenControlGroup", PrivateMethod),
                ("OnCharacterChange", PrivateMethod), ("PublishToggleScreenGroupMessage", PrivateMethod), ("UnsubscribeCharacterMessages", PrivateMethod),
                ("GetLastControlGroupMessageData", PublicMethod), (".ctor", PublicConstructor));
            Assert.That(type.GetMethod("OnRequestScreenControlGroup", Declared).GetParameters()[0].IsIn, Is.True);

            Type closure = Nested(type, "<>c");
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00102103));
            Fields(closure, ("<>9", closure, 0x36), ("<>9__8_0", typeof(Action), 0x16));
            Methods(closure, (".cctor", 0x1891), (".ctor", PublicConstructor), ("<Awake>b__8_0", 0x83));
        }

        // 02000994. Four compiler-generated property fields follow the exchange, in the order of the properties.
        [Test]
        public void UserEditableUIBaseKeepsItsFieldsAccessorsAndHooks()
        {
            Type type = typeof(UserEditableUIBase);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100081));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 1, 2);
            Fields(type, ("EditModeActive", typeof(Bindable<bool>), PublicReadonlyField), ("m_editStartEvent", typeof(UIModernEvent), PrivateField),
                ("m_editCancelEvent", typeof(UIModernEvent), PrivateField), ("m_editConfirmEvent", typeof(UIModernEvent), PrivateField),
                ("m_editFlipEvent", typeof(UIModernEvent), PrivateField), ("m_editResetEvent", typeof(UIModernEvent), PrivateField),
                ("m_uiModernMessage", typeof(UIModernMessage), PrivateReadonlyField),
                ("m_messageExchange", typeof(MessageExchangeBoundCallbackArg<UIModernMessage>), PrivateField),
                ("<m_initialised>k__BackingField", typeof(bool), PrivateField), ("<m_editMode>k__BackingField", typeof(bool), PrivateField),
                ("<m_saveDataSettings>k__BackingField", typeof(SaveDataSettings), PrivateField), ("<m_isFlipped>k__BackingField", typeof(bool), PrivateField));
            AttributeRows(type, "m_editStartEvent", typeof(HeaderAttribute), typeof(SerializeField));
            foreach (string field in new[] { "m_editCancelEvent", "m_editConfirmEvent", "m_editFlipEvent", "m_editResetEvent" })
                AttributeRows(type, field, typeof(SerializeField));
            Assert.That(type.GetField("m_editStartEvent", Declared).GetCustomAttribute<HeaderAttribute>().header, Is.EqualTo("UI events"));
            foreach (string field in new[] { "<m_initialised>k__BackingField", "<m_editMode>k__BackingField", "<m_saveDataSettings>k__BackingField", "<m_isFlipped>k__BackingField" })
                AttributeRows(type, field, typeof(CompilerGeneratedAttribute));
            Methods(type, ("get_m_initialised", 0x884), ("set_m_initialised", 0x881), ("get_m_editMode", 0x884), ("set_m_editMode", 0x881),
                ("get_m_saveDataSettings", 0x884), ("set_m_saveDataSettings", 0x881), ("get_m_isFlipped", 0x884), ("set_m_isFlipped", 0x881),
                ("Awake", PrivateMethod), ("OnValidate", ProtectedNewVirtual), ("OnDestroy", PrivateMethod), ("Update", PrivateMethod),
                ("DoUpdate", ProtectedNewVirtual), ("Initialise", PrivateMethod), ("DoInitialise", ProtectedNewVirtual),
                ("OnSaveManagerValid", ProtectedNewVirtual), ("OnEditStart", ProtectedNewVirtual), ("OnEditCancel", ProtectedNewVirtual),
                ("OnEditConfirm", ProtectedNewVirtual), ("OnEditFlip", ProtectedNewVirtual), ("OnEditReset", ProtectedNewVirtual),
                ("ExitEditMode", ProtectedNewVirtual), (".ctor", ProtectedConstructor));
        }

        // 02000995
        [Test]
        public void UserEditableUIComponentControllerKeepsItsSerializedFieldRows()
        {
            Type type = typeof(UserEditableUIComponentController);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UserEditableUIBase)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_saveId", typeof(string), PrivateField), ("m_movementBoundsRectTransform", typeof(RectTransform), PrivateField),
                ("m_editableUIComponent", typeof(MonoBehaviour), PrivateField), ("m_scaleMin", typeof(float), PrivateField), ("m_scaleMax", typeof(float), PrivateField),
                ("m_flips", typeof(Flip[]), PrivateField), ("m_onEditStart", typeof(UnityEvent), PrivateField), ("m_onEditEnd", typeof(UnityEvent), PrivateField),
                ("m_saveDataComponent", typeof(SaveDataSettingsEditableUIComponent), PrivateField),
                ("m_saveDataComponentBackupBeforeEdit", typeof(SaveDataSettingsEditableUIComponent), PrivateField),
                ("m_defaultPositionNormalised", typeof(Vector2), PrivateField), ("m_defaultScale", typeof(float), PrivateField),
                ("m_currentPositionNormalised", typeof(Vector2), PrivateField), ("m_currentScale", typeof(float), PrivateField),
                ("m_scaleAtPinchBegin", typeof(float), PrivateField), ("m_pinchActive", typeof(bool), PrivateField),
                ("m_temporaryCornersArray", typeof(Vector3[]), PrivateReadonlyField), ("m_movementRect", typeof(Rect), PrivateField),
                ("m_componentRect", typeof(Rect), PrivateField), ("m_movableWidth", typeof(float), PrivateField), ("m_movableHeight", typeof(float), PrivateField),
                ("m_componentRectTransform", typeof(RectTransform), PrivateField), ("m_editableUIComponentInterface", typeof(IUserEditableUIComponent), PrivateField),
                ("m_menuCamera", typeof(Camera), PrivateField));
            AttributeRows(type, "m_saveId", typeof(TooltipAttribute), typeof(SerializeField), typeof(HeaderAttribute));
            AttributeRows(type, "m_movementBoundsRectTransform", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_editableUIComponent", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_scaleMin", typeof(UnityEngine.RangeAttribute), typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_scaleMax", typeof(UnityEngine.RangeAttribute), typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_flips", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_onEditStart", typeof(SerializeField), typeof(HeaderAttribute));
            AttributeRows(type, "m_onEditEnd", typeof(SerializeField));
            Assert.That(Tooltip(type, "m_saveId"), Is.EqualTo("ID for saving the settings for this component into the save file."));
            Assert.That(Tooltip(type, "m_movementBoundsRectTransform"),
                Is.EqualTo("Bounds the component can be moved around in by the user in edit mode. Component must be fully inside the bounds."));
            Assert.That(Tooltip(type, "m_editableUIComponent"), Is.EqualTo("The component to be moved by the user. Must implement IUserEditableUIComponent."));
            Assert.That(Tooltip(type, "m_scaleMin"), Is.EqualTo("Minimum component to movement area width ratio the user can set."));
            Assert.That(Tooltip(type, "m_scaleMax"), Is.EqualTo("Maximum component to movement area width ratio the user can set."));
            Assert.That(Tooltip(type, "m_flips"), Is.EqualTo("Flips to flip horizontally if control sides are swapped."));
            Assert.That(type.GetField("m_saveId", Declared).GetCustomAttribute<HeaderAttribute>().header, Is.EqualTo("Editable component setup"));
            Assert.That(type.GetField("m_onEditStart", Declared).GetCustomAttribute<HeaderAttribute>().header, Is.EqualTo("Unity events"));
            foreach (string field in new[] { "m_scaleMin", "m_scaleMax" })
            {
                var range = type.GetField(field, Declared).GetCustomAttribute<UnityEngine.RangeAttribute>();
                Assert.That((range.min, range.max), Is.EqualTo((0f, 1f)), field);
            }
            Methods(type, ("OnSaveManagerValid", ProtectedOverride), ("OnValidate", ProtectedOverride), ("DoUpdate", ProtectedOverride),
                ("UpdateSavedSettings", PrivateMethod), ("DoInitialise", ProtectedOverride), ("RefreshRects", PrivateMethod), ("UpdateMovableSpace", PrivateMethod),
                ("RectTransformToRect", PrivateMethod), ("OnEditStart", ProtectedOverride), ("OnEditCancel", ProtectedOverride),
                ("UpdateFlipComponents", PrivateMethod), ("OnEditReset", ProtectedOverride), ("OnEditFlip", ProtectedOverride), ("ExitEditMode", ProtectedOverride),
                ("SetComponentNormalisedPositionAndScale", PrivateMethod), ("UpdateAfterParentFlipped", PublicMethod), ("GetPositionInfo", PublicMethod),
                (".ctor", PublicConstructor));
            Assert.That(type.GetMethod("OnEditReset", Declared).GetParameters()[0].Name, Is.EqualTo("uiMessage"));
            Assert.That(type.GetMethod("GetPositionInfo", Declared).GetParameters()[0].ParameterType, Is.EqualTo(typeof(StringBuilder).MakeByRefType()));
        }

        // 02000996
        [Test]
        public void UserEditableUIFlipperKeepsItsFieldsAndIteratorName()
        {
            Type type = typeof(UserEditableUIFlipper);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UserEditableUIBase)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_flipLayoutGroup", typeof(HorizontalLayoutGroup), PrivateField), ("m_editableComponents", typeof(UserEditableUIComponentController[]), PrivateField));
            AttributeRows(type, "m_flipLayoutGroup", typeof(SerializeField));
            AttributeRows(type, "m_editableComponents", typeof(SerializeField));
            Methods(type, ("OnValidate", ProtectedOverride), ("DoInitialise", ProtectedOverride), ("OnEditFlip", ProtectedOverride),
                ("OnEditConfirm", ProtectedOverride), ("OnEditReset", ProtectedOverride), ("UpdateFlip", PrivateMethod),
                ("UpdateFlipDelayed", PrivateMethod), (".ctor", PublicConstructor));
            Type iterator = Nested(type, "<UpdateFlipDelayed>d__8");
            Assert.That(type.GetMethod("UpdateFlipDelayed", Declared).GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType, Is.EqualTo(iterator));
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Fields(iterator, ("<>1__state", typeof(int), PrivateField), ("<>2__current", typeof(object), PrivateField), ("<>4__this", type, 6));
            Assert.That(typeof(IEnumerator).IsAssignableFrom(iterator), Is.True);
        }
    }
}
