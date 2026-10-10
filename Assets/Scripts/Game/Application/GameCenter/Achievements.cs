// Whole original Game.Runtime HardlightProject.Achievements recovery.
// All31 original owner APIs and52 natural APIs were read in full on ARM64 and x86_64.
// Local functions/lambdas express every natural body; exact generated names, flags,
// layout and body binding require the real compiler graph and remain unaccepted.
// Original Social/platform callbacks are retained and have not been executed.
using System;
using System.Diagnostics;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
using UnityEngine.SocialPlatforms.GameCenter;
#endif

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Achievements
    {
        public const string AppleFTUEPrefix = "arcadeFTUX.";
        private const string DebugMenuRoot = "Achievements";
        private const string DebugMenuChallenge = "Achievements/Issue Challenge";
        private readonly SystemRef<MissionManager> m_missionManagerRef =
            ProcessManager.GetSystemRef<MissionManager>(null, true);
        private event Action OnMissionCompleted = () => { };
        private LevelManager m_levelManager;
        private AchievementsManager m_achievementsManager;
        private EnemyManager m_enemyManager;
        private CollectableManager m_collectableManager;
        private CharacterManager m_characterManager;
        private SaveManager m_saveManager;
#if !PROJECT_LUCID_ORIGINAL_GAMECENTER
        private ProjectLucid.Offline.LocalAchievementSaveBinding m_offlineSaveBinding;
        private bool m_offlineStopped;
        private ProjectLucid.Offline.LocalAchievementManagerLease m_offlineManagerLease;
        private BossManager m_offlineBossManager;
        private MissionManager m_offlineMissionManager;
        private ChallengeManager m_offlineChallengeManager;
        private MissionManager m_offlineProgressionMissionManager;
#endif
        private int[] m_bonusZone1ActsAchieveSRank = new int[] { 0, 1, 2, 3 };

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void Initialise(App app)
        {
            app.RegisterAndInitialiseSystem<AchievementsManager>();
            m_achievementsManager = ProcessManager.GetSystem<AchievementsManager>(null, true);
            GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
            m_achievementsManager.OnPlatformSyncComplete += AddAllAchievements;
        }
#else
        public void Initialise(App app)
        {
            DrainOfflineSaveListenerRemoval();
            m_offlineSaveBinding = null;
            m_offlineStopped = false;
            m_offlineManagerLease = ProjectLucid.Offline.LocalGameAchievements.Prepare(app, AddAllAchievements,
                StopOfflineAchievementSubscriptions, () => m_offlineSaveBinding != null && m_offlineSaveBinding.IsOpen);
            m_achievementsManager = m_offlineManagerLease.Manager;
            ProjectLucid.Offline.LocalProfilePresentation.BindAchievements(m_achievementsManager);
            m_offlineManagerLease.Start();
        }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddAllAchievements()
        {
            m_achievementsManager.OnPlatformSyncComplete -= AddAllAchievements;
            ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(AddCollectableAchievements);
            ProcessManager.GetSystemRef<BossManager>(null, true).InvokeOnValid(AddBossAchievements);
        }
#else
        private void AddAllAchievements()
        {
            if (m_offlineStopped) return;

            m_achievementsManager.OnPlatformSyncComplete -= AddAllAchievements;
            ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(AddCollectableAchievements);
            ProcessManager.GetSystemRef<BossManager>(null, true).InvokeOnValid(AddBossAchievements);
                }
#endif

        private void AddAchievementBehaviour(string achievementID, int target, ref Action evaluateOn,
            Func<int> trackerFunction, int prerequisiteTarget = 0)
        {
            Achievement achievement = null;
            m_achievementsManager.AddBehaviour(achievementID, target, ref evaluateOn,
                trackerFunction, prerequisiteTarget);
            if (m_achievementsManager.Achievements.TryGetValue(achievementID, out achievement)
                && !achievement.ReportedComplete)
            {
                achievement.RefreshProgress();
            }
        }

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddCollectableAchievements(CollectableManager collectableManager)
        {
            m_collectableManager = collectableManager;
            ProcessManager.GetSystemRef<EnemyManager>(null, true).OnSystemStartup += AddEnemyAchievements;
        }
#else
        private void AddCollectableAchievements(CollectableManager collectableManager)
        {
            if (m_offlineStopped) return;

            m_collectableManager = collectableManager;
            ProcessManager.GetSystemRef<EnemyManager>(null, true).InvokeOnValid(AddEnemyAchievements);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddBossAchievements(BossManager bossManager)
        {
            int ZoneBossDefeatedAsInt(LevelSetupTypes zone) => bossManager.BossDefeatedForThisType(zone) ? 1 : 0;

            AddAchievementBehaviour(AchievementIdentifier.Deflated_Ego.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone1Boss));
            AddAchievementBehaviour(AchievementIdentifier.Production_Halted.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone2Boss));
            AddAchievementBehaviour(AchievementIdentifier.On_the_Hunt.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone3Boss));
            AddAchievementBehaviour(AchievementIdentifier.Good_Night.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone4Boss));
        }
