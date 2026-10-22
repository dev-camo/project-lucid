using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionFailure (Game.Runtime 0x020008da; OnEnable 0x63BCF0, Exit 0x63BDE0, Restart 0x63BFA0, CleanUp 0x63C030)
    // over a real App, MissionManager and LevelManager holding a current level. Not covered: the mixer volume reset of
    // LevelManager.Restart (it needs a registered AudioManager, which Edit Mode cannot build) and a restart that is refused
    // because a restart is already in progress or the meta game is active.
    public sealed class UIContainerMissionFailureTests
    {
        private const Strings Default = (Strings)111;
        private const Strings Stored = (Strings)222;

        private sealed class Rig
        {
            internal Owned Owned;
            internal App App;
            internal OriginalPersistentLevelObjectTests.Missions Missions;
            internal UIContainerMissionFailure Container;
            internal UIModernEvent ExitEvent;
            internal List<UIModernEvent> Events;
        }

        private static void Run(Action<Rig> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<UIScreenTransitionManager>(); // App's field initialiser
                var rig = new Rig { Owned = owned, Missions = missions };
                rig.App = owned.Register(owned.Host("Lucid app", false).AddComponent<App>());
                rig.Events = new List<UIModernEvent>();
                rig.App.Storage.SetValue(AppFSMKeys.UIModernEvents, rig.Events);
                rig.ExitEvent = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                rig.Container = owned.Host("Lucid mission failure", false).AddComponent<UIContainerMissionFailure>();
                Set(rig.Container, "m_exitEvent", rig.ExitEvent); // Reflection: serialized references
                Set(rig.Container, "m_defaultFailureMessage", Default);
                body(rig);
            });

        private static bool TryStored(Rig rig, out Strings stored) => rig.App.Storage.TryGetValue(AppFSMKeys.FailureScreenStringOverride, out stored);

        // OnEnable 0x63BCF0: the message the failing mission stored replaces the default only when it is present and not zero
        // (test esi, esi; cmove at 0x63BDA5, then test al, al; cmove at 0x63BDAB).
        [Test]
        public void TheStoredMessageReplacesTheDefaultOnlyWhenItIsPresentAndNotZero()
        {
            foreach ((Strings? stored, Strings expected) in new (Strings?, Strings)[]
            {
                (null, Default),
                ((Strings)0, Default),
                (Stored, Stored),
            })
            {
                Run(rig =>
                {
                    if (stored.HasValue) rig.App.Storage.SetValue(AppFSMKeys.FailureScreenStringOverride, stored.Value);

                    Call(rig.Container, "OnEnable");

                    Assert.That(rig.Container.FailureMessage.Value, Is.EqualTo(expected), "stored " + stored);
                });
            }
        }

        // Exit 0x63BDE0: once only. The stored message is cleared, the active mission is dropped, the exit event is queued
        // and the failure screen flag is written false.
        [Test]
        public void ExitClearsTheMessageDropsTheMissionQueuesTheEventAndShowsNoFailureScreen()
        {
            Run(rig =>
            {
                rig.Owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                rig.Owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                rig.Owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                var mission = ChallengeRewardTestSupport.Create<MissionDefinition>(rig.Owned);
                Set(mission, "m_guid", "lucid-failed-mission"); // Reflection: authored GUID of a serialized definition
                Set(mission, "m_subMissions", new List<MissionDefinition>());
                rig.Missions.Manager.SetActiveMissionContext(new MissionContextRegular(mission, rig.Missions.CurrentLevel.LevelDefinition, false));
                rig.App.Storage.SetValue(AppFSMKeys.FailureScreenStringOverride, Stored);
                rig.App.Storage.SetValue(AppFSMKeys.ShowFailureScreen, true);
                Assert.That(rig.Missions.Manager.ActiveMissionContext, Is.Not.Null);

                rig.Container.Action_Exit();

                Assert.That(TryStored(rig, out _), Is.False, "the stored message is cleared");
                Assert.That(rig.Missions.Manager.ActiveMissionContext, Is.Null);
                Assert.That(rig.Events, Is.EqualTo(new[] { rig.ExitEvent }));
                Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.ShowFailureScreen, out bool show), Is.True);
                Assert.That(show, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_isExiting"), Is.True);
            });
        }

        // Both Exit (0x63BE3A) and Restart (0x63BFD1) return at once once either has run.
        [Test]
        public void ExitAndRestartRunOnlyOnceBetweenThem()
        {
            Run(rig =>
            {
                rig.Container.Action_Exit();
                rig.Events.Clear();
                rig.App.Storage.SetValue(AppFSMKeys.FailureScreenStringOverride, Stored);
                rig.App.Storage.SetValue(AppFSMKeys.ShowFailureScreen, true);

                rig.Container.Action_Exit();
                rig.Container.Action_Restart();

                Assert.That(rig.Events, Is.Empty, "no second exit event");
                Assert.That(TryStored(rig, out Strings stored), Is.True, "no second clean-up");
                Assert.That(stored, Is.EqualTo(Stored));
                Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.ShowFailureScreen, out bool show) && show, Is.True, "the flag is not written again");
            });
        }

        // Restart 0x63BFA0 cleans up the stored message and asks the level manager to restart. With a current level, no restart
        // in progress and no meta game (LevelManager.Restart 0x5929F0) that queues the level manager's restart event and marks
        // the restart in progress and the game not post-game; the exit event is not queued and the failure screen flag is not written.
        [Test]
        public void RestartClearsTheMessageAndLeavesTheExitEventAndFlagAlone()
        {
            Run(rig =>
            {
                rig.App.Storage.SetValue(AppFSMKeys.FailureScreenStringOverride, Stored);
                rig.App.Storage.SetValue(AppFSMKeys.ShowFailureScreen, true);
                rig.App.Storage.SetValue(AppFSMKeys.IsInPostGame, true); // as ApplicationStatePostGameplaySetup does, so the restart's false write is visible
                // LevelManager.Initialise takes the App through its SystemRef, but also parents a DontDestroyOnLoad object that Edit
                // Mode cannot own, so the App it would take is assigned directly.
                Set(rig.Missions.Level, "m_app", rig.App); // Reflection: private field Initialise assigns
                var restartEvent = rig.Owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                Set(rig.Missions.Level, "m_restartUIEvent", restartEvent); // Reflection: serialized reference

                rig.Container.Action_Restart();

                Assert.That(TryStored(rig, out _), Is.False);
                Assert.That(rig.Events, Is.EqualTo(new[] { restartEvent }), "the restart event, not the exit event");
                Assert.That(rig.App.Storage.GetValueOnly<bool>(AppFSMKeys.RestartInProgress), Is.True);
                Assert.That(rig.App.Storage.GetValueOnly<bool>(AppFSMKeys.IsInPostGame), Is.False);
                Assert.That(rig.App.Storage.TryGetValue(AppFSMKeys.ShowFailureScreen, out bool show) && show, Is.True);
                Assert.That(Get<bool>(rig.Container, "m_isExiting"), Is.True);

                rig.Container.Action_Exit();
                Assert.That(rig.Events, Is.EqualTo(new[] { restartEvent }), "the restart already used the container");
            });
        }
    }
}
