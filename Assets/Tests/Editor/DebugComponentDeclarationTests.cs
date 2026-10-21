using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Declarations of the nine debug components restored from Game.Runtime: ConfluenceLinkButton (TypeDef 0x020009f8),
    // DeviceTypeUtilities (0x02000a01), FlightStaminaGizmoDisplay (0x02000a06), HLProfilerDebugMenu (0x020005f9),
    // ProjectConsole (0x020005fb), DebugMenuPerformanceProfiles (0x020005f4), DebugTargetingHomingAttack (0x02000a4e),
    // RuntimeIssueTracker (0x020005ff, with 0x02000600 and 0x02000601) and UIContainerDebug (0x020008a9, with its five closures
    // 0x020008aa..0x020008ae). Flags, member order and the compiler-generated names come from the DummyDll metadata of the 1.10.1
    // build; the closure and lambda ordinals (32, 33, 43, 44; 26 and 35) follow the order of the members that precede each method.
    public sealed class DebugComponentDeclarationTests
    {
        [Test]
        public void TheConfluenceLinkButtonKeepsItsSerializedUrl()
        {
            AssertMembers(typeof(ConfluenceLinkButton), 0x00100001,
                new[] { ("m_url", 0x0001) },
                new[] { ("get_URL", 0x0886), ("OpenLink", 0x0086), ("SetURL", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ConfluenceLinkButton), 1, 2);
            Assert.That(typeof(ConfluenceLinkButton).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(Field(typeof(ConfluenceLinkButton), "m_url")), Is.EqualTo(new[] { typeof(HideInInspector), typeof(SerializeField) }));
        }

        [Test]
        public void TheDeviceTypeUtilitiesIsAStaticClassWithOneUnusedConstant()
        {
            AssertMembers(typeof(DeviceTypeUtilities), 0x00100181,
                new[] { ("UnsupportedPlatform", 0x8051) },
                new[] { ("GetAsString", 0x0096) });
            AssertIl2CppOptions(typeof(DeviceTypeUtilities), 1, 2);
            Assert.That(Field(typeof(DeviceTypeUtilities), "UnsupportedPlatform").GetRawConstantValue(), Is.EqualTo("Unsupported"));
        }

        [Test]
        public void TheFlightStaminaGizmoDisplayKeepsItsFourSerializedFields()
        {
            AssertMembers(typeof(FlightStaminaGizmoDisplay), 0x00100001,
                new[] { ("m_collider", 0x0001), ("m_flightRulerSize", 0x0001), ("m_flightRulerOffset", 0x0001), ("m_gizmoColour", 0x0001) },
                new[] { ("Reset", 0x0081), ("OnDrawGizmos", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(FlightStaminaGizmoDisplay), 1, 2);
            Assert.That(typeof(FlightStaminaGizmoDisplay).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            foreach (string name in new[] { "m_collider", "m_flightRulerSize", "m_flightRulerOffset", "m_gizmoColour" })
                Assert.That(AttributeTypes(Field(typeof(FlightStaminaGizmoDisplay), name)), Is.EqualTo(new[] { typeof(SerializeField) }), name);
        }

        [Test]
        public void TheProfilerDebugMenuKeepsItsTwoSerializedReferences()
        {
            AssertMembers(typeof(HLProfilerDebugMenu), 0x00100001,
                new[] { ("m_profilerConsole", 0x0001), ("m_runtimeIssueTracker", 0x0001) },
                new[] { ("Awake", 0x0081), ("ToggleProfiler", 0x0081), ("ToggleIssueTracker", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(HLProfilerDebugMenu), 2, 1);
            Assert.That(typeof(HLProfilerDebugMenu).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(Field(typeof(HLProfilerDebugMenu), "m_profilerConsole").FieldType, Is.EqualTo(typeof(HLProfilerConsole)));
            Assert.That(Field(typeof(HLProfilerDebugMenu), "m_runtimeIssueTracker").FieldType, Is.EqualTo(typeof(RuntimeIssueTracker)));
        }

        [Test]
        public void TheProjectConsoleDerivesFromTheLogConsoleWithoutFieldsOrOptions()
        {
            AssertMembers(typeof(ProjectConsole), 0x00100001,
                new (string, int)[0],
                new[] { ("Start", 0x0081), ("OpenConsoleFromDebugMenu", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ProjectConsole));
            Assert.That(typeof(ProjectConsole).BaseType, Is.EqualTo(typeof(HLLogConsole)));
            Assert.That(typeof(ProjectConsole).GetCustomAttributesData(), Is.Empty);
        }

        [Test]
        public void TheDebugMenuPerformanceProfilesKeepsItsFeatureArrayAndFileNameConstants()
        {
            AssertMembers(typeof(DebugMenuPerformanceProfiles), 0x00100001,
                new[] { ("m_scalableFeatures", 0x0001), ("CustomProfileFileName", 0x8051), ("CustomProfileFileExtension", 0x8051) },
                new[]
                {
                    ("get_CustomProfileFilePath", 0x0891), ("Start", 0x0081), ("AddDebugPerformanceProfileSelection", 0x0081),
                    ("AddDebugCustomPerformanceOptions", 0x0086), ("UpdateCustomAttribute", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(DebugMenuPerformanceProfiles), 1, 2);
            FieldInfo features = Field(typeof(DebugMenuPerformanceProfiles), "m_scalableFeatures");
            Assert.That(features.FieldType, Is.EqualTo(typeof(ScalableFeature[])));
            Assert.That(AttributeTypes(features), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(features), Is.EqualTo("Reference ScalableFeatures that should be editable at runtime if using a custom performance profile"));
            Assert.That(Field(typeof(DebugMenuPerformanceProfiles), "CustomProfileFileName").GetRawConstantValue(), Is.EqualTo("UserPerformanceProfile"));
            Assert.That(Field(typeof(DebugMenuPerformanceProfiles), "CustomProfileFileExtension").GetRawConstantValue(), Is.EqualTo(".json"));
        }

        [Test]
        public void TheDebugTargetingHomingAttackKeepsItsSerializedConeSettingsAndPrivateState()
        {
            AssertMembers(typeof(DebugTargetingHomingAttack), 0x00100001,
                new[]
                {
                    ("m_projectDistance", 0x0001), ("m_projectStepDistance", 0x0001), ("m_projectConeAngle", 0x0001),
                    ("m_cone", 0x0001), ("m_targets", 0x0001), ("m_validTargets", 0x0001),
                },
                new[] { ("OnDrawGizmosSelected", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(DebugTargetingHomingAttack), 2, 1);
            foreach (string name in new[] { "m_projectDistance", "m_projectStepDistance", "m_projectConeAngle" })
                Assert.That(AttributeTypes(Field(typeof(DebugTargetingHomingAttack), name)), Is.EqualTo(new[] { typeof(SerializeField) }), name);
            foreach (string name in new[] { "m_cone", "m_targets", "m_validTargets" })
                Assert.That(AttributeTypes(Field(typeof(DebugTargetingHomingAttack), name)), Is.Empty, name);
            Assert.That(Field(typeof(DebugTargetingHomingAttack), "m_cone").FieldType, Is.EqualTo(typeof(ConeVolume.ConeDescription)));
        }

        [Test]
        public void TheRuntimeIssueTrackerKeepsItsMembersAndItsLambdaOrdinals()
        {
            AssertMembers(typeof(RuntimeIssueTracker), 0x00100001,
                new[]
                {
                    ("ProfilerFrameGroupName", 0x0021), ("m_referenceResolution", 0x0021), ("m_valuesColour", 0x8051), ("m_invalidColour", 0x8051),
                    ("m_raycastUIBlocker", 0x0001), ("<IsVisible>k__BackingField", 0x0001), ("m_dataHandler", 0x0001), ("m_configurations", 0x0001),
                    ("m_dataList", 0x0021), ("m_qualityLevel", 0x0001), ("m_levelManagerRef", 0x0021), ("m_scalablePerformanceRef", 0x0021),
                    ("m_canOpen", 0x0001), ("m_canOpenWhenInvalid", 0x0001), ("m_currentScaleUI", 0x0001), ("m_userScaleUI", 0x0001),
                },
                new[]
                {
                    ("get_IsVisible", 0x0886), ("set_IsVisible", 0x0881), ("get_UIScaledWidth", 0x0881), ("get_UIScaledHeight", 0x0881),
                    ("OpenTracker", 0x0086), ("CloseTracker", 0x0086), ("SetVisible", 0x0081), ("GameInputCloseTrackerDown", 0x0081),
                    ("GameInputEndTrackerDown", 0x0081), ("Start", 0x0081), ("OnLevelManagerValid", 0x0081), ("OnDestroy", 0x0081),
                    ("OnLevelActivated", 0x0081), ("OnLevelExit", 0x0081), ("OnPerformanceProfileUpdated", 0x0081), ("UpdateTrackers", 0x0081),
                    ("GetFrameData", 0x0081), ("GetTrackerList", 0x0091), ("FixedUpdate", 0x0081), ("OnGUI", 0x0081),
                    ("GUIRenderScaleUpdate", 0x0081), ("GUIHeaderDraw", 0x0081), ("GUIBodyDraw", 0x0081), ("TryGetStat", 0x0091),
                    ("GUIStatDraw", 0x0091), ("GUILabelSliderWithButton", 0x0091), ("GUIRightAlignedLabel", 0x0091), (".ctor", 0x1886),
                    ("<SetVisible>b__26_0", 0x0081),
                });
            Assert.That(typeof(RuntimeIssueTracker).GetCustomAttributesData()[0].AttributeType, Is.EqualTo(typeof(RequireComponent)));
            AssertIl2CppOptions(typeof(RuntimeIssueTracker), 2, 1);
            Assert.That(typeof(RuntimeIssueTracker).BaseType, Is.EqualTo(typeof(MonoBehaviour)));

            FieldInfo blocker = Field(typeof(RuntimeIssueTracker), "m_raycastUIBlocker");
            Assert.That(AttributeTypes(blocker), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(blocker), Is.EqualTo("Assign a UI element that blocks raycasts."));
            Assert.That(Field(typeof(RuntimeIssueTracker), "m_valuesColour").GetRawConstantValue(), Is.EqualTo("Orange"));
            Assert.That(Field(typeof(RuntimeIssueTracker), "m_invalidColour").GetRawConstantValue(), Is.EqualTo("Red"));

            MethodInfo trackerList = typeof(RuntimeIssueTracker).GetMethod("GetTrackerList", Declared);
            Assert.That(trackerList.IsGenericMethodDefinition, Is.True);
            Assert.That(trackerList.GetGenericArguments().Length, Is.EqualTo(1));
            ParameterInfo colour = typeof(RuntimeIssueTracker).GetMethod("GUILabelSliderWithButton", Declared).GetParameters()[5];
            Assert.That(((int)colour.Attributes, colour.DefaultValue), Is.EqualTo((0x1010, (object)"white")));
        }

        [Test]
        public void TheRuntimeIssueTrackerNestedTypesKeepTheirOriginalShape()
        {
            Type data = typeof(RuntimeIssueTracker).GetNestedType("RuntimeIssueData", BindingFlags.NonPublic);
            Assert.That(data, Is.Not.Null);
            AssertMembers(data, 0x00100003,
                new[] { ("Data", 0x0006), ("Configuration", 0x0006) },
                new[] { (".ctor", 0x1886) });
            Assert.That(Field(data, "Data").FieldType, Is.EqualTo(typeof(HLProfilerDataReference)));
            Assert.That(Field(data, "Configuration").FieldType, Is.EqualTo(typeof(RuntimeIssueConfiguration)));

            Type cache = typeof(RuntimeIssueTracker).GetNestedType("<>c", BindingFlags.NonPublic);
            Assert.That(cache, Is.Not.Null);
            AssertMembers(cache, 0x00102103,
                new[] { ("<>9", 0x0036), ("<>9__35_0", 0x0016), ("<>9__35_1", 0x0016), ("<>9__35_2", 0x0016) },
                new[]
                {
                    (".cctor", 0x1891), (".ctor", 0x1886), ("<UpdateTrackers>b__35_0", 0x0083), ("<UpdateTrackers>b__35_1", 0x0083),
                    ("<UpdateTrackers>b__35_2", 0x0083),
                });
        }

        [Test]
        public void TheUIContainerDebugKeepsItsFieldsAndMethodsInTheOriginalOrder()
        {
            AssertMembers(typeof(UIContainerDebug), 0x00100101,
                new[]
                {
                    ("m_scrollRectSelection", 0x0001), ("m_containerNavigation", 0x0001), ("m_missionCompleteRoot", 0x0001),
                    ("m_missionCompleteButtonPrefab", 0x0001), ("m_respawnButton", 0x0001), ("m_actToggle", 0x0001),
                    ("m_ornamentToggle", 0x0001), ("m_invincibilityToggle", 0x0001), ("m_fpsToggle", 0x0001), ("m_poolsToggle", 0x0001),
                    ("m_missionTimerUnlimitedToggle", 0x0001), ("m_challengeTestModeToggle", 0x0001), ("m_unlockDreamPowersToggle", 0x0001),
                    ("m_challengeCycleButtonNext", 0x0001), ("m_challengeCycleButtonPrevious", 0x0001), ("m_challengeFeatureToggle", 0x0001),
                    ("m_xpPlus1k", 0x0001), ("m_xpPlus10k", 0x0001), ("m_xpPlus100k", 0x0001), ("m_xpReset", 0x0001),
                    ("ChallengeXP", 0x0026), ("ChallengeCycleLabel", 0x0026), ("m_missionManagerRef", 0x0021), ("m_characterManagerRef", 0x0021),
                    ("m_appRef", 0x0021), ("m_saveManagerRef", 0x0021), ("m_trackManagerRef", 0x0021), ("m_ornamentManager", 0x0001),
                    ("m_challengeManager", 0x0001), ("m_pressedMissionButtonIndex", 0x0001),
                },
                new[]
                {
                    ("Start", 0x0081), ("OnCharacterManagerValid", 0x0081), ("OnMissionManagerValid", 0x0081), ("OnChallengeManagerValid", 0x0081),
                    ("UpdateChallengeXPLabel", 0x0081), ("AddXP", 0x0081), ("ResetXP", 0x0081), ("ChallengeCycleAdvance", 0x0081),
                    ("ChallengeCycleRegress", 0x0081), ("OnUpdatedChallengeCycle", 0x0081), ("OnDreamPowerStoreManagerValid", 0x0081),
                    ("SetupForMenu", 0x0081), ("CanSetupForMissionSelect", 0x0081), ("SetupForMission", 0x0081),
                    ("SetupMissionObjectiveButton", 0x0086), ("Action_OnUnlockAllLevelsToggle", 0x0086), ("SetUnlockAllLevels", 0x0091),
                    ("OnOrnamentManagerValid", 0x0081), ("Action_OnUnlockAllOrnamentsToggle", 0x0086), ("Action_ToggleInvincibility", 0x0086),
                    ("Action_OnFPSToggle", 0x0086), ("SetFPSToggle", 0x0081), ("SetPoolsToggleState", 0x0081), ("Action_OnPoolsToggle", 0x0086),
                    ("Action_OnMissionTimerToggle", 0x0086), ("SetRespawnButton", 0x0081), ("Action_OnRespawnRequest", 0x0086),
                    ("GetNextRespawnPoint", 0x0091), ("Action_TestNotification", 0x0086), ("Action_OnChallengeTestModeToggle", 0x0086),
                    ("Action_OnChallengeFeatureToggle", 0x0086), ("Action_UnlockDreamPowersToggle", 0x0086), ("Action_OnWidgetSelected", 0x0086),
                    (".ctor", 0x1886), ("<OnChallengeManagerValid>b__33_0", 0x0081), ("<OnChallengeManagerValid>b__33_1", 0x0081),
                    ("<OnChallengeManagerValid>b__33_2", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerDebug), 2, 1);
            Assert.That(typeof(UIContainerDebug).BaseType, Is.EqualTo(typeof(UIContainer)));

            // The XP buttons were renamed from 10/100/1000; each keeps the old name, in the attribute order of the original.
            Assert.That(AttributeTypes(Field(typeof(UIContainerDebug), "m_xpPlus1k")), Is.EqualTo(new[] { typeof(FormerlySerializedAsAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(UIContainerDebug), "m_xpPlus10k")), Is.EqualTo(new[] { typeof(SerializeField), typeof(FormerlySerializedAsAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(UIContainerDebug), "m_xpPlus100k")), Is.EqualTo(new[] { typeof(SerializeField), typeof(FormerlySerializedAsAttribute) }));
            Assert.That(FormerName("m_xpPlus1k"), Is.EqualTo("m_xpPlus10"));
            Assert.That(FormerName("m_xpPlus10k"), Is.EqualTo("m_xpPlus100"));
            Assert.That(FormerName("m_xpPlus100k"), Is.EqualTo("m_xpPlus1000"));
        }

        private static string FormerName(string field) =>
            (string)Field(typeof(UIContainerDebug), field).GetCustomAttributesData()
                .First(data => data.AttributeType == typeof(FormerlySerializedAsAttribute)).ConstructorArguments[0].Value;

        [Test]
        public void TheUIContainerDebugClosuresKeepTheirCompilerNamesAndCaptureOrder()
        {
            Type first = typeof(UIContainerDebug).GetNestedType("<>c__DisplayClass32_0", BindingFlags.NonPublic);
            AssertMembers(first, 0x00100103,
                new[] { ("app", 0x0006), ("<>4__this", 0x0006) },
                new[] { (".ctor", 0x1886) });

            Type loop = typeof(UIContainerDebug).GetNestedType("<>c__DisplayClass32_1", BindingFlags.NonPublic);
            AssertMembers(loop, 0x00100103,
                new[] { ("button", 0x0006), ("activeMissionState", 0x0006), ("CS$<>8__locals1", 0x0006) },
                new[] { (".ctor", 0x1886), ("<OnMissionManagerValid>b__0", 0x0083), ("<OnMissionManagerValid>g__Select|1", 0x0083) });

            Type navigation = typeof(UIContainerDebug).GetNestedType("<>c__DisplayClass32_2", BindingFlags.NonPublic);
            AssertMembers(navigation, 0x00100103,
                new[] { ("navigation", 0x0006) },
                new[] { (".ctor", 0x1886), ("<OnMissionManagerValid>b__2", 0x0083) });

            Type setup = typeof(UIContainerDebug).GetNestedType("<>c__DisplayClass43_0", BindingFlags.NonPublic);
            AssertMembers(setup, 0x00100103,
                new[] { ("nextMission", 0x0006), ("nextMissionSave", 0x0006), ("button", 0x0006), ("<>4__this", 0x0006) },
                new[] { (".ctor", 0x1886), ("<SetupForMission>b__0", 0x0083), ("<SetupForMission>g__Select|1", 0x0083) });

            Type objective = typeof(UIContainerDebug).GetNestedType("<>c__DisplayClass44_0", BindingFlags.NonPublic);
            AssertMembers(objective, 0x00100103,
                new[] { ("activeMissionState", 0x0006), ("<>4__this", 0x0006), ("buttonIndex", 0x0006) },
                new[] { (".ctor", 0x1886), ("<SetupMissionObjectiveButton>g__Callback|0", 0x0083) });
        }
    }
}
