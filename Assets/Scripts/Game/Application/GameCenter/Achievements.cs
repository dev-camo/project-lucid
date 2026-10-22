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
#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX // GameCenterPlatform exists only in Apple players; see docs/offline-hooks.md
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
        private int[] m_bonusZone1ActsAchieveSRank = new int[] { 0, 1, 2, 3 };

        public void Initialise(App app)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-030: the original synced with GameCenter and waited for the platform; we prepare the local achievement manager lease
            {
                DrainOfflineSaveListenerRemoval();
                m_offlineSaveBinding = null;
                m_offlineStopped = false;
                m_offlineManagerLease = ProjectLucid.Offline.LocalGameAchievements.Prepare(app, AddAllAchievements,
                    StopOfflineAchievementSubscriptions, IsOfflineSaveBindingOpen);
                m_achievementsManager = m_offlineManagerLease.Manager;
                ProjectLucid.Offline.LocalProfilePresentation.BindAchievements(m_achievementsManager);
                m_offlineManagerLease.Start();
                return;
            }
            app.RegisterAndInitialiseSystem<AchievementsManager>();
            m_achievementsManager = ProcessManager.GetSystem<AchievementsManager>(null, true);
#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX // GameCenterPlatform exists only in Apple players; see docs/offline-hooks.md
            GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
