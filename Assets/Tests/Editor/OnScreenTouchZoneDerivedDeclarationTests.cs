using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // Declaration shape of the stick, camera, editable UI and ornament touch zones, from the DummyDll metadata of the
    // 1.10.1 build: type flags, IL2CPP options in attribute row order, field order and flags, method order and flags, and
    // the nested types and the lambda whose names follow from the member order (fields and nested types count towards
    // the ordinal of the lambda's method, so 22 fields and the stick state enum put Initialise at 23).
    public sealed class OnScreenTouchZoneDerivedDeclarationTests
    {
        private const int PublicClass = 0x00100001;
        private const int PrivateMethod = 0x0081;
        private const int PublicMethod = 0x0086;
        private const int ProtectedOverride = 0x00C4;
        private const int PublicOverride = 0x00C6;
        private const int InterfaceImplementation = 0x01E6;
        private const int PublicConstructor = 0x1886;
        private const int PrivateField = 0x0001;
        private const int PrivateReadonlyField = 0x0021;
        private const int PrivateConstant = 0x8051;

        [Test]
        public void OnScreenTouchZoneOrnamentKeepsItsFieldsAndHooks()
        {
            Type type = typeof(OnScreenTouchZoneOrnament);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenTouchZone)));
            SetOptions(type, 1, 2);
            Fields(type, ("m_minX", typeof(float), PrivateField), ("m_maxX", typeof(float), PrivateField), ("m_touchSpan", typeof(float), PrivateField),
                ("m_dragDelta", typeof(float), PrivateField), ("m_dragReleaseImpulse", typeof(float), PrivateField), ("m_dragAmount", typeof(float), PrivateField));
            Methods(type, ("get_DragAmount", 0x0886), ("Initialise", PublicOverride), ("SupplyHeldGameInput", ProtectedOverride),
                ("OnInput", ProtectedOverride), ("OnTouchRelease", ProtectedOverride), ("ConsumeDragReleaseImpulse", PublicMethod),
                (".ctor", PublicConstructor));
            Assert.That(type.GetProperty("DragAmount").CanWrite, Is.False);
        }

        [Test]
        public void OnScreenTouchZoneEditableUIKeepsItsFieldsAndInterfaceMethods()
        {
            Type type = typeof(OnScreenTouchZoneEditableUI);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenTouchZone)));
            Assert.That(type.GetInterfaces(), Does.Contain(typeof(IUserEditableUIComponent)));
            SetOptions(type, 1, 2);
            Fields(type, ("m_editableComponentTransform", typeof(RectTransform), PrivateField), ("m_initiallyActive", typeof(bool), PrivateField),
                ("m_touchPosition", typeof(Vector2), PrivateField), ("m_active", typeof(bool), PrivateField));
            AttributeRows(type, "m_editableComponentTransform", typeof(SerializeField));
            AttributeRows(type, "m_initiallyActive", typeof(SerializeField));
            Methods(type, ("Awake", PrivateMethod), ("SupplyHeldGameInput", ProtectedOverride), ("OnInput", ProtectedOverride),
                ("TransformUpdated", InterfaceImplementation), ("GetRectTransform", InterfaceImplementation), ("SetActive", PublicMethod),
                ("GetTouchPosition", InterfaceImplementation), ("GetPinchInfo", InterfaceImplementation), ("SetEditing", InterfaceImplementation),
                ("IsActive", PublicOverride), ("GetScoreForTouchPosition", ProtectedOverride), (".ctor", PublicConstructor));
        }

        [Test]
        public void OnScreenControlZoneCameraKeepsItsFieldsHooksAndEntryClass()
        {
            Type type = typeof(OnScreenControlZoneCamera);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenControlZone)));
            SetOptions(type, 1, 2);
            Type entry = Nested(type, "GameInputCharacter");
            Fields(type, ("m_xSensitivity", typeof(float), PrivateField), ("m_ySensitivity", typeof(float), PrivateField),
                ("m_debugTouchZoneView", typeof(GameObject), PrivateField), ("m_cameraSnapDoubleTapTimeSeconds", typeof(float), PrivateField),
                ("m_minimumCancelDoubleTapInputMagnitude", typeof(float), PrivateField),
                ("m_doubleTapInputs", typeof(List<>).MakeGenericType(entry), PrivateField), ("DoubleTapCount", typeof(int), PrivateConstant),
                ("m_debugOverlayEnabled", typeof(bool), PrivateField), ("m_snapTapCount", typeof(int), PrivateField),
                ("m_doubleTapTimerSeconds", typeof(float), PrivateField), ("m_totalMovementAmountSqr", typeof(float), PrivateField),
                ("m_minimumCancelDoubleTapInputMagnitudeSqr", typeof(float), PrivateField), ("m_cameraSnapEnabled", typeof(bool), PrivateField));
            Assert.That(type.GetField("DoubleTapCount", Declared).GetRawConstantValue(), Is.EqualTo(2));
            foreach (string field in new[] { "m_xSensitivity", "m_ySensitivity", "m_debugTouchZoneView", "m_cameraSnapDoubleTapTimeSeconds",
                "m_minimumCancelDoubleTapInputMagnitude", "m_doubleTapInputs" })
                AttributeRows(type, field, typeof(SerializeField));
            Methods(type, ("Initialise", PublicOverride), ("Shutdown", PublicOverride), ("SetCameraSnapEnabled", PublicMethod),
                ("OnInput", ProtectedOverride), ("GetDebugTouchZoneViewText", PrivateMethod), ("ToggleDebugTouchZoneView", PrivateMethod),
                ("OnTouchRelease", ProtectedOverride), ("Update", PrivateMethod), ("ResetSnapValues", PrivateMethod), ("OnValidate", PrivateMethod),
                ("UpdateCachedValues", PrivateMethod), (".ctor", PublicConstructor));

            Assert.That((int)entry.Attributes, Is.EqualTo(0x00102003), "private, serializable");
            Assert.That(entry.BaseType, Is.EqualTo(typeof(object)));
            SetOptions(entry, 2, 1);
            Fields(entry, ("m_input", typeof(GameInput), PrivateField), ("m_ignoreAbilities", typeof(List<ActorAbilityType>), PrivateField));
            AttributeRows(entry, "m_input", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(entry, "m_ignoreAbilities", typeof(SerializeField), typeof(TooltipAttribute));
            Assert.That(Tooltip(entry, "m_input"), Is.EqualTo("The game input to supply."));
            Assert.That(Tooltip(entry, "m_ignoreAbilities"), Is.EqualTo("If any specified abilities are active on the character then ignore input while in use."));
            Methods(entry, ("get_Input", 0x0886), ("get_IgnoreAbilities", 0x0886), (".ctor", PublicConstructor));
        }

        [Test]
        public void OnScreenControlStickMovementKeepsItsFieldsStatesAndLambdaName()
        {
            Type type = typeof(OnScreenControlStickMovement);
            Assert.That((int)type.Attributes, Is.EqualTo(PublicClass));
            Assert.That(type.BaseType, Is.EqualTo(typeof(OnScreenControlZone)));
            Assert.That(type.GetInterfaces(), Does.Contain(typeof(IUserEditableUIComponent)));
            SetOptions(type, 2, 1);
            Type state = Nested(type, "StickState");
            Fields(type, ("m_dynamicPosition", typeof(bool), PrivateField), ("m_originTransform", typeof(RectTransform), PrivateField),
                ("m_container", typeof(RectTransform), PrivateField), ("m_stickTop", typeof(RectTransform), PrivateField),
                ("m_miniOuter", typeof(RectTransform), PrivateField), ("m_fullSolidOuter", typeof(RectTransform), PrivateField),
                ("m_rotatingOuter", typeof(RectTransform), PrivateField), ("m_baseScaleMultiplier", typeof(Vector3), PrivateField),
                ("m_magnitudeTolerance", typeof(float), PrivateField), ("m_hapticsReachingFullTilt", typeof(VibrationEvent), PrivateField),
                ("m_hapticsExitingFullTilt", typeof(VibrationEvent), PrivateField), ("m_hapticsOnSelect", typeof(VibrationEvent), PrivateField),
                ("m_secondaryXSensitivity", typeof(float), PrivateField), ("m_secondaryYSensitivity", typeof(float), PrivateField),
                ("m_vibrationManagerRef", typeof(SystemRef<VibrationManager>), PrivateReadonlyField),
                ("m_cameraManagerRef", typeof(SystemRef<CinemachineCameraManager>), PrivateReadonlyField),
                ("m_isControllingCamera", typeof(bool), PrivateField), ("m_origin", typeof(Vector2), PrivateField),
                ("m_visualDisplayAreaDimensions", typeof(Vector2), PrivateField), ("m_lastTouch", typeof(Vector2), PrivateField),
                ("m_originTransformCentrePrevious", typeof(Vector3), PrivateField), ("m_state", state, PrivateField));
            foreach (string field in new[] { "m_dynamicPosition", "m_originTransform", "m_container", "m_stickTop", "m_miniOuter", "m_fullSolidOuter",
                "m_rotatingOuter", "m_hapticsReachingFullTilt", "m_hapticsExitingFullTilt", "m_hapticsOnSelect", "m_secondaryXSensitivity", "m_secondaryYSensitivity" })
                AttributeRows(type, field, typeof(SerializeField));
            AttributeRows(type, "m_baseScaleMultiplier", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_magnitudeTolerance", typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(Tooltip(type, "m_baseScaleMultiplier"), Is.EqualTo("Can be applied after the real-world-inches scaling to make base images better reflect stick extents."));
            Assert.That(Tooltip(type, "m_magnitudeTolerance"), Is.EqualTo("Acts as an inner and outer deadzone for the fine-control state to prevent flickering from tiny fractions."));
            Methods(type, ("Initialise", PublicOverride), ("OnDestroy", PrivateMethod), ("OnSaveDataLoaded", PrivateMethod), ("SetActive", PublicMethod),
                ("SetStickOrigin", PrivateMethod), ("ProcessTouch", ProtectedOverride), ("OnInput", ProtectedOverride), ("OnDrag", PublicMethod),
                ("RotateToHeading", PrivateMethod), ("OnTouchRelease", ProtectedOverride), ("SetState", PrivateMethod), ("OnInputTypeChanged", PublicOverride),
                ("SetInputType", PrivateMethod), ("TransformUpdated", InterfaceImplementation), ("GetRectTransform", InterfaceImplementation),
                ("GetTouchPosition", InterfaceImplementation), ("GetPinchInfo", InterfaceImplementation), ("SetEditing", InterfaceImplementation),
                ("SetControllingCamera", PrivateMethod), ("IsSingleTouch", ProtectedOverride), (".ctor", PublicConstructor),
                ("<Initialise>b__23_0", PrivateMethod));

            Assert.That((int)state.Attributes, Is.EqualTo(0x00000103), "private, sealed");
            Assert.That(state.IsEnum, Is.True);
            Assert.That(Enum.GetNames(state), Is.EqualTo(new[] { "HIDDEN", "IDLE", "HELD_CENTRE", "FINE_CONTROL", "FULL_TILT" }));
            Assert.That(Enum.GetValues(state).Cast<object>().Select(Convert.ToInt32), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        }
    }
}
