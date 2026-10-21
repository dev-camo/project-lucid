using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionInstancedTracker (Initialise 0x5B01E0, StatusChanged 0x5B0D30, ResetMission 0x5B1A40,
    // OnMissionPreviouslyCompleted 0x5B1850 and the helpers between them) over real objectives, a real mission state
    // and save record, and the real managers a tracker reaches. Expected counts follow the native comparisons.
    public sealed class MissionInstancedTrackerTests
    {
        private sealed class Rig
        {
            internal World World;
            internal Mission Mission;
            internal MissionInstancedTracker Tracker;
            internal List<MissionObjective> Objectives;
            internal CountingEvent Completed;
            internal CountingEvent Any;
            internal CountingEvent Last;
            internal int StatusCallbacks;
        }

        // The tracker and its objectives are made and authored, then handed to beforeInitialise, then initialised.
        private static Rig Build(World world, int count, int target = -1, Action<Rig> beforeInitialise = null,
            IMissionContext context = null, Mission mission = null, Func<Transform, MissionObjective> newObjective = null)
        {
            var rig = new Rig { World = world, Mission = mission ?? world.NewMission(count, target) };
            rig.Tracker = world.NewTracker<MissionInstancedTracker>();
            newObjective = newObjective ?? (parent => world.NewObjective<MissionObjective>(parent));
            rig.Objectives = Enumerable.Range(0, count).Select(_ => newObjective(rig.Tracker.transform)).ToList();
            Set(rig.Tracker, "m_missionObjectives", new List<MissionObjective>(rig.Objectives)); // Reflection: authored list (the editor Reset finds none on inactive hosts)
            rig.Completed = Author(rig.Tracker, "m_onCompleted");
            rig.Any = Author(rig.Tracker, "m_onAnyObjectiveCompleted");
            rig.Last = Author(rig.Tracker, "m_onLastObjective");
            rig.Tracker.OnStatusChanged = _ => rig.StatusCallbacks++;
            beforeInitialise?.Invoke(rig);
            rig.Tracker.Initialise(rig.Mission.State, context);
            return rig;
        }

        private static void Run(int count, int target, Action<Rig> body, Action<Rig> beforeInitialise = null) =>
            WithWorld(world => body(Build(world, count, target, beforeInitialise)));

        private static Sprite NewSprite(World world)
        {
            var texture = world.Owned.Track(new Texture2D(1, 1));
            return world.Owned.Track(Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero));
        }

        [Test]
        public void InitialiseTakesTheTargetFromTheDefinitionAndPublishesItToTheState()
        {
            Run(3, 2, rig =>
            {
                Assert.That(rig.Tracker.ObjectiveCount, Is.EqualTo(3));
                Assert.That(rig.Tracker.ObjectivesTarget, Is.EqualTo(2), "an authored target wins over the total");
                Assert.That(rig.Mission.State.Target, Is.EqualTo(2));
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(0));
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));
            });
            Run(3, -1, rig => Assert.That(rig.Tracker.ObjectivesTarget, Is.EqualTo(3), "a non-positive target falls back to the total"));
        }

        // 0x5B0344..0x5B0356: after setup, the event fires when the progress is one short of the target.
        [Test]
        public void InitialiseAnnouncesTheLastObjectiveOnlyWhenOneMoreCompletesTheMission()
        {
            Run(1, 1, rig => Assert.That(rig.Last.Count, Is.EqualTo(1)));
            Run(2, 2, rig => Assert.That(rig.Last.Count, Is.EqualTo(0)));
        }

        // StatusChanged 0x5B0DE9..0x5B0DF9 returns unless the mission has started or persists its objectives.
        [Test]
        public void ObjectiveCompletionIsIgnoredUntilTheMissionStarts()
        {
            Run(2, 2, rig =>
            {
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));
                Assert.That(rig.Any.Count, Is.EqualTo(0));
                Assert.That(rig.StatusCallbacks, Is.EqualTo(0));

                rig.Tracker.StartMission(false);
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
            });
        }

        [Test]
        public void CompletingTheTargetNumberOfObjectivesCompletesTheMissionAndDetachesTheObjectives()
        {
            Run(3, 2, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1));
                Assert.That(rig.Any.Count, Is.EqualTo(1));
                Assert.That(rig.Last.Count, Is.EqualTo(1), "one short of the target of two");
                Assert.That(rig.StatusCallbacks, Is.EqualTo(1));
                Assert.That(rig.Completed.Count, Is.EqualTo(0));
                Assert.That(rig.Mission.State.Complete, Is.False);

                rig.Objectives[2].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(2));
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2));
                Assert.That(rig.Any.Count, Is.EqualTo(2));
                Assert.That(rig.Completed.Count, Is.EqualTo(1));
                Assert.That(rig.Mission.State.Complete, Is.True);
                Assert.That(rig.Objectives.Select(objective => objective.OnStatusChanged).ToArray(), Is.All.Null,
                    "UnsubscribeFromObjectives removed the tracker from every objective");

                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(2), "a detached objective no longer reaches the tracker");
                Assert.That(rig.StatusCallbacks, Is.EqualTo(2));
            });
        }

        // 0x5B0E22: HashSet.Add decides whether the bonus-time and event block runs; the remainder runs every time.
        [Test]
        public void ARepeatedCompletionReportIsNotCountedAgainButStillReportsTheStatus()
        {
            Run(3, 3, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
                Assert.That(rig.Any.Count, Is.EqualTo(1));
                Assert.That(rig.StatusCallbacks, Is.EqualTo(2));
            });
        }

        [Test]
        public void ResettingACompletedObjectiveWithdrawsItFromTheCount()
        {
            Run(3, 3, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(2));

                rig.Objectives[0].OnObjectiveReset();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
                Assert.That(rig.Mission.State.Progress, Is.EqualTo(1));
                Assert.That(rig.Any.Count, Is.EqualTo(2), "a withdrawal is not a completion");
            });
        }

        // 0x5B0FD0..0x5B1076: the first incomplete objective's icon is shown; with all complete, the first objective's.
        [Test]
        public void TheIconIsTheFirstIncompleteObjectivesOrTheFirstObjectivesOnceAllAreComplete()
        {
            WithWorld(world =>
            {
                Sprite[] icons = { NewSprite(world), NewSprite(world), NewSprite(world) };
                Rig rig = Build(world, 3, 3, r =>
                {
                    for (int i = 0; i < 3; i++) Set(r.Objectives[i], "m_icon", icons[i]); // Reflection: authored serialized icons
                });
                Assert.That(rig.Tracker.Icon, Is.Null, "nothing has reported a status yet");
                rig.Tracker.StartMission(false);

                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.Icon, Is.SameAs(icons[0]), "objective 0 is the first incomplete one");
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.Icon, Is.SameAs(icons[2]), "objective 2 is now the first incomplete one");
                rig.Objectives[2].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.Icon, Is.SameAs(icons[0]), "all complete: the first objective's icon");
            });
        }

        [Test]
        public void PersistedObjectiveCompletionIsRecordedInTheSaveAndNeedsNoStartedMission()
        {
            Run(3, 3, rig =>
            {
                Assert.That(rig.Tracker.PersistObjectiveCompletion, Is.True);
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1), "persisting trackers react before the mission starts");
                Assert.That(rig.Mission.Save.CompletedObjectiveIndices, Is.EqualTo(new[] { 1 }));

                rig.Objectives[1].OnObjectiveReset();
                Assert.That(rig.Mission.Save.CompletedObjectiveIndices, Is.Empty);
            }, rig => Set(rig.Tracker, "m_persistObjectiveCompletion", true)); // Reflection: authored toggle
        }

        // 0x5B0370..0x5B06BF with the 0x5B0271 predicate: saved indices are restored when saved progress is allowed
        // (a persistent definition without a context) and the mission is not a time trial (0x5B02C9, 0x5B02E6); an index
        // beyond the list is skipped.
        [Test]
        public void SavedObjectiveCompletionIsRestoredOnlyForAPersistentDefinitionThatIsNotATimeTrial()
        {
            foreach ((bool persistentDefinition, bool timeTrial, int completed) in new[] { (true, false, 1), (false, false, 0), (true, true, 0) })
            {
                WithWorld(world =>
                {
                    Mission mission = world.NewMission(3, 3);
                    Set(mission.Definition, "m_isPersistentTracker", persistentDefinition); // Reflection: authored definition flag
                    if (timeTrial)
                    {
                        Set(mission.Definition, "m_isBeingOverridden", true); // Reflection: set by a completed mission's overrides
                        Set(mission.Definition, "m_timeLimitSeconds", 60f); // Reflection: authored time limit
                    }
                    mission.Save.MarkObjectiveComplete(2);
                    mission.Save.MarkObjectiveComplete(9);
                    CountingEvent previouslyCompleted = null;
                    Rig rig = Build(world, 3, 3, r =>
                    {
                        Set(r.Tracker, "m_persistObjectiveCompletion", true); // Reflection: authored toggle
                        previouslyCompleted = Author(r.Objectives[2], "m_onPreviouslyCompleted");
                    }, mission: mission);
                    string row = $"persistent definition {persistentDefinition}, time trial {timeTrial}";
                    Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(completed), row);
                    Assert.That(rig.Objectives[2].IsComplete, Is.EqualTo(completed == 1), row);
                    Assert.That(previouslyCompleted.Count, Is.EqualTo(completed), row);
                });
            }
        }

        // 0x5B02CE..0x5B02E4: saved objectives are also restored by a tracker that does not persist them, when the saved
        // progress equals the target field's value before it is set up (0 on a fresh tracker); any other saved progress skips it.
        [Test]
        public void ATrackerThatDoesNotPersistRestoresSavedObjectivesOnlyWhileTheSavedProgressIsZero()
        {
            foreach ((int savedProgress, int completed) in new[] { (0, 1), (1, 0) })
            {
                WithWorld(world =>
                {
                    Mission mission = world.NewMission(3, 3);
                    Set(mission.Definition, "m_isPersistentTracker", true); // Reflection: authored definition flag
                    mission.Save.MarkObjectiveComplete(2);
                    if (savedProgress > 0) mission.State.UpdateMissionProgress(savedProgress, 3, false);
                    Rig rig = Build(world, 3, 3, mission: mission);
                    Assert.That(rig.Tracker.PersistObjectiveCompletion, Is.False);
                    Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(completed), "saved progress " + savedProgress);
                    Assert.That(rig.Objectives[2].IsComplete, Is.EqualTo(completed == 1), "saved progress " + savedProgress);
                });
            }
        }

        // The members InitialiseObjectivesToComplete reads; nothing else of the context is reached by Initialise.
        private sealed class ActiveObjectivesContext : IMissionContext
        {
            public IReadOnlyList<int> OverrideActiveObjectives { get; set; }
            public int ObjectiveTarget { get; set; }
            public bool IgnoreSavedProgress => true;
            public float OverrideTimeLimitSeconds => 0f;

            public MissionDefinition MissionDefinition => throw new NotSupportedException();
            public GameplayLevelDefinition LevelDefinition => throw new NotSupportedException();
            public IReadOnlyList<CharacterId> OverrideAllowedCharacters => throw new NotSupportedException();
            public float BestTimeSeconds => throw new NotSupportedException();
            public LevelStartPositionDefinition OverrideStartPosition => throw new NotSupportedException();
            public int RingXPEarned => throw new NotSupportedException();
            public bool IsReplay => throw new NotSupportedException();
            public float RealTimeTaken { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool SaveLastLevelVisited => throw new NotSupportedException();
            public string Description => throw new NotSupportedException();
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

        // 0x5B085E..0x5B0925: a context listing the active objectives zeroes the target, walks the objectives back to
        // front, counts each listed index (an index beyond the list counts nothing) and destroys and removes the others.
        [Test]
        public void AContextListingTheActiveObjectivesCountsThemAndRemovesTheOthers()
        {
            WithWorld(world =>
            {
                var context = new ActiveObjectivesContext { OverrideActiveObjectives = new[] { 0, 2, 7 }, ObjectiveTarget = 9 };
                // The native Destroy call is not DestroyImmediate, which Edit Mode refuses once per removed objective.
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
                Rig rig = Build(world, 4, 4, context: context);

                Assert.That(rig.Tracker.ObjectivesTarget, Is.EqualTo(2), "two listed indices exist; neither the list size nor the context target");
                Assert.That(rig.Tracker.ObjectiveCount, Is.EqualTo(2));
                Assert.That(rig.Tracker.MissionObjectives.ToArray(), Is.EqualTo(new[] { rig.Objectives[0], rig.Objectives[2] }));
                Assert.That(rig.Mission.State.Target, Is.EqualTo(2));
            });
        }

        // 0x5B0259..0x5B02B3: a context asks for its saved progress to be ignored; it also supplies the objective target.
        [Test]
        public void AReplayContextIgnoresSavedProgressAndSuppliesTheTarget()
        {
            foreach ((bool replay, int completed) in new[] { (true, 0), (false, 1) })
            {
                WithWorld(world =>
                {
                    world.Owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                    Mission mission = world.NewMission(3, 3);
                    mission.Save.MarkObjectiveComplete(2);
                    Mission contextMission = world.NewMission(3, 2);
                    var context = new MissionContextRegular(contextMission.Definition, null, replay);
                    Rig rig = Build(world, 3, 3, r => Set(r.Tracker, "m_persistObjectiveCompletion", true), // Reflection: authored toggle
                        context, mission);
                    Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(completed), "replay " + replay);
                    Assert.That(rig.Tracker.ObjectivesTarget, Is.EqualTo(2), "the context's target, not the state's definition");
                });
            }
        }

        // 0x5B1A40: a started mission is reset to not started; unless its progress already equals the target, every
        // objective resets and the completed set empties.
        [Test]
        public void ResettingTheMissionResetsItsObjectivesAndRequiresARestart()
        {
            Run(3, 3, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                rig.Tracker.ResetMission();
                Assert.That(rig.Objectives.Select(objective => objective.IsComplete).ToArray(), Is.All.False);
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));

                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0), "the reset cleared the started flag");
                rig.Tracker.StartMission(false);
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(1));
            });
        }

        [Test]
        public void ResettingACompleteMissionLeavesItsObjectivesComplete()
        {
            Run(2, 2, rig =>
            {
                rig.Tracker.StartMission(false);
                rig.Objectives[0].Action_ObjectiveComplete();
                // The objective's completion rewrote the progress to the one completed objective; the saved progress is
                // then set to the target, the state ResetMission (0x5B1A40) compares with it.
                rig.Mission.State.UpdateMissionProgress(2, 2, false);
                rig.Tracker.ResetMission();
                Assert.That(rig.Objectives[0].IsComplete, Is.True);
            });
        }

        // 0x5B1850: only a complete mission replays its completion on the tracker and every objective.
        [Test]
        public void APreviouslyCompletedMissionMarksEveryObjectiveComplete()
        {
            WithWorld(world =>
            {
                var objectiveEvents = new List<CountingEvent>();
                Rig rig = Build(world, 2, 2, r => objectiveEvents.AddRange(r.Objectives.Select(o => Author(o, "m_onPreviouslyCompleted"))));
                CountingEvent trackerEvent = Author(rig.Tracker, "m_onPreviouslyCompleted");

                rig.Tracker.OnMissionPreviouslyCompleted();
                Assert.That(trackerEvent.Count, Is.EqualTo(0), "progress is below the target");
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(0));

                rig.Mission.State.UpdateMissionProgress(2, 2, false);
                rig.Tracker.OnMissionPreviouslyCompleted();
                Assert.That(trackerEvent.Count, Is.EqualTo(1));
                Assert.That(rig.Tracker.ObjectivesComplete, Is.EqualTo(2));
                Assert.That(rig.Objectives.Select(o => o.IsComplete).ToArray(), Is.All.True);
                Assert.That(objectiveEvents.Select(e => e.Count).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            });
        }

        // 0x5B0E6C..0x5B0EBA: a first completion adds the objective's seconds to the tracker's timer; none is added for
        // an objective worth no time or for a repeated report.
        [Test]
        public void ACompletedObjectiveAddsItsBonusSecondsToTheMissionTimerOnce()
        {
            WithWorld(world =>
            {
                MissionTimer timer = null;
                Rig rig = Build(world, 3, 3, r =>
                {
                    timer = EnemyAbilityTestSupport.AddTimeScaled<MissionTimer>(world.Owned.Host("Lucid mission timer", false));
                    Set(r.Tracker, "m_optionalMissionTimer", timer); // Reflection: authored timer reference
                    Set(r.Objectives[0], "m_timeToAddSeconds", 2.5f); // Reflection: authored bonus time
                    Set(r.Objectives[1], "m_timeToAddSeconds", 4f);
                });
                rig.Tracker.StartMission(false);

                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(timer.GetTimeRemainingSeconds(), Is.EqualTo(2.5f));
                rig.Objectives[0].Action_ObjectiveComplete();
                Assert.That(timer.GetTimeRemainingSeconds(), Is.EqualTo(2.5f), "a repeated report adds nothing");
                rig.Objectives[2].Action_ObjectiveComplete();
                Assert.That(timer.GetTimeRemainingSeconds(), Is.EqualTo(2.5f), "an objective worth no time adds nothing");
                rig.Objectives[1].Action_ObjectiveComplete();
                Assert.That(timer.GetTimeRemainingSeconds(), Is.EqualTo(6.5f));
            });
        }

        // 0x5B0F18..0x5B0F3A and AddMissionScorerPoints 0x5B1310: the objective's score is added through the combo
        // multiplier only when the objective asks for it.
        [Test]
        public void AScoredObjectiveAddsItsPointsWithOrWithoutTheComboMultiplier()
        {
            foreach (bool usesCombo in new[] { true, false })
            {
                WithWorld(world =>
                {
                    MissionScorer scorer = EnemyAbilityTestSupport.AddTimeScaled<MissionScorer>(world.Owned.Host("Lucid mission scorer", false));
                    CountingEvent combo = Author(scorer, "m_onAddPoints"); // Reflection: authored events and stages
                    CountingEvent flat = Author(scorer, "m_onAddPointsFlat");
                    Author(scorer, "m_onLosePoints");
                    Set(scorer, "m_comboStages", new List<MissionScorerComboStage> { new MissionScorerComboStage() });
                    scorer.ResetComboStage();
                    Rig rig = Build(world, 1, 1, r => Set(r.Tracker, "m_optionalMissionScorer", scorer), // Reflection: authored scorer reference
                        newObjective: parent =>
                        {
                            MissionScorerObjective objective = world.NewObjective<MissionScorerObjective>(parent);
                            Set(objective, "m_usesComboMultiplier", usesCombo); // Reflection: authored toggle
                            objective.SetScoreToAdd(30f);
                            return objective;
                        });
                    rig.Tracker.StartMission(false);

                    rig.Objectives[0].Action_ObjectiveComplete();

                    Assert.That(scorer.PointsTotal, Is.EqualTo(30));
                    Assert.That(scorer.PointsAddedFromCombo, Is.EqualTo(usesCombo));
                    Assert.That(combo.Count, Is.EqualTo(usesCombo ? 1 : 0));
                    Assert.That(flat.Count, Is.EqualTo(usesCombo ? 0 : 1));
                });
            }
        }

        // StatusChanged 0x5B1190..0x5B1255: after the objective's own points, a streak completed by this objective adds
        // its score as a second batch, flagged as a streak; with no matching streak only the objective's points count.
        [Test]
        public void AStreakCompletedByTheObjectiveAddsItsScoreAsASecondBatch()
        {
            foreach ((bool streakContainsObjective, int total, int batches) in new[] { (true, 70, 2), (false, 30, 1) })
            {
                WithWorld(world =>
                {
                    MissionScorer scorer = EnemyAbilityTestSupport.AddTimeScaled<MissionScorer>(world.Owned.Host("Lucid mission scorer", false));
                    CountingEvent flat = Author(scorer, "m_onAddPointsFlat"); // Reflection: authored events and stages
                    Author(scorer, "m_onAddPoints");
                    Author(scorer, "m_onLosePoints");
                    Set(scorer, "m_comboStages", new List<MissionScorerComboStage> { new MissionScorerComboStage() });
                    scorer.ResetComboStage();
                    var manual = new List<MissionScorerStreak>();
                    Set(scorer, "m_streaksManual", manual); // Reflection: authored streak lists
                    Set(scorer, "m_streaksAuto", new List<MissionScorerStreak>());
                    Rig rig = Build(world, 1, 1, r =>
                    {
                        Set(r.Tracker, "m_optionalMissionScorer", scorer); // Reflection: authored scorer reference
                        var definition = world.Owned.Track(ScriptableObject.CreateInstance<MissionScorerStreakDefinition>());
                        Set(definition, "m_usesComboMultiplier", false); // Reflection: authored streak definition
                        Set(definition, "m_timeout", 5f);
                        Type thresholdType = typeof(MissionScorerStreakDefinition).GetNestedType("ScoreThreshold", System.Reflection.BindingFlags.NonPublic);
                        object threshold = Activator.CreateInstance(thresholdType);
                        Set(threshold, "m_count", 1f);
                        Set(threshold, "m_score", 40f);
                        var thresholds = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(thresholdType));
                        thresholds.Add(threshold);
                        Set(definition, "m_scoreThresholds", thresholds);
                        var streak = new MissionScorerStreak();
                        Set(streak, "m_definition", definition);
                        var members = new List<MissionScorerStreakObjective>();
                        if (streakContainsObjective) members.Add((MissionScorerStreakObjective)r.Objectives[0]);
                        Set(streak, "m_objectives", members);
                        manual.Add(streak);
                    }, newObjective: parent =>
                    {
                        MissionScorerStreakObjective objective = world.NewObjective<MissionScorerStreakObjective>(parent);
                        Set(objective, "m_usesComboMultiplier", false); // Reflection: authored toggle
                        objective.SetScoreToAdd(30f);
                        return objective;
                    });
                    rig.Tracker.StartMission(false);

                    rig.Objectives[0].Action_ObjectiveComplete();

                    Assert.That(scorer.PointsTotal, Is.EqualTo(total), "streak contains the objective: " + streakContainsObjective);
                    Assert.That(flat.Count, Is.EqualTo(batches));
                });
            }
        }

        [Test]
        public void DeclarationKeepsTheSerializedFieldsAndTheGateObjectivePrefix()
        {
            WithWorld(world =>
            {
                var tracker = world.NewTracker<MissionInstancedTracker>();
                Assert.That(Get<string>(tracker, "m_objectiveNamePrefix"), Is.EqualTo("gate"));
                Assert.That(Get<bool>(tracker, "m_persistObjectiveCompletion"), Is.False);
                Assert.That(typeof(MissionInstancedTracker).GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray(),
                    Is.EqualTo(new[]
                    {
                        "m_onAnyObjectiveCompleted", "m_onLastObjective", "m_missionObjectives", "m_persistObjectiveCompletion",
                        "m_uiContainerIdentifierGroup", "m_uiContainerIdentifierOverride", "m_uiContainerForResultsScreen",
                        "m_objectiveNamePrefix", "<Icon>k__BackingField", "OnStatusChanged", "m_completedObjectives",
                        "m_targetObjectivesToComplete",
                    }));
                AssertIl2CppOptions(typeof(MissionInstancedTracker), 2, 1); // metadata order: ArrayBoundsChecks, then NullChecks
            });
        }
    }
}
