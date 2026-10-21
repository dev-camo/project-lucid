using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionBase (Game.Runtime 0x020008d8; Setup 0x63B840, SetMissionState 0x63B9E0) through a concrete test
    // container, over a real MissionManager. The text comes from an owned string table: the mission name is
    // "Collect {1} in {0}" and the timer format is "{0}:{1}.{2}". Not covered: a mission that is the active one but whose
    // description is overridden by a completed-mission override (the override needs a saved completion).
    public sealed class UIContainerMissionBaseTests
    {
        private const Strings MissionName = (Strings)12345;
        private const string Guid = "lucid-mission-base-a";

        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { MissionName, "Collect {1} in {0}" },
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, "{0}:{1}.{2}" },
        };

        // A mission worth three objectives with a 90 second limit that counts down from the best time.
        private static MissionDefinition NewMission(Owned owned, string guid)
        {
            var mission = Create<MissionDefinition>(owned,
                "{\"m_missionName\":12345,\"m_timeLimitSeconds\":90,\"m_targetObjectivesCount\":3,\"m_totalObjectivesCount\":3,\"m_useBestTime\":true}");
            Set(mission, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
            return mission;
        }

        private static MissionState NewState(MissionDefinition mission)
        {
            var save = new SaveDataLevelMission(mission.GetGUID());
            save.DisableSaving();
            var state = new MissionState();
            state.SetUpData(mission, save, "lucid-level");
            return state;
        }

        // Setup adds a handler to the shared string-table actions; every handler of the container is taken off again.
        private static void WithStringHandlersRestored(object owner, Action body)
        {
            FastAction saved = StringTable.StringsTableHandlers;
            try
            {
                body();
            }
            finally
            {
                FastAction current = StringTable.StringsTableHandlers;
                if (current != null)
                    foreach (Action handler in current.GetInvocationList().ToArray())
                        if (ReferenceEquals(handler.Target, owner))
                            StringTable.StringsTableHandlers -= handler;
                StringTable.StringsTableHandlers = saved;
            }
        }

        private static void Run(Action<Owned, OriginalPersistentLevelObjectTests.Missions, MissionBaseProbeContainer> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                OwnedStringTable.Run(OwnedStrings, () =>
                {
                    var container = owned.Host("Lucid mission base", false).AddComponent<MissionBaseProbeContainer>();
                    WithStringHandlersRestored(container, () => body(owned, missions, container));
                });
            });

        private static Action[] Handlers(MissionBaseProbeContainer container) =>
            StringTable.StringsTableHandlers.GetInvocationList()
                .Where(handler => ReferenceEquals(handler.Target, container) && handler.Method.Name == "SetMissionState").ToArray();

        // SetMissionState 0x63B9E0 compares the active context's mission with the state's: with none active the name is the
        // mission's formatted name, and the objective image is the mission's objective icon.
        [Test]
        public void WithoutAnActiveContextTheNameIsTheFormattedMissionName()
        {
            Run((owned, missions, container) =>
            {
                MissionDefinition mission = NewMission(owned, Guid);
                Sprite icon = NewSprite(owned);
                Set(mission, "m_missionObjectiveIcon", icon); // Reflection: serialized icon
                Assert.That(missions.Manager.ActiveMissionContext, Is.Null);

                container.Setup(new MissionBaseProbeParameters(NewState(mission)));

                Assert.That(container.MissionNameLabel.Value, Is.EqualTo("Collect 3 in 1:30.00"));
                Assert.That(container.MissionObjectiveImage.Value, Is.SameAs(icon));
            });
        }

        // The active context describes its mission from the best time (none saved), the formatted name from the time limit:
        // "Collect 3 in " against "Collect 3 in 1:30.00". Only the state of the active mission takes the description.
        [Test]
        public void OnlyTheActiveMissionTakesTheContextsDescription()
        {
            Run((owned, missions, container) =>
            {
                MissionDefinition active = NewMission(owned, Guid);
                MissionDefinition other = NewMission(owned, "lucid-mission-base-b");
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(active, missions.CurrentLevel.LevelDefinition, false));

                container.Setup(new MissionBaseProbeParameters(NewState(active)));
                Assert.That(container.MissionNameLabel.Value, Is.EqualTo("Collect 3 in "), "the active mission");

                container.Setup(new MissionBaseProbeParameters(NewState(other)));
                Assert.That(container.MissionNameLabel.Value, Is.EqualTo("Collect 3 in 1:30.00"), "another mission");
            });
        }

        // The comparison at 0x63BACB is ScriptableObjectWithGuid.op_Equality, which compares GUIDs: a state whose definition is a
        // distinct object with the active mission's GUID still takes the context's description.
        [Test]
        public void TheActiveContextIsMatchedByGuidNotByReference()
        {
            Run((owned, missions, container) =>
            {
                MissionDefinition active = NewMission(owned, Guid);
                MissionDefinition twin = NewMission(owned, Guid);
                Assert.That(twin, Is.Not.SameAs(active));
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(active, missions.CurrentLevel.LevelDefinition, false));

                container.Setup(new MissionBaseProbeParameters(NewState(twin)));

                Assert.That(container.MissionNameLabel.Value, Is.EqualTo("Collect 3 in "));
            });
        }

        // Setup 0x63B8F0..0x63B9DA removes SetMissionState from StringTable.StringsTableHandlers before adding it, so it is
        // held once however often the container is set up; running that handler applies the state again.
        [Test]
        public void TheStringTableHandlerIsHeldOnceAndAppliesTheStateAgain()
        {
            Run((owned, missions, container) =>
            {
                MissionDefinition mission = NewMission(owned, Guid);
                var parameters = new MissionBaseProbeParameters(NewState(mission));

                container.Setup(parameters);
                container.Setup(parameters);

                Assert.That(Handlers(container).Length, Is.EqualTo(1));
                container.MissionNameLabel.Value = "stale";
                Handlers(container)[0]();
                Assert.That(container.MissionNameLabel.Value, Is.EqualTo("Collect 3 in 1:30.00"));
            });
        }

        // SetMissionState returns at once for parameters without a mission state (0x63BA1E), writing neither binding.
        [Test]
        public void ParametersWithoutAMissionStateWriteNothing()
        {
            Run((owned, missions, container) =>
            {
                container.Setup(new MissionBaseProbeParameters(null));

                Assert.That(container.MissionNameLabel.Value, Is.Null);
                Assert.That(container.MissionObjectiveImage.Value, Is.Null);
                Assert.That(Handlers(container).Length, Is.EqualTo(1), "the handler is still subscribed");
            });
        }
    }
}
