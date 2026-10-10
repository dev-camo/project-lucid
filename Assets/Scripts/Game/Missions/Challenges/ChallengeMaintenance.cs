using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Version = Hardlight.Utils.Version;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [UnityEngine.CreateAssetMenuAttribute(menuName = "HardlightProject/Config/ChallengeMaintenance")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    // Original Game.Runtime 020006eb: complete owned type, fields and seven methods.
    public sealed class ChallengeMaintenance : SingleScriptableObject
    {
        [UnityEngine.SerializeField]
        // Original 040018d9, native offset 0x18.
        private ChallengeMaintenanceData[] m_maintenanceSteps;

        // Original 040018da, native offset 0x20; constructor literal is original "0".
        private readonly Version m_defaultVersion = new Version("0");

        // Original 040018db, native offset 0x38.
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);

        // Original 040018dc, native offset 0x40.
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // Original 06002579: capture the authored array once, then execute in array order.
        // No null guard, sorting or save-version update exists in the shipping bodies.
        public void RunMaintenanceSteps(SaveDataGame saveDataGame)
        {
            ChallengeMaintenanceData[] steps = m_maintenanceSteps;
            for (int i = 0; i < steps.Length; i++)
            {
                ChallengeMaintenanceData step = steps[i];
                if (CanPerformMaintenance(saveDataGame, step.VersionToMaintain))
                    step.MaintenanceToRun.Invoke(saveDataGame);
            }
        }

        // Original 0600257a: threshold failure stops traversal, rather than skipping a reward.
        // Collected is set for every eligible reward outside the ornament subtype branch.
        public void Action_RunCollectStatue(SaveDataGame currentSave)
        {
            ChallengeRewardTrackState state = m_challengeManagerRef.Get().RewardTrackStates[RewardTrackType.Adventure];
            int totalXP = currentSave.SaveDataChallenges.TotalXPEarned;
            foreach (ChallengeReward reward in state.Definition.Rewards)
            {
                if (reward.XPThreshold > totalXP)
                    break;
                if (reward is ChallengeRewardOrnament ornament)
                    currentSave.GetOrCreateOrnamentData(ornament.GetRewardGUID()).Unlocked = true;
                currentSave.GetRewardDataByTrackGUIDUnsafe(reward.GetGUID()).Collected = true;
            }
        }

        // Original 0600257b: a missing saved reward, or one already collected, may unlock
        // the track. A present uncollected saved reward does not. Unlock is stored before
        // the matching live state is notified; no save request is added here.
        public void Action_RunCollectRewardTrack(SaveDataGame currentSave)
        {
            ChallengeManager manager = m_challengeManagerRef.Get();
            foreach (KeyValuePair<RewardTrackType, ChallengeRewardTrackState> pair in manager.RewardTrackStates)
            {
                pair.Deconstruct(out RewardTrackType ignoredType, out ChallengeRewardTrackState state);
                foreach (ChallengeReward reward in state.Definition.Rewards)
                {
                    if (reward.XPThreshold > state.SaveData.SpentXP)
                        break;
                    if (!(reward is ChallengeRewardRewardTrack rewardTrack))
                        continue;
                    SaveDataChallengeReward savedReward = currentSave.GetRewardDataByTrackGUIDUnsafe(reward.GetGUID());
                    if (savedReward != null && !savedReward.Collected)
                        continue;
                    RewardTrackType type = rewardTrack.RewardTrackType.Type;
                    SaveDataChallengeRewardTrack savedTrack = currentSave.GetOrCreateRewardTrackData(type);
                    if (savedTrack.UnlockState != SaveDataChallengeRewardTrack.FeatureUnlockState.Locked)
                        continue;
                    savedTrack.UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
                    if (manager.RewardTrackStates.TryGetValue(type, out ChallengeRewardTrackState targetState))
                        targetState.OnSaveGameOpen(savedTrack);
                }
            }
        }

        // Original 0600257c: obtain/create the adventure save before reading total XP,
        // retain Int32 assignment, then refresh the original manager's reward states.
        public void Action_RunUpdateSpentXPFromAdventure(SaveDataGame currentSave)
        {
            ChallengeManager manager = m_challengeManagerRef.Get();
            RewardTrackType type = manager.RewardTrackStates[RewardTrackType.Adventure].Definition.Type;
            SaveDataChallengeRewardTrack savedTrack = currentSave.GetOrCreateRewardTrackData(type);
            savedTrack.SpentXP = currentSave.SaveDataChallenges.TotalXPEarned;
            manager.RefreshXPRewards();
        }

        // Original 0600257d: predicates retain XP, reward value, then collectable-type order.
        // The original dictionary setter overwrites duplicate GUID entries. Both totals and
        // subtraction use unchecked Int32 arithmetic; the saved total precedes telemetry.
        public void Action_RetroactivelyAwardCompletedMissionXP(SaveDataGame currentSave)
        {
            if (currentSave.SaveDataChallenges.TotalXPEarned > 0)
                currentSave.SaveDataChallenges.HasPlayedChallenge = true;
            List<MissionDefinition> definitions = m_missionManagerRef.Get().GetAllMissionDefinitions(true);
            var missionXP = new Dictionary<string, int>(definitions.Count);
            foreach (MissionDefinition definition in definitions)
            {
                if (definition.XPReward > 0 && definition.RewardValue > 0 &&
                    (definition.RewardType == CollectableType.Orb || definition.RewardType == CollectableType.Moon))
                    missionXP[definition.GetGUID()] = definition.XPReward;
            }
            int totalXP = 0;
            foreach (SaveDataLevel level in currentSave.Levels)
            {
                foreach (SaveDataLevelMission mission in level.Missions)
                {
                    if (mission.Complete && missionXP.TryGetValue(mission.GUID, out int xp))
                        totalXP = unchecked(totalXP + xp);
                }
            }
            int difference = unchecked(totalXP - currentSave.SaveDataChallenges.TotalStoryMissionXPEarned);
            if (difference > 0)
            {
                currentSave.SaveDataChallenges.TotalStoryMissionXPEarned = totalXP;
                Hardlight.Analytics.AnalyticsEventCollector.XPGainedEvent(difference, ChallengeManager.XPSource.SaveMaintenance, null);
            }
        }

        // Original 0600257e: default maintenance or blank saved version returns true.
        // Parse the saved version before reading/parsing Application.version; no catch or
        // invented version guard changes the original exception and comparison order.
        private bool CanPerformMaintenance(SaveDataGame currentSave, Version maintenanceVersion)
        {
            if (maintenanceVersion == m_defaultVersion || string.IsNullOrWhiteSpace(currentSave.GameVersion))
                return true;
            Version savedVersion = new Version(currentSave.GameVersion);
            Version applicationVersion = new Version(Application.version);
            if (savedVersion >= applicationVersion)
                return false;
            return maintenanceVersion > savedVersion;
        }

        // Original 0600257f: the three field initializers execute in declaration order
        // before the genuine SingleScriptableObject base constructor; the body is empty.
        public ChallengeMaintenance()
        {
        }

        // Original 020006ec: Serializable is the original metadata flag, not a custom
        // attribute blob. No UnityEvent allocation or version initializer exists here.
        [Serializable]
        public class ChallengeMaintenanceData
        {
            [UnityEngine.SerializeField]
            [UnityEngine.TooltipAttribute("All saves earlier than this should run this maintenance. If no version set, this maintenance step will always run")]
            // Original 040018dd, native offset 0x10.
            private Version m_versionToMaintain;

            [UnityEngine.SerializeField]
            // Original 040018de, native offset 0x28.
            private UnityEngine.Events.UnityEvent<SaveDataGame> m_maintenanceToRun;

            // Original 06002580 copies the complete Version value; 06002581 returns
            // the live authored event reference. Neither accessor invents default data.
            public Version VersionToMaintain => m_versionToMaintain;
            public UnityEngine.Events.UnityEvent<SaveDataGame> MaintenanceToRun => m_maintenanceToRun;

            // Original 06002582 only invokes Object's constructor.
            public ChallengeMaintenanceData()
            {
            }
        }
    }
}
