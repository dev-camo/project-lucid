using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Emission-only checks of the declarations of the restored camera components. Flags, field and method order and
    // attribute rows come from the original DummyDll metadata of the 1.10.1 build (AutoPlayCameraStartPoint
    // 0x020001D0, CameraAversionWithLimit 0x02000256, CameraFacingListener 0x02000257, CameraRequestChange
    // 0x02000262, CameraTypeManipulator 0x02000263, CameraTypeTriggerVolume 0x02000264,
    // CinemachineAddTransformToTargetGroup 0x02000268, CinemachineAssignTargetToCameraType 0x02000269,
    // CinemachineCameraResetYRecenter 0x0200026F, CinemachineCameraYAxisOverride 0x02000270, CameraInputHandler
    // 0x020006A7, CameraHeadingTrigger 0x02000731 and its enum 0x02000732). They say nothing about behaviour.
    public sealed class CameraComponentDeclarationTests
    {
        private const int Private = 0x0001, PrivateReadOnly = 0x0021;
        private const int PrivateMethod = 0x0081, PublicMethod = 0x0086, ProtectedOverride = 0x00c4, Constructor = 0x1886;

        private static (string, int)[] Fields(int flags, params string[] names)
        {
            var rows = new (string, int)[names.Length];
            for (int i = 0; i < names.Length; i++) rows[i] = (names[i], flags);
            return rows;
        }

        private static void Check(Type type, Type baseType, int[] options, (string name, int flags)[] fields, params (string name, int flags)[] methods)
        {
            AssertMembers(type, 0x00100001, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static void AssertAttributes(Type type, string field, params Type[] expected) =>
            Assert.That(AttributeTypes(Field(type, field)), Is.EqualTo(expected), type.Name + "." + field);

        [Test]
        public void EmissionCheck_StartPointAndSimpleComponentsKeepTheirDeclarations()
        {
            Check(typeof(AutoPlayCameraStartPoint), typeof(MonoBehaviour), new[] { 2, 1 }, new (string, int)[0],
                ("Awake", PrivateMethod), ("OnCameraManagerAvailable", PrivateMethod), (".ctor", Constructor));

            Check(typeof(CameraRequestChange), typeof(MonoBehaviour), new[] { 2, 1 },
                Fields(Private, "m_cameraType", "m_cinemachineCameraManger"),
                ("Start", PrivateMethod), ("Action_RequestCameraChange", PublicMethod), (".ctor", Constructor), ("<Start>b__2_0", PrivateMethod));
            AssertAttributes(typeof(CameraRequestChange), "m_cameraType", typeof(SerializeField));

            Check(typeof(CameraAversionWithLimit), typeof(TimeScaledComponent_SDT), new[] { 2, 1 },
                Fields(Private, "m_orbitRadius", "m_affectX", "m_affectY", "m_affectZ", "m_cameraManagerRef", "m_camera",
                    "m_relativeDirection", "m_transform", "m_originalLocal"),
                ("Awake", ProtectedOverride), ("OnEnable", ProtectedOverride), ("InternalUpdate", ProtectedOverride),
                (".ctor", Constructor), ("<OnEnable>b__10_0", PrivateMethod));

            Check(typeof(CinemachineAddTransformToTargetGroup), typeof(MonoBehaviour), new[] { 1, 2 },
                Fields(Private, "m_targetGroup", "m_weight", "m_radius", "m_addOnEnable", "m_removeOnDisable", "m_removeAllOthers",
                    "m_restoreAllOthersOnRemove", "m_clearedTargetGroup"),
                ("AddTransformToGroup", PublicMethod), ("OverrideTargetGroup", PublicMethod), ("RemoveTransformFromGroup", PublicMethod),
                ("OnEnable", PrivateMethod), ("OnDisable", PrivateMethod), (".ctor", Constructor));
        }

        [Test]
        public void EmissionCheck_FacingListenerAndCameraTypeComponentsKeepTheirDeclarations()
        {
            Check(typeof(CameraFacingListener), typeof(MonoBehaviour), new[] { 2, 1 },
                Fields(Private, "m_lookTarget", "m_alignmentThresholdEnter", "m_alignmentThresholdLeave", "m_onCameraFacingEnter",
                    "m_onCameraFacingStay", "m_onCameraFacingLeave", "m_entered", "m_characterManagerRef", "m_cameraManagerRef"),
                ("Awake", PrivateMethod), ("OnEnable", PrivateMethod), ("Update", PrivateMethod), ("SetEntered", PrivateMethod),
                ("OnValidate", PrivateMethod), ("Reset", PrivateMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CameraFacingListener), "m_alignmentThresholdEnter",
                typeof(DelayedAttribute), typeof(UnityEngine.RangeAttribute), typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CameraFacingListener), "m_alignmentThresholdLeave",
                typeof(TooltipAttribute), typeof(SerializeField), typeof(UnityEngine.RangeAttribute), typeof(DelayedAttribute));

            Check(typeof(CameraTypeManipulator), typeof(MonoBehaviour), new[] { 2, 1 },
                Fields(Private, "m_cameraType", "m_transform", "m_cameraManagerRef", "m_onComplete", "m_stackableDataHandle"),
                ("Awake", PrivateMethod), ("ApplyCameraType", PublicMethod), ("RemoveCameraType", PublicMethod),
                ("ApplyCameraType", PublicMethod), ("RemoveCameraType", PublicMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CameraTypeManipulator), "m_cameraType", typeof(SerializeField), typeof(TooltipAttribute));
            AssertAttributes(typeof(CameraTypeManipulator), "m_transform", typeof(TooltipAttribute), typeof(SerializeField));

            Check(typeof(CameraTypeTriggerVolume), typeof(CharacterTrigger), new[] { 2, 1 },
                Fields(Private, "m_cameraType", "m_followOverride", "m_lookAtOverride", "m_delayTypeOverrideSeconds",
                    "m_delaySetTargetsSeconds", "m_handle", "m_applyOverrideRoutine", "m_setTargetsRoutine")
                    .Plus(("m_cameraManagerRef", PrivateReadOnly)),
                ("DoTriggerEnter", ProtectedOverride), ("DoTriggerExit", ProtectedOverride), ("OnDisable", PrivateMethod),
                ("ApplyOverride", PrivateMethod), ("TryRemoveOverride", PrivateMethod), ("SetCameraTargets", PrivateMethod),
                ("StopRoutines", PrivateMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CameraTypeTriggerVolume), "m_delayTypeOverrideSeconds", typeof(TooltipAttribute), typeof(SerializeField));
        }

        [Test]
        public void EmissionCheck_CinemachineClientComponentsKeepTheirDeclarations()
        {
            Check(typeof(CinemachineAssignTargetToCameraType), typeof(MonoBehaviour), new[] { 1, 2 },
                Fields(Private, "m_cameraType", "m_target", "m_setAsLookAtTarget", "m_setAsFollowTarget")
                    .Plus(("m_cameraManagerRef", PrivateReadOnly)),
                ("OnEnable", PrivateMethod), ("OnDisable", PrivateMethod), ("OnCameraManagerValid", PrivateMethod),
                ("OnMainCameraReady", PrivateMethod), (".ctor", Constructor));

            Check(typeof(CinemachineCameraResetYRecenter), typeof(MonoBehaviour), new[] { 1, 2 },
                Fields(Private, "m_targetCamera", "m_resetValue", "m_cameraTypesToAllowsResetFrom")
                    .Plus(("m_cameraManagerRef", PrivateReadOnly), ("m_levelManagerRef", PrivateReadOnly),
                        ("m_allowResetOnCameraLive", Private), ("m_virtualCamerasToAlwaysResetFrom", Private)),
                ("OnEnable", PrivateMethod), ("LevelManagerReady", PrivateMethod), ("OnLevelActivated", PrivateMethod),
                ("OnDisable", PrivateMethod), ("CameraManagerReady", PrivateMethod), ("OnCameraLive", PublicMethod),
                ("Action_SetAllowResetOnCameraLive", PublicMethod), ("ResetYAxisValue", PrivateMethod), (".ctor", Constructor));

            Check(typeof(CinemachineCameraYAxisOverride), typeof(MonoBehaviour), new[] { 1, 2 },
                Fields(Private, "m_targetCamera", "m_yAxisStateOverrides").Plus(("m_cameraManagerRef", PrivateReadOnly)),
                ("Awake", PrivateMethod), ("OnCameraManagerReady", PrivateMethod), ("OnCameraSensitivityChanged", PrivateMethod),
                ("OnDestroy", PrivateMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CinemachineCameraYAxisOverride), "m_targetCamera", typeof(TooltipAttribute), typeof(SerializeField));
        }

        [Test]
        public void EmissionCheck_InputHandlerAndHeadingTriggerKeepTheirDeclarations()
        {
            Check(typeof(CameraInputHandler), typeof(InputHandler), new[] { 1, 2 },
                Fields(PrivateReadOnly, "m_dataManagerRef", "m_controlsMapping"),
                (".ctor", Constructor), ("ApplyInput", PublicMethod));

            Check(typeof(CameraHeadingTrigger), typeof(CharacterTrigger), new[] { 1, 2 },
                Fields(Private, "m_headingOverrides", "m_headingSelectionMode", "m_useReleaseTimer", "m_releaseAfterSeconds",
                        "m_onlyTriggerOnce", "m_continuouslyUpdate", "m_continuousUpdateSwitchDelaySeconds")
                    .Plus(("m_cameraManagerRef", PrivateReadOnly), ("m_timeManagerRef", PrivateReadOnly), ("m_settingHandle", Private),
                        ("m_releaseTimerRoutine", Private), ("m_canTrigger", Private), ("m_currentHeadingOverride", Private),
                        ("m_continuousUpdateSwitchTimerSeconds", Private)),
                ("Awake", ProtectedOverride), ("DoTriggerEnter", ProtectedOverride), ("DoTriggerStay", ProtectedOverride),
                ("DoTriggerExit", ProtectedOverride), ("OnDisable", PrivateMethod), ("Action_ApplySettingOverride", PublicMethod),
                ("Action_RemoveSettingOverride", PublicMethod), ("ApplyCameraSettingOverride", PrivateMethod),
                ("RemoveCameraSettingOverride", PrivateMethod), ("GetHeadingOverride", PrivateMethod), ("SelectClosest", PrivateMethod),
                ("SelectBestAlignedIncomingAngle", PrivateMethod), ("SelectBestAlignedCharacterForward", PrivateMethod),
                ("SelectBestAlignedCharacterVelocity", PrivateMethod), ("SelectBestAlignedCharacterDirection", PrivateMethod),
                ("SelectBestAlignedCameraForward", PrivateMethod), ("IsHeadingValidSelection", PrivateMethod),
                ("UpdateCachedValues", PrivateMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CameraHeadingTrigger), "m_useReleaseTimer", typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CameraHeadingTrigger), "m_releaseAfterSeconds", typeof(ShowIfAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CameraHeadingTrigger), "m_onlyTriggerOnce", typeof(SerializeField), typeof(TooltipAttribute));
            AssertAttributes(typeof(CameraHeadingTrigger), "m_continuouslyUpdate", typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CameraHeadingTrigger), "m_continuousUpdateSwitchDelaySeconds",
                typeof(ShowIfAttribute), typeof(TooltipAttribute), typeof(SerializeField));

            Type mode = typeof(CameraHeadingTrigger).GetNestedType("SelectionMode", System.Reflection.BindingFlags.NonPublic);
            AssertMembers(mode, 0x00000103,
                new[]
                {
                    ("value__", 0x0606), ("BestAlignedToIncomingDirection", 0x8056), ("BestAlignedToCharacterForward", 0x8056),
                    ("BestAlignedToCameraForward", 0x8056), ("ClosestDistance", 0x8056), ("BestAlignedToCharacterVelocity", 0x8056)
                },
                new (string, int)[0]);
            Assert.That(Enum.GetValues(mode).Length, Is.EqualTo(5));
            Assert.That(Enum.ToObject(mode, 3).ToString(), Is.EqualTo("ClosestDistance"));
        }
    }

    internal static class DeclarationRows
    {
        internal static (string, int)[] Plus(this (string, int)[] rows, params (string, int)[] more)
        {
            var all = new (string, int)[rows.Length + more.Length];
            rows.CopyTo(all, 0);
            more.CopyTo(all, rows.Length);
            return all;
        }
    }
}
