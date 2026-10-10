using System;
using System.Collections.Generic;
using Hardlight.Utils;
using HardlightProject;

namespace ProjectLucid.Offline
{
    // UI delivery is explicit and local. No global rank or platform player is synthesized.
    // Actual widgets must subscribe before invoking the genuine preserved UI callers.
    public static class LocalProfilePresentation
    {
        private static AchievementsManager achievements;
        public static event Action<IReadOnlyDictionary<string, Achievement>> OnAchievementsRequested;
        public static event Action<LeaderboardIdentifier?, IReadOnlyDictionary<string, long>> OnRecordsRequested;
        internal static void BindAchievements(AchievementsManager manager) => achievements = manager;
        internal static void UnbindAchievements() => achievements = null;
        public static void ShowAchievements()
        {
            if (!LocalAchievementRuntime.IsReady(achievements)) throw new InvalidOperationException("Local achievements are not ready.");
            OnAchievementsRequested?.Invoke(achievements.Achievements);
        }
        public static void ShowRecords(LeaderboardIdentifier? identifier)
        {
            OnRecordsRequested?.Invoke(identifier, LocalPersonalRecords.Snapshot());
        }
    }
}
