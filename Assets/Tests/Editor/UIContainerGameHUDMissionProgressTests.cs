using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerGameHUDMissionProgress (Game.Runtime 0x020008c0; Start 0x633F90, OnDestroy 0x634020, InitialiseMissionUI
    // 0x634590, CloseMissionUI 0x6340F0, OnMissionsReset 0x634840, SetupMissionUI 0x634620, SetupActiveMissionsUI 0x6348A0,
    // OnMissionIslandActive 0x635080, RefreshActiveMission 0x6353E0, the prefab getters 0x635270..0x635350, constructor 0x635810)
    // over a real mission manager whose states hold real instanced trackers (set up through MissionState's own tracker set-up
    // step) and real UIContainerMissionProgress prefabs. The progress containers are always displayed, so no fade coroutine
    // runs, except in the one test that hides a container that is not always displayed (its fade coroutine is refused on the inactive parent). Not covered: the island override state of
    // the manager (SetupMissionUI's call of OnMissionIslandActive for it).
    public sealed class UIContainerGameHUDMissionProgressTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { (Strings)(-336558325), "{0} of {1}" }, // the progress container's default format string
        };

        private sealed class Rig
        {
            internal World World;
            internal Owned Owned;
            internal UIContainerGameHUDMissionProgress Hud;
            internal Transform DefaultParent;
            internal Transform PersistentParent;
            internal UIContainerMissionProgress ActivePrefab;
            internal UIContainerMissionProgress PersistentPrefab;
        }

        private static UIContainerMissionProgress NewPrefab(Owned owned, string name, bool onlyShowWhenIslandActive)
        {
            var prefab = owned.Host(name, false).AddComponent<UIContainerMissionProgress>();
            Set(prefab, "m_alwaysDisplay", true); // Reflection: serialized options
            Set(prefab, "m_onlyShowWhenMissionIslandActive", onlyShowWhenIslandActive);
            return prefab;
        }

        private static void Run(Action<Rig> body) => WithWorld(world =>
        {
            Owned owned = world.Owned;
            var rig = new Rig { World = world, Owned = owned };
            rig.Hud = owned.Host("Lucid HUD mission progress", false).AddComponent<UIContainerGameHUDMissionProgress>();
            rig.DefaultParent = owned.Host("Lucid default progress parent", false).transform;
            rig.PersistentParent = owned.Host("Lucid persistent progress parent", false).transform;
            rig.ActivePrefab = NewPrefab(owned, "Lucid active progress prefab", false);
            rig.PersistentPrefab = NewPrefab(owned, "Lucid persistent progress prefab", true);
            Set(rig.Hud, "m_defaultMissionProgressParent", rig.DefaultParent); // Reflection: serialized references
            Set(rig.Hud, "m_persistentMissionProgressParent", rig.PersistentParent);
            Set(rig.Hud, "m_defaultActiveMissionProgressPrefab", rig.ActivePrefab);
            Set(rig.Hud, "m_defaultPersistentMissionProgressPrefab", rig.PersistentPrefab);
            OwnedStringTable.Run(OwnedStrings, () => body(rig));
        });

        // A mission with a real instanced tracker, set up the way MissionState.SetUpTracker's callback does, and recorded in the
        // mission manager's states under its GUID.
        private static MissionState AddMission(Rig rig, bool persistent = false, bool canComplete = true, MissionType? type = null, int target = 2)
        {
            Mission mission = rig.World.NewMission(target, target);
            if (persistent) Set(mission.Definition, "m_isPersistentTracker", true); // Reflection: authored mission data
            if (type.HasValue) Set(mission.Definition, "m_type", type.Value);
            var tracker = rig.World.NewTracker<MissionInstancedTracker>();
            Set(tracker, "m_missionObjectives", new List<MissionObjective>()); // Reflection: authored (empty) objective list
            Set(tracker, "m_canComplete", canComplete);
            Call(mission.State, "OnTrackerSetup", tracker, null, null, true);
            Get<Dictionary<string, MissionState>>(rig.World.Missions, "m_missionStates")[mission.Definition.GetGUID()] = mission.State; // Reflection: what LoadMission records
            return mission.State;
        }

        private static Dictionary<MissionDefinition, UIContainerMissionProgress> Containers(Rig rig) =>
            Get<Dictionary<MissionDefinition, UIContainerMissionProgress>>(rig.Hud, "m_activeMissionContainersByDefinition");

        private static int Hooks(Delegate handlers, object target) =>
            handlers == null ? 0 : handlers.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target));

        // dump.cs TypeDefIndex 2239 and the DummyDll rows (fields 0x040022e1..0x040022e6).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerGameHUDMissionProgress), 0x00100101,
                new (string, int)[]
                {
                    ("m_defaultActiveMissionProgressPrefab", 0x0001), ("m_defaultPersistentMissionProgressPrefab", 0x0001),
                    ("m_persistentMissionProgressParent", 0x0001), ("m_defaultMissionProgressParent", 0x0001),
                    ("m_missionManagerRef", 0x0021), ("m_activeMissionContainersByDefinition", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("InitialiseMissionUI", 0x0081), ("CloseMissionUI", 0x0081),
                    ("OnMissionsReset", 0x0081), ("SetupMissionUI", 0x0081), ("SetupActiveMissionsUI", 0x0081),
                    ("OnMissionIslandActive", 0x0081), ("RefreshActiveMission", 0x0081), ("GetActiveContainerPrefab", 0x0081),
                    ("GetPersistentContainerPrefab", 0x0081), ("GetContainerPrefab", 0x0081), (".ctor", 0x1886),
                });
        }

        // SetupActiveMissionsUI 0x6348A0: each tracker that can complete gets a container instantiated from its default prefab
        // under its parent (a persistent tracker, 0x634BF9 get_IsPersistentTracker, under the persistent prefab and parent), set up
        // with the tracker, recorded under the tracker's definition and hooked to the tracker's status changes. A container is left
        // hidden unless it is always displayed and not tied to its island (0x634E6E..0x634E7F). A state without a tracker and a
        // tracker that cannot complete (0x634BEE) get none.
        [Test]
        public void EachCompletableTrackerGetsAContainerFromItsDefaultPrefabUnderItsParent()
        {
            Run(rig =>
            {
                MissionState ordinary = AddMission(rig);
                MissionState persistent = AddMission(rig, persistent: true);
                AddMission(rig, canComplete: false);
                Get<Dictionary<string, MissionState>>(rig.World.Missions, "m_missionStates")["lucid-no-tracker"] = new MissionState();

                Call(rig.Hud, "SetupActiveMissionsUI");

                Dictionary<MissionDefinition, UIContainerMissionProgress> containers = Containers(rig);
                Assert.That(containers.Count, Is.EqualTo(2));
                UIContainerMissionProgress ordinaryContainer = containers[ordinary.Definition];
                UIContainerMissionProgress persistentContainer = containers[persistent.Definition];
                Assert.That(ordinaryContainer.name, Is.EqualTo("Lucid active progress prefab(Clone)"));
                Assert.That(ordinaryContainer.transform.parent, Is.SameAs(rig.DefaultParent));
                Assert.That(ordinaryContainer.InstancedTracker, Is.SameAs(ordinary.Tracker));
                Assert.That(persistentContainer.name, Is.EqualTo("Lucid persistent progress prefab(Clone)"));
                Assert.That(persistentContainer.transform.parent, Is.SameAs(rig.PersistentParent));
                Assert.That(persistentContainer.InstancedTracker, Is.SameAs(persistent.Tracker));
                Assert.That(ordinaryContainer.gameObject.activeSelf, Is.True, "always displayed and not tied to its island");
                Assert.That(persistentContainer.gameObject.activeSelf, Is.False, "tied to its island");
                Assert.That(Hooks(((MissionInstancedTracker)ordinary.Tracker).OnStatusChanged, rig.Hud), Is.EqualTo(1));
                Assert.That(Hooks(((MissionInstancedTracker)persistent.Tracker).OnStatusChanged, rig.Hud), Is.EqualTo(1));
            });
        }

        // 0x634E6E..0x634E74: a container is hidden when it is not always displayed, whether or not it is tied to its island.
        [Test]
        public void AContainerThatIsNotAlwaysDisplayedIsHiddenEvenWhenNotTiedToItsIsland()
        {
            Run(rig =>
            {
                var prefab = rig.Owned.Host("Lucid timed progress prefab", false).AddComponent<UIContainerMissionProgress>();
                Set(prefab, "m_canvasGroup", prefab.gameObject.AddComponent<CanvasGroup>()); // Reflection: serialized reference, read when the container is enabled
                Set(rig.Hud, "m_defaultActiveMissionProgressPrefab", prefab); // Reflection: serialized reference
                MissionState state = AddMission(rig);

                LogAssert.Expect(LogType.Error, RefusedCoroutine); // Refresh starts the fade of a container that is not always displayed
                Call(rig.Hud, "SetupActiveMissionsUI");

                Assert.That(prefab.AlwaysDisplay, Is.False);
                Assert.That(prefab.OnlyShowWhenMissionIslandActive, Is.False);
                Assert.That(Containers(rig)[state.Definition].gameObject.activeSelf, Is.False);
            });
        }

        // SetupActiveMissionsUI 0x6348A0: a tracker that names a group gets a group container, cloned from the group identifier's
        // prefab under the tracker's parent (0x634DC9) and kept per mission type (TryGetValue 0x634D31, set_Item 0x634DE6), and
        // its container is parented to the group's transform (0x634DF1). A second tracker of the same type uses the first group
        // even with another group identifier; a tracker of another type gets its own.
        [Test]
        public void GroupedTrackersShareOneGroupContainerPerMissionType()
        {
            Run(rig =>
            {
                Func<string, UIContainerIdentifier> newGroup = name =>
                {
                    var identifier = rig.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                    identifier.RegisterContainer(NewPrefab(rig.Owned, name, false));
                    return identifier;
                };
                MissionState first = AddMission(rig);
                MissionState second = AddMission(rig);
                MissionState other = AddMission(rig, type: MissionType.Jukebox);
                Set(first.Tracker, "m_uiContainerIdentifierGroup", newGroup("Lucid group A")); // Reflection: authored groups
                Set(second.Tracker, "m_uiContainerIdentifierGroup", newGroup("Lucid group B"));
                Set(other.Tracker, "m_uiContainerIdentifierGroup", newGroup("Lucid group C"));

                Call(rig.Hud, "SetupActiveMissionsUI");

                Assert.That(rig.DefaultParent.childCount, Is.EqualTo(2));
                Transform groupOfFirst = Containers(rig)[first.Definition].transform.parent;
                Assert.That(groupOfFirst.name, Is.EqualTo("Lucid group A(Clone)"));
                Assert.That(groupOfFirst.parent, Is.SameAs(rig.DefaultParent));
                Assert.That(Containers(rig)[second.Definition].transform.parent, Is.SameAs(groupOfFirst));
                Transform groupOfOther = Containers(rig)[other.Definition].transform.parent;
                Assert.That(groupOfOther.name, Is.EqualTo("Lucid group C(Clone)"));
                Assert.That(groupOfOther.parent, Is.SameAs(rig.DefaultParent));
            });
        }

        // The prefab getters 0x635270 and 0x6352E0: no override identifier gives the default of the kind; an identifier gives its
        // loaded container when that is a progress container (GetContainerPrefab 0x635350, an exact class test) and null otherwise.
        [Test]
        public void AnOverrideIdentifierReplacesTheDefaultPrefabAndAForeignContainerGivesNone()
        {
            Run(rig =>
            {
                var own = rig.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                UIContainerMissionProgress custom = NewPrefab(rig.Owned, "Lucid custom progress prefab", false);
                own.RegisterContainer(custom);
                var foreign = rig.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                foreign.RegisterContainer(rig.Owned.Host("Lucid other container", false).AddComponent<UIContainerChallengeResults>());

                Assert.That(Call(rig.Hud, "GetActiveContainerPrefab", new object[] { null }), Is.SameAs(rig.ActivePrefab));
                Assert.That(Call(rig.Hud, "GetPersistentContainerPrefab", new object[] { null }), Is.SameAs(rig.PersistentPrefab));
                Assert.That(Call(rig.Hud, "GetActiveContainerPrefab", own), Is.SameAs(custom));
                Assert.That(Call(rig.Hud, "GetPersistentContainerPrefab", own), Is.SameAs(custom));
                Assert.That(Call(rig.Hud, "GetContainerPrefab", own), Is.SameAs(custom));
                Assert.That(Call(rig.Hud, "GetActiveContainerPrefab", foreign), Is.Null);
                Assert.That(Call(rig.Hud, "GetPersistentContainerPrefab", foreign), Is.Null);
            });
        }

        // OnMissionIslandActive 0x635080: only containers tied to their island change, shown exactly when their tracker is the
        // active island mission's tracker (op_Equality at 0x6351CF); the others are left as they were.
        [Test]
        public void AnIslandBoundContainerIsShownOnlyForTheActiveIslandMission()
        {
            Run(rig =>
            {
                MissionState ordinary = AddMission(rig);
                MissionState persistent = AddMission(rig, persistent: true);
                Call(rig.Hud, "SetupActiveMissionsUI");
                UIContainerMissionProgress ordinaryContainer = Containers(rig)[ordinary.Definition];
                UIContainerMissionProgress persistentContainer = Containers(rig)[persistent.Definition];
                ordinaryContainer.gameObject.SetActive(false); // changed by hand: an untied container must not be touched

                Call(rig.Hud, "OnMissionIslandActive", persistent);
                Assert.That(persistentContainer.gameObject.activeSelf, Is.True);
                Assert.That(ordinaryContainer.gameObject.activeSelf, Is.False, "not tied to its island");

                Call(rig.Hud, "OnMissionIslandActive", ordinary);
                Assert.That(persistentContainer.gameObject.activeSelf, Is.False);
                Assert.That(ordinaryContainer.gameObject.activeSelf, Is.False);
            });
        }

        // SetupMissionUI 0x634620: after the containers are built, the island event is hooked once, for the first container tied to
        // its island, and the loop ends there (the jump at 0x6347AB goes to the enumerator's disposal).
        [Test]
        public void TheIslandEventIsHookedOnceWhateverTheNumberOfIslandBoundContainers()
        {
            Run(rig =>
            {
                AddMission(rig, persistent: true);
                AddMission(rig, persistent: true);
                AddMission(rig, persistent: true);

                Call(rig.Hud, "SetupMissionUI", rig.World.Missions);

                Assert.That(Containers(rig).Count, Is.EqualTo(3));
                Assert.That(Hooks(Get<Delegate>(rig.World.Missions, "OnMissionIslandActive"), rig.Hud), Is.EqualTo(1));
            });
        }

        // Without any island-bound container the island event is not hooked.
        [Test]
        public void WithoutIslandBoundContainersTheIslandEventIsNotHooked()
        {
            Run(rig =>
            {
                AddMission(rig);

                Call(rig.Hud, "SetupMissionUI", rig.World.Missions);

                Assert.That(Containers(rig).Count, Is.EqualTo(1));
                Assert.That(Hooks(Get<Delegate>(rig.World.Missions, "OnMissionIslandActive"), rig.Hud), Is.EqualTo(0));
            });
        }

        // RefreshActiveMission 0x6353E0: a tracker without a group refreshes its own container only; a grouped tracker (0x6353F8
        // op_Equality against null) refreshes the containers of every definition of its mission type (get_Type at 0x635564),
        // with no early exit. A refresh is visible in the container's target count, which is read from the tracker.
        [Test]
        public void AGroupedTrackerRefreshesEveryContainerOfItsMissionType()
        {
            Run(rig =>
            {
                MissionState first = AddMission(rig);
                MissionState second = AddMission(rig);
                MissionState other = AddMission(rig, type: MissionType.Jukebox);
                Call(rig.Hud, "SetupActiveMissionsUI");
                var trackers = new[] { first, second, other }.Select(state => (MissionInstancedTracker)state.Tracker).ToArray();
                foreach (MissionInstancedTracker tracker in trackers) Set(tracker, "m_targetObjectivesToComplete", 7); // Reflection: progress the containers have not seen
                Func<MissionState, int> shownTarget = state => Containers(rig)[state.Definition].ObjectivesTarget.Value;

                Call(rig.Hud, "RefreshActiveMission", trackers[0]);

                Assert.That(shownTarget(first), Is.EqualTo(7), "its own container");
                Assert.That(shownTarget(second), Is.EqualTo(2), "an ungrouped tracker leaves the others alone");
                Assert.That(shownTarget(other), Is.EqualTo(2));

                Set(trackers[1], "m_uiContainerIdentifierGroup", rig.Owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>())); // Reflection: authored group
                Set(trackers[0], "m_targetObjectivesToComplete", 9);
                Set(trackers[1], "m_targetObjectivesToComplete", 9);

                Call(rig.Hud, "RefreshActiveMission", trackers[1]);

                Assert.That(shownTarget(first), Is.EqualTo(9), "the same type");
                Assert.That(shownTarget(second), Is.EqualTo(9), "the same type");
                Assert.That(shownTarget(other), Is.EqualTo(2), "another type");
            });
        }

        // CloseMissionUI 0x6340F0: every instanced tracker is unhooked, the island event is unhooked and the container records are
        // forgotten (the containers themselves stay).
        [Test]
        public void ClosingTheUiUnhooksTheTrackersAndForgetsTheContainers()
        {
            Run(rig =>
            {
                MissionState ordinary = AddMission(rig);
                MissionState persistent = AddMission(rig, persistent: true);
                Call(rig.Hud, "SetupMissionUI", rig.World.Missions);
                UIContainerMissionProgress container = Containers(rig)[ordinary.Definition];

                Call(rig.Hud, "CloseMissionUI");

                Assert.That(Containers(rig), Is.Empty);
                Assert.That(Hooks(((MissionInstancedTracker)ordinary.Tracker).OnStatusChanged, rig.Hud), Is.EqualTo(0));
                Assert.That(Hooks(((MissionInstancedTracker)persistent.Tracker).OnStatusChanged, rig.Hud), Is.EqualTo(0));
                Assert.That(Hooks(Get<Delegate>(rig.World.Missions, "OnMissionIslandActive"), rig.Hud), Is.EqualTo(0));
                Assert.That(container != null, Is.True, "the spawned container is not destroyed");
            });
        }

        // InitialiseMissionUI 0x634590 (reached by Start 0x633F90 once the manager is valid) follows the manager's resets and
        // builds the UI; OnMissionsReset 0x634840 rebuilds it only when the sub-missions were reloaded; OnDestroy 0x634020
        // closes the UI and stops following the resets.
        [Test]
        public void StartFollowsTheResetsAndAReloadRebuildsTheContainers()
        {
            Run(rig =>
            {
                MissionState ordinary = AddMission(rig);

                Call(rig.Hud, "Start");

                Assert.That(Hooks(Get<Delegate>(rig.World.Missions, "OnMissionsReset"), rig.Hud), Is.EqualTo(1));
                UIContainerMissionProgress first = Containers(rig)[ordinary.Definition];

                Call(rig.Hud, "OnMissionsReset", false);
                Assert.That(Containers(rig)[ordinary.Definition], Is.SameAs(first), "nothing is rebuilt");

                Call(rig.Hud, "OnMissionsReset", true);
                Assert.That(Containers(rig).Count, Is.EqualTo(1));
                Assert.That(Containers(rig)[ordinary.Definition], Is.Not.SameAs(first), "a new container");
                Assert.That(Hooks(((MissionInstancedTracker)ordinary.Tracker).OnStatusChanged, rig.Hud), Is.EqualTo(1), "unhooked then hooked again");

                Call(rig.Hud, "OnDestroy");
                Assert.That(Containers(rig), Is.Empty);
                Assert.That(Hooks(Get<Delegate>(rig.World.Missions, "OnMissionsReset"), rig.Hud), Is.EqualTo(0));
            });
        }
    }
}
