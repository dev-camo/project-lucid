using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Utils
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AchievementsManager : ISystem
    {
        private readonly Dictionary<string, Achievement> m_achievements = new Dictionary<string, Achievement>();
        // Original HLUnityCore.Runtime06001111, ARM1b35ac0/x861b2f2d0.
        public IReadOnlyDictionary<string, Achievement> Achievements => m_achievements;

        public event Action OnPlatformSyncComplete = () => { };
        private readonly SystemRef<GameCenterLocalPlayer> m_gameCenterLocalPlayerRef =
            ProcessManager.GetSystemRef<GameCenterLocalPlayer>();

        // Original06001114: dictionary/event/ref initializers precede the two subscriptions.
        public AchievementsManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.Initialise, Initialise);
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, Shutdown);
        }

        // Original06001115 with original two captured callbacks06001119/111a.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void Initialise(object _)
        {
            if (Social.localUser.authenticated)
            {
                Social.LoadAchievementDescriptions(retrievedDescriptions =>
                {
                    foreach (var description in retrievedDescriptions)
                    {
                        if (description == null || description.id == null)
                            continue;
                        m_achievements.Add(description.id, new Achievement(description.id));
                        UnityEngine.Object.Destroy(description.image);
                    }
                    if (m_achievements.Count == 0)
                    {
                        HLOutput.LogError("Failed to load achievements from the platform.");
                        return;
                    }
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Social.LoadAchievements(achievements =>
                    {
                        foreach (var achievement in achievements)
                        {
                            if (achievement == null || achievement.id == null)
                                continue;
                            if (achievement.id.GetType() != typeof(string))
                            {
                                HLOutput.LogError("Achievement id corrupted.");
                                return;
                            }
                            if (m_achievements.TryGetValue(achievement.id, out var state))
                                state.SyncPlatformValues((float)achievement.percentCompleted, achievement.completed);
                            else
                                HLOutput.LogError("Achievement " + achievement.id +
                                    " was retrieved by LoadAchievements (all with progress) but was not retrieved by LoadAchievementDescriptions (all).");
                        }
                        OnPlatformSyncComplete();
                    });
                });
            }
            else
            {
                OnPlatformSyncComplete();
            }
        }
#else
        private void Initialise(object _)
        {
            ProjectLucid.Offline.LocalAchievementRuntime.Initialise(this, m_achievements, () => OnPlatformSyncComplete());
        }
#endif

        // Original06001116 deliberately reads the readonly-dictionary indexer twice.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void AddBehaviour(string achievementID, int target, ref Action evaluateOn,
            Func<int> trackerFunction, int prerequisiteTarget = 0)
        {
            if (!Social.localUser.authenticated)
                return;
            if (!Achievements.ContainsKey(achievementID))
            {
                HLOutput.LogError("Achievement with ID " + achievementID + " not retrieved from the platform.");
                return;
            }
            if (!Achievements[achievementID].ReportedComplete)
                Achievements[achievementID].AddBehaviour(target, ref evaluateOn, trackerFunction, prerequisiteTarget);
        }
#else
        public void AddBehaviour(string achievementID, int target, ref Action evaluateOn,
            Func<int> trackerFunction, int prerequisiteTarget = 0)
        {
            ProjectLucid.Offline.LocalAchievementRuntime.AddBehaviour(this, m_achievements, achievementID, target, ref evaluateOn, trackerFunction, prerequisiteTarget);
        }
#endif

        // Original06001117 logs at error level before routing to the original GameCenter provider.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void IssueChallenge(string achievementKey, string message,
            UnityHLAchievementChallengeIssuedCallback callback)
        {
            if (m_gameCenterLocalPlayerRef.TryGet(out var player))
            {
                HLOutput.LogError("AchievementsManager IssueChallenge " + achievementKey + ".");
                player.IssueAchievementChallenge(achievementKey, message, callback);
            }
        }
#else
        public void IssueChallenge(string achievementKey, string message,
            UnityHLAchievementChallengeIssuedCallback callback)
        {
            callback?.Invoke(false, "Platform achievement challenges are unavailable offline.");
        }
#endif

        // Original06001118 clears only the achievement dictionary.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void Shutdown(object objectContext)
        {
            m_achievements.Clear();
        }
#else
        private void Shutdown(object objectContext)
        {
            ProjectLucid.Offline.LocalAchievementRuntime.Shutdown(this, m_achievements);
        }
#endif
    }
}