#else
        private void AddBossAchievements(BossManager bossManager)
        {
            if (m_offlineStopped) return;
            m_offlineBossManager = bossManager;

            int ZoneBossDefeatedAsInt(LevelSetupTypes zone) => bossManager.BossDefeatedForThisType(zone) ? 1 : 0;

            AddAchievementBehaviour(AchievementIdentifier.Deflated_Ego.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone1Boss));
            AddAchievementBehaviour(AchievementIdentifier.Production_Halted.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone2Boss));
            AddAchievementBehaviour(AchievementIdentifier.On_the_Hunt.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone3Boss));
            AddAchievementBehaviour(AchievementIdentifier.Good_Night.GetString(), 1,
                ref bossManager.OnBossFullyDefeated, () => ZoneBossDefeatedAsInt(LevelSetupTypes.Zone4Boss));
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddLevelAchievements(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            m_missionManagerRef.InvokeOnValid(AddMissionAchievements);
        }
#else
        private void AddLevelAchievements(LevelManager levelManager)
        {
            if (m_offlineStopped) return;

            m_levelManager = levelManager;
            m_missionManagerRef.InvokeOnValid(AddMissionAchievements);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddMissionAchievements(MissionManager missionManager)
        {
            int zoneCount = m_levelManager.GameLevels.GetZones().Count;
            if (zoneCount == 0)
            {
                return;
            }
            missionManager.OnMissionCompleted += MissionCompleted;
            AddAchievementBehaviour(AppleFTUEPrefix + AchievementIdentifier.Day_Dreamer2.GetString(), 1,
                ref m_levelManager.OnLevelExit, HasFTUEBeenCompleted);
            AddAchievementBehaviour(AchievementIdentifier.Beach_Combing.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(1));
            AddAchievementBehaviour(AchievementIdentifier.Producing_the_Goods.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(2));
            AddAchievementBehaviour(AchievementIdentifier.Misplaced_in_the_Maze.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(3));
            AddAchievementBehaviour(AchievementIdentifier.City_Search.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(4));
            AddAchievementBehaviour(AchievementIdentifier.Collect_All_Musical_Notes.GetString(), GetMissionCount(MissionType.Jukebox, false),
                ref missionManager.OnRewardCountChanged, () => GetMissionCount(MissionType.Jukebox, true));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_1.GetString(), GetRewardTotalForZone1Level(1),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(1));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_2.GetString(), GetRewardTotalForZone1Level(2),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(2));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_3.GetString(), GetRewardTotalForZone1Level(3),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(3));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Boss.GetString(), GetRewardTotalForZone1Level(4),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(4));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_1.GetString(), GetRewardTotalForLevel(2, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_2.GetString(), GetRewardTotalForLevel(2, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_3.GetString(), GetRewardTotalForLevel(2, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Boss.GetString(), GetRewardTotalForLevel(2, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 4, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_1.GetString(), GetRewardTotalForLevel(3, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_2.GetString(), GetRewardTotalForLevel(3, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_3.GetString(), GetRewardTotalForLevel(3, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Boss.GetString(), GetRewardTotalForLevel(3, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 4, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_1.GetString(), GetRewardTotalForLevel(4, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_2.GetString(), GetRewardTotalForLevel(4, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_3.GetString(), GetRewardTotalForLevel(4, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Boss.GetString(), GetRewardTotalForLevel(4, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 4, CollectableType.Orb));
            CollectableType moon = CollectableType.Moon;
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_1.GetString(), GetRewardTotalForLevel(5, 1, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 1, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_2.GetString(), GetRewardTotalForLevel(5, 2, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 2, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_3.GetString(), GetRewardTotalForLevel(5, 3, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 3, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_4.GetString(), GetRewardTotalForLevel(5, 4, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 4, moon));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_1.GetString(), GetRankedMissionsForZone(1),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(1));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_2.GetString(), GetRankedMissionsForZone(2),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(2));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_3.GetString(), GetRankedMissionsForZone(3),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(3));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_4.GetString(), GetRankedMissionsForZone(4),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(4));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Bonus_Zone_1.GetString(), GetRankedMissionsForZoneActs(5, m_bonusZone1ActsAchieveSRank),
                ref OnMissionCompleted, () => GetSRanksAchievedInZoneActs(5, m_bonusZone1ActsAchieveSRank));
            ProcessManager.GetSystemRef<ProgressionManager>(null, true).InvokeOnValid(AddProgressionAchievements);

            int HasFTUEBeenCompleted() => m_levelManager.HasFTUEBeenCompleted() ? 1 : 0;
            int RSRCompletionsInZone(int zoneIndex) =>
                m_levelManager.MissionsOfTypeInZone(zoneIndex - 1, MissionType.RedStarRings, true);
            int GetRewardTotalForLevel(int zone, int act, CollectableType collectableType) =>
                m_levelManager.GetRewardTotalForLevel(zone - 1, act - 1, collectableType);
            int GetRewardCollectedForLevel(int zone, int act, CollectableType collectableType) =>
                m_levelManager.GetRewardCollectedForLevel(zone - 1, act - 1, collectableType);
            int GetRankedMissionsForZone(int zoneIndex) =>
                m_levelManager.GetRankedMissionCountForZone(zoneIndex - 1, null);
            int GetRankedMissionsForZoneActs(int zoneIndex, int[] specificActs) =>
                m_levelManager.GetRankedMissionCountForZone(zoneIndex - 1, specificActs);
            int GetSRanksAchievedInZone(int zoneIndex) =>
                m_levelManager.RankCountAchievedInZone(zoneIndex - 1, RankType.S, null);
            int GetSRanksAchievedInZoneActs(int zoneIndex, int[] specificActs) =>
                m_levelManager.RankCountAchievedInZone(zoneIndex - 1, RankType.S, specificActs);
            int GetRewardTotalForZone1Level(int act) =>
                m_levelManager.GetRewardTotalForLevel(0, act, CollectableType.Orb);
            int GetRewardsCollectedForZone1Level(int act) =>
                m_levelManager.GetRewardCollectedForLevel(0, act, CollectableType.Orb);
        }
#else
        private void AddMissionAchievements(MissionManager missionManager)
        {
            if (m_offlineStopped) return;
            m_offlineMissionManager = missionManager;

            int zoneCount = m_levelManager.GameLevels.GetZones().Count;
            if (zoneCount == 0)
            {
                return;
            }
            missionManager.OnMissionCompleted += MissionCompleted;
            AddAchievementBehaviour(AppleFTUEPrefix + AchievementIdentifier.Day_Dreamer2.GetString(), 1,
                ref m_levelManager.OnLevelExit, HasFTUEBeenCompleted);
            AddAchievementBehaviour(AchievementIdentifier.Beach_Combing.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(1));
            AddAchievementBehaviour(AchievementIdentifier.Producing_the_Goods.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(2));
            AddAchievementBehaviour(AchievementIdentifier.Misplaced_in_the_Maze.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(3));
            AddAchievementBehaviour(AchievementIdentifier.City_Search.GetString(), 3,
                ref missionManager.OnRewardCountChanged, () => RSRCompletionsInZone(4));
            AddAchievementBehaviour(AchievementIdentifier.Collect_All_Musical_Notes.GetString(), GetMissionCount(MissionType.Jukebox, false),
                ref missionManager.OnRewardCountChanged, () => GetMissionCount(MissionType.Jukebox, true));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_1.GetString(), GetRewardTotalForZone1Level(1),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(1));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_2.GetString(), GetRewardTotalForZone1Level(2),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(2));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Act_3.GetString(), GetRewardTotalForZone1Level(3),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(3));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_1_Boss.GetString(), GetRewardTotalForZone1Level(4),
                ref missionManager.OnRewardCountChanged, () => GetRewardsCollectedForZone1Level(4));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_1.GetString(), GetRewardTotalForLevel(2, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_2.GetString(), GetRewardTotalForLevel(2, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Act_3.GetString(), GetRewardTotalForLevel(2, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_2_Boss.GetString(), GetRewardTotalForLevel(2, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(2, 4, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_1.GetString(), GetRewardTotalForLevel(3, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_2.GetString(), GetRewardTotalForLevel(3, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Act_3.GetString(), GetRewardTotalForLevel(3, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_3_Boss.GetString(), GetRewardTotalForLevel(3, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(3, 4, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_1.GetString(), GetRewardTotalForLevel(4, 1, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 1, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_2.GetString(), GetRewardTotalForLevel(4, 2, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 2, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Act_3.GetString(), GetRewardTotalForLevel(4, 3, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 3, CollectableType.Orb));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Zone_4_Boss.GetString(), GetRewardTotalForLevel(4, 4, CollectableType.Orb),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(4, 4, CollectableType.Orb));
            CollectableType moon = CollectableType.Moon;
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_1.GetString(), GetRewardTotalForLevel(5, 1, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 1, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_2.GetString(), GetRewardTotalForLevel(5, 2, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 2, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_3.GetString(), GetRewardTotalForLevel(5, 3, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 3, moon));
            AddAchievementBehaviour(AchievementIdentifier.Clear_Bonus_Zone_1_Act_4.GetString(), GetRewardTotalForLevel(5, 4, moon),
                ref missionManager.OnRewardCountChanged, () => GetRewardCollectedForLevel(5, 4, moon));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_1.GetString(), GetRankedMissionsForZone(1),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(1));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_2.GetString(), GetRankedMissionsForZone(2),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(2));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_3.GetString(), GetRankedMissionsForZone(3),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(3));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Zone_4.GetString(), GetRankedMissionsForZone(4),
                ref OnMissionCompleted, () => GetSRanksAchievedInZone(4));
            AddAchievementBehaviour(AchievementIdentifier.S_Rank_Bonus_Zone_1.GetString(), GetRankedMissionsForZoneActs(5, m_bonusZone1ActsAchieveSRank),
                ref OnMissionCompleted, () => GetSRanksAchievedInZoneActs(5, m_bonusZone1ActsAchieveSRank));
            ProcessManager.GetSystemRef<ProgressionManager>(null, true).InvokeOnValid(AddProgressionAchievements);

            int HasFTUEBeenCompleted() => m_levelManager.HasFTUEBeenCompleted() ? 1 : 0;
            int RSRCompletionsInZone(int zoneIndex) =>
                m_levelManager.MissionsOfTypeInZone(zoneIndex - 1, MissionType.RedStarRings, true);
            int GetRewardTotalForLevel(int zone, int act, CollectableType collectableType) =>
                m_levelManager.GetRewardTotalForLevel(zone - 1, act - 1, collectableType);
            int GetRewardCollectedForLevel(int zone, int act, CollectableType collectableType) =>
                m_levelManager.GetRewardCollectedForLevel(zone - 1, act - 1, collectableType);
            int GetRankedMissionsForZone(int zoneIndex) =>
                m_levelManager.GetRankedMissionCountForZone(zoneIndex - 1, null);
            int GetRankedMissionsForZoneActs(int zoneIndex, int[] specificActs) =>
                m_levelManager.GetRankedMissionCountForZone(zoneIndex - 1, specificActs);
            int GetSRanksAchievedInZone(int zoneIndex) =>
                m_levelManager.RankCountAchievedInZone(zoneIndex - 1, RankType.S, null);
            int GetSRanksAchievedInZoneActs(int zoneIndex, int[] specificActs) =>
                m_levelManager.RankCountAchievedInZone(zoneIndex - 1, RankType.S, specificActs);
            int GetRewardTotalForZone1Level(int act) =>
                m_levelManager.GetRewardTotalForLevel(0, act, CollectableType.Orb);
            int GetRewardsCollectedForZone1Level(int act) =>
                m_levelManager.GetRewardCollectedForLevel(0, act, CollectableType.Orb);
                }
#endif

        private int GetMissionCount(MissionType missionType, bool isComplete)
        {
            int zoneCount = m_levelManager.GameLevels.GetZones().Count;
            int count = 0;
            for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
            {
                count += m_levelManager.MissionsOfTypeInZone(zoneIndex, missionType, isComplete);
            }
            return count;
        }

        private void MissionCompleted(MissionState missionState)
        {
            OnMissionCompleted();
        }

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddEnemyAchievements(EnemyManager enemyManager)
        {
            m_enemyManager = enemyManager;
            ProcessManager.GetSystemRef<CharacterManager>(null, true).OnSystemStartup += AddCharacterAchievements;
        }
#else
        private void AddEnemyAchievements(EnemyManager enemyManager)
        {
            if (m_offlineStopped) return;

            m_enemyManager = enemyManager;
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(AddCharacterAchievements);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddCharacterAchievements(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(SaveManagerValid);
        }
#else
        private void AddCharacterAchievements(CharacterManager characterManager)
        {
            if (m_offlineStopped) return;

            m_characterManager = characterManager;
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(SaveManagerValid);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void SaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            if (m_saveManager.CurrentSave != null)
            {
                AddSaveDataAchievements();
            }
            else
            {
                m_saveManager.OnLoadCompleted += AddSaveDataAchievements;
            }
        }
#else
        private void SaveManagerValid(SaveManager saveManager)
        {
            if (m_offlineStopped) return;
            m_saveManager = saveManager;
            m_offlineSaveBinding = new ProjectLucid.Offline.LocalAchievementSaveBinding(saveManager, AddSaveDataAchievements, Shutdown);
            m_offlineSaveBinding.Start();
        }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddSaveDataAchievements()
        {
            m_saveManager.OnLoadCompleted -= AddSaveDataAchievements;
            int GetStatFromSave(SaveDataPlayerStat.Type statType) =>
                unchecked((int)m_saveManager.CurrentSave.GetOrCreatePlayerStatData(statType).PlayerStatCounter);
            int GetStatMSToMinutes(SaveDataPlayerStat.Type statType) =>
                unchecked((int)(m_saveManager.CurrentSave.GetOrCreatePlayerStatData(statType).PlayerStatCounter / 60000L));

            AddAchievementBehaviour(AchievementIdentifier.Anger_Management.GetString(), 500,
                ref m_enemyManager.OnEnemyDestroyedByPlayer, () => GetStatFromSave(SaveDataPlayerStat.Type.EnemiesDestroyed));
            AddAchievementBehaviour(AchievementIdentifier.Ring_Hoarder.GetString(), 10000,
                ref m_collectableManager.OnAnyCollectableAmountChanged, () => GetStatFromSave(SaveDataPlayerStat.Type.RingsCollected));
            AddAchievementBehaviour(AchievementIdentifier.Need_for_Speed.GetString(), 60,
                ref m_characterManager.OnCharacterStoppedBoosting, () => GetStatMSToMinutes(SaveDataPlayerStat.Type.BoostTimeMS));
            AddAchievementBehaviour(AchievementIdentifier.High_Flyer.GetString(), 30,
                ref m_characterManager.OnCharacterAirTimeReported, () => GetStatMSToMinutes(SaveDataPlayerStat.Type.AirTimeMS));
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(AddLevelAchievements);
            ProcessManager.GetSystemRef<ChallengeManager>(null, true).InvokeOnValid(AddChallengeAchievements);
        }
#else
        private void AddSaveDataAchievements()
        {
            if (m_offlineStopped) return;

            m_saveManager.OnLoadCompleted -= AddSaveDataAchievements;
            int GetStatFromSave(SaveDataPlayerStat.Type statType) =>
                unchecked((int)m_saveManager.CurrentSave.GetOrCreatePlayerStatData(statType).PlayerStatCounter);
            int GetStatMSToMinutes(SaveDataPlayerStat.Type statType) =>
                unchecked((int)(m_saveManager.CurrentSave.GetOrCreatePlayerStatData(statType).PlayerStatCounter / 60000L));

            AddAchievementBehaviour(AchievementIdentifier.Anger_Management.GetString(), 500,
                ref m_enemyManager.OnEnemyDestroyedByPlayer, () => GetStatFromSave(SaveDataPlayerStat.Type.EnemiesDestroyed));
            AddAchievementBehaviour(AchievementIdentifier.Ring_Hoarder.GetString(), 10000,
                ref m_collectableManager.OnAnyCollectableAmountChanged, () => GetStatFromSave(SaveDataPlayerStat.Type.RingsCollected));
            AddAchievementBehaviour(AchievementIdentifier.Need_for_Speed.GetString(), 60,
                ref m_characterManager.OnCharacterStoppedBoosting, () => GetStatMSToMinutes(SaveDataPlayerStat.Type.BoostTimeMS));
            AddAchievementBehaviour(AchievementIdentifier.High_Flyer.GetString(), 30,
                ref m_characterManager.OnCharacterAirTimeReported, () => GetStatMSToMinutes(SaveDataPlayerStat.Type.AirTimeMS));
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(AddLevelAchievements);
            ProcessManager.GetSystemRef<ChallengeManager>(null, true).InvokeOnValid(AddChallengeAchievements);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddChallengeAchievements(ChallengeManager challengeManager)
        {
            AddAchievementBehaviour(AchievementIdentifier.A_New_Challenger.GetString(), 1,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.ChallengesCompleted);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Bronze_Award, 100000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Silver_Award, 250000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Gold_Award, 750000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Platinum_Award, 1500000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Ultimate_Award, 1800000);
        }
#else
        private void AddChallengeAchievements(ChallengeManager challengeManager)
        {
            if (m_offlineStopped) return;
            m_offlineChallengeManager = challengeManager;

            AddAchievementBehaviour(AchievementIdentifier.A_New_Challenger.GetString(), 1,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.ChallengesCompleted);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Bronze_Award, 100000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Silver_Award, 250000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Gold_Award, 750000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Platinum_Award, 1500000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Ultimate_Award, 1800000);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddTotalXPEarnedAchievement(ChallengeManager challengeManager,
            AchievementIdentifier identifier, int totalXPEarnedTarget)
        {
            AddAchievementBehaviour(identifier.GetString(), totalXPEarnedTarget,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.TotalXPEarned);
        }
#else
        private void AddTotalXPEarnedAchievement(ChallengeManager challengeManager,
            AchievementIdentifier identifier, int totalXPEarnedTarget)
        {
            if (m_offlineStopped) return;
            m_offlineChallengeManager = challengeManager;

            AddAchievementBehaviour(identifier.GetString(), totalXPEarnedTarget,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.TotalXPEarned);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void AddProgressionAchievements(ProgressionManager progressionManager)
        {
            int target = progressionManager.GetTotalProgress(CollectableType.Orb).Total;
            AddAchievementBehaviour(AchievementIdentifier.Wakey_Wakey.GetString(), target,
                ref m_missionManagerRef.Get().OnRewardCountChanged,
                () => progressionManager.GetTotalProgress(CollectableType.Orb).Collected);
        }
#else
        private void AddProgressionAchievements(ProgressionManager progressionManager)
        {
            if (m_offlineStopped) return;
            m_offlineProgressionMissionManager = m_missionManagerRef.Get();

            int target = progressionManager.GetTotalProgress(CollectableType.Orb).Total;
            AddAchievementBehaviour(AchievementIdentifier.Wakey_Wakey.GetString(), target,
                ref m_offlineProgressionMissionManager.OnRewardCountChanged,
                () => progressionManager.GetTotalProgress(CollectableType.Orb).Collected);
                }
#endif

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void Shutdown()
        {
            ProcessManager.GetSystem<App>(null, true).ShutdownAndUnregisterSystem<AchievementsManager>();
            SystemRef<CharacterManager> characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            if (characterManagerRef != null)
            {
                characterManagerRef.OnSystemStartup -= AddCharacterAchievements;
            }
            SystemRef<EnemyManager> enemyManagerRef = ProcessManager.GetSystemRef<EnemyManager>(null, true);
            if (enemyManagerRef != null)
            {
                enemyManagerRef.OnSystemStartup -= AddEnemyAchievements;
            }
            if (m_saveManager != null)
            {
                m_saveManager.OnLoadCompleted -= AddSaveDataAchievements;
            }
        }
#else
        public void Shutdown()
        {
            try
            {
                if (m_offlineManagerLease != null) m_offlineManagerLease.Dispose();
                else StopOfflineAchievementSubscriptions();
            }
            finally { ProjectLucid.Offline.LocalProfilePresentation.UnbindAchievements(); }
        }
#endif
#if !PROJECT_LUCID_ORIGINAL_GAMECENTER
        // Called by the normal Update/Shutdown boundary, outside save-listener dispatch.
        internal void DrainOfflineSaveListenerRemoval() => m_offlineSaveBinding?.DrainPendingRemoval();

        // Adapter bridge has access to the actual private callback owners.
        private void StopOfflineAchievementSubscriptions()
        {

            m_offlineStopped = true;
            m_offlineSaveBinding?.Dispose();
            if (m_offlineBossManager != null) ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_offlineBossManager.OnBossFullyDefeated);
            if (m_levelManager != null) ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_levelManager.OnLevelExit);
            if (m_offlineMissionManager != null)
            {
                m_offlineMissionManager.OnMissionCompleted -= MissionCompleted;
                ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_offlineMissionManager.OnRewardCountChanged);
            }
            ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref OnMissionCompleted);
            if (m_enemyManager != null) ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_enemyManager.OnEnemyDestroyedByPlayer);
            if (m_collectableManager != null) ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_collectableManager.OnAnyCollectableAmountChanged);
            if (m_characterManager != null)
            {
                ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_characterManager.OnCharacterStoppedBoosting);
                ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_characterManager.OnCharacterAirTimeReported);
            }
            if (m_offlineChallengeManager != null) ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref m_offlineChallengeManager.OnChallengeXPAwarded);
            MissionManager progressionMissionManager = m_offlineProgressionMissionManager;
            if (progressionMissionManager != null && !ReferenceEquals(progressionMissionManager, m_offlineMissionManager))
                ProjectLucid.Offline.LocalAchievementRuntime.DetachEvaluators(m_achievementsManager, ref progressionMissionManager.OnRewardCountChanged);
            SystemRef<CharacterManager> characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, false);
            if (characterManagerRef != null) characterManagerRef.OnSystemStartup -= AddCharacterAchievements;
            SystemRef<EnemyManager> enemyManagerRef = ProcessManager.GetSystemRef<EnemyManager>(null, false);
            if (enemyManagerRef != null) enemyManagerRef.OnSystemStartup -= AddEnemyAchievements;
            if (m_saveManager != null) m_saveManager.OnLoadCompleted -= AddSaveDataAchievements;
                }
#endif

        [Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons()
        {
            // The shipped retail body still enumerates and deconstructs each pair.
            foreach (var (achievementID, achievement) in m_achievementsManager.Achievements)
            {
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Action<bool, string>))]
        public static void IssueChallengeCompletedCallback(bool success, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                HLOutput.LogError("AchievementsManager IssueChallenge failed - could not issue challenge: " + error + ".", null);
            }
        }

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private static void ResetAchievements()
        {
            GameCenterPlatform.ResetAllAchievements(null);
        }
#else
        private static void ResetAchievements()
        {
            throw new InvalidOperationException("Offline achievement reset requires explicit local profile reset policy.");
        }
#endif

        public Achievements()
        {
        }
    }
}
