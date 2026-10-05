// Game.Runtime SaveData progress records: bounded checks derived from original
// named ARM64 methods. These checks do not establish SaveDataGame/slot behavior.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    public static class SaveProgressRecordVerification
    {
        private static int checks;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly Type[] Types =
        {
            typeof(SaveDataBonusZone1), typeof(SaveDataChallengeReward), typeof(SaveDataChallengeRewardTrack),
            typeof(SaveDataCharacterArchetype), typeof(SaveDataCharacterCustomisation), typeof(SaveDataCharacterSkin),
            typeof(SaveDataCollectable), typeof(SaveDataDreamPowerLoadout), typeof(SaveDataDreamPowerStore),
            typeof(SaveDataDreamPowerStoreItem), typeof(SaveDataLevel), typeof(SaveDataLevelMission),
            typeof(SaveDataLevelPersistentObject), typeof(SaveDataMusicTrack), typeof(SaveDataOrnament), typeof(SaveDataPlayerStat)
        };
        private static void Check(bool condition, string label)
        {
            ++checks;
            if (!condition) throw new Exception(label);
        }
        private static FieldInfo Field(object record, string name)
        {
            for (Type type = record.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field != null) return field;
            }
            throw new Exception(name);
        }
        private static T Get<T>(object record, string name) => (T)Field(record, name).GetValue(record);
        private static void Set(object record, string name, object value) => Field(record, name).SetValue(record, value);
        private static bool Dirty(SaveDataItem record) => Get<bool>(record, "m_dirty");
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            ++checks;
            try { action(); }
            catch (T) { return; }
            catch (TargetInvocationException e) when (e.InnerException is T) { return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + label);
        }
        private static SaveDataItem Make(Type type)
        {
            ConstructorInfo constructor = type.GetConstructors().Single();
            object[] arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(string)
                ? (object)"key" : Activator.CreateInstance(parameter.ParameterType)).ToArray();
            return (SaveDataItem)constructor.Invoke(arguments);
        }
        private static string Canonical(Type type)
        {
            return type.IsGenericType
                ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(Canonical)) + ">"
                : type.FullName;
        }
        private static void FieldEvidence(Type type, string[] original)
        {
            Check(type.BaseType == typeof(SaveDataItem), type.Name + " original base");
            Check(type.IsDefined(typeof(SerializableAttribute), false), type.Name + " Serializable");
            FieldInfo[] fields = type.GetFields(Fields).OrderBy(field => field.MetadataToken).ToArray();
            Check(fields.Length == original.Length, type.Name + " field count");
            for (int i = 0; i < fields.Length; ++i)
            {
                string[] evidence = original[i].Split('|');
                FieldInfo field = fields[i];
                Check(field.Name == evidence[0], type.Name + " field name/order");
                Check(Canonical(field.FieldType) == evidence[1], type.Name + "." + field.Name + " type");
                Check(field.IsPrivate && !field.IsStatic && !field.IsInitOnly && !field.IsLiteral, type.Name + "." + field.Name + " flags");
                Check(field.IsDefined(typeof(SerializeField), false) == (evidence[2] == "1"), type.Name + "." + field.Name + " SerializeField");
                Check(!field.IsDefined(typeof(SerializeReference), false) && !field.IsDefined(typeof(NonSerializedAttribute), false), type.Name + "." + field.Name + " other serialization flags");
            }
        }
        private static void Layouts()
        {
            // Game.Runtime.dll 0x020007b0.
            FieldEvidence(typeof(SaveDataBonusZone1), new[]
            {
                "m_unlockSeen|System.Boolean|1",
                "m_isNewState|HardlightProject.BonusZoneIsNewState|1",
            });
            // Game.Runtime.dll 0x020007b1.
            FieldEvidence(typeof(SaveDataChallengeReward), new[]
            {
                "m_trackGUID|System.String|1",
                "m_rewardGUID|System.String|1",
                "m_collected|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007b2.
            FieldEvidence(typeof(SaveDataChallengeRewardTrack), new[]
            {
                "m_type|HardlightProject.RewardTrackType|1",
                "m_rewards|System.Collections.Generic.List`1<HardlightProject.SaveDataChallengeReward>|1",
                "m_spentXP|System.Int32|1",
                "m_spentXPMinor|System.Int32|1",
                "m_unlockState|HardlightProject.SaveDataChallengeRewardTrack+FeatureUnlockState|1",
                "m_rewardsByTrackGUID|System.Collections.Generic.Dictionary`2<System.String,HardlightProject.SaveDataChallengeReward>|0",
                "m_rewardsByRewardGUID|System.Collections.Generic.Dictionary`2<System.String,HardlightProject.SaveDataChallengeReward>|0",
            });
            // Game.Runtime.dll 0x020007b9.
            FieldEvidence(typeof(SaveDataCharacterArchetype), new[]
            {
                "m_characterArchetype|HardlightProject.CharacterArchetype|1",
                "m_unlocked|System.Boolean|1",
                "m_unlockSeen|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007ba.
            FieldEvidence(typeof(SaveDataCharacterCustomisation), new[]
            {
                "m_characterId|HardlightProject.CharacterId|1",
                "m_skinGUID|System.String|1",
            });
            // Game.Runtime.dll 0x020007bb.
            FieldEvidence(typeof(SaveDataCharacterSkin), new[]
            {
                "m_guid|System.String|1",
                "m_unlocked|System.Boolean|1",
                "m_unlockSeen|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007bc.
            FieldEvidence(typeof(SaveDataCollectable), new[]
            {
                "m_guid|System.String|1",
                "m_spent|System.Int32|1",
            });
            // Game.Runtime.dll 0x020007be.
            FieldEvidence(typeof(SaveDataDreamPowerLoadout), new[]
            {
                "m_archetype|HardlightProject.CharacterArchetype|1",
                "m_slots|System.Collections.Generic.List`1<System.String>|1",
                "m_slotsByIndex|System.Collections.Generic.Dictionary`2<System.Int32,System.String>|0",
            });
            // Game.Runtime.dll 0x020007bf.
            FieldEvidence(typeof(SaveDataDreamPowerStore), new[]
            {
                "m_dreamPowers|System.Collections.Generic.List`1<HardlightProject.SaveDataDreamPowerStoreItem>|1",
                "m_isNewState|HardlightProject.DreamPowerStoreIsNewState|1",
                "m_dreamPowersByGuid|System.Collections.Generic.Dictionary`2<System.String,HardlightProject.SaveDataDreamPowerStoreItem>|0",
            });
            // Game.Runtime.dll 0x020007c1.
            FieldEvidence(typeof(SaveDataDreamPowerStoreItem), new[]
            {
                "m_guid|System.String|1",
                "m_bought|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007c7.
            FieldEvidence(typeof(SaveDataLevel), new[]
            {
                "m_guid|System.String|1",
                "m_missions|System.Collections.Generic.List`1<HardlightProject.SaveDataLevelMission>|1",
                "m_missionIntrosSeen|System.Boolean|1",
                "m_persistentObjects|System.Collections.Generic.List`1<HardlightProject.SaveDataLevelPersistentObject>|1",
                "m_cutscenesSeen|System.Collections.Generic.List`1<System.String>|1",
                "m_unlockSeen|System.Boolean|1",
                "m_levelSelectUnlockSeen|System.Boolean|1",
                "m_missionGroupsUnlockSeen|System.Collections.Generic.List`1<System.String>|1",
                "m_missionsByGuid|System.Collections.Generic.Dictionary`2<System.String,HardlightProject.SaveDataLevelMission>|0",
                "m_persistentObjectsById|System.Collections.Generic.Dictionary`2<HardlightProject.PersistentObjectIdentifierType,HardlightProject.SaveDataLevelPersistentObject>|0",
            });
            // Game.Runtime.dll 0x020007c9.
            FieldEvidence(typeof(SaveDataLevelMission), new[]
            {
                "m_guid|System.String|1",
                "m_complete|System.Boolean|1",
                "m_completedAt|System.Int64|1",
                "m_progress|System.Int32|1",
                "m_completedObjectiveIndices|System.Collections.Generic.List`1<System.Int32>|1",
                "m_levelSelectUnlockSeen|System.Boolean|1",
                "m_timeTrialUnlockSeen|System.Boolean|1",
                "m_isNewState|HardlightProject.MissionIsNewState|1",
                "m_attempts|System.Int32|1",
                "m_bestTimeSeconds|System.Single|1",
                "m_xpEarned|System.Int32|1",
                "m_completedAtTime|System.DateTime|0",
            });
            // Game.Runtime.dll 0x020007ca.
            FieldEvidence(typeof(SaveDataLevelPersistentObject), new[]
            {
                "m_id|HardlightProject.PersistentObjectIdentifierType|1",
                "m_valueType|HardlightProject.ValueType|1",
                "m_boolValue|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007cb.
            FieldEvidence(typeof(SaveDataMusicTrack), new[]
            {
                "m_guid|System.String|1",
                "m_seen|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007ce.
            FieldEvidence(typeof(SaveDataOrnament), new[]
            {
                "m_guid|System.String|1",
                "m_unlocked|System.Boolean|1",
                "m_unlockSeen|System.Boolean|1",
            });
            // Game.Runtime.dll 0x020007cf.
            FieldEvidence(typeof(SaveDataPlayerStat), new[]
            {
                "m_playerStatType|HardlightProject.SaveDataPlayerStat+Type|1",
                "m_playerStatCounter|System.Int64|1",
            });
        }
        private static void ConstructorsAndRepair()
        {
            foreach (Type type in Types)
            {
                SaveDataItem record = Make(type);
                Check(!Dirty(record) && Get<bool>(record, "m_savingEnabled"), type.Name + " constructor clean/enabled");
                record.Initialise();
                Check(!Dirty(record), type.Name + " initialise clean");
            }
            var level = new SaveDataLevel("level");
            Check(level.GUID == "level" && level.Missions.Count == 0, "level constructor data");
            Check(Get<Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>>(level, "m_persistentObjectsById").Comparer
                == HardlightEnumComparers.PersistentObjectIdentifierTypeComparer, "level original enum comparer");
            foreach (string name in new[] { "m_missionsByGuid", "m_missions", "m_persistentObjectsById", "m_persistentObjects", "m_cutscenesSeen", "m_missionGroupsUnlockSeen" }) Set(level, name, null);
            level.Initialise();
            Check(Get<object>(level, "m_missionsByGuid") != null && level.Missions != null && Get<object>(level, "m_persistentObjects") != null, "level null repair");
            Check(Get<Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>>(level, "m_persistentObjectsById").Comparer
                == HardlightEnumComparers.PersistentObjectIdentifierTypeComparer, "level repaired comparer");
            var track = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);
            Check((int)track.Type == 632844063 && track.Rewards.Count == 0, "Adventure original enum literal");
            foreach (string name in new[] { "m_rewards", "m_rewardsByTrackGUID", "m_rewardsByRewardGUID" }) Set(track, name, null);
            track.Initialise();
            Check(track.Rewards != null && Get<object>(track, "m_rewardsByTrackGUID") != null && Get<object>(track, "m_rewardsByRewardGUID") != null, "reward null repair");
            var store = new SaveDataDreamPowerStore();
            Set(store, "m_dreamPowers", null); Set(store, "m_dreamPowersByGuid", null); store.Initialise();
            Check(store.DreamPowers != null && Get<object>(store, "m_dreamPowers") != null, "store null repair");
            var loadout = new SaveDataDreamPowerLoadout((CharacterArchetype)7);
            Set(loadout, "m_slots", null); Set(loadout, "m_slotsByIndex", null); loadout.Initialise();
            Check(loadout.Archetype == (CharacterArchetype)7 && loadout.SlotsByIndex != null && Get<object>(loadout, "m_slots") != null, "loadout constructor and repair");
            var mission = new SaveDataLevelMission("mission");
            Check(mission.GUID == "mission" && mission.IsNewState == MissionIsNewState.None && mission.CompletedObjectiveIndices == null, "mission constructor null/defaults");
            Check(new SaveDataCollectable().GUID == null, "collectable constructor leaves GUID null");
        }
        private static void Setters()
        {
            foreach (Type type in Types)
            foreach (PropertyInfo property in type.GetProperties().Where(property => property.CanRead && property.CanWrite))
            {
                SaveDataItem record = Make(type); record.Initialise(); record.MarkSaved();
                object initial = property.GetValue(record);
                property.SetValue(record, initial);
                Check(Dirty(record) == (property.Name != "UnlockState"), type.Name + "." + property.Name + " equal-value dirty behavior");
                Type p = property.PropertyType;
                object next = p == typeof(bool) ? (object)!(bool)initial : p == typeof(string) ? "changed" : p == typeof(DateTime)
                    ? new DateTime(2026, 1, 2) : p == typeof(float) ? (object)2.5f : p == typeof(long) ? 7L : p.IsEnum ? Enum.ToObject(p, 30) : Convert.ChangeType(7, p);
                property.SetValue(record, next);
                Check(Equals(property.GetValue(record), next) && Dirty(record), type.Name + "." + property.Name + " changed value");
            }
            var track = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);
            track.UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Seen;
            track.MarkSaved(); track.UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Locked;
            Check(track.UnlockState == SaveDataChallengeRewardTrack.FeatureUnlockState.Seen && !Dirty(track), "unlock state decreases ignored");
        }
        private static void PairRoundTrip(SaveDataItem owner, string dictionary, string list, object key, SaveDataItem value)
        {
            owner.Initialise();
            var dict = (IDictionary)Get<object>(owner, dictionary); dict.Add(key, value);
            var callbacks = (ISerializationCallbackReceiver)owner; callbacks.OnBeforeSerialize();
            var items = (IList)Get<object>(owner, list);
            Check(items.Count == 1 && ReferenceEquals(items[0], value), owner.GetType().Name + " dictionary to list");
            dict.Clear(); callbacks.OnAfterDeserialize();
            Check(dict.Count == 1 && ReferenceEquals(dict[key], value), owner.GetType().Name + " list key reconstruction");
            Check(!Dirty(owner), owner.GetType().Name + " callbacks clean");
        }
        private static void Callbacks()
        {
            var level = new SaveDataLevel("level");
            PairRoundTrip(level, "m_missionsByGuid", "m_missions", "mission", new SaveDataLevelMission("mission"));
            PairRoundTrip(level, "m_persistentObjectsById", "m_persistentObjects", (PersistentObjectIdentifierType)27,
                new SaveDataLevelPersistentObject((PersistentObjectIdentifierType)27, HardlightProject.ValueType.Boolean));
            PairRoundTrip(new SaveDataDreamPowerStore(), "m_dreamPowersByGuid", "m_dreamPowers", "power", new SaveDataDreamPowerStoreItem("power"));
            var track = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);
            var reward = new SaveDataChallengeReward("track", "reward");
            PairRoundTrip(track, "m_rewardsByTrackGUID", "m_rewards", "track", reward);
            Check(ReferenceEquals(Get<Dictionary<string, SaveDataChallengeReward>>(track, "m_rewardsByRewardGUID")["reward"], reward), "second reward index");
            var first = new SaveDataLevelMission("duplicate"); var second = new SaveDataLevelMission("duplicate");
            Set(level, "m_missions", new List<SaveDataLevelMission> { first, second }); level.OnAfterDeserialize();
            var missions = Get<Dictionary<string, SaveDataLevelMission>>(level, "m_missionsByGuid");
            Check(ReferenceEquals(missions["duplicate"], second), "last duplicate key wins");
            Set(level, "m_missions", new List<SaveDataLevelMission> { first, null });
            Throws<NullReferenceException>(level.OnAfterDeserialize, "null child not filtered");
            Check(missions.Count == 1 && ReferenceEquals(missions["duplicate"], first), "failure leaves previous insert");
            Set(level, "m_missions", new List<SaveDataLevelMission> { new SaveDataLevelMission(null) });
            Throws<ArgumentNullException>(level.OnAfterDeserialize, "null string key");
            Check(missions.Count == 0, "clear precedes failing key");
            var loadout = new SaveDataDreamPowerLoadout((CharacterArchetype)2);
            Set(loadout, "m_slots", new List<string> { "same", "same", null, null, "last" }); loadout.OnAfterDeserialize();
            Check(loadout.SlotsByIndex.Count == 3 && loadout.SlotsByIndex[0] == "same" && loadout.SlotsByIndex[2] == null && loadout.SlotsByIndex[4] == "last", "duplicate/null slots use first IndexOf");
            var slots = Get<Dictionary<int, string>>(loadout, "m_slotsByIndex"); slots.Clear(); slots.Add(9, "a"); slots.Add(31, "b"); loadout.OnBeforeSerialize();
            Check(Get<List<string>>(loadout, "m_slots").SequenceEqual(new[] { "a", "b" }), "loadout serializes values only");
            loadout.OnAfterDeserialize(); Check(slots.Keys.SequenceEqual(new[] { 0, 1 }), "sparse numeric keys compact");
        }
        private sealed class ObservedMission : SaveDataLevelMission
        {
            public int Initialisations;
            public ObservedMission(string guid) : base(guid) { }
            public override void Initialise() { ++Initialisations; base.Initialise(); }
        }
        private static void Traversal()
        {
            var level = new SaveDataLevel("level");
            var old = new ObservedMission("old"); var incoming = new ObservedMission("new");
            Get<Dictionary<string, SaveDataLevelMission>>(level, "m_missionsByGuid").Add("old", old);
            Set(level, "m_missions", new List<SaveDataLevelMission> { incoming }); level.OnAfterDeserialize();
            Check(old.Initialisations == 1 && incoming.Initialisations == 0, "Initialise visits old dictionary before rebuilding");
            var missions = Get<Dictionary<string, SaveDataLevelMission>>(level, "m_missionsByGuid");
            var persistent = new SaveDataLevelPersistentObject((PersistentObjectIdentifierType)4, HardlightProject.ValueType.Boolean);
            Get<Dictionary<PersistentObjectIdentifierType, SaveDataLevelPersistentObject>>(level, "m_persistentObjectsById").Add((PersistentObjectIdentifierType)4, persistent);
            var seen = new List<SaveDataItem>();
            MethodInfo iterate = typeof(SaveDataLevel).GetMethod("IterateChildren", BindingFlags.Instance | BindingFlags.NonPublic);
            iterate.Invoke(level, new object[] { (Action<SaveDataItem>)seen.Add });
            Check(seen.SequenceEqual(new SaveDataItem[] { incoming, persistent }), "mission dictionary then persistent dictionary");
            Throws<InvalidOperationException>(() => iterate.Invoke(level, new object[]
            {
                (Action<SaveDataItem>)(child => missions.Add("added", new SaveDataLevelMission("added")))
            }), "live dictionary mutation");
            var track = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);
            var reward = new SaveDataChallengeReward("track", "reward");
            Set(track, "m_rewards", new List<SaveDataChallengeReward> { reward }); reward.MarkDirty();
            Check(track.HasChangesToSave(), "reward traversal uses list before callback");
            var store = new SaveDataDreamPowerStore(); var power = new SaveDataDreamPowerStoreItem("power"); power.MarkDirty();
            Set(store, "m_dreamPowers", new List<SaveDataDreamPowerStoreItem> { power });
            Check(!store.HasChangesToSave(), "store traversal uses dictionary before callback");
            store.OnAfterDeserialize(); Check(store.HasChangesToSave(), "rebuilt store traverses child");
        }
        private static void Dates()
        {
            var mission = new SaveDataLevelMission("mission");
            Check(mission.CompletedAt == default(DateTime), "cached date default"); mission.OnBeforeSerialize();
            Check(Get<long>(mission, "m_completedAt") == -62135596800000L, "minimum cached date milliseconds");
            mission.CompletedAt = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Unspecified); mission.MarkSaved();
            Set(mission, "m_completedObjectiveIndices", new List<int>()); mission.OnBeforeSerialize();
            Check(Get<object>(mission, "m_completedObjectiveIndices") == null && !Dirty(mission), "empty objective list null without dirty");
            long timestamp = Get<long>(mission, "m_completedAt"); mission.CompletedAt = default(DateTime); mission.MarkSaved(); mission.OnAfterDeserialize();
            Check(mission.CompletedAt == Hardlight.TimeUtils.FromUnixTime(timestamp) && mission.CompletedAt.Kind == DateTimeKind.Utc && !Dirty(mission), "after callback milliseconds/UTC without dirty");
            var indices = new List<int> { 3, 8 }; Set(mission, "m_completedObjectiveIndices", indices); mission.OnBeforeSerialize();
            Check(ReferenceEquals(indices, Get<object>(mission, "m_completedObjectiveIndices")), "nonempty objective list retained");
            Set(mission, "m_completedAt", long.MaxValue);
            Throws<ArgumentOutOfRangeException>(mission.OnAfterDeserialize, "timestamp range failure propagated");
        }
        public static int Run()
        {
            checks = 0;
            Layouts(); ConstructorsAndRepair(); Setters(); Callbacks(); Traversal(); Dates();
            return checks;
        }
    }
}
