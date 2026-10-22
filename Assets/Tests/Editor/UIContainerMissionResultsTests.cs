using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionResultsParameters (Game.Runtime 0x020008e6, ctor 0x637230) and UIContainerMissionResults (0x020008e5;
    // Setup 0x63FDD0, CanShow 0x63FF70, OnSaveManagerValid 0x63FF90, GetMissions 0x640100, IsValidMission 0x640670,
    // InstantiateInstancedTrackerPrefab 0x640370, OnDestroy 0x640750) over a real MissionManager and SaveManager. The results
    // prefab of each mission is a container the identifier already holds (RegisterContainer), so no asset is loaded. Not
    // covered: clearing existing children of the root (Object.Destroy refuses in Edit Mode).
    public sealed class UIContainerMissionResultsTests
    {
        private static readonly string ValidJson = "{\"m_showProgressInUI\":true,\"m_isInstancedTracker\":true,\"m_targetObjectivesCount\":3," +
            "\"m_totalObjectivesCount\":3,\"m_rewardType\":" + (int)CollectableType.Ring + "}";

        private static void Run(Action<Owned, OriginalPersistentLevelObjectTests.Missions> body) =>
            OriginalPersistentLevelObjectTests.WithMissions(true, (owned, missions) =>
            {
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                owned.LeaseRow<UIManager>(); // MissionState's field initialiser
                owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser
                body(owned, missions);
            });

        private static MissionDefinition NewMission(Owned owned, string guid, string json = null)
        {
            var mission = Create<MissionDefinition>(owned, json ?? ValidJson);
            Set(mission, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored (empty) list
            return mission;
        }

        private static UIContainerIdentifier NewIdentifier(Owned owned, out MissionResultsRecordingContainer prefab)
        {
            var identifier = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
            prefab = owned.Host("Lucid results prefab", false).AddComponent<MissionResultsRecordingContainer>();
            identifier.RegisterContainer(prefab);
            return identifier;
        }

        private static UIContainerMissionResults NewContainer(Owned owned, out Transform root)
        {
            var container = owned.Host("Lucid mission results", false).AddComponent<UIContainerMissionResults>();
            GameObject rootHost = owned.Host("Lucid results root", false);
            root = rootHost.AddComponent<RectTransform>();
            Set(container, "m_root", root); // Reflection: serialized reference
            return container;
        }

        private static SaveDataLevelMission Record(OriginalPersistentLevelObjectTests.Missions missions, MissionDefinition mission) =>
            missions.Save.CurrentSave.GetOrCreateLevelData(missions.CurrentLevel.LevelDefinition.GetGUID()).GetOrCreateMissionData(mission.GetGUID());

        private static UIContainerInstancedMissionProgressParameters Parameters(Transform root, int child)
        {
            var clone = root.GetChild(child).GetComponent<MissionResultsRecordingContainer>();
            Assert.That(clone.SetupParameters.Count, Is.EqualTo(1));
            return (UIContainerInstancedMissionProgressParameters)clone.SetupParameters[0];
        }

        [Test]
        public void TheParametersKeepTheMissionAndTheLevel()
        {
            Run((owned, missions) =>
            {
                MissionDefinition mission = NewMission(owned, "lucid-results-mission");
                var parameters = new UIContainerMissionResultsParameters(mission, missions.CurrentLevel.LevelDefinition);
                Assert.That(parameters.MissionDefinition, Is.SameAs(mission));
                Assert.That(parameters.LevelDefinition, Is.SameAs(missions.CurrentLevel.LevelDefinition));
                Assert.That(default(UIContainerMissionResultsParameters).MissionDefinition, Is.Null);
            });
        }

        // IsValidMission 0x640670: a mission is listed when it shows its progress, is an instanced tracker (and a persistent one
        // when the container is persistent), has a target (TargetObjectivesToComplete, which falls back to the total), has the
        // results identifier the container uses (the short one when m_useShortResultsContainer) and its reward is not ignored.
        [Test]
        public void AMissionIsValidOnlyWhenEveryConditionHolds()
        {
            foreach ((string name, string json, bool persistentCompletion, bool useShort, bool resultsId, bool shortId, bool ignoreReward, bool expected) in new[]
            {
                ("the basic case", ValidJson, false, false, true, false, false, true),
                ("progress not shown", ValidJson.Replace("\"m_showProgressInUI\":true", "\"m_showProgressInUI\":false"), false, false, true, false, false, false),
                ("not instanced", ValidJson.Replace("\"m_isInstancedTracker\":true", "\"m_isInstancedTracker\":false"), false, false, true, false, false, false),
                ("persistent container, plain tracker", ValidJson, true, false, true, false, false, false),
                ("persistent container, persistent tracker", ValidJson.Replace("{", "{\"m_isPersistentTracker\":true,"), true, false, true, false, false, true),
                ("plain container, persistent tracker", ValidJson.Replace("{", "{\"m_isPersistentTracker\":true,"), false, false, true, false, false, true),
                ("no target", ValidJson.Replace("\"m_targetObjectivesCount\":3", "\"m_targetObjectivesCount\":0").Replace("\"m_totalObjectivesCount\":3", "\"m_totalObjectivesCount\":0"), false, false, true, false, false, false),
                ("the target falls back to the total", ValidJson.Replace("\"m_targetObjectivesCount\":3", "\"m_targetObjectivesCount\":0"), false, false, true, false, false, true),
                ("only the short identifier, full container", ValidJson, false, false, false, true, false, false),
                ("only the short identifier, short container", ValidJson, false, true, false, true, false, true),
                ("only the results identifier, short container", ValidJson, false, true, true, false, false, false),
                ("ignored reward", ValidJson, false, false, true, false, true, false),
            })
            {
                Run((owned, missions) =>
                {
                    MissionDefinition mission = NewMission(owned, "lucid-results-mission", json);
                    if (resultsId) Set(mission, "m_resultsScreenIdentifier", NewIdentifier(owned, out _)); // Reflection: serialized identifiers
                    if (shortId) Set(mission, "m_shortResultsScreenIdentifier", NewIdentifier(owned, out _));
                    UIContainerMissionResults container = NewContainer(owned, out _);
                    Set(container, "m_persistentCompletion", persistentCompletion); // Reflection: serialized options
                    Set(container, "m_useShortResultsContainer", useShort);
                    if (ignoreReward) Get<List<CollectableType>>(container, "m_rewardsToIgnore").Add(CollectableType.Ring);
                    else Get<List<CollectableType>>(container, "m_rewardsToIgnore").Add(CollectableType.Moon);

                    Assert.That(Call(container, "IsValidMission", mission), Is.EqualTo(expected), name); // Reflection: private UIContainerMissionResults.IsValidMission
                });
            }
        }

        // GetMissions 0x640100: the valid sub-missions in their order, then the mission itself when it is valid.
        [Test]
        public void TheValidSubMissionsComeBeforeTheMissionItself()
        {
            Run((owned, missions) =>
            {
                MissionDefinition first = NewMission(owned, "lucid-results-first");
                MissionDefinition invalid = NewMission(owned, "lucid-results-invalid", ValidJson.Replace("\"m_isInstancedTracker\":true", "\"m_isInstancedTracker\":false"));
                MissionDefinition second = NewMission(owned, "lucid-results-second");
                MissionDefinition main = NewMission(owned, "lucid-results-main");
                Set(main, "m_subMissions", new List<MissionDefinition> { first, invalid, second }); // Reflection: authored list
                foreach (MissionDefinition mission in new[] { first, second, main })
                    Set(mission, "m_resultsScreenIdentifier", NewIdentifier(owned, out _)); // Reflection: serialized identifiers
                UIContainerMissionResults container = NewContainer(owned, out _);

                Assert.That((List<MissionDefinition>)Call(container, "GetMissions", main), Is.EqualTo(new[] { first, second, main })); // Reflection: private UIContainerMissionResults.GetMissions

                Set(main, "m_resultsScreenIdentifier", null); // Reflection: serialized m_resultsScreenIdentifier the editor assigns
                Assert.That((List<MissionDefinition>)Call(container, "GetMissions", main), Is.EqualTo(new[] { first, second })); // Reflection: private UIContainerMissionResults.GetMissions
            });
        }

        // Setup 0x63FDD0 keeps the parameters; with a valid save manager OnSaveManagerValid 0x63FF90 builds one container per
        // valid mission: each from the mission's results identifier (kept so OnDestroy closes it), parented to the root and
        // set up with the mission's progress now (record +0x30, completed list +0x38) and before the level (the cached copy).
        // The pre-level data of the sub-mission is progress 1 with objective 0 done; the mission's own is untouched.
        [Test]
        public void SetupBuildsAProgressContainerForEachValidMissionAndOnDestroyClosesTheirIdentifiers()
        {
            Run((owned, missions) =>
            {
                MissionDefinition sub = NewMission(owned, "lucid-results-sub");
                MissionDefinition main = NewMission(owned, "lucid-results-main");
                Set(main, "m_subMissions", new List<MissionDefinition> { sub }); // Reflection: authored list
                UIContainerIdentifier subIdentifier = NewIdentifier(owned, out _);
                UIContainerIdentifier mainIdentifier = NewIdentifier(owned, out _);
                Set(sub, "m_resultsScreenIdentifier", subIdentifier); // Reflection: serialized identifiers
                Set(main, "m_resultsScreenIdentifier", mainIdentifier);
                GameplayLevelDefinition level = missions.CurrentLevel.LevelDefinition;
                missions.Manager.SetActiveMissionContext(new MissionContextRegular(main, level, false));
                Record(missions, sub).Progress = 1;
                Record(missions, sub).MarkObjectiveComplete(0);
                missions.Manager.CachePreLevelMissionSaveData(level.GetGUID());
                Record(missions, sub).Progress = 2;
                Record(missions, sub).MarkObjectiveComplete(1);
                Record(missions, main).Progress = 3;
                UIContainerMissionResults container = NewContainer(owned, out Transform root);
                Set(container, "m_animateMissionProgressDelta", true); // Reflection: serialized option

                container.Setup(new UIContainerMissionResultsParameters(main, level));

                Assert.That(container.CanShow(), Is.True);
                Assert.That(root.childCount, Is.EqualTo(2));
                UIContainerInstancedMissionProgressParameters subParameters = Parameters(root, 0);
                Assert.That(subParameters.MissionDefinition, Is.SameAs(sub));
                Assert.That(subParameters.Progress, Is.EqualTo(2));
                Assert.That(subParameters.PreviousProgress, Is.EqualTo(1));
                Assert.That(subParameters.AnimateProgressDelta, Is.True);
                Assert.That(subParameters.PreviouslyCompletedObjectiveIndices, Is.EqualTo(new[] { 0 }));
                Assert.That(subParameters.CompletedObjectiveIndices, Is.EqualTo(new[] { 0, 1 }));
                UIContainerInstancedMissionProgressParameters mainParameters = Parameters(root, 1);
                Assert.That(mainParameters.MissionDefinition, Is.SameAs(main));
                Assert.That(mainParameters.Progress, Is.EqualTo(3));
                Assert.That(mainParameters.PreviousProgress, Is.EqualTo(0));
                Assert.That(mainParameters.PreviouslyCompletedObjectiveIndices, Is.Empty, "the cached copy has an empty list");
                Assert.That(mainParameters.CompletedObjectiveIndices, Is.Null, "nothing was ever marked on the record");
                Assert.That(Get<int>(subIdentifier, "m_refCount"), Is.EqualTo(2), "registered, then taken for the container");

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(Get<int>(subIdentifier, "m_refCount"), Is.EqualTo(1), "closed once");
                Assert.That(Get<int>(mainIdentifier, "m_refCount"), Is.EqualTo(1));
            });
        }

        // InstantiateInstancedTrackerPrefab 0x640370: the short results container is chosen when asked (0x640453); without cached
        // pre-level data the previous progress is 0 and the previously completed list is absent (0x6405A6..0x64065C), and the
        // progress delta is animated only as the option says.
        [Test]
        public void TheShortContainerIsUsedWhenAskedAndWithoutCachedDataThePreviousProgressIsZero()
        {
            foreach (bool useShort in new[] { false, true })
            {
                Run((owned, missions) =>
                {
                    MissionDefinition main = NewMission(owned, "lucid-results-main");
                    UIContainerIdentifier fullIdentifier = NewIdentifier(owned, out MissionResultsRecordingContainer fullPrefab);
                    UIContainerIdentifier shortIdentifier = NewIdentifier(owned, out MissionResultsRecordingContainer shortPrefab);
                    Set(main, "m_resultsScreenIdentifier", fullIdentifier); // Reflection: serialized identifiers
                    Set(main, "m_shortResultsScreenIdentifier", shortIdentifier);
                    GameplayLevelDefinition level = missions.CurrentLevel.LevelDefinition;
                    Record(missions, main).Progress = 2;
                    UIContainerMissionResults container = NewContainer(owned, out Transform root);
                    Set(container, "m_useShortResultsContainer", useShort); // Reflection: serialized option

                    container.Setup(new UIContainerMissionResultsParameters(main, level));

                    Assert.That(root.childCount, Is.EqualTo(1));
                    UIContainerInstancedMissionProgressParameters parameters = Parameters(root, 0);
                    Assert.That(parameters.Progress, Is.EqualTo(2));
                    Assert.That(parameters.PreviousProgress, Is.EqualTo(0));
                    Assert.That(parameters.PreviouslyCompletedObjectiveIndices, Is.Null);
                    Assert.That(parameters.AnimateProgressDelta, Is.False);
                    Assert.That(Get<int>(shortIdentifier, "m_refCount"), Is.EqualTo(useShort ? 2 : 1), "short identifier used: " + useShort);
                    Assert.That(Get<int>(fullIdentifier, "m_refCount"), Is.EqualTo(useShort ? 1 : 2), "full identifier used: " + !useShort);
                    Assert.That(fullPrefab.SetupParameters, Is.Empty, "the prefab itself is never set up");
                    Assert.That(shortPrefab.SetupParameters, Is.Empty);
                });
            }
        }

        // With no valid mission nothing is built and Show is written false (setg at 0x640080): the first write notifies.
        [Test]
        public void WithoutAValidMissionNothingIsBuiltAndShowIsFalse()
        {
            Run((owned, missions) =>
            {
                MissionDefinition invalid = NewMission(owned, "lucid-results-invalid", ValidJson.Replace("\"m_isInstancedTracker\":true", "\"m_isInstancedTracker\":false"));
                UIContainerMissionResults container = NewContainer(owned, out Transform root);
                int notifications = 0;
                container.Show.AddListener(() => notifications++);

                container.Setup(new UIContainerMissionResultsParameters(invalid, missions.CurrentLevel.LevelDefinition));

                Assert.That(container.CanShow(), Is.False);
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(root.childCount, Is.EqualTo(0));
            });
        }
    }
}
