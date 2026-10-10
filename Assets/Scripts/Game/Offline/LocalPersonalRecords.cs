using System;
using System.Collections.Generic;
using System.Globalization;
using Hardlight;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // Local record transport only. Original mission/challenge algorithms decide
    // when a score is submitted and retain their own authoritative progression.
    public static class LocalPersonalRecords
    {
        private const string KeyPrefix = "ProjectLucid.Local.default.Records.";
        private static HLPropertyStore store;
        private static readonly HashSet<LeaderboardIdentifier> TimeTrials = new HashSet<LeaderboardIdentifier>();
        private static readonly Dictionary<string, long> Records = new Dictionary<string, long>(StringComparer.Ordinal);
        public static bool IsReady => store != null;

        internal static void Initialise(DataManager data, HLPropertyStore propertyStore)
        {
            if (store != null) throw new InvalidOperationException("Local records require explicit shutdown before rebinding.");
            if (!propertyStore.IsLoaded || !propertyStore.CanSave) throw new InvalidOperationException("Local records require a loaded idle store.");
            var identifiers = new HashSet<LeaderboardIdentifier>();
            foreach (MissionDefinition mission in data.MissionDefinitionLevelLookup.Keys)
            {
                LeaderboardIdentifier id = mission.LeaderboardIdentifier;
                if (id == LeaderboardIdentifier.TailsChallenges_Daily
                    || id == LeaderboardIdentifier.TailsChallenges_Weekly
                    || id == LeaderboardIdentifier.TailsChallenge_HourlyQA)
                    throw new InvalidOperationException("Challenge identifier cannot be inferred as a mission time trial.");
                if (id != LeaderboardIdentifier.None) identifiers.Add(id);
            }
            // Exact genuine daily identifier used by ChallengeLeaderboardIntegration_GameCenter.
            identifiers.Add(LeaderboardIdentifier.TailsChallenges_Daily);
            var loaded = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (LeaderboardIdentifier id in identifiers)
            {
                if (!Enum.IsDefined(typeof(LeaderboardIdentifier), id)) throw new InvalidOperationException("Unknown genuine leaderboard identifier.");
                string key = id.GetString();
                string value = propertyStore.GetPropertyValueFromSave(LocalAchievementRuntime.SaveIdentifier, KeyPrefix + key);
                if (value == HLPropertyStore.DefaultPropertyValue) continue;
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long score))
                    throw new FormatException("Invalid local personal record value.");
                loaded.Add(key, score);
            }
            TimeTrials.Clear();
            foreach (LeaderboardIdentifier id in identifiers)
                if (id != LeaderboardIdentifier.TailsChallenges_Daily) TimeTrials.Add(id);
            Records.Clear();
            foreach (var row in loaded) Records.Add(row.Key, row.Value);
            store = propertyStore;
        }

        internal static void RequireReady()
        {
            if (!IsReady) throw new InvalidOperationException("Local records are not ready.");
        }
        internal static void Report(long score, LeaderboardIdentifier id)
        {
            RequireReady();
            bool timeTrial = TimeTrials.Contains(id);
            if (!timeTrial && id != LeaderboardIdentifier.TailsChallenges_Daily) throw new KeyNotFoundException(id.ToString());
            string key = id.GetString();
            if (Records.TryGetValue(key, out long previous) && (timeTrial ? score >= previous : score <= previous)) return;
            if (!store.CanSave || !store.TrySetPropertyValueOnSave(LocalAchievementRuntime.SaveIdentifier, KeyPrefix + key, score))
                throw new InvalidOperationException("Local personal record was not saved.");
            Records[key] = score;
        }
        public static IReadOnlyDictionary<string, long> Snapshot()
        {
            RequireReady();
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(new Dictionary<string, long>(Records, StringComparer.Ordinal));
        }
        internal static void Shutdown()
        {
            store = null;
            TimeTrials.Clear();
            Records.Clear();
        }
    }
}
