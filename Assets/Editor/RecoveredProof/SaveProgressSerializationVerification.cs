using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Actual Unity callbacks for independent progression records. Original
    // SaveDataGame/SaveManager slot and gameplay acceptance remains separate.
    public static class SaveProgressSerializationVerification
    {
        private static int checks;

        public static void Run()
        {
            checks = 0;
            VerifyLevelAndMission();
            VerifyLoadout();
            VerifyRewardTrack();
            Debug.Log("[Project Lucid] Progress-record Unity JSON checks passed: " + checks);
        }

        private static void VerifyLevelAndMission()
        {
            var mission = new SaveDataLevelMission("mission-proof")
            {
                Complete = true, Progress = 3, Attempts = 4, BestTimeSeconds = 12.25f,
                XPEarned = 150, LevelSelectUnlockSeen = true, TimeTrialUnlockSeen = true,
                CompletedAt = TimeUtils.FromUnixTime(1234567890123L)
            };
            Set(mission, "m_completedObjectiveIndices", new List<int> { 2, 5 });
            var level = new SaveDataLevel("level-proof") { UnlockSeen = true, LevelSelectUnlockSeen = true };
            level.SeenCutsceneOnLevelGuids.Add("cutscene-proof");
            level.MissionGroupsUnlockSeen.Add("group-proof");
            Get<Dictionary<string, SaveDataLevelMission>>(level, "m_missionsByGuid").Add(mission.GUID, mission);
            var objectId = (PersistentObjectIdentifierType)unchecked((int)0xdeadbeef);
            var persistent = new SaveDataLevelPersistentObject(objectId, HardlightProject.ValueType.Boolean);
            Set(persistent, "m_boolValue", true);
            Get<Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>>(level, "m_persistentObjectsById").Add(objectId, persistent);

            Require(level.Missions.Count == 0, "authoritative dictionary stays separate from serialized list before callback");
            string json = JsonUtility.ToJson(level);
            Require(level.Missions.Count == 1 && ReferenceEquals(level.Missions[0], mission), "Unity before-serialize exports dictionary identities");
            Require(json.Contains("\"m_completedAt\":1234567890123"), "mission callback writes Unix milliseconds");
            Require(json.Contains("\"m_missions\"") && json.Contains("\"m_persistentObjects\"") && !json.Contains("m_missionsByGuid") && !json.Contains("m_dirty"), "serialized child lists retain original names and exclude runtime maps/flags");
            SaveDataLevel restored = JsonUtility.FromJson<SaveDataLevel>(json);
            Require(restored.GUID == level.GUID && restored.UnlockSeen && restored.LevelSelectUnlockSeen, "level identity and unlock fields survive JSON");
            Require(restored.SeenCutsceneOnLevelGuids.Count == 1 && restored.MissionGroupsUnlockSeen[0] == "group-proof", "level presentation/progression lists survive JSON");
            Require(restored.Missions.Count == 1 && ReferenceEquals(restored.Missions[0], Get<Dictionary<string, SaveDataLevelMission>>(restored, "m_missionsByGuid")["mission-proof"]), "Unity after-deserialize rebuilds original child dictionary identities");
            SaveDataLevelMission loaded = restored.Missions[0];
            Require(loaded.Complete && loaded.Progress == 3 && loaded.Attempts == 4 && loaded.BestTimeSeconds == 12.25f && loaded.XPEarned == 150, "mission completion and records survive JSON");
            Require(loaded.CompletedAt == TimeUtils.FromUnixTime(1234567890123L) && loaded.CompletedAt.Kind == DateTimeKind.Utc, "mission callback restores original timestamp and UTC DateTime kind");
            Require(loaded.CompletedObjectiveIndices.Count == 2 && loaded.CompletedObjectiveIndices[1] == 5 && loaded.TimeTrialUnlockSeen, "mission objective and unlock fields survive JSON");
            var persistentMap = Get<Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>>(restored, "m_persistentObjectsById");
            Require(persistentMap.Count == 1 && Get<bool>(persistentMap[objectId], "m_boolValue"), "undefined signed enum identity and persistent object payload survive JSON");
            Require(ReferenceEquals(persistentMap.Comparer, HardlightProject.HardlightEnumComparers.PersistentObjectIdentifierTypeComparer), "after-deserialize repair restores the original persistent-object comparer");
            Require(!SavingEnabled(restored) && !SavingEnabled(loaded), "parameterized-only constructors retain matching Unity's zero runtime flags after JSON");
            loaded.Progress = 9;
            Require(!loaded.HasChangesToSave() && !restored.HasChangesToSave(), "original disabled record flags remain visible at dirty traversal boundary");

            Set(mission, "m_completedObjectiveIndices", new List<int>());
            JsonUtility.ToJson(mission);
            Require(mission.CompletedObjectiveIndices == null, "empty objective list is nulled by the original callback");
            SaveDataLevel duplicates = JsonUtility.FromJson<SaveDataLevel>("{\"m_guid\":\"duplicate-proof\",\"m_missions\":[{\"m_guid\":\"same\",\"m_progress\":1},{\"m_guid\":\"same\",\"m_progress\":2}]}");
            Require(duplicates.Missions.Count == 2 && Get<Dictionary<string, SaveDataLevelMission>>(duplicates, "m_missionsByGuid")["same"].Progress == 2, "original after-deserialize keeps list duplicates but the dictionary's last value");
            JsonUtility.ToJson(duplicates);
            Require(duplicates.Missions.Count == 1 && duplicates.Missions[0].Progress == 2, "next serialization collapses list through the authoritative map");
        }

        private static void VerifyLoadout()
        {
            var loadout = new SaveDataDreamPowerLoadout(CharacterArchetype.Dashing);
            Get<Dictionary<int, string>>(loadout, "m_slotsByIndex").Add(2, "first");
            Get<Dictionary<int, string>>(loadout, "m_slotsByIndex").Add(7, "second");
            string json = JsonUtility.ToJson(loadout);
            Require(json.Contains("\"m_slots\":[\"first\",\"second\"]") && !json.Contains("m_slotsByIndex"), "original loadout serializer copies values without numeric slot keys");
            SaveDataDreamPowerLoadout restored = JsonUtility.FromJson<SaveDataDreamPowerLoadout>(json);
            Require(restored.Archetype == CharacterArchetype.Dashing && restored.SlotsByIndex.Count == 2 && restored.SlotsByIndex[0] == "first" && restored.SlotsByIndex[1] == "second", "original list callback assigns compact string-list indices");
            SaveDataDreamPowerLoadout duplicates = JsonUtility.FromJson<SaveDataDreamPowerLoadout>("{\"m_slots\":[\"same\",\"same\",\"other\"]}");
            Require(duplicates.SlotsByIndex.Count == 2 && duplicates.SlotsByIndex.ContainsKey(0) && duplicates.SlotsByIndex.ContainsKey(2), "IndexOf-based original callback collapses duplicate strings to their first index");
        }

        private static void VerifyRewardTrack()
        {
            // Literal fixture uses the original field layout to exercise both
            // differently keyed dictionaries through actual Unity callbacks.
            string json = "{\"m_type\":" + (int)RewardTrackType.Adventure + ",\"m_rewards\":[{\"m_trackGUID\":\"track-a\",\"m_rewardGUID\":\"reward-a\"},{\"m_trackGUID\":\"track-a\",\"m_rewardGUID\":\"reward-b\"},{\"m_trackGUID\":\"track-b\",\"m_rewardGUID\":\"reward-a\"}]}";
            SaveDataChallengeRewardTrack restored = JsonUtility.FromJson<SaveDataChallengeRewardTrack>(json);
            var byTrack = Get<Dictionary<string, SaveDataChallengeReward>>(restored, "m_rewardsByTrackGUID");
            var byReward = Get<Dictionary<string, SaveDataChallengeReward>>(restored, "m_rewardsByRewardGUID");
            Require(restored.Type == RewardTrackType.Adventure && restored.Rewards.Count == 3 && byTrack.Count == 2 && byReward.Count == 2, "reward track rebuilds two original maps while retaining duplicate input list");
            Require(byTrack["track-a"].RewardGUID == "reward-b" && byReward["reward-a"].TrackGUID == "track-b", "each reward dictionary independently retains its last duplicate");
            JsonUtility.ToJson(restored);
            Require(restored.Rewards.Count == 2 && ReferenceEquals(restored.Rewards[0], byTrack["track-a"]), "before-serialize chooses track map as the authoritative reward list");
        }

        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static bool SavingEnabled(SaveDataItem item) => (bool)Field(typeof(SaveDataItem), "m_savingEnabled").GetValue(item);
        private static void Require(bool good, string label) { ++checks; if (!good) throw new InvalidOperationException("Progress JSON proof: " + label); }
    }
}