#endif
            m_achievementsManager.OnPlatformSyncComplete += AddAllAchievements;
        }

        private void AddAllAchievements()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active && m_offlineStopped) // Offline hook H-031: the adapter can stop the subscriptions; the original had no stop state
                return;
            m_achievementsManager.OnPlatformSyncComplete -= AddAllAchievements;
            ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(AddCollectableAchievements);
            ProcessManager.GetSystemRef<BossManager>(null, true).InvokeOnValid(AddBossAchievements);
        }

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

        private void AddCollectableAchievements(CollectableManager collectableManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-032: the original waited for the enemy manager through OnSystemStartup; we invoke on a valid reference
            {
                if (m_offlineStopped) return;
                m_collectableManager = collectableManager;
                ProcessManager.GetSystemRef<EnemyManager>(null, true).InvokeOnValid(AddEnemyAchievements);
                return;
            }
            m_collectableManager = collectableManager;
            ProcessManager.GetSystemRef<EnemyManager>(null, true).OnSystemStartup += AddEnemyAchievements;
        }

        private void AddBossAchievements(BossManager bossManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-033: the adapter can stop the subscriptions and remembers the boss manager so they can be detached
            {
                if (m_offlineStopped) return;
                m_offlineBossManager = bossManager;
            }
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

        private void AddLevelAchievements(LevelManager levelManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active && m_offlineStopped) // Offline hook H-034: the adapter can stop the subscriptions; the original had no stop state
                return;
            m_levelManager = levelManager;
            m_missionManagerRef.InvokeOnValid(AddMissionAchievements);
        }

        private void AddMissionAchievements(MissionManager missionManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-035: the adapter can stop the subscriptions and remembers the mission manager so they can be detached
            {
                if (m_offlineStopped) return;
                m_offlineMissionManager = missionManager;
            }
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

        private void AddEnemyAchievements(EnemyManager enemyManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-036: the original waited for the character manager through OnSystemStartup; we invoke on a valid reference
            {
                if (m_offlineStopped) return;
                m_enemyManager = enemyManager;
                ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(AddCharacterAchievements);
                return;
            }
            m_enemyManager = enemyManager;
            ProcessManager.GetSystemRef<CharacterManager>(null, true).OnSystemStartup += AddCharacterAchievements;
        }

        private void AddCharacterAchievements(CharacterManager characterManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active && m_offlineStopped) // Offline hook H-037: the adapter can stop the subscriptions; the original had no stop state
                return;
            m_characterManager = characterManager;
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(SaveManagerValid);
        }

        private void SaveManagerValid(SaveManager saveManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-038: the original waited for the save through OnLoadCompleted; we bind through the local save binding
            {
                if (m_offlineStopped) return;
                m_saveManager = saveManager;
                m_offlineSaveBinding = new ProjectLucid.Offline.LocalAchievementSaveBinding(saveManager, AddSaveDataAchievements, Shutdown);
                m_offlineSaveBinding.Start();
                return;
            }
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

        private void AddSaveDataAchievements()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active && m_offlineStopped) // Offline hook H-039: the adapter can stop the subscriptions; the original had no stop state
                return;
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

        private void AddChallengeAchievements(ChallengeManager challengeManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-040: the adapter can stop the subscriptions and remembers the challenge manager so they can be detached
            {
                if (m_offlineStopped) return;
                m_offlineChallengeManager = challengeManager;
            }
            AddAchievementBehaviour(AchievementIdentifier.A_New_Challenger.GetString(), 1,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.ChallengesCompleted);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Bronze_Award, 100000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Silver_Award, 250000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Gold_Award, 750000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Platinum_Award, 1500000);
            AddTotalXPEarnedAchievement(challengeManager, AchievementIdentifier.Tails_Ultimate_Award, 1800000);
        }

        private void AddTotalXPEarnedAchievement(ChallengeManager challengeManager,
            AchievementIdentifier identifier, int totalXPEarnedTarget)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-041: the adapter can stop the subscriptions and remembers the challenge manager so they can be detached
            {
                if (m_offlineStopped) return;
                m_offlineChallengeManager = challengeManager;
            }
            AddAchievementBehaviour(identifier.GetString(), totalXPEarnedTarget,
                ref challengeManager.OnChallengeXPAwarded,
                () => m_saveManager.CurrentSave.SaveDataChallenges.TotalXPEarned);
        }

        private void AddProgressionAchievements(ProgressionManager progressionManager)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-042: the adapter can stop the subscriptions and remembers the mission manager (the same instance the original reads below) so they can be detached
            {
                if (m_offlineStopped) return;
                m_offlineProgressionMissionManager = m_missionManagerRef.Get();
            }
            int target = progressionManager.GetTotalProgress(CollectableType.Orb).Total;
            AddAchievementBehaviour(AchievementIdentifier.Wakey_Wakey.GetString(), target,
                ref m_missionManagerRef.Get().OnRewardCountChanged,
                () => progressionManager.GetTotalProgress(CollectableType.Orb).Collected);
        }

        public void Shutdown()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-043: the original unregistered the AchievementsManager and removed its startup subscriptions; we dispose the manager lease and unbind the presentation
            {
                try
                {
                    if (m_offlineManagerLease != null) m_offlineManagerLease.Dispose();
                    else StopOfflineAchievementSubscriptions();
                }
                finally { ProjectLucid.Offline.LocalProfilePresentation.UnbindAchievements(); }
                return;
            }
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

        private static void ResetAchievements()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-044: the original reset the GameCenter achievements; offline there is no reset policy, so it throws
            {
                throw new InvalidOperationException("Offline achievement reset requires explicit local profile reset policy.");
            }
#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX // GameCenterPlatform exists only in Apple players; see docs/offline-hooks.md
            GameCenterPlatform.ResetAllAchievements(null);
#endif
        }

        public Achievements()
        {
        }

        // Offline adapter members sit after the original members so the compiler's member
        // ordinals (and with them the generated lambda and closure names) stay the original's.
        private ProjectLucid.Offline.LocalAchievementSaveBinding m_offlineSaveBinding;
        private bool m_offlineStopped;
        private ProjectLucid.Offline.LocalAchievementManagerLease m_offlineManagerLease;
        private BossManager m_offlineBossManager;
        private MissionManager m_offlineMissionManager;
        private ChallengeManager m_offlineChallengeManager;
        private MissionManager m_offlineProgressionMissionManager;
        // Method group instead of a lambda, so Initialise gains no generated member the original lacks.
        private bool IsOfflineSaveBindingOpen() => m_offlineSaveBinding != null && m_offlineSaveBinding.IsOpen;

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
    }
}
