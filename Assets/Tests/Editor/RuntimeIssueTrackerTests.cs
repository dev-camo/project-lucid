using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // RuntimeIssueTracker (Game.Runtime TypeDef 0x020005ff, x86-64 0x5606D0..0x563210, its row 0x561A40 and lambda cache
    // 0x563300..0x5633C0) as a real component on an owned inactive host, over a real LevelManager, ScalablePerformance, SystemConfiguration
    // holding a CoreGameConfiguration with an authored RuntimeIssueProfile, and the component's own HLProfilerDataManager. The profiler's
    // frame statistics have private setters (their writer is its Update, which reads the engine's timers), so the cases give them distinct
    // values through those setters. Closing needs a CoroutineUtils host, which an inactive owned host refuses with the engine's error.
    // Not covered: OnGUI and the GUI* methods (the engine accepts IMGUI calls only inside OnGUI), UIScaledWidth and UIScaledHeight
    // (they read the screen's safe area), the m_raycastUIBlocker toggle of SetVisible and the AverageValue of a recorded stat in megabytes
    // (a recorder with no samples reads zero).
    public sealed class RuntimeIssueTrackerTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal sealed class Rig
        {
            internal World World;
            internal RuntimeIssueTracker Tracker;
            internal ScalablePerformance Performance;
            internal PerformanceProfile Profile;
            internal RuntimeIssueProfile IssueProfile;
            internal CoreGameConfiguration Config;
            internal HLProfilerDataManager Profiler;

            internal Owned Owned => World.Owned;
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The tracker's close and end inputs are leased, as Subscribe fills the shared tables. The tracker is built last, after the
        // registered services whose rows its field initialisers cache.
        internal static void WithTracker(Action<Rig> body) => ControlTableLease.Run(() => WithWorld(world =>
        {
            Owned owned = world.Owned;
            var rig = new Rig { World = world };
            SystemConfiguration configuration = owned.Register(new SystemConfiguration());
            rig.IssueProfile = owned.Track(ScriptableObject.CreateInstance<RuntimeIssueProfile>());
            rig.Config = owned.Track(ScriptableObject.CreateInstance<CoreGameConfiguration>());
            SetSerialized(rig.Config, "m_runtimeIssueProfile", property => property.objectReferenceValue = rig.IssueProfile);
            configuration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(CoreGameConfiguration)), (SystemConfigurationAsset)rig.Config);
            rig.Performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
            rig.Profile = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
            // Profiles compare equal by GUID, and a fresh asset's is empty, so each profile a case swaps in needs its own.
            Set(rig.Profile, "m_guid", "lucid-runtime-issue-profile-a"); // Reflection: authored GUID of a serialized asset
            rig.Performance.SetPerformanceProfile(rig.Profile);
            rig.Tracker = owned.Host("Lucid runtime issue tracker", false).AddComponent<RuntimeIssueTracker>();
            rig.Profiler = rig.Tracker.GetComponent<HLProfilerDataManager>();
            body(rig);
        }), GameInput.IssueTrackerClose, GameInput.IssueTrackerEnd);

        private static RuntimeIssueConfiguration Configuration(string path, float value, bool isMaxThreshold)
        {
            var configuration = new RuntimeIssueConfiguration();
            Set(configuration, "m_path", path); // Reflection: authored serialized data
            Set(configuration, "m_value", value);
            Set(configuration, "m_isMaxThreshold", isMaxThreshold);
            return configuration;
        }

        // A runtime issue attribute that the profile supports exactly when requiredLevel is Medium: a profile holding no attribute for the
        // feature supports exactly Medium (PerformanceProfile.IsExactlySupported 0x1b0ff60).
        private static RuntimeIssueAttribute Attribute(Owned owned, PerformanceProfile.QualityLevel qualityLevel,
            PerformanceProfile.QualityLevel requiredLevel, params RuntimeIssueConfiguration[] configurations)
        {
            var feature = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            feature.ID = "lucid-feature";
            var attribute = new RuntimeIssueAttribute();
            Set(attribute, "m_level", qualityLevel); // Reflection: authored serialized data
            Set(attribute, "m_performanceAttribute", new PerformanceAttribute { Feature = feature, Level = requiredLevel });
            Set(attribute, "m_configurations", new List<RuntimeIssueConfiguration>(configurations));
            return attribute;
        }

        private static readonly PerformanceProfile.QualityLevel Supported = PerformanceProfile.QualityLevel.Medium;
        private static readonly PerformanceProfile.QualityLevel Unsupported = PerformanceProfile.QualityLevel.High;

        private static void SetStatistic(HLProfilerDataManager profiler, string property, float value) =>
            typeof(HLProfilerDataManager).GetProperty(property, AnyMember).GetSetMethod(true).Invoke(profiler, new object[] { value });

        private static object Invoke(MethodInfo method, object target, object[] arguments)
        {
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }

        private static bool FrameData(RuntimeIssueTracker tracker, string path, out float value)
        {
            object[] arguments = { path, 0f };
            bool result = (bool)Invoke(typeof(RuntimeIssueTracker).GetMethod("GetFrameData", AnyMember), tracker, arguments);
            value = (float)arguments[1];
            return result;
        }

        private static IList Rows(RuntimeIssueTracker tracker) => Get<IList>(tracker, "m_dataList");

        private static object RowData(object row) => row.GetType().GetField("Data").GetValue(row);

        private static object RowConfiguration(object row) => row.GetType().GetField("Configuration").GetValue(row);

        internal static void WithRefusingCoroutineHost(Owned owned, Action body)
        {
            FieldInfo host = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = host.GetValue(null);
            GameObject inactive = owned.Host("Lucid refusing coroutine host", false);
            host.SetValue(null, inactive.AddComponent<CoroutineUtils>());
            try
            {
                body();
            }
            finally
            {
                host.SetValue(null, prior);
            }
        }

        // Constructor 0x5630D0 and the field initialisers: the frame group name "Frame" (0x563132), the 800 by 480 reference resolution
        // (0x563149, 0x43f0000044480000), a user scale of 1 (0x5631ef) and the typed references of the LevelManager and
        // ScalablePerformance rows. Nothing is visible, open or allowed to open.
        [Test]
        public void AFreshTrackerIsClosedWithAUnitUserScale()
        {
            WithTracker(rig =>
            {
                RuntimeIssueTracker tracker = rig.Tracker;
                Assert.That(tracker.IsVisible, Is.False);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.False);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.False);
                Assert.That(Get<float>(tracker, "m_userScaleUI"), Is.EqualTo(1f));
                Assert.That(Get<float>(tracker, "m_currentScaleUI"), Is.EqualTo(0f));
                Assert.That(Get<string>(tracker, "ProfilerFrameGroupName"), Is.EqualTo("Frame"));
                Assert.That(Get<Vector2>(tracker, "m_referenceResolution"), Is.EqualTo(new Vector2(800f, 480f)));
                Assert.That(Rows(tracker), Is.Not.Null.And.Empty);
                Assert.That(Get<object>(tracker, "m_configurations"), Is.Null);
                Assert.That(Get<SystemRef<LevelManager>>(tracker, "m_levelManagerRef").GetSafe(), Is.SameAs(rig.World.Levels));
                Assert.That(Get<SystemRef<ScalablePerformance>>(tracker, "m_scalablePerformanceRef").GetSafe(), Is.SameAs(rig.Performance));
            });
        }

        // OpenTracker 0x560450 returns while the tracker may not open unless forced; otherwise it enables the component, allows opening and
        // shows itself (SetVisible 0x560730), which listens to the close and end inputs on the down trigger.
        [Test]
        public void OpeningNeedsPermissionUnlessForced()
        {
            WithTracker(rig =>
            {
                RuntimeIssueTracker tracker = rig.Tracker;
                tracker.enabled = false;

                tracker.OpenTracker(false);

                Assert.That(tracker.IsVisible, Is.False);
                Assert.That(tracker.enabled, Is.False);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.False);
                Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerClose, tracker), Is.Empty);

                tracker.OpenTracker(true);

                Assert.That(tracker.IsVisible, Is.True);
                Assert.That(tracker.enabled, Is.True);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.True);
                Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerClose, tracker),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "RuntimeIssueTracker.GameInputCloseTrackerDown")));
                Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerEnd, tracker),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "RuntimeIssueTracker.GameInputEndTrackerDown")));
            });
        }

        [Test]
        public void ATrackerThatMayOpenOpensWithoutBeingForced()
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);
                rig.Tracker.enabled = false;

                rig.Tracker.OpenTracker(false);

                Assert.That(rig.Tracker.IsVisible, Is.True);
                Assert.That(rig.Tracker.enabled, Is.True);
            });
        }

        // Opening twice listens once: SetVisible returns while the visibility already matches (0x5607a8..0x5607ae).
        [Test]
        public void OpeningAnOpenTrackerListensOnlyOnce()
        {
            WithTracker(rig =>
            {
                rig.Tracker.OpenTracker(true);
                rig.Tracker.OpenTracker(true);

                Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerClose, rig.Tracker)["s_onButtonsDown"],
                    Is.EqualTo(new[] { "RuntimeIssueTracker.GameInputCloseTrackerDown@-1" }));
            });
        }

        // CloseTracker 0x560420 hides the tracker, stores canReopen as m_canOpen and always clears m_canOpenWhenInvalid. The hide releases the
        // two inputs on the next frame (OnNextFrame 0x5608de with the lambda <SetVisible>b__26_0), so they stay subscribed until it runs.
        [TestCase(true, TestName = "ClosingAnOpenTrackerKeepsItsInputsUntilTheNextFrameAndMayReopen")]
        [TestCase(false, TestName = "ClosingAnOpenTrackerForGoodKeepsItsInputsUntilTheNextFrame")]
        public void ClosingAnOpenTrackerReleasesItsInputsOnTheNextFrame(bool canReopen)
        {
            WithTracker(rig =>
            {
                WithRefusingCoroutineHost(rig.Owned, () =>
                {
                    RuntimeIssueTracker tracker = rig.Tracker;
                    Call(tracker, "OnLevelActivated", (LevelManagerLevel)null);
                    tracker.OpenTracker(false);
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);

                    tracker.CloseTracker(canReopen);

                    Assert.That(tracker.IsVisible, Is.False);
                    Assert.That(Get<bool>(tracker, "m_canOpen"), Is.EqualTo(canReopen));
                    Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.False);
                    Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerClose, tracker), Is.Not.Empty, "still listening this frame");
                    Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerEnd, tracker), Is.Not.Empty);

                    Call(tracker, "<SetVisible>b__26_0");

                    Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerClose, tracker), Is.Empty);
                    Assert.That(ControlTables.Subscriptions(GameInput.IssueTrackerEnd, tracker), Is.Empty);
                });
            });
        }

        // Hiding a tracker that is not visible returns before the next-frame request (no coroutine host is needed), but the gates are
        // still stored.
        [TestCase(true, TestName = "ClosingAHiddenTrackerOnlyAllowsItToOpenAgain")]
        [TestCase(false, TestName = "ClosingAHiddenTrackerForGoodOnlyForbidsOpening")]
        public void ClosingAHiddenTrackerOnlyStoresTheGates(bool canReopen)
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);

                rig.Tracker.CloseTracker(canReopen);

                Assert.That(Get<bool>(rig.Tracker, "m_canOpen"), Is.EqualTo(canReopen));
                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.False);
            });
        }

        // GameInputCloseTrackerDown 0x5608F0 closes with canReopen and GameInputEndTrackerDown 0x560910 without; each clears the
        // open-when-invalid gate (word stores at +0x70).
        [Test]
        public void TheCloseAndEndInputsCloseTheTracker()
        {
            WithTracker(rig =>
            {
                RuntimeIssueTracker tracker = rig.Tracker;
                Call(tracker, "OnLevelActivated", (LevelManagerLevel)null);

                Call(tracker, "GameInputEndTrackerDown", 1f);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.False);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.False);

                Call(tracker, "OnLevelActivated", (LevelManagerLevel)null);
                Call(tracker, "GameInputCloseTrackerDown", 1f);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.True);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.False);
            });
        }

        // OnLevelActivated 0x560F50 stores both gates (word 0x101); OnLevelExit 0x560F60 clears only m_canOpen.
        [Test]
        public void ALevelOpensBothGatesAndLeavingClosesOnlyTheFirst()
        {
            WithTracker(rig =>
            {
                RuntimeIssueTracker tracker = rig.Tracker;

                Call(tracker, "OnLevelActivated", (LevelManagerLevel)null);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.True);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.True);

                Call(tracker, "OnLevelExit");
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.False);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.True);
            });
        }

        // Start 0x560930 takes the HLProfilerDataManager of its own object, applies the active performance profile and subscribes
        // to profile updates; the LevelManager is valid, so OnLevelManagerValid 0x560C40 subscribes to level activation and exit at
        // once. OnDestroy 0x560D60 removes all three from the systems that are still alive.
        [Test]
        public void StartSubscribesToTheSystemsAndOnDestroyRemovesTheSubscriptions()
        {
            WithTracker(rig =>
            {
                RuntimeIssueTracker tracker = rig.Tracker;
                LevelManager levels = rig.World.Levels;

                Call(tracker, "Start");

                Assert.That(Get<HLProfilerDataManager>(tracker, "m_dataHandler"), Is.SameAs(rig.Profiler));
                Assert.That(Targets(Get<Delegate>(rig.Performance, "OnProfileUpdated")), Is.EqualTo(new[] { "RuntimeIssueTracker.OnPerformanceProfileUpdated" }));
                Assert.That(Targets(Get<Delegate>(levels, "m_actionOnLevelActivated")), Is.EqualTo(new[] { "RuntimeIssueTracker.OnLevelActivated" }));
                Assert.That(Targets(levels.OnLevelExit), Is.EqualTo(new[] { "RuntimeIssueTracker.OnLevelExit" }));

                Call(tracker, "OnDestroy");

                Assert.That(Targets(Get<Delegate>(rig.Performance, "OnProfileUpdated")), Is.Empty);
                Assert.That(Targets(Get<Delegate>(levels, "m_actionOnLevelActivated")), Is.Empty);
                Assert.That(Targets(levels.OnLevelExit), Is.Empty);
            });
        }

        // The invocation list of a delegate as "DeclaringType.Method" for those aimed at a RuntimeIssueTracker.
        private static string[] Targets(Delegate handlers) => handlers == null
            ? new string[0]
            : handlers.GetInvocationList().Where(handler => handler.Target is RuntimeIssueTracker)
                .Select(handler => handler.Method.DeclaringType.Name + "." + handler.Method.Name).ToArray();

        // OnDestroy reads each system with GetSafe and tests it with Unity null equality, so a destroyed or absent system is skipped.
        [Test]
        public void OnDestroyToleratesDestroyedSystems()
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "Start");
                Object.DestroyImmediate(rig.Performance.gameObject);
                Object.DestroyImmediate(rig.World.Levels.gameObject);

                Assert.DoesNotThrow(() => Call(rig.Tracker, "OnDestroy"));
            });
        }

        // OnPerformanceProfileUpdated 0x560A70 takes the configurations and quality level of the first attribute that the profile
        // supports exactly (0x560b6d..0x560b8b, then the loop's exit), enables the component when it has configurations and refreshes
        // its trackers. A later profile change re-selects through the subscription.
        [Test]
        public void TheFirstSupportedAttributeChoosesTheConfigurationsAndFollowsProfileChanges()
        {
            WithTracker(rig =>
            {
                RuntimeIssueConfiguration low = Configuration("Frame/AverageFPS", 30f, false);
                RuntimeIssueConfiguration high = Configuration("Frame/CPUFrequency", 1f, true);
                RuntimeIssueConfiguration other = Configuration("Frame/GPUFrequency", 2f, true);
                RuntimeIssueAttribute skipped = Attribute(rig.Owned, PerformanceProfile.QualityLevel.VeryLow, Unsupported, other);
                RuntimeIssueAttribute first = Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Supported, low);
                RuntimeIssueAttribute second = Attribute(rig.Owned, PerformanceProfile.QualityLevel.High, Supported, high);
                rig.IssueProfile.m_attributes.AddRange(new[] { skipped, first, second });
                rig.Tracker.enabled = false;

                Call(rig.Tracker, "Start");

                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.SameAs(first.Configurations), "the first supported attribute, not a later one");
                Assert.That(Get<PerformanceProfile.QualityLevel>(rig.Tracker, "m_qualityLevel"), Is.EqualTo(PerformanceProfile.QualityLevel.Low));
                Assert.That(rig.Tracker.enabled, Is.True);
                Assert.That(Rows(rig.Tracker).Count, Is.EqualTo(1));
                Assert.That(RowConfiguration(Rows(rig.Tracker)[0]), Is.SameAs(low));

                // A profile holding the feature at High supports the attributes that require High, so the update picks the first of those.
                var feature = rig.IssueProfile.m_attributes[0].PerformanceAttribute.Feature;
                var highProfile = rig.Owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
                Set(highProfile, "m_guid", "lucid-runtime-issue-profile-b"); // Reflection: authored GUID of a serialized asset
                Set(highProfile, "m_attributes", new List<PerformanceAttribute> { new PerformanceAttribute { Feature = feature, Level = Unsupported } });
                rig.Performance.SetPerformanceProfile(highProfile);

                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.SameAs(skipped.Configurations));
                Assert.That(Get<PerformanceProfile.QualityLevel>(rig.Tracker, "m_qualityLevel"), Is.EqualTo(PerformanceProfile.QualityLevel.VeryLow));
                Assert.That(RowConfiguration(Rows(rig.Tracker)[0]), Is.SameAs(other));
            });
        }

        // Without a RuntimeIssueProfile the update disables the component and returns (0x560b01..0x560b1a), before any attribute is read.
        [Test]
        public void WithoutARuntimeIssueProfileTheTrackerIsDisabledAndKeepsItsConfigurations()
        {
            WithTracker(rig =>
            {
                RuntimeIssueConfiguration configuration = Configuration("Frame/AverageFPS", 30f, false);
                rig.IssueProfile.m_attributes.Add(Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Supported, configuration));
                Call(rig.Tracker, "Start");
                Assert.That(rig.Tracker.enabled, Is.True);
                object configurations = Get<object>(rig.Tracker, "m_configurations");
                Assert.That(configurations, Is.Not.Null);

                SetSerialized(rig.Config, "m_runtimeIssueProfile", property => property.objectReferenceValue = null);
                Call(rig.Tracker, "OnPerformanceProfileUpdated", rig.Profile);

                Assert.That(rig.Tracker.enabled, Is.False);
                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.SameAs(configurations));
            });
        }

        // With no supported attribute the previous configurations stay and keep the component enabled; with none at all it is disabled.
        [Test]
        public void WithoutASupportedAttributeThePreviousConfigurationsRemain()
        {
            WithTracker(rig =>
            {
                rig.IssueProfile.m_attributes.Add(Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Unsupported));
                Call(rig.Tracker, "Start");
                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.Null);
                Assert.That(rig.Tracker.enabled, Is.False, "nothing was ever chosen");

                RuntimeIssueAttribute supported = Attribute(rig.Owned, PerformanceProfile.QualityLevel.High, Supported, Configuration("Frame/AverageFPS", 1f, false));
                rig.IssueProfile.m_attributes.Add(supported);
                Call(rig.Tracker, "OnPerformanceProfileUpdated", rig.Profile);
                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.SameAs(supported.Configurations));
                Assert.That(rig.Tracker.enabled, Is.True);

                rig.IssueProfile.m_attributes.Remove(supported);
                Call(rig.Tracker, "OnPerformanceProfileUpdated", rig.Profile);
                Assert.That(Get<object>(rig.Tracker, "m_configurations"), Is.SameAs(supported.Configurations), "no later attribute matches, so the choice stays");
                Assert.That(Get<PerformanceProfile.QualityLevel>(rig.Tracker, "m_qualityLevel"), Is.EqualTo(PerformanceProfile.QualityLevel.High));
                Assert.That(rig.Tracker.enabled, Is.True);
            });
        }

        // UpdateTrackers 0x560F70 clears the rows and adds one per configuration whose path is a frame statistic (no data reference) or
        // resolves through group, layout and stat names (the last name's reference); a path that resolves neither adds nothing. A name
        // that is the whole rest of the path is not consumed (GetTrackerList drops it only when more text follows), so a layout name that
        // ends the path is looked up again as the stat name.
        [Test]
        public void EachResolvablePathBecomesOneRowInConfigurationOrder()
        {
            WithTracker(rig =>
            {
                var marker = new ProfilerMarker("lucid runtime issue tracker");
                rig.Profiler.StartTrackingProfilerMarker(marker, HLProfilerCategory.Memory, "Lucid group", "stat", 0, false);
                try
                {
                    HLProfilerDataReference reference = rig.Profiler.GroupList.Single().LayoutList.Single().DataList.Single();
                    RuntimeIssueConfiguration frame = Configuration("Frame/MinFrameDeltaTime", 1f, true);
                    RuntimeIssueConfiguration stat = Configuration("Lucid group/stat/stat", 1f, true);
                    RuntimeIssueConfiguration unknownGroup = Configuration("Nope/stat/stat", 1f, true);
                    RuntimeIssueConfiguration unknownLayout = Configuration("Lucid group/nope/stat", 1f, true);
                    RuntimeIssueConfiguration unknownStat = Configuration("Lucid group/stat/nope", 1f, true);
                    RuntimeIssueConfiguration layoutOnly = Configuration("Lucid group/stat", 1f, true);
                    RuntimeIssueConfiguration trailing = Configuration("Lucid group/stat/stat/extra", 1f, true);
                    rig.IssueProfile.m_attributes.Add(Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Supported,
                        unknownGroup, frame, unknownLayout, stat, unknownStat, layoutOnly, trailing));

                    Call(rig.Tracker, "Start");

                    IList rows = Rows(rig.Tracker);
                    Assert.That(rows.Cast<object>().Select(RowConfiguration).ToArray(), Is.EqualTo(new object[] { frame, stat, layoutOnly, trailing }));
                    Assert.That(RowData(rows[0]), Is.Null, "a frame statistic has no data reference");
                    Assert.That(RowData(rows[1]), Is.SameAs(reference));
                    Assert.That(RowData(rows[2]), Is.SameAs(reference), "the layout name is looked up again as the stat name");
                    Assert.That(RowData(rows[3]), Is.SameAs(reference), "text after the last name is ignored");

                    Call(rig.Tracker, "UpdateTrackers");
                    Assert.That(rows.Count, Is.EqualTo(4), "the rows are rebuilt, not appended to");
                }
                finally
                {
                    foreach (HLProfilerData data in Get<Dictionary<string, HLProfilerData>>(rig.Profiler, "m_customDataList").Values) data.Shutdown();
                }
            });
        }

        // GetFrameData 0x5617D0: the path must begin with the five characters of "Frame" (a culture-sensitive, case-sensitive compare of
        // that many characters), then one skipped separator, then name a statistic; the statistics are the data manager's AverageFPS
        // (+0x28) to GPUFrequency (+0x40), in field order. The out value is zero when nothing matches.
        [Test]
        public void EachFrameStatisticNameReadsItsOwnStatistic()
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "Start");
                string[] names = { "AverageFPS", "AverageFrameDeltaTime", "MinFrameDeltaTime", "MaxFrameDeltaTime", "CurrentFrameDeltaTime", "CPUFrequency", "GPUFrequency" };
                for (int i = 0; i < names.Length; i++) SetStatistic(rig.Profiler, names[i], i + 1);

                for (int i = 0; i < names.Length; i++)
                {
                    Assert.That(FrameData(rig.Tracker, "Frame/" + names[i], out float read), Is.True, names[i]);
                    Assert.That(read, Is.EqualTo(i + 1f), names[i]);
                }
            });
        }

        [Test]
        public void AFramePathNeedsTheFramePrefixAndAKnownStatisticName()
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "Start");
                SetStatistic(rig.Profiler, "AverageFPS", 60f);

                Assert.That(FrameData(rig.Tracker, "Frame#AverageFPS", out float value), Is.True, "the separator is skipped without being checked");
                Assert.That(value, Is.EqualTo(60f));
                foreach (string path in new[] { "Frame/Unknown", "Frame/averagefps", "Frame/", "Fram/AverageFPS", "frame/AverageFPS", "Fra", "", "Other/AverageFPS" })
                {
                    value = -1f;
                    Assert.That(FrameData(rig.Tracker, path, out value), Is.False, path);
                    Assert.That(value, Is.EqualTo(0f), path + " leaves the out value at zero");
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => FrameData(rig.Tracker, "Frame", out _), "the separator is read past the end of the path");
            });
        }

        // GetTrackerList<T> (shared 0x9B5C50): the first entry whose name is a prefix of the path wins; the path then drops that
        // name and one separator when anything follows the name; without a match the entry is the default and the path is unchanged.
        private static object[] TrackerList(string[] names, string path)
        {
            MethodInfo method = typeof(RuntimeIssueTracker).GetMethod("GetTrackerList", AnyMember).MakeGenericMethod(typeof(string));
            object[] arguments = { names, path, null, new Func<string, string>(name => name) };
            bool found = (bool)Invoke(method, null, arguments);
            return new[] { found, arguments[1], arguments[2] };
        }

        [Test]
        public void TheFirstPrefixMatchIsTakenAndItsNameAndSeparatorAreConsumed()
        {
            object[] result = TrackerList(new[] { "A", "AB", "ABC" }, "AB/x");
            Assert.That(result, Is.EqualTo(new object[] { true, "/x", "A" }), "the first entry matches; a later, longer one is not looked at");

            result = TrackerList(new[] { "X", "AB", "A" }, "AB/x");
            Assert.That(result, Is.EqualTo(new object[] { true, "x", "AB" }), "a non-matching entry is skipped");
        }

        [Test]
        public void ANameThatIsTheWholePathLeavesThePathAndAnUnmatchedListLeavesTheEntryEmpty()
        {
            Assert.That(TrackerList(new[] { "Frame" }, "Frame"), Is.EqualTo(new object[] { true, "Frame", "Frame" }), "nothing follows, so nothing is dropped");
            Assert.That(TrackerList(new[] { "Frame" }, "Frame/"), Is.EqualTo(new object[] { true, "", "Frame" }), "the separator alone is dropped");
            Assert.That(TrackerList(new[] { "A", "B" }, "C/x"), Is.EqualTo(new object[] { false, "C/x", null }));
            Assert.That(TrackerList(new string[0], "C/x"), Is.EqualTo(new object[] { false, "C/x", null }));
        }

        // TryGetStat 0x562E40: valueStr starts as the whole text; the first space starts the units; a number before it is reformatted
        // to two decimals and the call succeeds. Without a space, or with text that is not a number, it fails (leaving the units as the
        // text from the space, or empty without a space).
        [TestCase("3.14159 ms", true, "3.14", " ms", TestName = "AFormattedNumberIsRoundedToTwoDecimalsAndKeepsItsUnits")]
        [TestCase("12 MB", true, "12.00", " MB", TestName = "AWholeNumberGainsTwoDecimals")]
        [TestCase("42", false, "42", "", TestName = "WithoutASpaceTheStatFailsWithEmptyUnits")]
        [TestCase("abc MB", false, "abc MB", " MB", TestName = "ATextBeforeTheSpaceFailsButKeepsTheUnits")]
        [TestCase(" MB", false, " MB", " MB", TestName = "ALeadingSpaceHasNoNumber")]
        public void TheStatTextIsSplitIntoANumberAndItsUnits(string text, bool expectedResult, string expectedValue, string expectedUnits)
        {
            CultureInfo prior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                object[] arguments = { text, null, null };
                bool result = (bool)Invoke(typeof(RuntimeIssueTracker).GetMethod("TryGetStat", AnyMember), null, arguments);

                Assert.That(result, Is.EqualTo(expectedResult));
                Assert.That(arguments[1], Is.EqualTo(expectedValue));
                Assert.That(arguments[2], Is.EqualTo(expectedUnits));
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
            }
        }

        // FixedUpdate 0x561A50 checks each row in order: a data reference is valid when its average value, in megabytes, is in range;
        // otherwise a frame statistic by the configuration's path. The first invalid row opens the tracker when both gates allow it
        // and ends the scan without re-arming the open-when-invalid gate; a scan that finds every row valid sets the gate.
        // Rows for AverageFPS (valid at 30 or more) and CurrentFrameDeltaTime (valid at 0.05 or less), in that order.
        private static void WithTwoFrameRows(float fps, float delta, Action<Rig> body) => WithTracker(rig =>
        {
            rig.IssueProfile.m_attributes.Add(Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Supported,
                Configuration("Frame/AverageFPS", 30f, false), Configuration("Frame/CurrentFrameDeltaTime", 0.05f, true)));
            Call(rig.Tracker, "Start");
            Assert.That(Rows(rig.Tracker).Count, Is.EqualTo(2));
            SetStatistic(rig.Profiler, "AverageFPS", fps);
            SetStatistic(rig.Profiler, "CurrentFrameDeltaTime", delta);
            body(rig);
        });

        [Test]
        public void AScanWithEveryRowValidArmsTheOpenWhenInvalidGate()
        {
            WithTwoFrameRows(60f, 0.01f, rig =>
            {
                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.False);

                Call(rig.Tracker, "FixedUpdate");

                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.True);
                Assert.That(rig.Tracker.IsVisible, Is.False);
            });
        }

        [Test]
        public void AValueOnTheLimitIsValid()
        {
            WithTwoFrameRows(30f, 0.05f, rig =>
            {
                Call(rig.Tracker, "FixedUpdate");

                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.True, "30 meets the minimum and 0.05 meets the maximum");
            });
        }

        [Test]
        public void AnInvalidRowOpensAnAllowedTracker()
        {
            WithTwoFrameRows(10f, 0.01f, rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);
                rig.Tracker.enabled = false;

                Call(rig.Tracker, "FixedUpdate");

                Assert.That(rig.Tracker.IsVisible, Is.True);
                Assert.That(rig.Tracker.enabled, Is.True);
            });
        }

        // The second row is the invalid one here, after a valid first row.
        [Test]
        public void ALaterInvalidRowOpensTheTrackerToo()
        {
            WithTwoFrameRows(60f, 0.5f, rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);

                Call(rig.Tracker, "FixedUpdate");

                Assert.That(rig.Tracker.IsVisible, Is.True);
            });
        }

        [TestCase(true, false, TestName = "AnInvalidRowDoesNotOpenATrackerThatMayNotOpen")]
        [TestCase(false, true, TestName = "AnInvalidRowDoesNotOpenATrackerThatMayNotOpenWhenInvalid")]
        public void AnInvalidRowRespectsBothGates(bool canOpenWhenInvalid, bool canOpen)
        {
            WithTwoFrameRows(10f, 0.01f, rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);
                if (!canOpen) Call(rig.Tracker, "OnLevelExit");
                if (!canOpenWhenInvalid) Call(rig.Tracker, "GameInputCloseTrackerDown", 1f);

                Call(rig.Tracker, "FixedUpdate");

                Assert.That(rig.Tracker.IsVisible, Is.False);
            });
        }

        // The scan returns at the first invalid row, so a valid row after it does not arm the gate, and a valid row before it does not
        // either (the arming store is after the loop).
        [TestCase(10f, 0.01f, TestName = "AnInvalidFirstRowEndsTheScanBeforeTheGateIsArmed")]
        [TestCase(60f, 0.5f, TestName = "AnInvalidLastRowAlsoLeavesTheGateDisarmed")]
        public void AnInvalidRowEndsTheScanWithoutArmingTheGate(float fps, float delta)
        {
            WithTwoFrameRows(fps, delta, rig =>
            {
                Call(rig.Tracker, "OnLevelActivated", (LevelManagerLevel)null);
                Call(rig.Tracker, "GameInputCloseTrackerDown", 1f); // opens nothing; clears the open-when-invalid gate

                Call(rig.Tracker, "FixedUpdate");

                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.False);
                Assert.That(rig.Tracker.IsVisible, Is.False);
            });
        }

        // A data row reads its reference's average value (zero with no samples) in megabytes and applies the row's own thresholds: a
        // maximum of 5 holds, a minimum of 1 does not. A failed data row falls through to the frame lookup, which this path misses.
        [TestCase(true, 5f, true, TestName = "ADataRowBelowItsMaximumIsValid")]
        [TestCase(false, 1f, false, TestName = "ADataRowBelowItsMinimumIsInvalid")]
        public void ADataRowIsCheckedAgainstItsThreshold(bool isMaxThreshold, float limit, bool valid)
        {
            WithTracker(rig =>
            {
                rig.Profiler.StartTrackingProfilerMarker(new ProfilerMarker("lucid runtime issue tracker"), HLProfilerCategory.Memory, "Lucid group", "stat", 0, false);
                try
                {
                    rig.IssueProfile.m_attributes.Add(Attribute(rig.Owned, PerformanceProfile.QualityLevel.Low, Supported,
                        Configuration("Lucid group/stat/stat", limit, isMaxThreshold)));
                    Call(rig.Tracker, "Start");
                    Assert.That(Rows(rig.Tracker).Count, Is.EqualTo(1));

                    Call(rig.Tracker, "FixedUpdate");

                    Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.EqualTo(valid));
                }
                finally
                {
                    foreach (HLProfilerData data in Get<Dictionary<string, HLProfilerData>>(rig.Profiler, "m_customDataList").Values) data.Shutdown();
                }
            });
        }

        [Test]
        public void ATrackerWithNoRowsIsAlwaysValid()
        {
            WithTracker(rig =>
            {
                Call(rig.Tracker, "FixedUpdate");

                Assert.That(Get<bool>(rig.Tracker, "m_canOpenWhenInvalid"), Is.True);
            });
        }
    }
}
