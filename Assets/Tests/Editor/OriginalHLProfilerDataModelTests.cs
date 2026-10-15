using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Declarations and bodies recovered from the original 1.10.1 HLProfiler types. Edit mode
    // never runs Awake, so these cases exercise the data model without registering systems.
    public sealed class OriginalHLProfilerDataModelTests
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<GameObject> m_objects = new List<GameObject>();
        private readonly List<HLProfilerData> m_data = new List<HLProfilerData>();

        [TearDown]
        public void TearDown()
        {
            // Marker data is never shut down by the original manager; release the recorders here.
            foreach (GameObject gameObject in m_objects)
            {
                var manager = gameObject.GetComponent<HLProfilerDataManager>();
                var customData = (IDictionary)typeof(HLProfilerDataManager).GetField("m_customDataList", Instance).GetValue(manager);
                foreach (HLProfilerData data in customData.Values)
                    data.Shutdown();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
            m_objects.Clear();
            foreach (HLProfilerData data in m_data)
                data.Shutdown();
            m_data.Clear();
        }

        private static Option[] Options(Type type) =>
            type.GetCustomAttributesData().Where(data => data.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(data => (Option)data.ConstructorArguments[0].Value).ToArray();

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);

        private static string Snapshot(HLProfilerDataManager manager, bool logOnlyActiveData) =>
            manager.GetProfilerSnapshot(logOnlyActiveData).ToString().Replace(Environment.NewLine, "\n");

        private HLProfilerConsole CreateConsole()
        {
            var gameObject = new GameObject("Lucid HLProfiler test");
            m_objects.Add(gameObject);
            return gameObject.AddComponent<HLProfilerConsole>();
        }

        [Test]
        public void ConsoleAndManagerRetainOriginalDeclarations()
        {
            Assert.That((int)typeof(HLProfilerConsole).Attributes, Is.EqualTo(0x00100001));
            Assert.That(Options(typeof(HLProfilerConsole)), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
            var required = typeof(HLProfilerConsole).GetCustomAttribute<RequireComponent>();
            Assert.That(required.m_Type0, Is.EqualTo(typeof(HLProfilerDataManager)));
            Assert.That((int)typeof(HLProfilerDataManager).Attributes, Is.EqualTo(0x00100001));
            Assert.That(Options(typeof(HLProfilerDataManager)), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
            Assert.That(typeof(HLProfilerDataManager).GetInterfaces(), Is.EqualTo(new[] { typeof(ISystem) }));
            Type frameTimeInfo = typeof(HLProfilerDataManager).GetNestedType("FrameTimeInfo", BindingFlags.NonPublic);
            Assert.That((int)frameTimeInfo.Attributes, Is.EqualTo(0x0010010b));
            Assert.That((int)typeof(HLProfilerConsole).GetNestedType("GUISkinModifierTarget", BindingFlags.NonPublic).Attributes, Is.EqualTo(0x105));
            Type generalStats = typeof(HLProfilerConsole).GetNestedType("GUIGeneralStats", BindingFlags.NonPublic);
            Assert.That(generalStats.IsDefined(typeof(FlagsAttribute)), Is.True);
            Assert.That(Enum.GetValues(generalStats).Cast<object>().Select(value => Convert.ToInt32(value)), Is.EqualTo(new[] { 0, 2, 4, 8 }));
            Type utils = typeof(HLProfilerConsole).Assembly.GetType("Hardlight.HLProfilerUtils");
            Assert.That((int)utils.GetMethod("FormatCount", BindingFlags.NonPublic | BindingFlags.Static).Attributes, Is.EqualTo(0x93));
            Assert.That((int)utils.GetMethod("GUIRightAlignedLabel", BindingFlags.NonPublic | BindingFlags.Static).Attributes, Is.EqualTo(0x91));
        }

        [Test]
        public void ConsoleFieldInitializersMatchTheOriginalConstructor()
        {
            HLProfilerConsole console = CreateConsole();
            Assert.That(console.GetComponent<HLProfilerDataManager>(), Is.Not.Null, "RequireComponent adds the manager");
            Assert.That(Field<Vector2>(console, "m_referenceResolution"), Is.EqualTo(new Vector2(480f, 800f)));
            Assert.That(Field<List<Vector4>>(console, "m_statsWindowScalePresets"),
                Is.EqualTo(new[] { Vector4.zero, new Vector4(0.5f, 0.3f, 1f, 1f), new Vector4(1f, 0.3f, 1f, 1f) }));
            Assert.That(Field<int>(console, "m_minFontSize"), Is.EqualTo(12));
            Assert.That(Field<int>(console, "m_maxFontSize"), Is.EqualTo(30));
            Assert.That(Convert.ToInt32(Field<object>(console, "m_valuesColor")), Is.EqualTo(4));
            Assert.That(Convert.ToInt32(Field<object>(console, "m_generalStatsFlags")), Is.EqualTo(14));
            Assert.That(Field<StringBuilder>(console, "m_workingStringBuilder").Capacity, Is.EqualTo(500));
            Assert.That(Field<string[]>(console, "m_statsText").Length, Is.EqualTo(2));
            Assert.That(Field<bool>(console, "m_displayStatsWindow") && Field<bool>(console, "m_displayHeader"), Is.True);
            Assert.That(Field<float>(console, "m_windowPositionX"), Is.EqualTo(float.MaxValue));
            Assert.That(Field<float>(console, "m_windowPositionY"), Is.EqualTo(float.MaxValue));
            Assert.That(Field<float>(console, "m_userScaleUI"), Is.EqualTo(1f));
            Assert.That(Field<int>(console, "m_currentLabelFontSize"), Is.EqualTo(13));
            Assert.That(Field<int>(console, "m_currentScalePresetIndex"), Is.EqualTo(1));
        }

        [Test]
        public void SampleBufferAlwaysHoldsTheMaximumBecauseTheClampIsReversed()
        {
            foreach (uint requested in new uint[] { 0, 1, 5, 60, 600 })
            {
                var data = new HLProfilerData(new ProfilerMarker("Lucid.HLProfilerTest"), "Lucid", requested, HLProfilerData.SampleType.Average, true);
                m_data.Add(data);
                Assert.That(Field<int>(data, "m_sampleCount"), Is.EqualTo(60), requested.ToString());
                Assert.That(data.StatName, Is.EqualTo("Lucid"));
            }
        }

        [Test]
        public void TrackedMarkersShareAGroupAndAppearInSnapshots()
        {
            HLProfilerDataManager manager = CreateConsole().GetComponent<HLProfilerDataManager>();
            manager.StartTrackingProfilerMarker(new ProfilerMarker("Lucid.HLProfilerTest.A"), HLProfilerCategory.Scripts, "Lucid", "A", 60);
            manager.StartTrackingProfilerMarker(new ProfilerMarker("Lucid.HLProfilerTest.B"), HLProfilerCategory.Scripts, "Lucid", "B", 60);
            manager.StartTrackingProfilerMarker(new ProfilerMarker("Lucid.HLProfilerTest.C"), HLProfilerCategory.Render, "Lucid", "C", 60, false);
            Assert.That(manager.GroupList.Select(group => group.Name + ":" + group.Category + ":" + group.IsVisible),
                Is.EqualTo(new[] { "Lucid:Scripts:True", "Lucid:Render:False" }));
            Assert.That(manager.GroupList[0].LayoutList.Select(layout => layout.Name), Is.EqualTo(new[] { "A", "B" }));

            string snapshot = Snapshot(manager, false);
            StringAssert.Contains("\nLucid\n(Not active) - A: NA\n(Not active) - B: NA\n---\n", snapshot);
            StringAssert.Contains("\n(Not active) - Lucid\n(Not active) - C: NA\n---\n", snapshot);
            // Active-only snapshots keep the visible group's title but none of its inactive values.
            snapshot = Snapshot(manager, true);
            StringAssert.Contains("\nLucid\n---\n", snapshot);
            StringAssert.DoesNotContain("Not active", snapshot);

            manager.StartGroupProfiling();
            Assert.That(manager.GroupList[0].LayoutList.All(layout => layout.DataList.All(data => data.IsActive)), Is.True);
            StringAssert.Contains("\nLucid\nA: NA\nB: NA\n---\n", Snapshot(manager, true));
            manager.StopGroupProfiling(true);
            Assert.That(manager.GroupList.Any(group => group.IsVisible), Is.False, "forced invisible");

            // Stopping drops A from the manager without shutting it down; the test still owns its recorder.
            var customData = (IDictionary)typeof(HLProfilerDataManager).GetField("m_customDataList", Instance).GetValue(manager);
            m_data.Add((HLProfilerData)customData["Lucid" + HLProfilerCategory.Scripts + "A"]);
            manager.StopTrackingProfilerMarker(HLProfilerCategory.Scripts, "Lucid", "A");
            Assert.That(customData.Contains("Lucid" + HLProfilerCategory.Scripts + "A"), Is.False);
            Assert.That(manager.GroupList[0].LayoutList.Select(layout => layout.Name), Is.EqualTo(new[] { "B" }));
        }

        [Test]
        public void TwoColumnStatsAlternateColumnsAndClearTheBuilder()
        {
            HLProfilerConsole console = CreateConsole();
            typeof(HLProfilerConsole).GetField("m_useTwoColumns", Instance).SetValue(console, true);
            typeof(HLProfilerConsole).GetField("m_richTextValueColorString", Instance).SetValue(console, "<color=Orange>");
            MethodInfo append = typeof(HLProfilerConsole).GetMethod("GUIStatsGroupsAppendToString", Instance);
            var builder = new StringBuilder();
            var group = new HLProfilerGroup("Lucid", HLProfilerCategory.Scripts, false);
            Assert.That(append.Invoke(console, new object[] { group, builder, 0 }), Is.EqualTo(1));
            Assert.That(append.Invoke(console, new object[] { group, builder, 1 }), Is.EqualTo(0));
            Assert.That(builder.Length, Is.Zero);
            string[] columns = Field<string[]>(console, "m_statsText");
            Assert.That(columns[0], Is.EqualTo("<color=Orange><b>Lucid</b></color>" + Environment.NewLine));
            Assert.That(columns[1], Is.EqualTo(columns[0]));

            typeof(HLProfilerConsole).GetField("m_useTwoColumns", Instance).SetValue(console, false);
            Assert.That(append.Invoke(console, new object[] { group, builder, 1 }), Is.EqualTo(0), "single column always returns 0");
            Assert.That(builder.ToString(), Is.EqualTo("<color=Orange><b>Lucid</b></color>" + Environment.NewLine));
        }
    }
}
