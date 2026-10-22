using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionXpRewards (Game.Runtime 0x020008eb; Awake 0x669640, Refresh 0x669650, constructor 0x669AD0) over a real
    // mission manager, collectable manager and save. The XP label is "XP", so the XP strings are "+{0}XP". The active context is
    // either a data-only context that supplies the ring XP, or a real regular context. Not covered: XP from clearing the mission
    // (MissionClearXPEarned has a private setter and is written only by OnActiveMissionComplete, which also needs the level
    // manager's ring reward table, the challenge manager's AddXP and the progression manager's level cache; writing the property's
    // compiler field instead is not allowed).
    public sealed class UIContainerMissionXpRewardsTests
    {
        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { Strings.CHALLENGES_LABEL_XP, "XP" },
        };

        // The members Refresh reads; nothing else of the context is reached.
        private sealed class RingXpContext : IMissionContext
        {
            public int RingXPEarned { get; set; }

            public MissionDefinition MissionDefinition => null;
            public GameplayLevelDefinition LevelDefinition => throw new NotSupportedException();
            public IReadOnlyList<int> OverrideActiveObjectives => throw new NotSupportedException();
            public IReadOnlyList<CharacterId> OverrideAllowedCharacters => throw new NotSupportedException();
            public float BestTimeSeconds => throw new NotSupportedException();
            public float OverrideTimeLimitSeconds => throw new NotSupportedException();
            public LevelStartPositionDefinition OverrideStartPosition => throw new NotSupportedException();
            public bool IgnoreSavedProgress => throw new NotSupportedException();
            public bool IsReplay => throw new NotSupportedException();
            public float RealTimeTaken { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool SaveLastLevelVisited => throw new NotSupportedException();
            public string Description => throw new NotSupportedException();
            public int ObjectiveTarget => throw new NotSupportedException();
            public float NewTime => throw new NotSupportedException();
            public void OnActiveMissionComplete(MissionManager missionManager, float timeElapsedSeconds, int score) => throw new NotSupportedException();
            public void MarkNewAttempt(MissionState state) => throw new NotSupportedException();
            public void SendMissionStartAnalytics(MissionState missionState) => throw new NotSupportedException();
            public void SendMissionQuitAnalytics(MissionState missionState) => throw new NotSupportedException();
            public void SendMissionFailedAnalytics(MissionState missionState) => throw new NotSupportedException();
            public void SendMissionCompleteAnalytics(MissionState missionState) => throw new NotSupportedException();
            public string CalculateMissionIndex(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition) => throw new NotSupportedException();
            public AnalyticsMissionType CalculateAnalyticsMissionType(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition) => throw new NotSupportedException();
        }

        private sealed class Rig
        {
            internal World World;
            internal SaveDataGame Save;
            internal UIContainerMissionXpRewards Container;
            internal UIAnimatingCounter RingCounter;
            internal UIAnimatingCounter MissionCounter;
        }

        private static void Run(Action<Rig> body) => WithWorld(world =>
        {
            Owned owned = world.Owned;
            owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
            SaveManager save = owned.Register(new SaveManager());
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(save)));
            var rig = new Rig { World = world, Save = new SaveDataGame() };
            rig.Save.Initialise();
            Set(save, "m_currentSave", rig.Save); // Reflection: the open save the manager keeps
            rig.Container = owned.Host("Lucid mission XP rewards", false).AddComponent<UIContainerMissionXpRewards>();
            rig.RingCounter = owned.Host("Lucid ring XP counter", false).AddComponent<UIAnimatingCounter>();
            rig.MissionCounter = owned.Host("Lucid mission XP counter", false).AddComponent<UIAnimatingCounter>();
            Set(rig.Container, "m_ringXPAnimatingCounter", rig.RingCounter); // Reflection: serialized references
            Set(rig.Container, "m_missionXPAnimatingCounter", rig.MissionCounter);
            OwnedStringTable.Run(OwnedStrings, () => body(rig));
        });

        private static void CollectRings(Rig rig, int collected)
        {
            rig.World.Collectables.RegisterCollectable(CollectableType.Ring, 9);
            rig.World.Collectables.TryGetCollectableState(CollectableType.Ring, out CollectableState state);
            state.AdjustCollected(collected, true);
        }

        // dump.cs TypeDefIndex 2282 and the DummyDll rows (fields 0x04002428..0x04002431).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerMissionXpRewards), 0x00100001,
                new (string, int)[]
                {
                    ("m_ringXPAnimatingCounter", 0x0001), ("m_missionXPAnimatingCounter", 0x0001), ("EarnedXPFromRings", 0x0026), ("EarnedXPFromMission", 0x0026),
                    ("RingsCollected", 0x0026), ("RingXPEarned", 0x0026), ("MissionXPEarned", 0x0026), ("XPTotal", 0x0026),
                    ("m_missionManageRef", 0x0021), ("m_saveManagerRef", 0x0021),
                },
                new (string, int)[] { ("Awake", 0x0081), ("Refresh", 0x0081), (".ctor", 0x1886) });
        }

        // Refresh 0x669650 shows the ring XP: the flag follows a positive amount (setg at 0x669802), the rings collected read "x7"
        // (0x66983B), the XP reads "+15XP" and the ring counter is set up from zero towards the amount with the same format. The
        // running total (the save's total XP) uses the same format. With no mission XP the mission side stays untouched. Awake calls
        // Refresh (0x669645 jumps to it).
        [Test]
        public void AwakeShowsTheRingXpAndTheRunningTotal()
        {
            Run(rig =>
            {
                CollectRings(rig, 7);
                rig.World.Missions.SetActiveMissionContext(new RingXpContext { RingXPEarned = 15 });
                rig.Save.SaveDataChallenges.TotalXPEarned = 1234;

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.EarnedXPFromRings.Value, Is.True);
                Assert.That(rig.Container.RingsCollected.Value, Is.EqualTo("x7"));
                Assert.That(rig.Container.RingXPEarned.Value, Is.EqualTo("+15XP"));
                Assert.That(Get<int>(rig.RingCounter, "m_endAmount"), Is.EqualTo(15));
                Assert.That(rig.RingCounter.CurrentValueString.Value, Is.EqualTo("+0XP"), "counting starts from zero");
                Assert.That(rig.Container.XPTotal.Value, Is.EqualTo("+1234XP"));
                Assert.That(rig.Container.EarnedXPFromMission.Value, Is.False);
                Assert.That(rig.Container.MissionXPEarned.Value, Is.Null);
                Assert.That(rig.MissionCounter.CurrentValueString.Value, Is.Null);
            });
        }

        // The ring XP flag needs a strictly positive amount.
        [Test]
        public void NoRingXpClearsTheRingFlag()
        {
            foreach (int ringXp in new[] { 0, -3 })
            {
                Run(rig =>
                {
                    CollectRings(rig, 2);
                    rig.World.Missions.SetActiveMissionContext(new RingXpContext { RingXPEarned = ringXp });
                    rig.Container.EarnedXPFromRings.Value = true;

                    Call(rig.Container, "Refresh");

                    Assert.That(rig.Container.EarnedXPFromRings.Value, Is.False, "ring XP " + ringXp);
                    Assert.That(rig.Container.RingXPEarned.Value, Is.EqualTo("+" + ringXp + "XP"), "the amount is shown whatever it is");
                });
            }
        }

        // Without a state for the ring collectable nothing is shown (the early return at 0x669751): every binding is left as it was.
        [Test]
        public void WithoutRingCollectableStateNothingIsTouched()
        {
            Run(rig =>
            {
                rig.World.Missions.SetActiveMissionContext(new RingXpContext { RingXPEarned = 15 });
                rig.Container.RingsCollected.Value = "kept";
                rig.Container.XPTotal.Value = "kept";

                Call(rig.Container, "Refresh");

                Assert.That(rig.Container.EarnedXPFromRings.Value, Is.False);
                Assert.That(rig.Container.RingsCollected.Value, Is.EqualTo("kept"));
                Assert.That(rig.Container.XPTotal.Value, Is.EqualTo("kept"));
                Assert.That(rig.RingCounter.CurrentValueString.Value, Is.Null);
            });
        }

        // A regular context that earned nothing for clearing the mission (its reward is set when the mission completes) shows no
        // mission XP: the flag and text stay as they were (it is only ever set, 0x6699D4 jle), the mission counter is not set up.
        [Test]
        public void ARegularContextWithoutMissionXpShowsNoMissionXp()
        {
            Run(rig =>
            {
                CollectRings(rig, 4);
                Mission mission = rig.World.NewMission();
                Set(mission.Definition, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
                var level = rig.World.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(level, "m_guid", "lucid-xp-level"); // Reflection: authored GUID of a serialized definition
                rig.World.Missions.SetActiveMissionContext(new MissionContextRegular(mission.Definition, level, false));
                rig.Container.EarnedXPFromMission.Value = true;

                Call(rig.Container, "Refresh");

                Assert.That(rig.Container.EarnedXPFromRings.Value, Is.False, "a regular context has earned no ring XP yet");
                Assert.That(rig.Container.RingsCollected.Value, Is.EqualTo("x4"));
                Assert.That(rig.Container.EarnedXPFromMission.Value, Is.True, "never cleared");
                Assert.That(rig.Container.MissionXPEarned.Value, Is.Null);
                Assert.That(rig.MissionCounter.CurrentValueString.Value, Is.Null);
            });
        }
    }
}
