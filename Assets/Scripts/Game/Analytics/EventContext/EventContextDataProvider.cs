using System;
using HardlightProject;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime02000026, all33 own APIs; original transport remains separate.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class EventContextDataProvider
    {
        private const long EpochOffsetMS = 62135596800000L;

        // Original060000e8..ec: live reads, rather than cached values.
        public static long EVTM => GetEVTM();
        public static string GID => Analytics.GetAnalyticsSettings().GameID;
        public static string ClientVersion => Application.version;
        public static string SessionID => GetSessionID();
        public static int EventIDX => Analytics.GetEventIndex();

        // Original.cctor sets only these first four cached integers to -1.
        public static int OrbTotal { get; private set; } = -1;
        public static int MoonTotal { get; private set; } = -1;
        public static int ZonesTotal { get; private set; } = -1;
        public static int AchievementStatueTotal { get; private set; } = -1;
        public static int DreamPowerTier { get; private set; }
        public static string BlueCoinsWalletTotalRange { get; private set; }
        public static string XpAmountTotalRange { get; private set; }
        public static bool? ChallengesState { get; private set; }

        private static readonly SystemRef<DataManager> s_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
        private static readonly SystemRef<SaveManager> s_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private static readonly SystemRef<ProgressionManager> s_progressionManagerRef = ProcessManager.GetSystemRef<ProgressionManager>();
        private static readonly SystemRef<DreamPowerStoreManager> s_dreamPowerStoreManagerRef = ProcessManager.GetSystemRef<DreamPowerStoreManager>();
        private static readonly SystemRef<ChallengeManager> s_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>();

        private const string FTUELevelGUID = "4a7843e27a95bdb43b3b84004d783a94";
        private const string FTUEMissionGUID = "a942c9127f5b68047abf0437917913ff";
        private const int ChallengesStateLocked = 0;
        private const int ChallengesStateUnlocked = 1;
        private const int ChallengesStatePlayed = 2;

        // Original060000fd: GetSafe tests the typed reference, not IsValid's untyped reference.
        public static bool IsEventContextLateDataReady()
        {
            SaveManager saveManager = s_saveManagerRef.GetSafe();
            return saveManager != null && saveManager.IsAnySaveOpen;
        }

        // Original060000fe: seven cache writes precede all interface callbacks.
        // The achievement lookup and publication follow the first seven successful callbacks.
        public static void SetEventContextLateData(IEventContext eventContext)
        {
            OrbTotal = GetRewardTotal(CollectableType.Orb);
            MoonTotal = GetRewardTotal(CollectableType.Moon);
            ZonesTotal = GetZoneCount();
            DreamPowerTier = GetDreamPowerTierNumber();
            BlueCoinsWalletTotalRange = GetBlueCoinsWalletTotal();
            XpAmountTotalRange = GetXPAmountTotal();
            ChallengesState = GetChallengesState();

            eventContext.OrbTotal = OrbTotal;
            eventContext.MoonTotal = MoonTotal;
            eventContext.ZonesTotal = ZonesTotal;
            eventContext.DreamPowerTier = DreamPowerTier;
            eventContext.BlueCoinsWalletTotalRange = BlueCoinsWalletTotalRange;
            eventContext.XpAmountTotalRange = XpAmountTotalRange;
            eventContext.ChallengesState = ChallengesState;

            AchievementStatueTotal = GetAchievementStatueTotal();
            eventContext.AchievementStatueTotal = AchievementStatueTotal;
        }

        // Original060000ff: integer millisecond truncation before the epoch subtraction.
        private static long GetEVTM() => DateTime.UtcNow.Ticks / 10000L - EpochOffsetMS;

        // Original06000100: the authentic Collected member, with -1 when the system is absent.
        private static int GetRewardTotal(CollectableType collectableType)
        {
            if (s_progressionManagerRef.TryGet(out ProgressionManager progressionManager))
                return progressionManager.GetTotalProgress(collectableType).Collected;
            return -1;
        }

        // Original06000101: only the real typed-null and Valid predicates precede the category count.
        private static int GetAchievementStatueTotal()
        {
            OrnamentManager ornamentManager = ProcessManager.GetSystemSafe<OrnamentManager>();
            return ornamentManager != null && ornamentManager.Initialised
                ? ornamentManager.GetUnlockedOrnamentCount(OrnamentCategory.Achievement)
                : -1;
        }

        // Original06000102: the FTUE lookups may create genuine save records.
        // CurrentSave is read again after completion, then the seen-zone list is counted.
        private static int GetZoneCount()
        {
            SaveManager saveManager = s_saveManagerRef.GetSafe();
            if (saveManager == null || !saveManager.IsAnySaveOpen)
                return -1;
            if (!saveManager.CurrentSave.GetOrCreateLevelData(FTUELevelGUID)
                .GetOrCreateMissionData(FTUEMissionGUID).Complete)
                return 0;
            return saveManager.CurrentSave.ZoneUnlockSeenGuids.Count;
        }

        // Original06000103: enumeration order determines the last recognized collected band.
        // None or an unrecognized collected type returns immediately, with foreach disposal.
        private static int GetDreamPowerTierNumber()
        {
            SaveManager saveManager = s_saveManagerRef.GetSafe();
            if (saveManager == null || !saveManager.IsAnySaveOpen)
                return -1;
            if (!s_dataManagerRef.TryGet(out DataManager dataManager))
                return -1;
            if (!s_dreamPowerStoreManagerRef.TryGet(out DreamPowerStoreManager dreamPowerStoreManager))
                return -1;
            if (!dreamPowerStoreManager.IsAvailable())
                return -1;

            int tier = 0;
            foreach (var entry in dataManager.DreamPowerStoreBandDefinitions)
            {
                entry.Deconstruct(out DreamPowerStoreBandType type, out DreamPowerStoreBandDefinition definition);
                if (!saveManager.CurrentSave.TryGetRewardDataByRewardGUID(definition.GetGUID(), out SaveDataChallengeReward rewardData)
                    || !rewardData.Collected)
                    continue;
                switch (type)
                {
                    case DreamPowerStoreBandType.None:
                        return 0;
                    case DreamPowerStoreBandType.Band1:
                        tier = 1;
                        break;
                    case DreamPowerStoreBandType.Band2:
                        tier = 2;
                        break;
                    case DreamPowerStoreBandType.Band3:
                        tier = 3;
                        break;
                    default:
                        return -1;
                }
            }
            return tier;
        }

        // Original06000104: progress is read before the save/DataManager gates.
        // Spent is subtracted with the original unchecked Int32 arithmetic.
        private static string GetBlueCoinsWalletTotal()
        {
            if (!s_progressionManagerRef.TryGet(out ProgressionManager progressionManager))
                return string.Empty;
            ProgressionCollectable progress = progressionManager.GetTotalProgress(CollectableType.BlueCoin);
            SaveManager saveManager = s_saveManagerRef.GetSafe();
            if (saveManager == null || !saveManager.IsAnySaveOpen)
                return string.Empty;
            if (!s_dataManagerRef.TryGet(out DataManager dataManager))
                return string.Empty;
            int blueCoins = unchecked(progress.Collected - saveManager.CurrentSave.BlueCoins.Spent);
            return dataManager.AnalyticsConfiguration.GetBlueCoinsBand(blueCoins);
        }

        // Original06000105: both system lookups precede GetCurrentXP and the configuration read.
        private static string GetXPAmountTotal()
        {
            if (!s_challengeManagerRef.TryGet(out ChallengeManager challengeManager))
                return string.Empty;
            if (!s_dataManagerRef.TryGet(out DataManager dataManager))
                return string.Empty;
            int xp = challengeManager.GetCurrentXP();
            return dataManager.AnalyticsConfiguration.GetXPBand(xp);
        }

        // Original06000106: the whole original Game AnalyticsSessionManager remains a true supplier.
        private static string GetSessionID()
        {
            AnalyticsSessionManager sessionManager = ProcessManager.GetSystemSafe<AnalyticsSessionManager>();
            return sessionManager?.SessionID;
        }

        // Original06000107: the DataManager lookup is retained even though its value is not consumed.
        // A null CurrentSave or Locked feature yields no nullable value; Challenges itself is not guarded.
        private static bool? GetChallengesState()
        {
            if (!s_saveManagerRef.TryGet(out SaveManager saveManager))
                return null;
            if (!s_dataManagerRef.TryGet(out DataManager dataManager))
                return null;
            SaveDataGame saveData = saveManager.CurrentSave;
            if (saveData == null)
                return null;
            SaveDataChallenges challenges = saveData.SaveDataChallenges;
            if (challenges.FeatureState == ChallengeManager.ChallengeFeatureState.Locked)
                return null;
            return challenges.HasPlayedChallenge;
        }
    }
}
