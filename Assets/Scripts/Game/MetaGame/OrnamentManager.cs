using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime020006df: complete33 direct APIs, readonly status2,
    // and the genuine captured mission predicate2. Natural CIL binding is held.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrnamentManager : ISystem, ISaveGameListener
    {
        private DataManager m_dataManager;
        private SaveManager m_saveManager;
        private AchievementsManager m_achievementsManager;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private const string DebugMenuPath = "Ornaments";
        private readonly Dictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentStatus>> m_ornamentCollections =
            new Dictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentStatus>>(HardlightEnumComparers.OrnamentCategoryComparer);
        private int m_totalCollected;
        private int m_totalOrnaments;
        private bool m_debugAllUnlocked;
        public Action OnInitialised;

        //0600250e..2512: the automatic backing field follows all explicit fields.
        public int TotalCollected => m_totalCollected;
        public int TotalOrnaments => m_totalOrnaments;
        public IReadOnlyDictionary<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentStatus>> OrnamentCollections => m_ornamentCollections;
        public bool Initialised { get; private set; }

        //06002513/14: this flag stores and rebuilds; the original rebuild does
        // not consult it to override the saved unlock state.
        public bool DebugAllUnlocked
        {
            get => m_debugAllUnlocked;
            set { m_debugAllUnlocked = value; BuildMasterList(); }
        }

        //06002515: original field initializers precede Object base construction;
        // Initialise(1) is subscribed before Shutdown(2).
        public OrnamentManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, PreInitialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        //06002516: startup subscription has different immediate-validity
        // behavior from the two InvokeOnValid calls; retain that distinction.
        private void PreInitialise(object _)
        {
            ProcessManager.GetSystemRef<DataManager>().InvokeOnValid(DataManagerValid);
            m_missionManagerRef.OnSystemStartup += OnMissionManagerValid;
            ProcessManager.GetSystemRef<AchievementsManager>().InvokeOnValid(AchievementsManagerValid);
        }

        //06002517: validity and Get are separate reads; totals and save listener
        // registrations survive this original shutdown path.
        private void Shutdown(object _)
        {
            if (m_missionManagerRef.IsValid())
                m_missionManagerRef.Get().OnMissionCompleted -= OnMissionCompleted;
            m_missionManagerRef.OnSystemStartup -= OnMissionManagerValid;
            OnInitialised -= CheckAllAchievementUnlocks;
            Initialised = false;
        }

        //06002518
        private void DataManagerValid(DataManager dataManager)
        {
            m_dataManager = dataManager;
            ProcessManager.GetSystemRef<SaveManager>().InvokeOnValid(SaveManagerValid);
        }

        //06002519: original dependant-listener registration fires if open.
        private void SaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            saveManager.AddDependantListener(this, true);
        }

        //0600251a: remove one matching delegate before adding it again.
        private void OnMissionManagerValid(MissionManager missionManager)
        {
            missionManager.OnMissionCompleted -= OnMissionCompleted;
            missionManager.OnMissionCompleted += OnMissionCompleted;
        }

        //0600251b: the pending callback is stored on the plain OnInitialised
        // field; no platform-sync event is subscribed here.
        private void AchievementsManagerValid(AchievementsManager achievementsManager)
        {
            m_achievementsManager = achievementsManager;
            if (Initialised) CheckAllAchievementUnlocks();
            else OnInitialised += CheckAllAchievementUnlocks;
        }

        //0600251c: argument unused; rebuilding precedes level checks.
        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            BuildMasterList();
            CheckLevelUnlocks();
        }

        //0600251d: keep the three category insertion order, live dictionary
        // traversal and repeated GetGUID reads. TotalCollected counts unlocked
        // entries even when a reward record still says Collected=false.
        private void BuildMasterList()
        {
            m_ornamentCollections.Clear();
            m_ornamentCollections.Add(OrnamentCategory.Story,
                new Dictionary<OrnamentIdentifier, OrnamentStatus>(HardlightEnumComparers.OrnamentIdentifierComparer));
            m_ornamentCollections.Add(OrnamentCategory.Challenge,
                new Dictionary<OrnamentIdentifier, OrnamentStatus>(HardlightEnumComparers.OrnamentIdentifierComparer));
            m_ornamentCollections.Add(OrnamentCategory.Achievement,
                new Dictionary<OrnamentIdentifier, OrnamentStatus>(HardlightEnumComparers.OrnamentIdentifierComparer));
            m_totalCollected = 0;
            SaveDataGame saveDataGame = m_saveManager.CurrentSave;
            foreach (var (identifier, definition) in m_dataManager.OrnamentDefinitions)
            {
                bool unlocked = saveDataGame.OrnamentsByGuid.TryGetValue(definition.GetGUID(), out SaveDataOrnament saveOrnament) && saveOrnament.Unlocked;
                bool seen = saveOrnament != null && saveOrnament.UnlockSeen;
                bool collected = !saveDataGame.TryGetRewardDataByRewardGUID(definition.GetGUID(), out SaveDataChallengeReward reward) || reward.Collected;
                m_ornamentCollections[definition.OrnamentCategory].Add(identifier, new OrnamentStatus(unlocked, seen, collected));
                if (unlocked) ++m_totalCollected;
            }
            m_totalOrnaments = m_dataManager.OrnamentDefinitions.Count;
            if (!Initialised)
            {
                Initialised = true;
                OnInitialised?.Invoke();
            }
        }

        //0600251e: actual inherited ScriptableObjectWithGuid equality governs
        // this null check; analytics precedes the progression queue mutation.
        private void AwardOrnament(OrnamentIdentifier ornamentId, AchievementIdentifier achievementId)
        {
            OrnamentDefinition definition = AwardOrnament_Internal(ornamentId, true);
            if (definition == null) return;
            AnalyticsEventCollector.StatueCollectedEvent(definition, null, null, 0);
            UpdateUnlocks(new MetaGameUnlockOrnament(definition, achievementId));
        }

        //0600251f: exact original optional mission argument.
        private void AwardOrnament(OrnamentIdentifier ornamentId, MissionDefinition missionDefinition = null)
        {
            OrnamentDefinition definition = AwardOrnament_Internal(ornamentId, true);
            if (definition == null) return;
            AnalyticsEventCollector.StatueCollectedEvent(definition, missionDefinition, null, 0);
            UpdateUnlocks(new MetaGameUnlockOrnament(definition, missionDefinition));
        }

        //06002520: shipping xpThreshold is unused; reward collection is false.
        public void AwardOrnament(OrnamentIdentifier ornamentId, int xpThreshold = 0)
        {
            AwardOrnament_Internal(ornamentId, false);
        }

        //06002521: cache mutation precedes save-record creation and RequestSave.
        // Failures retain the already executed prefix; totals are not updated.
        private OrnamentDefinition AwardOrnament_Internal(OrnamentIdentifier ornamentId, bool collected = true)
        {
            OrnamentDefinition definition = m_dataManager.OrnamentDefinitions[ornamentId];
            string guid = definition.GetGUID();
            SaveDataGame saveDataGame = m_saveManager.CurrentSave;
            if (saveDataGame.OrnamentsByGuid.TryGetValue(guid, out SaveDataOrnament existing) && existing.Unlocked) return null;
            m_ornamentCollections[definition.OrnamentCategory][ornamentId] = new OrnamentStatus(true, false, collected);
            saveDataGame.GetOrCreateOrnamentData(guid).Unlocked = true;
            if (saveDataGame.TryGetRewardDataByRewardGUID(guid, out SaveDataChallengeReward reward)) reward.Collected = collected;
            m_saveManager.RequestSave();
            return definition;
        }

        //06002522: the unsafe saved reward write and RequestSave precede the
        // reread of definition keys and the cache write.
        public void CollectOrnament(OrnamentDefinition definition)
        {
            if (!m_ornamentCollections[definition.OrnamentCategory].TryGetValue(definition.OrnamentIdentifier, out OrnamentStatus status)
                || !status.Unlocked || status.Collected) return;
            m_saveManager.CurrentSave.GetRewardDataByRewardGUIDUnsafe(definition.GetGUID()).Collected = true;
            m_saveManager.RequestSave();
            m_ornamentCollections[definition.OrnamentCategory][definition.OrnamentIdentifier] = new OrnamentStatus(true, status.Seen, true);
        }

        //06002523: original development-only cache change does not touch save.
        [Conditional("BUILD_DEVELOPMENT")]
        private void UnAwardOrnament(OrnamentIdentifier identifier)
        {
            OrnamentDefinition definition = m_dataManager.OrnamentDefinitions[identifier];
            m_ornamentCollections[definition.OrnamentCategory][identifier] = new OrnamentStatus(false, false, false);
        }

        //06002524: shipping retained the enum/name/string computations and
        // live enumerations even though this body has no debug-UI registration.
        [Conditional("BUILD_DEVELOPMENT")]
        private void InitialiseDebugMenu()
        {
            foreach (var (category, collection) in m_ornamentCollections)
            {
                string categoryName = category.ToString();
                foreach (var (identifier, status) in collection)
                {
                    string ornamentName = m_dataManager.OrnamentDefinitions[identifier].name + (status.Unlocked ? " (Un-Award)" : " (Award)");
                }
            }
        }

        //06002525: totals and provider references remain unchanged.
        public void OnSaveGameClose(SaveDataGame saveDataGame)
        {
            m_ornamentCollections.Clear();
            Initialised = false;
        }

        //06002526: Definition is fetched again for the award call.
        private void OnMissionCompleted(MissionState missionState)
        {
            OrnamentDefinition ornament = missionState.Definition.Ornament;
            if (ornament == null) return;
            AwardOrnament(ornament.OrnamentIdentifier, missionState.Definition);
        }

        //06002527: the original storage default is null with storeDefault=true;
        // no replacement list is created if that lookup returns null.
        private static void UpdateUnlocks(MetaGameUnlockOrnament ornamentUnlock)
        {
            ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true).Add(ornamentUnlock);
        }

        //06002528/29: no Unity-object or managed-null definition guard.
        public bool IsOrnamentUnlocked(OrnamentDefinition definition) =>
            m_ornamentCollections.TryGetValue(definition.OrnamentCategory, out Dictionary<OrnamentIdentifier, OrnamentStatus> collection)
            && collection.TryGetValue(definition.OrnamentIdentifier, out OrnamentStatus status) && status.Unlocked;

        public bool IsOrnamentCollected(OrnamentDefinition definition) =>
            m_ornamentCollections.TryGetValue(definition.OrnamentCategory, out Dictionary<OrnamentIdentifier, OrnamentStatus> collection)
            && collection.TryGetValue(definition.OrnamentIdentifier, out OrnamentStatus status) && status.Collected;

        //0600252a: dispose the enumerator before the recursive restart. Keeping
        // the recursive call inside the foreach would reverse that fault order.
        public void CheckAllAchievementUnlocks()
        {
            if (m_achievementsManager == null) return;
            OnInitialised -= CheckAllAchievementUnlocks;
            bool restart = false;
            foreach (var entry in OrnamentCollections[OrnamentCategory.Achievement])
            {
                if (!CheckAchievementUnlock(entry.Key)) continue;
                restart = true;
                break;
            }
            if (restart) CheckAllAchievementUnlocks();
        }

        //0600252b and genuine closure06002531/32: only seen level-select rows
        // and complete missions participate. The captured predicate compares
        // original definition GetGUID() with the saved mission GUID.
        private void CheckLevelUnlocks()
        {
            IReadOnlyList<SaveDataLevel> levels = m_saveManager.CurrentSave.Levels;
            List<MissionDefinition> missionDefinitions = ProcessManager.GetSystem<MissionManager>().GetAllMissionDefinitions(false);
            foreach (SaveDataLevel level in levels)
            {
                if (!level.LevelSelectUnlockSeen) continue;
                foreach (SaveDataLevelMission mission in level.Missions)
                {
                    if (!mission.Complete) continue;
                    if (missionDefinitions.TryFind(definition => definition.GetGUID() == mission.GUID, out MissionDefinition missionDefinition)
                        && missionDefinition.Ornament != null)
                        AwardOrnament_Internal(missionDefinition.Ornament.OrnamentIdentifier, true);
                }
            }
        }

        //0600252c: Day_Dreamer2 uses the original arcadeFTUX. prefix; other
        // achievement keys are the actual generated enum strings.
        private bool CheckAchievementUnlock(OrnamentIdentifier ornamentKey)
        {
            if (m_ornamentCollections[OrnamentCategory.Achievement][ornamentKey].Unlocked) return false;
            AchievementIdentifier achievementId = m_dataManager.OrnamentDefinitions[ornamentKey].UnlockedByAchievement;
            string key = HardlightEnumExtensions.GetString(achievementId);
            if (achievementId == AchievementIdentifier.Day_Dreamer2) key = "arcadeFTUX." + key;
            if (m_achievementsManager.Achievements.TryGetValue(key, out Achievement achievement) && achievement.ReportedComplete)
            {
                AwardOrnament(ornamentKey, achievementId);
                return true;
            }
            return false;
        }

        //0600252d: use the IReadOnlyDictionary property before the concrete
        // inner dictionary traversal; availability/collection are not consulted.
        public int GetUnlockedOrnamentCount(OrnamentCategory category)
        {
            int count = 0;
            foreach (var (identifier, status) in OrnamentCollections[category])
                if (status.Unlocked) ++count;
            return count;
        }

        //0600252e: the outer property is traversed through its original read-
        // only dictionary interface; nested enumerators dispose on fault.
        public int GetNewOrnamentsCount()
        {
            int count = 0;
            foreach (var (category, collection) in OrnamentCollections)
                foreach (var (identifier, status) in collection)
                    if (status.IsAvailable() && !status.Seen) ++count;
            return count;
        }

        // Original020006e0/0600252f..30: no extra Seen condition is introduced.
        public readonly struct OrnamentStatus
        {
            public readonly bool Unlocked;
            public readonly bool Seen;
            public readonly bool Collected;
            public OrnamentStatus(bool unlocked, bool seen, bool collected)
            {
                Unlocked = unlocked;
                Seen = seen;
                Collected = collected;
            }
            public bool IsAvailable() => Unlocked && Collected;
        }
    }
}
