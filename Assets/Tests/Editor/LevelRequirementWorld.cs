using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Real services and authored definitions behind the level, mission-group and category requirements:
    // OwnedCharacterManagers supplies the SaveManager (holding an open save), DataManager and App, on top of
    // which a LevelManager component with an authored GameLevels and a real MissionManager are registered
    // (the MissionManager over the open save, as in AppRatingRequesterBossTests). Definitions are built by their
    // real constructors; their authored lists are written the way the engine serializer would deliver them.
    internal sealed class LevelRequirementWorld
    {
        private readonly Owned m_owned;

        public SaveManager Save { get; private set; }
        public SaveDataGame SaveGame { get; private set; }
        public DataManager Data { get; private set; }
        public MissionManager Missions { get; private set; }
        public GameLevels GameLevels { get; private set; }

        private LevelRequirementWorld(Owned owned) => m_owned = owned;

        internal static void Run(Action<Owned, LevelRequirementWorld> body) =>
            Owned.Run(owned => body(owned, Build(owned)));

        private static LevelRequirementWorld Build(Owned owned)
        {
            // Taken first and released last, as the fixture documents.
            owned.Lease(new OwnedCharacterManagers());
            // Rows read by the MissionManager, MissionDefinition and MissionState field initialisers.
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<TimeTrialLeaderboardManager>();
            owned.LeaseRow<UIManager>();

            var world = new LevelRequirementWorld(owned)
            {
                Save = ProcessManager.GetSystem<SaveManager>(),
                Data = ProcessManager.GetSystem<DataManager>(),
            };
            world.SaveGame = world.Save.CurrentSave;
            world.SaveGame.DisableSaving();

            LevelManager levelManager = owned.Register(owned.Host("Lucid level manager", false).AddComponent<LevelManager>());
            world.GameLevels = Create<GameLevels>(owned);
            // Reflection: the property is filled from the configuration asset by the manager's initialisation.
            typeof(LevelManager).GetProperty(nameof(LevelManager.GameLevels)).GetSetMethod(true).Invoke(levelManager, new object[] { world.GameLevels });

            var missions = new MissionManager();
            // The constructor subscribes now, before the row is admitted; released after the unregistration.
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(missions)));
            world.Missions = owned.Register(missions);
            return world;
        }

        internal MissionDefinition NewMission(string guid, bool persistentTracker = false) =>
            Create<MissionDefinition>(m_owned, "{\"m_guid\":\"" + guid + "\",\"m_subMissions\":[],\"m_isPersistentTracker\":" +
                (persistentTracker ? "true" : "false") + "}");

        // Reflection: authored contents of a serialized group, which has no constructor arguments.
        internal MissionGroup NewGroup(string guid, params MissionDefinition[] missions)
        {
            var group = new MissionGroup();
            Set(group, "m_guid", guid);
            Set(group, "m_definitions", new List<MissionDefinition>(missions));
            return group;
        }

        // A level whose mission list holds the given groups; a null list leaves the level without one.
        // Reflection: the list reference and group list are authored data without setters.
        internal GameplayLevelDefinition NewLevel(string guid, bool bossLevel, params MissionGroup[] groups)
        {
            GameplayLevelDefinition level = Create<GameplayLevelDefinition>(m_owned,
                "{\"m_guid\":\"" + guid + "\",\"m_isBossLevel\":" + (bossLevel ? "true" : "false") + ",\"m_requirements\":[]}");
            if (groups != null)
            {
                MissionList list = Create<MissionList>(m_owned);
                Set(list, "m_groups", new List<MissionGroup>(groups));
                Set(level, "m_missionList", list);
            }
            return level;
        }

        // Adds a zone holding the levels to the authored GameLevels, in order.
        // Reflection: the zone's entry list and the configuration's entries are authored data.
        internal GameplayLevelCategory AddZone(string guid, params GameplayLevelDefinition[] levels)
        {
            GameplayLevelCategory zone = Create<GameplayLevelCategory>(m_owned, "{\"m_guid\":\"" + guid + "\"}");
            Set(zone, "m_gameplayEntries", new List<GameplayLevelUIEntry>(levels));
            Get<List<GameplayLevelUIEntry>>(GameLevels, "m_gameplayEntries").Add(zone);
            return zone;
        }

        // The mission's save record for that level, created on demand, marked as asked.
        internal void SetComplete(GameplayLevelDefinition level, MissionDefinition mission, bool complete) =>
            SaveGame.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(mission.GetGUID()).Complete = complete;

        // Puts the mission among the manager's mission states through its own creation routine.
        // Reflection: the routine is private; LoadMission, its only public route, loads tracker prefabs.
        internal void AddMissionState(GameplayLevelDefinition level, MissionDefinition mission) =>
            Call(Missions, "CreateMissionState", mission, SaveGame.GetOrCreateLevelData(level.GetGUID()));

        // Authors the level's reward requirements the way the engine serializer delivers them (an enum is its integer value).
        internal void AuthorRewardRequirements(GameplayLevelDefinition level, params (CollectableType type, int count)[] requirements)
        {
            string json = "{\"m_rewardRequirements\":[";
            for (int index = 0; index < requirements.Length; ++index)
                json += (index > 0 ? "," : "") + "{\"RewardType\":" + (int)requirements[index].type + ",\"Count\":" + requirements[index].count + "}";
            JsonUtility.FromJsonOverwrite(json + "]}", level);
        }

        // A real ProgressionManager over the world's SaveManager. The save is closed first because the manager's
        // Initialise registers its save listener before it creates its progress handler, so an open save would call
        // OnSaveGameOpen on a missing handler (the original order). Initialise is the manager's own subscribed
        // callback, called through reflection since only the process manager invokes it. The closed save's cache slot
        // is created empty through the handler, and the returned dictionary is that slot's total, which the
        // collectable scan would otherwise fill from authored mission collectables.
        internal Dictionary<CollectableType, ProgressionCollectable> AddProgression()
        {
            Save.CloseSave();
            OwnedChallengeServices.Register(m_owned);
            m_owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            var progression = new ProgressionManager();
            // The constructor subscribes now, before the row is admitted; released after the unregistration.
            m_owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                ProcessManager.UnsubscribeFromAllActions(progression);
                Save.RemoveListener(progression);
            }));
            m_owned.Register(progression);
            Call(progression, "Initialise", (object)null);
            var handler = Get<ProgressionCollectableHandler>(progression, "m_collectables");
            handler.CacheEmptyProgress(Save.CurrentSaveIndex);
            // Reflection: the total of a cached save is private and only filled from authored collectables.
            return Get<Dictionary<CollectableType, ProgressionCollectable>>(handler.ProgressPerSave[Save.CurrentSaveIndex], "m_total");
        }
    }
}
