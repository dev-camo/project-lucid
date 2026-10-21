using System;
using Cinemachine;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Emission-only checks of the declarations of the restored camera proxy, trigger, extension and assigner components.
    // Flags, field and method order, attribute rows and the compiler-generated lambda names come from the original DummyDll
    // metadata of the 1.10.1 build (CameraProxyAssignTrigger 0x0200025C, CameraProxyTarget_Recentering 0x02000261,
    // CinemachineRegisterForGimbalLock 0x0200028D, CinemachineRelativeOffsetExtension 0x02000290,
    // CinemachineSceneTriggerCamera 0x02000291, CinemachineSlopeAngleClamp 0x02000293,
    // CinemachineTargetGroupAssigner_CameraProxy 0x02000296, CinemachineTargetGroupAssigner_PlayerCharacter 0x02000297,
    // CinemachineYUpOverrideTrigger 0x02000298, SequencedCamera 0x02000611 and its enum 0x02000612,
    // CameraProxySettingsOverrideTrigger 0x02000733, SnapCameraToCharacterHeadingTrigger 0x0200073E,
    // SmoothedTransformProxy_CameraWorldSpace 0x02000A28 and FTUECameraSection 0x02000A69). They say nothing about
    // behaviour.
    public sealed class CameraProxyAndTriggerDeclarationTests
    {
        private const int Private = 0x0001, PrivateReadOnly = 0x0021, PrivateConstant = 0x8051;
        private const int PrivateMethod = 0x0081, PrivateStaticMethod = 0x0091, PublicMethod = 0x0086;
        private const int ProtectedOverride = 0x00c4, PublicOverride = 0x00c6, ProtectedVirtualNewSlot = 0x01c4;
        private const int ProtectedGetterOverride = 0x08c4, Constructor = 0x1886;

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
        public void EmissionCheck_CameraProxyComponentsKeepTheirDeclarations()
        {
            Check(typeof(CameraProxyAssignTrigger), typeof(TransformAssignEventTrigger), new[] { 1, 2 },
                Fields(PrivateReadOnly, "m_cameraManagerRef"),
                ("get_SetNullOnExit", ProtectedGetterOverride), ("GetTargetTransform", ProtectedOverride), (".ctor", Constructor));

            Check(typeof(CameraProxySettingsOverrideTrigger), typeof(CharacterTrigger), new[] { 1, 2 },
                Fields(Private, "m_overrideSettings").Plus(("m_cameraManagerRef", PrivateReadOnly), ("m_cameraOverrideHandle", Private)),
                ("DoTriggerEnter", ProtectedOverride), ("DoTriggerExit", ProtectedOverride), ("OnDisable", PrivateMethod),
                ("ClearOverride", PublicMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CameraProxySettingsOverrideTrigger), "m_overrideSettings", typeof(HeaderAttribute), typeof(SerializeField));

            Check(typeof(CameraProxyTarget_Recentering), typeof(CameraProxyTarget), new[] { 1, 2 },
                Fields(Private, "m_headingExtensionFreeLook").Plus(("m_cameraManagerRef", PrivateReadOnly), ("m_hasHeadingExtension", Private)),
                ("Awake", ProtectedOverride), ("OnDestroy", PublicOverride), ("OnCameraManagerValid", PrivateMethod),
                ("OnCameraLive", PrivateMethod), ("TrySetHeadingExtension", PrivateMethod),
                ("UpdateSmoothedTransformProxy", ProtectedOverride), ("UpdateSmoothedTransformProxy", ProtectedOverride),
                (".ctor", Constructor));

            Check(typeof(SmoothedTransformProxy_CameraWorldSpace), typeof(SmoothedTransformProxy), new[] { 2, 1 },
                Fields(Private, "m_forwardRotationChild").Plus(("m_cameraYAxisProxyManagerRef", PrivateReadOnly),
                    ("m_cameraYAxisProxyManager", Private)),
                ("Awake", ProtectedOverride), ("UpdatePositionAndRotation", PublicOverride), ("SetRotation", ProtectedOverride),
                (".ctor", Constructor), ("<Awake>b__3_0", PrivateMethod));
            AssertAttributes(typeof(SmoothedTransformProxy_CameraWorldSpace), "m_forwardRotationChild", typeof(SerializeField));
        }

        [Test]
        public void EmissionCheck_CinemachineExtensionsKeepTheirDeclarations()
        {
            Check(typeof(CinemachineRelativeOffsetExtension), typeof(CinemachineExtension), new[] { 2, 1 },
                Fields(Private, "m_applyAfterStage", "m_followRelativeOffset"),
                ("PostPipelineStageCallback", ProtectedOverride), (".ctor", Constructor));
            AssertAttributes(typeof(CinemachineRelativeOffsetExtension), "m_applyAfterStage", typeof(SerializeField));
            AssertAttributes(typeof(CinemachineRelativeOffsetExtension), "m_followRelativeOffset", typeof(SerializeField));

            Check(typeof(CinemachineSlopeAngleClamp), typeof(CinemachineExtension), new[] { 2, 1 },
                Fields(Private, "m_applyAfterStage", "m_timeCategory", "m_upwardSlopeAngle", "m_downwardSlopeAngle", "m_clampedYAxisValue",
                    "m_lowerThreshold", "m_angleCorrectionCurve")
                    .Plus(("m_characterManagerRef", PrivateReadOnly), ("m_timeManagerRef", PrivateReadOnly),
                        ("m_unclampedYAxisValue", Private), ("m_lastFrameNumber", Private), ("m_clampApplied", Private)),
                ("PostPipelineStageCallback", ProtectedOverride), ("ApplyCameraAxisClamp", PrivateMethod),
                ("RemoveCameraAxisClamp", PrivateMethod), ("ResetClampingValues", PrivateMethod),
                ("TryGetFreeLookCam", PrivateStaticMethod), (".ctor", Constructor));
            AssertAttributes(typeof(CinemachineSlopeAngleClamp), "m_upwardSlopeAngle", typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CinemachineSlopeAngleClamp), "m_downwardSlopeAngle", typeof(TooltipAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CinemachineSlopeAngleClamp), "m_lowerThreshold", typeof(SerializeField), typeof(TooltipAttribute));
            AssertAttributes(typeof(CinemachineSlopeAngleClamp), "m_angleCorrectionCurve", typeof(TooltipAttribute), typeof(SerializeField));
        }

        [Test]
        public void EmissionCheck_CameraTriggersKeepTheirDeclarations()
        {
            Check(typeof(CinemachineSceneTriggerCamera), typeof(CharacterTrigger), new[] { 2, 1 },
                Fields(PrivateConstant, "DisabledPriority")
                    .Plus(Fields(Private, "m_virtualCammera", "m_destroyOnExit", "m_exitAfterSeconds", "m_lookAtCharacter",
                        "m_onTriggerCameraPriority"))
                    .Plus(("m_levelManagerRef", PrivateReadOnly)),
                ("Start", PrivateMethod), ("OnDestroy", PrivateMethod), ("OnLevelActivated", PrivateMethod),
                ("DisableCamera", PrivateMethod), ("DoTriggerEnter", ProtectedOverride), ("DoTriggerExit", ProtectedOverride),
                ("OnTriggerTimeout", PrivateMethod), ("ExitCamera", PublicMethod), (".ctor", Constructor),
                ("<Start>b__7_0", PrivateMethod), ("<OnDestroy>b__8_0", PrivateMethod));
            AssertAttributes(typeof(CinemachineSceneTriggerCamera), "m_virtualCammera", typeof(FormerlySerializedAsAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CinemachineSceneTriggerCamera), "m_destroyOnExit", typeof(FormerlySerializedAsAttribute), typeof(SerializeField));
            AssertAttributes(typeof(CinemachineSceneTriggerCamera), "m_exitAfterSeconds", typeof(SerializeField), typeof(FormerlySerializedAsAttribute));

            Check(typeof(CinemachineYUpOverrideTrigger), typeof(CharacterTrigger), new[] { 1, 2 },
                Fields(Private, "m_targetData", "m_removeOnDisable", "m_overrideHandle"),
                ("DoTriggerEnter", ProtectedOverride), ("DoTriggerExit", ProtectedOverride), ("OnDisable", PrivateMethod),
                ("RemoveCameraYUpOverride", PrivateMethod), (".ctor", Constructor));

            Check(typeof(FTUECameraSection), typeof(CharacterTrigger), new[] { 2, 1 },
                Fields(PrivateReadOnly, "m_cameraManagerRef"),
                ("ToggleRequiredSettings", PublicMethod), (".ctor", Constructor));

            Check(typeof(SnapCameraToCharacterHeadingTrigger), typeof(CharacterTrigger), new[] { 2, 1 },
                Fields(Private, "m_snapDelaySeconds", "m_allowStickyControls", "m_triggerOnEnter", "m_triggerOnExit", "m_delayRoutine"),
                ("DoTriggerEnter", ProtectedOverride), ("DoTriggerExit", ProtectedOverride), ("DoTrigger", PrivateMethod),
                ("SnapToCharacterHeading", PrivateMethod), (".ctor", Constructor));
        }

        [Test]
        public void EmissionCheck_AssignersAndTheSequencedEffectKeepTheirDeclarations()
        {
            Check(typeof(CinemachineRegisterForGimbalLock), typeof(MonoBehaviour), new[] { 1, 2 },
                Fields(Private, "m_virtualCamera"),
                ("Start", PrivateMethod), (".ctor", Constructor));

            Check(typeof(CinemachineTargetGroupAssigner_CameraProxy), typeof(CinemachineTargetGroupAssigner), new[] { 1, 2 },
                Fields(PrivateReadOnly, "m_cameraManagerRef"),
                ("OnEnable", PrivateMethod), ("OnDisable", PrivateMethod), ("OnCameraManagerValid", PrivateMethod), (".ctor", Constructor));

            Check(typeof(CinemachineTargetGroupAssigner_PlayerCharacter), typeof(CinemachineTargetGroupAssigner), new[] { 1, 2 },
                Fields(Private, "m_characterManagerRef"),
                ("Start", PrivateMethod), ("OnCharacterManagerValid", PrivateMethod), ("OnCharacterChange", ProtectedVirtualNewSlot),
                ("OnCharacterPreClose", PrivateMethod), ("SetCharacter", PrivateMethod), ("OnDestroy", PrivateMethod),
                (".ctor", Constructor));

            Check(typeof(SequencedCamera), typeof(SequencedEffect), new[] { 1, 2 },
                Fields(Private, "m_triggerParameter", "m_cameraTypeManipulator"),
                ("OnEffectTriggered", ProtectedOverride), (".ctor", Constructor));
            AssertAttributes(typeof(SequencedCamera), "m_triggerParameter", typeof(SerializeField));
            AssertAttributes(typeof(SequencedCamera), "m_cameraTypeManipulator", typeof(SerializeField));

            Type parameter = typeof(SequencedCamera).GetNestedType("TriggerParameter", System.Reflection.BindingFlags.NonPublic);
            AssertMembers(parameter, 0x00000103, new[] { ("value__", 0x0606), ("Start", 0x8056), ("End", 0x8056) }, new (string, int)[0]);
        }
    }
}
