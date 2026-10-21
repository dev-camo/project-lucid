using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The small debug components of Game.Runtime, each as a real component on an owned inactive host: ConfluenceLinkButton
    // (x86-64 0x6AF7B0..0x6AF877), DeviceTypeUtilities (0x69BBF0), FlightStaminaGizmoDisplay (0x6B0B40..0x6B0CE2), ProjectConsole
    // (0x5604C0..0x560578), DebugTargetingHomingAttack (0x6BE260..0x6BE78A) and DebugMenuPerformanceProfiles (0x55E210..0x55E92C).
    // Not covered: ConfluenceLinkButton.OpenLink (it opens the system browser), the drawing of FlightStaminaGizmoDisplay.OnDrawGizmos
    // and of DebugTargetingHomingAttack (the engine accepts Gizmos calls only inside its own gizmo callback), the rest of
    // DebugMenuPerformanceProfiles.AddDebugCustomPerformanceOptions after the profile exists (it reads the user's profile file) and
    // UpdateCustomAttribute (it writes that file).
    public sealed class DebugSupportComponentTests
    {
        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The shared TimeScaledComponent_SDT lookup is read by the editor Reset and OnValidate of a HomingTarget.
        private static readonly System.Reflection.FieldInfo TimeConfig =
            typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        // 0x6af849 stores the Confluence page literal into m_url before the MonoBehaviour base call.
        [Test]
        public void AFreshConfluenceButtonHoldsTheAuthoredPageUrl()
        {
            Owned.Run(owned =>
            {
                var button = owned.Host("Lucid confluence link", false).AddComponent<ConfluenceLinkButton>();
                Assert.That(button.URL, Is.EqualTo("https://hardlight.atlassian.net/wiki/spaces/HL/pages/71368746/SDT+Environment+collision+workflow"));
                Assert.That(Get<string>(button, "m_url"), Is.EqualTo(button.URL));
            });
        }

        // SetURL 0x6AF820 is a bare return: the argument is never stored.
        [Test]
        public void SettingTheUrlDoesNotReplaceIt()
        {
            Owned.Run(owned =>
            {
                var button = owned.Host("Lucid confluence link", false).AddComponent<ConfluenceLinkButton>();
                string authored = button.URL;
                button.SetURL("https://example.invalid/replacement");
                button.SetURL(null);
                Assert.That(button.URL, Is.EqualTo(authored));
            });
        }

        // GetAsString 0x69BBF0 tail-calls SystemInfo.get_deviceModel.
        [Test]
        public void TheDeviceTypeIsTheSystemDeviceModel()
        {
            Assert.That(DeviceTypeUtilities.GetAsString(), Is.EqualTo(SystemInfo.deviceModel));
        }

        // The constructor 0x6B0CD0 stores 1,1,1,1 (the constant at 0x29a70b0) into m_gizmoColour; the other fields stay zero.
        [Test]
        public void AFreshFlightGizmoIsOpaqueWhiteWithAnEmptyRuler()
        {
            Owned.Run(owned =>
            {
                var display = owned.Host("Lucid flight stamina gizmo", false).AddComponent<FlightStaminaGizmoDisplay>();
                Assert.That(Get<Color>(display, "m_gizmoColour"), Is.EqualTo(new Color(1f, 1f, 1f, 1f)));
                Assert.That(Get<Vector3>(display, "m_flightRulerSize"), Is.EqualTo(Vector3.zero));
                Assert.That(Get<Vector3>(display, "m_flightRulerOffset"), Is.EqualTo(Vector3.zero));
            });
        }

        // Reset 0x6B0B40 takes GetComponent<SphereCollider>() of its own object, and clears the field when there is none.
        [Test]
        public void ResetTakesTheSphereColliderOfTheSameObject()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid flight stamina gizmo", false);
                var display = host.AddComponent<FlightStaminaGizmoDisplay>();
                Assert.That(Get<SphereCollider>(display, "m_collider") == null, Is.True, "no collider yet");

                var sphere = host.AddComponent<SphereCollider>();
                Call(display, "Reset");
                Assert.That(Get<SphereCollider>(display, "m_collider"), Is.SameAs(sphere));

                Object.DestroyImmediate(sphere);
                Call(display, "Reset");
                Assert.That(Get<SphereCollider>(display, "m_collider") == null, Is.True, "Reset overwrites the field with the missing component");
            });
        }

        // OnDrawGizmos 0x6B0B90 returns at once while the collider reference is missing, before any gizmo call.
        [Test]
        public void TheFlightGizmoDrawsNothingWithoutACollider()
        {
            Owned.Run(owned =>
            {
                var display = owned.Host("Lucid flight stamina gizmo", false).AddComponent<FlightStaminaGizmoDisplay>();
                Assert.DoesNotThrow(() => Call(display, "OnDrawGizmos"));
            });
        }

        // ProjectConsole.Start 0x5604C0 stores CoreGameConfiguration.DeviceConsoleEnabled (+0x40) as the component's enabled state.
        [TestCase(true, TestName = "StartEnablesTheProjectConsoleWhenTheConfigurationAllowsTheDeviceConsole")]
        [TestCase(false, TestName = "StartDisablesTheProjectConsoleWhenTheConfigurationDoesNot")]
        public void StartFollowsTheDeviceConsoleSetting(bool deviceConsoleEnabled)
        {
            Owned.Run(owned =>
            {
                SystemConfiguration configuration = owned.Register(new SystemConfiguration());
                var config = owned.Track(ScriptableObject.CreateInstance<CoreGameConfiguration>());
                SetSerialized(config, "m_deviceConsoleEnabled", property => property.boolValue = deviceConsoleEnabled);
                configuration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(CoreGameConfiguration)), (SystemConfigurationAsset)config);
                var console = owned.Host("Lucid project console", false).AddComponent<ProjectConsole>();
                console.enabled = !deviceConsoleEnabled;

                Call(console, "Start");

                Assert.That(console.enabled, Is.EqualTo(deviceConsoleEnabled));
            });
        }

        // OpenConsoleFromDebugMenu 0x560520 tail-calls HLLogConsole.Action_ConsoleShow.
        [Test]
        public void OpeningTheConsoleFromTheDebugMenuShowsIt()
        {
            Owned.Run(owned =>
            {
                var console = owned.Host("Lucid project console", false).AddComponent<ProjectConsole>();
                var asset = owned.Track(ScriptableObject.CreateInstance<HLLogConsoleScriptableObject>());
                SetSerialized(console, "m_logConsoleAsset", property => property.objectReferenceValue = asset);
                int opened = 0;
                console.OnConsoleOpen += () => opened++;
                Assert.That(console.IsConsoleVisible, Is.False);

                Call(console, "OpenConsoleFromDebugMenu");

                Assert.That(console.IsConsoleVisible, Is.True);
                Assert.That(opened, Is.EqualTo(1));
            });
        }

        // Constructor 0x6BE6D0: distance 10 and step 5 (the pair at 0x29a76f0), a 30 by 30 degree cone (0x41f00000 twice) and two
        // empty target lists.
        [Test]
        public void AFreshHomingAttackDebugHoldsTheAuthoredConeAndEmptyLists()
        {
            Owned.Run(owned =>
            {
                var attack = owned.Host("Lucid homing attack debug", false).AddComponent<DebugTargetingHomingAttack>();
                Assert.That(Get<float>(attack, "m_projectDistance"), Is.EqualTo(10f));
                Assert.That(Get<float>(attack, "m_projectStepDistance"), Is.EqualTo(5f));
                Assert.That(Get<Vector2>(attack, "m_projectConeAngle"), Is.EqualTo(new Vector2(30f, 30f)));
                Assert.That(Get<List<HomingTarget>>(attack, "m_targets"), Is.Not.Null.And.Empty);
                Assert.That(Get<List<HomingTarget>>(attack, "m_validTargets"), Is.Not.Null.And.Empty);
                Assert.That(Get<List<HomingTarget>>(attack, "m_targets"), Is.Not.SameAs(Get<List<HomingTarget>>(attack, "m_validTargets")));
            });
        }

        // The homing target for this component's cone: its WorldPosition is its BoxCollider's bounds centre.
        private static HomingTarget TargetAt(Owned owned, float x, float y, float z)
        {
            GameObject host = owned.Host("Lucid homing target");
            host.transform.position = new Vector3(x, y, z);
            var collider = host.AddComponent<BoxCollider>();
            var target = host.AddComponent<HomingTarget>();
            Set(target, "m_collider", collider); // Reflection: serialized reference
            return target;
        }

        // The engine accepts the Gizmos calls at the end of OnDrawGizmosSelected only inside its own gizmo callback, so the call
        // may fault there; everything asserted is state built before the first gizmo call.
        private static void RunGizmos(DebugTargetingHomingAttack attack)
        {
            bool ignoring = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Call(attack, "OnDrawGizmosSelected");
            }
            catch (Exception)
            {
                // Raised by the first gizmo call, after the state below was built.
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoring;
            }
        }

        // OnDrawGizmosSelected 0x6BE260 rebuilds the cone from this transform and the serialized settings. The tangent of each
        // angle comes from tanf at 0x6be3c9/0x6be3e3 (the angle times 0.0174533), x from the first angle and y from the second;
        // the cone itself is passed by value, so its distance stays the authored one.
        [Test]
        public void TheGizmoRebuildsTheConeFromTheTransformAndTheSerializedSettings()
        {
            Owned.Run(owned =>
            {
                var attack = owned.Host("Lucid homing attack debug", false).AddComponent<DebugTargetingHomingAttack>();
                SetSerialized(attack, "m_projectDistance", property => property.floatValue = 7f);
                SetSerialized(attack, "m_projectStepDistance", property => property.floatValue = 2f);
                SetSerialized(attack, "m_projectConeAngle", property => property.vector2Value = new Vector2(45f, 60f));
                Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
                attack.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), rotation);

                RunGizmos(attack);

                var cone = Get<ConeVolume.ConeDescription>(attack, "m_cone");
                Assert.That(cone.Origin, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Quaternion.Angle(cone.Rotation, rotation), Is.LessThan(1e-3f));
                Assert.That(cone.Distance, Is.EqualTo(7f));
                Assert.That(cone.Step, Is.EqualTo(2f));
                Assert.That(cone.Tangents.x, Is.EqualTo(1f).Within(1e-5f), "tan 45 degrees");
                Assert.That(cone.Tangents.y, Is.EqualTo(1.7320508f).Within(1e-5f), "tan 60 degrees");
            });
        }

        // The projection grows the cone from the origin along the transform's forward by Step, keeping the first step that finds a
        // target (the near one at depth 3 is inside the cone of step 5; the wide one at depth 2 is 2.4 off the axis, outside its
        // radius of 2); both lists are cleared and refilled by every call.
        [Test]
        public void TheGizmoFindsTheNearestTargetInTheConeAndRefillsBothListsEachTime()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                object prior = TimeConfig.GetValue(null);
                try
                {
                    TimeConfig.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    HomingTarget wide = TargetAt(owned, 2.4f, 0f, 2f);
                    HomingTarget far = TargetAt(owned, 0f, 0f, 6f);
                    HomingTarget near = TargetAt(owned, 0.5f, 0f, 3f);
                    var attack = owned.Host("Lucid homing attack debug", false).AddComponent<DebugTargetingHomingAttack>();
                    SetSerialized(attack, "m_projectConeAngle", property => property.vector2Value = new Vector2(45f, 45f));
                    SetSerialized(attack, "m_projectDistance", property => property.floatValue = 10f);
                    SetSerialized(attack, "m_projectStepDistance", property => property.floatValue = 2.5f);

                    RunGizmos(attack);

                    Assert.That(Get<List<HomingTarget>>(attack, "m_targets"), Is.EquivalentTo(new[] { wide, far, near }));
                    Assert.That(Get<List<HomingTarget>>(attack, "m_validTargets"), Is.EqualTo(new[] { near }));

                    Object.DestroyImmediate(near.gameObject);
                    RunGizmos(attack);

                    Assert.That(Get<List<HomingTarget>>(attack, "m_targets"), Is.EquivalentTo(new[] { wide, far }));
                    Assert.That(Get<List<HomingTarget>>(attack, "m_validTargets"), Is.EqualTo(new[] { far }),
                        "the near target left; at step 7.5 the far one at depth 6 is the first inside");
                }
                finally
                {
                    TimeConfig.SetValue(null, prior);
                }
            });
        }

        // get_CustomProfileFilePath 0x55E210 combines Application.persistentDataPath with the folded "UserPerformanceProfile.json".
        [Test]
        public void TheCustomProfileFileLivesInThePersistentDataPath()
        {
            object path = typeof(DebugMenuPerformanceProfiles)
                .GetProperty("CustomProfileFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            Assert.That(path, Is.EqualTo(Path.Combine(Application.persistentDataPath, "UserPerformanceProfile.json")));
        }

        // Start 0x55E2A0 asks the registry row once and queues both callbacks, which run at once for a valid system: the first finds the
        // custom profile already in the list and adds nothing; the second reads each profile's name and changes nothing.
        [Test]
        public void StartLeavesAListThatAlreadyHoldsACustomProfileAsItIs()
        {
            Owned.Run(owned =>
            {
                ScalablePerformance performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
                var plain = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
                plain.name = "Lucid plain";
                var custom = owned.Track(ScriptableObject.CreateInstance<PerformanceProfileCustom>());
                custom.name = "Lucid custom";
                Set(performance, "m_performanceProfiles", new List<PerformanceProfile> { plain, custom }); // Reflection: serialized list
                var menu = owned.Host("Lucid performance profile menu", false).AddComponent<DebugMenuPerformanceProfiles>();

                Call(menu, "Start");

                // Profiles compare equal by GUID and a fresh asset's is empty, so the entries are compared by identity.
                Assert.That(performance.PerformanceProfiles.Count, Is.EqualTo(2));
                Assert.That(performance.PerformanceProfiles[0], Is.SameAs(plain));
                Assert.That(performance.PerformanceProfiles[1], Is.SameAs(custom));
            });
        }

        // AddDebugCustomPerformanceOptions 0x55E510 skips null entries and plain profiles while looking for a PerformanceProfileCustom,
        // and adds nothing once it has found one.
        [Test]
        public void ACustomProfileAfterOtherEntriesIsEnoughAndNothingIsAdded()
        {
            Owned.Run(owned =>
            {
                ScalablePerformance performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
                var plain = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
                var first = owned.Track(ScriptableObject.CreateInstance<PerformanceProfileCustom>());
                var second = owned.Track(ScriptableObject.CreateInstance<PerformanceProfileCustom>());
                var profiles = new List<PerformanceProfile> { null, plain, first, second };
                Set(performance, "m_performanceProfiles", profiles); // Reflection: serialized list
                var menu = owned.Host("Lucid performance profile menu", false).AddComponent<DebugMenuPerformanceProfiles>();

                menu.AddDebugCustomPerformanceOptions(performance);

                Assert.That(profiles.Count, Is.EqualTo(4), "nothing is added");
                Assert.That(profiles[0], Is.Null);
                Assert.That(profiles[1], Is.SameAs(plain));
                Assert.That(profiles[2], Is.SameAs(first));
                Assert.That(profiles[3], Is.SameAs(second));
            });
        }

        // Without a custom profile the method loads or creates "UserPerformanceProfile" and appends it, giving it the serialized features
        // when it has no attributes. It only reads the user's profile file; the profile it adds is destroyed here.
        [Test]
        public void WithoutACustomProfileTheUserProfileIsAppended()
        {
            Owned.Run(owned =>
            {
                ScalablePerformance performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
                var plain = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
                var profiles = new List<PerformanceProfile> { plain };
                Set(performance, "m_performanceProfiles", profiles); // Reflection: serialized list
                var menu = owned.Host("Lucid performance profile menu", false).AddComponent<DebugMenuPerformanceProfiles>();
                Set(menu, "m_scalableFeatures", new ScalableFeature[0]); // Reflection: serialized array

                try
                {
                    menu.AddDebugCustomPerformanceOptions(performance);

                    Assert.That(profiles.Count, Is.EqualTo(2));
                    Assert.That(profiles[0], Is.SameAs(plain));
                    Assert.That(profiles[1], Is.InstanceOf<PerformanceProfileCustom>());
                    Assert.That(profiles[1].name, Is.EqualTo("UserPerformanceProfile"));
                }
                finally
                {
                    foreach (PerformanceProfile profile in profiles.Skip(1)) Object.DestroyImmediate(profile);
                }
            });
        }
    }
}
