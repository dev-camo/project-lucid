using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    // HLProfilerDebugMenu (Game.Runtime TypeDef 0x020005f9, x86-64 0x560390..0x5604A7) as a real component on an owned inactive host
    // over a real HLProfilerConsole and, for the tracker toggle, the RuntimeIssueTracker rig of RuntimeIssueTrackerTests. The DebugMenu
    // buttons that call the two toggles are compiled out of retail, so the toggles are called directly.
    public sealed class HLProfilerDebugMenuTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private static void SetSerialized(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Awake 0x560390 is a bare return.
        [Test]
        public void AwakeDoesNothing()
        {
            Owned.Run(owned =>
            {
                var menu = owned.Host("Lucid profiler debug menu", false).AddComponent<HLProfilerDebugMenu>();
                Assert.DoesNotThrow(() => Call(menu, "Awake"));
            });
        }

        // ToggleProfiler 0x5603A0 closes an enabled console (CloseProfiler raises the block-input request with false) and opens a disabled
        // one (OpenProfiler enables it and raises nothing).
        [Test]
        public void TheProfilerToggleClosesAnEnabledConsoleAndOpensADisabledOne()
        {
            Owned.Run(owned =>
            {
                var menu = owned.Host("Lucid profiler debug menu", false).AddComponent<HLProfilerDebugMenu>();
                var console = owned.Host("Lucid profiler console", false).AddComponent<HLProfilerConsole>();
                SetSerialized(menu, "m_profilerConsole", console);
                var blocks = new List<bool>();
                console.OnProfilerBlockInputRequestEvent += blocks.Add;
                Assert.That(console.enabled, Is.True);

                Call(menu, "ToggleProfiler");
                Assert.That(console.enabled, Is.False);
                Assert.That(blocks, Is.EqualTo(new[] { false }));

                Call(menu, "ToggleProfiler");
                Assert.That(console.enabled, Is.True);
                Assert.That(blocks, Is.EqualTo(new[] { false }), "opening raises no request");

                Call(menu, "ToggleProfiler");
                Assert.That(console.enabled, Is.False);
                Assert.That(blocks, Is.EqualTo(new[] { false, false }));
            });
        }

        // ToggleIssueTracker 0x5603D0 shows a hidden tracker as OpenTracker(true) does (it enables the tracker, allows opening and
        // shows it, leaving the open-when-invalid gate as it was) and hides a visible one as CloseTracker(true) does (opening stays allowed
        // and the open-when-invalid gate is cleared).
        [Test]
        public void TheTrackerToggleOpensAHiddenTrackerAndClosesAVisibleOne()
        {
            RuntimeIssueTrackerTests.WithTracker(rig =>
            {
                var menu = rig.Owned.Host("Lucid profiler debug menu", false).AddComponent<HLProfilerDebugMenu>();
                SetSerialized(menu, "m_runtimeIssueTracker", rig.Tracker);
                RuntimeIssueTracker tracker = rig.Tracker;
                Call(tracker, "OnLevelActivated", (LevelManagerLevel)null);
                tracker.enabled = false;

                Call(menu, "ToggleIssueTracker");

                Assert.That(tracker.IsVisible, Is.True);
                Assert.That(tracker.enabled, Is.True);
                Assert.That(Get<bool>(tracker, "m_canOpen"), Is.True);
                Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.True, "opening leaves the gate");

                RuntimeIssueTrackerTests.WithRefusingCoroutineHost(rig.Owned, () =>
                {
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);

                    Call(menu, "ToggleIssueTracker");

                    Assert.That(tracker.IsVisible, Is.False);
                    Assert.That(Get<bool>(tracker, "m_canOpen"), Is.True, "closing from the menu allows reopening");
                    Assert.That(Get<bool>(tracker, "m_canOpenWhenInvalid"), Is.False);
                    Call(tracker, "<SetVisible>b__26_0");
                });
            });
        }
    }
}
