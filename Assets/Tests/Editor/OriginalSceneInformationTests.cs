using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime SceneInformation (TypeDef 0x020001cf, x86-64 0x6F26F0..0x6F2B10) over
    // an unawakened HLProfilerDataManager registered in an owned row, so its group list is
    // empty unless a case adds its own hidden group. The active Edit Mode scene holds unrelated
    // objects too, so the object counter is compared before and after the case adds its own hierarchy.
    public sealed class OriginalSceneInformationTests
    {
        private static readonly MethodInfo TotalChildCount =
            typeof(SceneInformation).GetMethod("GetTotalChildCount", BindingFlags.Static | BindingFlags.NonPublic);

        private static int Total(Transform transform, int count) => (int)TotalChildCount.Invoke(null, new object[] { transform, count });

        private static HLProfilerDataManager Profiler(Owned owned) =>
            owned.Register(owned.Host("Lucid profiler data manager", false).AddComponent<HLProfilerDataManager>());

        // Metadata of TypeDef 0x020001cf; GetTotalChildCount's count defaults to 1.
        [Test]
        public void DeclarationsKeepTheReadonlyPropertiesAndDefaultCount()
        {
            AssertMembers(typeof(SceneInformation), 0x00100001,
                new[] { ("<SceneName>k__BackingField", 0x0021), ("<GameObjectCounter>k__BackingField", 0x0021), ("Snapshots", 0x0006), ("m_profilerManager", 0x0001) },
                new[] { ("get_SceneName", 0x0886), ("get_GameObjectCounter", 0x0886), (".ctor", 0x1886), ("CaptureGameSnapshot", 0x0086),
                        ("OutputData", 0x0086), ("GetGameObjectCount", 0x0091), ("GetTotalChildCount", 0x0091) });
            AssertIl2CppOptions(typeof(SceneInformation), 1, 2);
            ParameterInfo count = TotalChildCount.GetParameters()[1];
            Assert.That(((int)count.Attributes, count.DefaultValue), Is.EqualTo((0x1010, (object)1)));
        }

        // GetTotalChildCount 0x6F2B10 returns count for a leaf. Otherwise it reads childCount
        // once, folds each child's subtree into count (count = recursion, 0x6f2b57) and adds the
        // direct child count last (0x6f2b61), so the result is count plus every descendant.
        [Test]
        public void ChildCountAddsEveryDescendantToTheCount()
        {
            Owned.Run(owned =>
            {
                Transform root = owned.Host("Lucid count root", false).transform;
                Transform leaf = owned.Host("Lucid count leaf", false, root).transform;
                Transform branch = owned.Host("Lucid count branch", true, root).transform;
                owned.Host("Lucid count twig", false, branch);
                Assert.That(Total(leaf, 4), Is.EqualTo(4));
                Assert.That(Total(branch, 1), Is.EqualTo(2));
                Assert.That(Total(root, 1), Is.EqualTo(4));
                Assert.That(Total(root, 10), Is.EqualTo(13));
            });
        }

        // The constructor 0x6F2710 creates Snapshots and the profiler reference before the base
        // constructor, then calls Get().StartGroupProfiling(true), stores scene.name and sums
        // GetTotalChildCount over the root objects, inactive ones included.
        [Test]
        public void ConstructionNamesTheSceneAndCountsEveryObject()
        {
            Owned.Run(owned =>
            {
                HLProfilerDataManager profiler = Profiler(owned);
                Scene scene = SceneManager.GetActiveScene();
                int before = new SceneInformation(scene).GameObjectCounter;

                Transform first = owned.Host("Lucid scene root", false).transform;
                owned.Host("Lucid scene leaf", true, first);
                Transform branch = owned.Host("Lucid scene branch", false, first).transform;
                owned.Host("Lucid scene twig", true, branch);
                owned.Host("Lucid scene second root", true);

                var information = new SceneInformation(scene);
                Assert.That(information.SceneName, Is.EqualTo(scene.name));
                Assert.That(information.GameObjectCounter - before, Is.EqualTo(5));
                Assert.That(information.Snapshots, Is.Not.Null.And.Empty);
                Assert.That(Get<SystemRef<HLProfilerDataManager>>(information, "m_profilerManager").GetSafe(), Is.SameAs(profiler));
            });
        }

        // Without a registered profiler the constructor's Get (0x6f27fe) diagnoses the null system.
        [Test]
        public void ConstructionRequiresTheProfilerDataManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<HLProfilerDataManager>();
                var missing = Assert.Throws<NullReferenceException>(() => new SceneInformation(SceneManager.GetActiveScene()));
                Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<HLProfilerDataManager>() + "' is null"));
            });
        }

        // CaptureGameSnapshot 0x6F2900 appends GetProfilerSnapshot(false). OutputData 0x6F29C0
        // only enumerates the snapshots (the retail loop body is empty) and stops profiling;
        // the list is kept.
        [Test]
        public void SnapshotsAccumulateAndOutputKeepsThem()
        {
            Owned.Run(owned =>
            {
                Profiler(owned);
                var information = new SceneInformation(SceneManager.GetActiveScene());
                information.CaptureGameSnapshot();
                information.CaptureGameSnapshot();
                StringBuilder[] snapshots = information.Snapshots.ToArray();
                Assert.That(snapshots.Length, Is.EqualTo(2));
                Assert.That(snapshots[0], Is.Not.SameAs(snapshots[1]));
                Assert.That(snapshots.All(snapshot => snapshot.ToString().StartsWith("HLProfiler snapshot - frame ")), Is.True);

                information.OutputData();
                Assert.That(information.Snapshots, Is.EqualTo(snapshots));
            });
        }

        // The three profiler calls pass literal flags: StartGroupProfiling(true) (esi = 1 at
        // 0x6f2806), GetProfilerSnapshot(false) (esi = 0 at 0x6f2947) and StopGroupProfiling(true)
        // (esi = 1 at 0x6f2a98), against the defaults false, true and false. A hidden group
        // added through StartTrackingProfilerMarker shows each one: construction makes it
        // visible, a snapshot lists it while hidden behind HLProfilerUtils.NotActivePrefix, and OutputData
        // hides it again. The marker's data is shut down afterwards, disposing its recorder.
        [Test]
        public void ProfilingIsForcedVisibleSnapshotsIncludeHiddenGroupsAndOutputHidesThem()
        {
            Owned.Run(owned =>
            {
                HLProfilerDataManager profiler = Profiler(owned);
                const HLProfilerCategory category = HLProfilerCategory.Memory;
                try
                {
                    profiler.StartTrackingProfilerMarker(new ProfilerMarker("lucid scene information"), category, "Lucid group", "marker", 0, false);
                    HLProfilerGroup group = profiler.GroupList.Single();
                    Assert.That(group.IsVisible, Is.False);

                    var information = new SceneInformation(SceneManager.GetActiveScene());
                    Assert.That(group.IsVisible, Is.True, "StartGroupProfiling(true)");

                    group.ToggleVisible(false);
                    information.CaptureGameSnapshot();
                    Assert.That(information.Snapshots.Single().ToString(), Does.Contain("(Not active) - Lucid group"),
                        "GetProfilerSnapshot(false) keeps the hidden group");
                    Assert.That(profiler.GetProfilerSnapshot().ToString(), Does.Not.Contain("Lucid group"), "the default skips it");

                    group.ToggleVisible(true);
                    information.OutputData();
                    Assert.That(group.IsVisible, Is.False, "StopGroupProfiling(true)");
                }
                finally
                {
                    foreach (HLProfilerData data in Get<Dictionary<string, HLProfilerData>>(profiler, "m_customDataList").Values) data.Shutdown();
                }
            });
        }
    }
}
