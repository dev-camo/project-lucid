using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Checks use the complete original record owners and their genuine dirty-state
    // base. Reflection authors private serialized state; it supplies no provider.
    public static class OriginalSaveRecordMergeVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private sealed class Checks
        {
            public int Count;
            public void That(bool value, string reason)
            {
                if (!value) throw new InvalidOperationException(reason);
                ++Count;
            }
            public void Throws<T>(Action action, string reason) where T : Exception
            {
                Exception failure = null;
                try { action(); } catch (Exception error) { failure = error; }
                That(failure != null && failure.GetType() == typeof(T), reason);
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                FieldInfo result = cursor.GetField(name, Own);
                if (result != null) return result;
            }
            throw new MissingFieldException(type.FullName, name);
        }
        private static void Write(object value, string name, object authored)
        { Field(value.GetType(), name).SetValue(value, authored); }
        private static T Read<T>(object value, string name)
        { return (T)Field(value.GetType(), name).GetValue(value); }
        private static bool Dirty(SaveDataItem value) => Read<bool>(value, "m_dirty");
        private static bool SavingEnabled(SaveDataItem value) => Read<bool>(value, "m_savingEnabled");
        private static Dictionary<string, SaveDataDreamPowerStoreItem> Map(SaveDataDreamPowerStore value)
        { return Read<Dictionary<string, SaveDataDreamPowerStoreItem>>(value, "m_dreamPowersByGuid"); }

        public static int RunOriginalDeclarationsAndDefault()
        {
            var check = new Checks();
            Type[] owners = { typeof(SaveDataBonusZone1), typeof(SaveDataCharacterArchetype),
                typeof(SaveDataCharacterCustomisation), typeof(SaveDataCharacterSkin), typeof(SaveDataCollectable),
                typeof(SaveDataDreamPowerStore), typeof(SaveDataDreamPowerStoreItem), typeof(SaveDataMusicTrack),
                typeof(SaveDataOrnament), typeof(SaveDataPlayerStat) };
            int[] methods = { 7, 10, 6, 10, 6, 10, 6, 8, 10, 8 };
            int[] fields = { 2, 3, 2, 3, 2, 3, 2, 2, 3, 2 };
            for (int i = 0; i < owners.Length; ++i)
            {
                Type owner = owners[i];
                check.That(owner.IsPublic && owner.IsClass && !owner.IsAbstract && !owner.IsSealed, owner.Name + " original public owner");
                check.That(owner.BaseType == typeof(SaveDataItem), owner.Name + " genuine original base");
                check.That(owner.IsSerializable, owner.Name + " original serialization attribute");
                check.That(owner.GetFields(Own).Length == fields[i], owner.Name + " complete local field count");
                check.That(owner.GetMethods(Own).Length + owner.GetConstructors(Own).Length == methods[i], owner.Name + " complete direct API count");
                check.That(owner.GetCustomAttributesData().Count(a => a.AttributeType.FullName == "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute") == 2,
                    owner.Name + " original IL2CPP options");
                MethodInfo merge = owner.GetMethod("ResolveNewData", Own);
                check.That(merge != null && merge.IsPublic && !merge.IsStatic && merge.ReturnType == typeof(void), owner.Name + " original merge API");
                ParameterInfo[] parameters = merge.GetParameters();
                check.That(parameters.Length == 1 && parameters[0].ParameterType == owner && parameters[0].Name == "newSaveData" && !parameters[0].IsOptional,
                    owner.Name + " original merge parameter");
            }
            MethodInfo increment = typeof(SaveDataPlayerStat).GetMethod("IncrementCounter", Own);
            ParameterInfo value = increment.GetParameters().Single();
            check.That(increment.IsPublic && !increment.IsStatic && increment.ReturnType == typeof(void), "original increment API");
            check.That(value.Name == "value" && value.ParameterType == typeof(long), "original signed Int64 parameter");
            check.That(value.IsOptional && value.HasDefaultValue && value.DefaultValue is long && (long)value.DefaultValue == 1L, "original optional Int64 one");
            MethodInfo set = typeof(SaveDataCharacterCustomisation).GetMethod("Set", Own);
            check.That(set.IsPublic && !set.IsStatic && set.ReturnType == typeof(void) && set.GetParameters().Length == 1 &&
                set.GetParameters()[0].Name == "skinDefinition" && set.GetParameters()[0].ParameterType == typeof(CharacterSkinDefinition), "original skin Set API");
            MethodInfo create = typeof(SaveDataDreamPowerStore).GetMethod("GetOrCreateDreamPowerData", Own);
            check.That(create.IsPublic && !create.IsStatic && create.ReturnType == typeof(SaveDataDreamPowerStoreItem) &&
                create.GetParameters().Length == 1 && create.GetParameters()[0].Name == "dreamPowerGuid" && create.GetParameters()[0].ParameterType == typeof(string), "original store creation API");
            check.That((int)BonusZoneIsNewState.None == 0 && (int)BonusZoneIsNewState.IsNew == -1216131765 && (int)BonusZoneIsNewState.Seen == 1832392787,
                "original bonus state literals");
            check.That((int)DreamPowerStoreIsNewState.None == 0 && (int)DreamPowerStoreIsNewState.IsNew == -1216131765 && (int)DreamPowerStoreIsNewState.Seen == 1832392787,
                "original store state literals");
            var stat = new SaveDataPlayerStat((SaveDataPlayerStat.Type)123);
            check.That(stat.StatType == (SaveDataPlayerStat.Type)123 && stat.PlayerStatCounter == 0 && SavingEnabled(stat) && !Dirty(stat), "real counter constructor defaults");
            var store = new SaveDataDreamPowerStore();
            check.That(store.DreamPowers != null && store.DreamPowers.Count == 0 && Map(store).Count == 0 && !Dirty(store) && SavingEnabled(store), "real store constructor defaults");
            var item = new SaveDataDreamPowerStoreItem(null);
            check.That(item.GUID == null && !item.Bought && !Dirty(item) && SavingEnabled(item), "real item permits authored null GUID");
            return check.Count;
        }

        private static void CheckFlags(Checks check, SaveDataItem target, SaveDataItem incoming,
            Action merge, string[] fieldNames, bool[] expected, object identity, Func<object> readIdentity)
        {
            bool dirty = Dirty(target);
            bool incomingDirty = Dirty(incoming);
            merge();
            for (int i = 0; i < fieldNames.Length; ++i)
                check.That(Read<bool>(target, fieldNames[i]) == expected[i], "fixed unlock/seen truth table");
            check.That(ReferenceEquals(readIdentity(), identity) || Equals(readIdentity(), identity), "merge retains destination identity");
            check.That(Dirty(target) == dirty && Dirty(incoming) == incomingDirty, "merge retains both raw dirty flags");
            check.That(SavingEnabled(target), "merge retains saving enabled");
        }

        public static int RunUnlockAndSeenMerges()
        {
            var check = new Checks();
            bool[] truth = { false, true, true, true };
            for (int row = 0; row < 4; ++row)
            foreach (bool priorDirty in new[] { false, true })
            {
                bool oldFlag = row >= 2, newFlag = row % 2 != 0;
                var ornament = new SaveDataOrnament("destination"); var nextOrnament = new SaveDataOrnament("different");
                Write(ornament, "m_unlocked", oldFlag); Write(ornament, "m_unlockSeen", !oldFlag);
                Write(nextOrnament, "m_unlocked", newFlag); Write(nextOrnament, "m_unlockSeen", newFlag);
                if (priorDirty) ornament.MarkDirty();
                CheckFlags(check, ornament, nextOrnament, () => ornament.ResolveNewData(nextOrnament), new[] { "m_unlocked", "m_unlockSeen" },
                    new[] { truth[row], truth[(!oldFlag ? 2 : 0) + (newFlag ? 1 : 0)] }, ornament.GUID, () => ornament.GUID);
                var skin = new SaveDataCharacterSkin("destination"); var nextSkin = new SaveDataCharacterSkin("different");
                Write(skin, "m_unlocked", oldFlag); Write(skin, "m_unlockSeen", oldFlag);
                Write(nextSkin, "m_unlocked", newFlag); Write(nextSkin, "m_unlockSeen", newFlag);
                if (priorDirty) skin.MarkDirty();
                CheckFlags(check, skin, nextSkin, () => skin.ResolveNewData(nextSkin), new[] { "m_unlocked", "m_unlockSeen" },
                    new[] { truth[row], truth[row] }, skin.GUID, () => skin.GUID);
                var archetype = new SaveDataCharacterArchetype((CharacterArchetype)123); var nextArchetype = new SaveDataCharacterArchetype((CharacterArchetype)456);
                Write(archetype, "m_unlocked", oldFlag); Write(archetype, "m_unlockSeen", oldFlag);
                Write(nextArchetype, "m_unlocked", newFlag); Write(nextArchetype, "m_unlockSeen", newFlag);
                if (priorDirty) archetype.MarkDirty();
                CheckFlags(check, archetype, nextArchetype, () => archetype.ResolveNewData(nextArchetype), new[] { "m_unlocked", "m_unlockSeen" },
                    new[] { truth[row], truth[row] }, archetype.CharacterArchetype, () => archetype.CharacterArchetype);
                var music = new SaveDataMusicTrack("destination"); var nextMusic = new SaveDataMusicTrack("different");
                Write(music, "m_seen", oldFlag); Write(nextMusic, "m_seen", newFlag); if (priorDirty) music.MarkDirty();
                CheckFlags(check, music, nextMusic, () => music.ResolveNewData(nextMusic), new[] { "m_seen" }, new[] { truth[row] }, music.GUID, () => music.GUID);
                var item = new SaveDataDreamPowerStoreItem("destination"); var nextItem = new SaveDataDreamPowerStoreItem("different");
                Write(item, "m_bought", oldFlag); Write(nextItem, "m_bought", newFlag); if (priorDirty) item.MarkDirty();
                CheckFlags(check, item, nextItem, () => item.ResolveNewData(nextItem), new[] { "m_bought" }, new[] { truth[row] }, item.GUID, () => item.GUID);
            }
            var unchanged = new SaveDataOrnament("null boundary");
            check.Throws<NullReferenceException>(() => unchanged.ResolveNewData(null), "null merge record faults");
            check.That(!Dirty(unchanged) && !unchanged.Unlocked && !unchanged.UnlockSeen, "null merge keeps preceding fields");
            unchanged.DisableSaving(); unchanged.Unlocked = true;
            check.That(Dirty(unchanged) && !unchanged.HasChangesToSave(), "genuine dirty flag is separate from saving enablement");
            unchanged.MarkSaved();
            check.That(Dirty(unchanged), "genuine disabled record retains dirty bit on MarkSaved");
            return check.Count;
        }

        public static int RunSignedStatAndCollectableBoundaries()
        {
            var check = new Checks();
            var value = new SaveDataPlayerStat(SaveDataPlayerStat.Type.AirTimeMS);
            value.IncrementCounter();
            check.That(value.PlayerStatCounter == 1 && Dirty(value), "default increment one dirties");
            value.MarkSaved(); value.IncrementCounter(0);
            check.That(value.PlayerStatCounter == 1 && !Dirty(value), "zero increment leaves clean record untouched");
            value.MarkDirty(); value.IncrementCounter(0);
            check.That(value.PlayerStatCounter == 1 && Dirty(value), "zero increment preserves existing dirtiness");
            long[] start = { 0, 9, long.MaxValue, long.MinValue, long.MaxValue, long.MinValue, -5, 5 };
            long[] add = { -1, -20, 1, -1, long.MaxValue, long.MinValue, 5, -5 };
            long[] expected = { -1, -11, long.MinValue, long.MaxValue, -2, 0, 0, 0 };
            for (int i = 0; i < start.Length; ++i)
            {
                Write(value, "m_playerStatCounter", start[i]); value.MarkSaved(); value.IncrementCounter(add[i]);
                check.That(value.PlayerStatCounter == expected[i], "fixed signed/unchecked addition vector");
                check.That(Dirty(value) && value.StatType == SaveDataPlayerStat.Type.AirTimeMS, "nonzero increment dirties and retains stat type");
            }
            long[] left = { long.MinValue, long.MaxValue, -9, -1, 0, 3 };
            long[] right = { long.MaxValue, long.MinValue, -1, -9, 0, 3 };
            long[] maximum = { long.MaxValue, long.MaxValue, -1, -1, 0, 3 };
            for (int i = 0; i < left.Length; ++i)
            foreach (bool dirty in new[] { false, true })
            {
                var next = new SaveDataPlayerStat(SaveDataPlayerStat.Type.RingsCollected);
                Write(value, "m_playerStatCounter", left[i]); Write(next, "m_playerStatCounter", right[i]); value.MarkSaved();
                if (dirty) value.MarkDirty(); value.ResolveNewData(next);
                check.That(value.PlayerStatCounter == maximum[i], "signed Int64 Math.Max merge");
                check.That(Dirty(value) == dirty && !Dirty(next), "counter merge does not dirty either record");
                check.That(value.StatType == SaveDataPlayerStat.Type.AirTimeMS, "counter merge retains destination stat identity");
            }
            check.Throws<NullReferenceException>(() => value.ResolveNewData(null), "null counter merge faults");
            value.DisableSaving(); value.IncrementCounter(1);
            check.That(Dirty(value) && !value.HasChangesToSave(), "nonzero addition still dirties disabled record");
            int[] spentLeft = { int.MinValue, int.MaxValue, -9, -1, 0, 9 };
            int[] spentRight = { int.MaxValue, int.MinValue, -1, -9, 0, 9 };
            int[] spentExpected = { int.MaxValue, int.MaxValue, -1, -1, 0, 9 };
            for (int i = 0; i < spentLeft.Length; ++i)
            {
                var spent = new SaveDataCollectable(); var incoming = new SaveDataCollectable();
                Write(spent, "m_spent", spentLeft[i]); Write(incoming, "m_spent", spentRight[i]); Write(spent, "m_guid", "original"); Write(incoming, "m_guid", "incoming");
                spent.ResolveNewData(incoming);
                check.That(spent.Spent == spentExpected[i] && spent.GUID == "original", "fixed signed Int32 maximum retains identity");
                check.That(!Dirty(spent) && !Dirty(incoming), "spent merge does not dirty records");
            }
            return check.Count;
        }

        public static int RunOriginalStateTransitions()
        {
            var check = new Checks();
            int[] states = { 0, -1216131765, 1832392787, 12345 };
            int[,] expected = { { 0, -1216131765, 1832392787, 0 }, { -1216131765, -1216131765, 1832392787, -1216131765 },
                { 1832392787, 1832392787, 1832392787, 1832392787 }, { 12345, 12345, 1832392787, 12345 } };
            for (int row = 0; row < 4; ++row)
            for (int col = 0; col < 4; ++col)
            foreach (bool dirty in new[] { false, true })
            {
                var zone = new SaveDataBonusZone1(); var nextZone = new SaveDataBonusZone1();
                Write(zone, "m_isNewState", (BonusZoneIsNewState)states[row]); Write(nextZone, "m_isNewState", (BonusZoneIsNewState)states[col]);
                Write(zone, "m_unlockSeen", row % 2 == 0); Write(nextZone, "m_unlockSeen", col % 2 == 0); if (dirty) zone.MarkDirty();
                zone.ResolveNewData(nextZone);
                check.That((int)zone.IsNewState == expected[row, col], "fixed bonus state table including unknown literals");
                check.That(zone.UnlockSeen == (row % 2 == 0 || col % 2 == 0), "bonus unlockSeen OR precedes state merge");
                check.That(Dirty(zone) == dirty && !Dirty(nextZone), "bonus merge preserves raw dirty state");
                var store = new SaveDataDreamPowerStore(); var nextStore = new SaveDataDreamPowerStore();
                Write(store, "m_isNewState", (DreamPowerStoreIsNewState)states[row]); Write(nextStore, "m_isNewState", (DreamPowerStoreIsNewState)states[col]); if (dirty) store.MarkDirty();
                store.ResolveNewData(nextStore);
                check.That((int)store.IsNewState == expected[row, col], "fixed store state table including unknown literals");
                check.That(Dirty(store) == dirty && !Dirty(nextStore), "store state-only merge does not dirty");
            }
            return check.Count;
        }

        public static int RunCustomisationManagedFaults()
        {
            var check = new Checks();
            var target = new SaveDataCharacterCustomisation((CharacterId)123); var incoming = new SaveDataCharacterCustomisation((CharacterId)456);
            Write(target, "m_skinGUID", "before");
            check.Throws<NullReferenceException>(() => target.Set(null), "CLR null skin faults at GetGUID");
            check.That(target.SkinGUID == "before" && !Dirty(target), "GetGUID fault precedes assignment and MarkDirty");
            target.MarkDirty();
            check.Throws<NullReferenceException>(() => target.Set(null), "dirty destination still faults on CLR null skin");
            check.That(target.SkinGUID == "before" && Dirty(target), "failed Set retains previous dirtiness");
            target.MarkSaved();
            string authored = new string(new[] { 's', 'k', 'i', 'n' }); Write(incoming, "m_skinGUID", authored);
            target.ResolveNewData(incoming);
            check.That(ReferenceEquals(target.SkinGUID, authored) && target.CharacterId == (CharacterId)123, "customisation copies exact source GUID reference only");
            check.That(!Dirty(target) && !Dirty(incoming), "customisation merge does not dirty");
            Write(incoming, "m_skinGUID", null); target.ResolveNewData(incoming);
            check.That(target.SkinGUID == null && !Dirty(target), "customisation accepts source null GUID without fallback");
            Write(target, "m_skinGUID", "null failure");
            check.Throws<NullReferenceException>(() => target.ResolveNewData(null), "null customisation merge faults");
            check.That(target.SkinGUID == "null failure" && !Dirty(target), "null merge retains state");
            return check.Count;
        }

        public static int RunCustomisationUnityGUIDBoundaries()
        {
            var check = new Checks();
            CharacterSkinDefinition skin = ScriptableObject.CreateInstance<CharacterSkinDefinition>();
            try
            {
                string authored = new string(new[] { 'a', 'u', 't', 'h', 'o', 'r', 'e', 'd' });
                Write(skin, "m_guid", authored);
                var target = new SaveDataCharacterCustomisation((CharacterId)123);
                target.Set(skin);
                check.That(ReferenceEquals(target.SkinGUID, authored) && Dirty(target), "real skin GetGUID supplies exact reference before dirty");
                target.MarkSaved(); Write(skin, "m_guid", null); target.Set(skin);
                check.That(target.SkinGUID == null && Dirty(target), "authored null GUID is assigned and dirtied");
                target.MarkSaved(); Write(skin, "m_guid", authored);
                UnityEngine.Object.DestroyImmediate(skin);
                check.That((UnityEngine.Object)skin == null && !ReferenceEquals(skin, null), "real destroyed Unity object retains CLR reference");
                target.Set(skin);
                check.That(ReferenceEquals(target.SkinGUID, authored) && Dirty(target), "original direct managed GetGUID retains destroyed-object path");
                target.DisableSaving(); target.Set(skin);
                check.That(Dirty(target) && !target.HasChangesToSave(), "Set dirties destroyed-object path even when saving disabled");
            }
            finally { if ((UnityEngine.Object)skin != null) UnityEngine.Object.DestroyImmediate(skin); }
            return check.Count;
        }

        public static int RunStoreCreationAndDirtyBoundaries()
        {
            var check = new Checks();
            var store = new SaveDataDreamPowerStore();
            var list = store.DreamPowers; var map = Map(store);
            var created = store.GetOrCreateDreamPowerData("power");
            check.That(created.GUID == "power" && !created.Bought && SavingEnabled(created), "missing entry uses genuine item constructor");
            check.That(ReferenceEquals(map["power"], created) && ReferenceEquals(Map(store), map), "new item installed into existing dictionary");
            check.That(Dirty(store) && !Dirty(created), "only creation dirties parent");
            check.That(ReferenceEquals(store.DreamPowers, list) && list.Count == 0, "creation does not populate serialized list");
            store.MarkSaved();
            check.That(ReferenceEquals(store.GetOrCreateDreamPowerData("power"), created) && !Dirty(store), "existing lookup retains identity without dirty");
            check.Throws<ArgumentNullException>(() => store.GetOrCreateDreamPowerData(null), "null key faults before creation");
            check.That(map.Count == 1 && !Dirty(store) && list.Count == 0, "null key preserves dictionary list and dirty bit");
            var empty = store.GetOrCreateDreamPowerData("");
            check.That(empty.GUID == "" && ReferenceEquals(map[""], empty) && Dirty(store), "empty string remains a valid distinct key");
            store.MarkSaved(); map["present-null"] = null;
            check.That(store.GetOrCreateDreamPowerData("present-null") == null && !Dirty(store), "present null value is returned without replacement");
            map.Remove("present-null");
            created.Bought = true;
            check.That(!Dirty(store) && Dirty(created), "child setter dirties child before parent query");
            check.That(store.HasChangesToSave() && Dirty(store), "real base child query bubbles dirty state");
            store.MarkSaved();
            check.That(!Dirty(store) && !Dirty(created), "real MarkSaved clears parent and dictionary children");
            store.DisableSaving(); store.GetOrCreateDreamPowerData("disabled");
            check.That(Dirty(store) && !store.HasChangesToSave(), "creation still dirties disabled parent");
            return check.Count;
        }

        public static int RunStoreMergeAliasingAndPartialFaults()
        {
            var check = new Checks();
            var target = new SaveDataDreamPowerStore(); var incoming = new SaveDataDreamPowerStore();
            var current = target.GetOrCreateDreamPowerData("same"); var added = incoming.GetOrCreateDreamPowerData("added");
            var same = incoming.GetOrCreateDreamPowerData("same"); Write(same, "m_bought", true);
            Write(incoming, "m_isNewState", DreamPowerStoreIsNewState.Seen); target.MarkSaved(); incoming.MarkSaved();
            var targetList = target.DreamPowers;
            target.ResolveNewData(incoming);
            check.That(ReferenceEquals(Map(target)["same"], current) && current.Bought, "existing item merges Bought without replacing identity");
            check.That(ReferenceEquals(Map(target)["added"], added), "new entry aliases exact source record");
            check.That(!Dirty(target) && !Dirty(current) && !Dirty(added), "record and dictionary merge does not dirty");
            check.That(ReferenceEquals(target.DreamPowers, targetList) && targetList.Count == 0, "merge leaves serialized list stale");
            check.That(target.IsNewState == DreamPowerStoreIsNewState.Seen, "state applies after dictionary merge");
            added.Bought = true;
            check.That(Map(target)["added"].Bought && Dirty(Map(target)["added"]) && !Dirty(target), "later source item mutation is visible through alias before query");
            target.MarkSaved();
            check.That(!Dirty(added) && !Dirty(target), "saving destination clears aliased source child");
            var before = Map(target).ToArray(); target.ResolveNewData(target);
            check.That(Map(target).Count == before.Length && before.All(pair => ReferenceEquals(Map(target)[pair.Key], pair.Value)), "self-merge retains every map identity");
            check.That(!Dirty(target) && target.DreamPowers.Count == 0, "self-merge does not dirty or synchronize list");
            check.Throws<NullReferenceException>(() => target.ResolveNewData(null), "null store merge faults before mutation");
            check.That(Map(target).Count == before.Length && !Dirty(target), "null store preserves destination state");

            var prefix = new SaveDataDreamPowerStore(); var broken = new SaveDataDreamPowerStore();
            Map(broken).Add("first", new SaveDataDreamPowerStoreItem("first"));
            Map(broken).Add("second", new SaveDataDreamPowerStoreItem("second"));
            string[] order = Map(broken).Keys.ToArray();
            SaveDataDreamPowerStoreItem first = Map(broken)[order[0]];
            Map(broken)[order[1]] = null;
            Map(prefix).Add(order[1], new SaveDataDreamPowerStoreItem("existing fault destination"));
            Write(broken, "m_isNewState", DreamPowerStoreIsNewState.Seen);
            check.Throws<NullReferenceException>(() => prefix.ResolveNewData(broken), "later source null item faults at existing child merge");
            check.That(ReferenceEquals(Map(prefix)[order[0]], first), "earlier dictionary alias insertion survives later fault");
            check.That(prefix.IsNewState == DreamPowerStoreIsNewState.None && !Dirty(prefix), "fault precedes state update and no dirty is introduced");
            check.That(prefix.DreamPowers.Count == 0, "partial merge retains stale serialized list");
            var insertedNull = new SaveDataDreamPowerStore();
            insertedNull.ResolveNewData(broken);
            check.That(Map(insertedNull).ContainsKey(order[1]) && Map(insertedNull)[order[1]] == null, "missing key aliases incoming null value without validation");
            check.That(insertedNull.IsNewState == DreamPowerStoreIsNewState.Seen && !Dirty(insertedNull), "null alias insertion itself permits final state update");
            return check.Count;
        }

        public static int RunStoreSerializationCallbacks()
        {
            var check = new Checks();
            var store = new SaveDataDreamPowerStore(); var first = store.GetOrCreateDreamPowerData("a"); var second = store.GetOrCreateDreamPowerData("b"); store.MarkSaved();
            var list = store.DreamPowers;
            store.OnBeforeSerialize();
            check.That(ReferenceEquals(store.DreamPowers, list) && list.Count == 2, "before serialize retains existing list identity");
            check.That(list.Contains(first) && list.Contains(second) && !Dirty(store), "dictionary values populate list without dirty");
            Map(store).Remove("a"); store.OnBeforeSerialize();
            check.That(list.Count == 1 && ReferenceEquals(list[0], second), "before serialize clears obsolete serialized entries");
            Write(store, "m_dreamPowers", null); store.OnBeforeSerialize();
            check.That(store.DreamPowers != null && store.DreamPowers.Count == 1 && ReferenceEquals(store.DreamPowers[0], second), "null serialized list is created from map");

            var duplicate = new SaveDataDreamPowerStore(); var one = new SaveDataDreamPowerStoreItem("same"); var two = new SaveDataDreamPowerStoreItem("same"); Write(two, "m_bought", true);
            duplicate.DreamPowers.Add(one); duplicate.DreamPowers.Add(two); Map(duplicate).Add("obsolete", new SaveDataDreamPowerStoreItem("obsolete"));
            duplicate.OnAfterDeserialize();
            check.That(Map(duplicate).Count == 1 && ReferenceEquals(Map(duplicate)["same"], two), "after deserialize clears dictionary and last duplicate wins");
            check.That(duplicate.DreamPowers.Count == 2 && ReferenceEquals(duplicate.DreamPowers[0], one), "deserialization retains authored duplicate list");
            check.That(!Dirty(duplicate) && !Dirty(one) && !Dirty(two), "callbacks do not dirty real constructed records");
            duplicate.MarkDirty(); duplicate.OnAfterDeserialize();
            check.That(Dirty(duplicate), "callback retains preexisting dirty flag");

            var partial = new SaveDataDreamPowerStore(); var valid = new SaveDataDreamPowerStoreItem("prefix");
            partial.DreamPowers.Add(valid); partial.DreamPowers.Add(null); Map(partial).Add("obsolete", new SaveDataDreamPowerStoreItem("obsolete"));
            check.Throws<NullReferenceException>(() => partial.OnAfterDeserialize(), "null authored list entry faults in original key getter");
            check.That(Map(partial).Count == 1 && ReferenceEquals(Map(partial)["prefix"], valid), "clear and valid prefix survive callback fault");
            check.That(partial.DreamPowers.Count == 2 && !Dirty(partial), "callback fault leaves original list and dirty flag");
            partial.DreamPowers[1] = new SaveDataDreamPowerStoreItem(null);
            check.Throws<ArgumentNullException>(() => partial.OnAfterDeserialize(), "null GUID faults at original dictionary insertion");
            check.That(Map(partial).Count == 1 && ReferenceEquals(Map(partial)["prefix"], valid), "null-key callback retains valid prefix");

            var preinitialiseFault = new SaveDataDreamPowerStore(); Map(preinitialiseFault).Add("null child", null);
            preinitialiseFault.DreamPowers.Add(valid);
            check.Throws<NullReferenceException>(() => preinitialiseFault.OnAfterDeserialize(), "real Initialise traverses old dictionary before import");
            check.That(Map(preinitialiseFault).Count == 1 && Map(preinitialiseFault).ContainsKey("null child"), "initial child fault occurs before dictionary clear");
            var recreate = new SaveDataDreamPowerStore(); Write(recreate, "m_dreamPowersByGuid", null); recreate.DreamPowers.Add(valid); recreate.OnAfterDeserialize();
            check.That(ReferenceEquals(Map(recreate)["prefix"], valid) && !Dirty(recreate), "Initialise recreates genuine null dictionary before import");
            Write(recreate, "m_dreamPowers", null); recreate.OnAfterDeserialize();
            check.That(recreate.DreamPowers.Count == 0 && Map(recreate).Count == 0, "null list recreated empty and then clears old map");
            return check.Count;
        }

        public static int RunUnitySerializationBoundaries()
        {
            var check = new Checks();
            var store = new SaveDataDreamPowerStore(); var item = store.GetOrCreateDreamPowerData("serialized"); item.Bought = true;
            store.IsNewState = DreamPowerStoreIsNewState.Seen;
            string json = JsonUtility.ToJson(store);
            check.That(json.Contains("\"m_dreamPowers\"") && json.Contains("\"m_guid\":\"serialized\"") && json.Contains("\"m_bought\":true"), "actual Unity serializes original record fields and invokes list callback");
            check.That(!json.Contains("m_dreamPowersByGuid") && !json.Contains("m_dirty") && !json.Contains("m_savingEnabled"), "runtime dictionary and dirty flags excluded from JSON");
            check.That(store.DreamPowers.Count == 1 && ReferenceEquals(store.DreamPowers[0], item), "real serializer callback retains source item identity");
            var restored = JsonUtility.FromJson<SaveDataDreamPowerStore>(json);
            var restoredItem = restored.GetOrCreateDreamPowerData("serialized");
            check.That(restoredItem.Bought && restored.IsNewState == DreamPowerStoreIsNewState.Seen, "actual Unity roundtrip preserves bought/state data");
            check.That(restored.DreamPowers.Count == 1 && ReferenceEquals(restored.DreamPowers[0], restoredItem), "real callback reconstructs dictionary from serialized child identities");
            check.That(SavingEnabled(restored) && !SavingEnabled(restoredItem) && !Dirty(restored) && !Dirty(restoredItem), "actual default-root and parameterized-child runtime flag distinction");
            restoredItem.Bought = false;
            check.That(Dirty(restoredItem) && !restoredItem.HasChangesToSave() && !restored.HasChangesToSave(), "disabled deserialized child does not bubble through enabled clean root");
            string duplicates = "{\"m_dreamPowers\":[{\"m_guid\":\"same\",\"m_bought\":false},{\"m_guid\":\"same\",\"m_bought\":true}]}";
            var duplicate = JsonUtility.FromJson<SaveDataDreamPowerStore>(duplicates);
            check.That(duplicate.GetOrCreateDreamPowerData("same").Bought && ReferenceEquals(duplicate.GetOrCreateDreamPowerData("same"), duplicate.DreamPowers[1]), "actual Unity after-deserialize last duplicate alias wins");
            var stat = new SaveDataPlayerStat(SaveDataPlayerStat.Type.BoostTimeMS); stat.IncrementCounter(long.MinValue);
            string statJson = JsonUtility.ToJson(stat); var restoredStat = JsonUtility.FromJson<SaveDataPlayerStat>(statJson);
            check.That(restoredStat.PlayerStatCounter == long.MinValue && restoredStat.StatType == SaveDataPlayerStat.Type.BoostTimeMS, "actual Unity retains signed Int64 endpoint and enum");
            check.That(!SavingEnabled(restoredStat) && !Dirty(restoredStat), "parameterized-only stat does not acquire constructor runtime flags");
            restoredStat.IncrementCounter(-1);
            check.That(restoredStat.PlayerStatCounter == long.MaxValue && Dirty(restoredStat) && !restoredStat.HasChangesToSave(), "restored disabled stat retains unchecked increment and raw dirty semantics");
            return check.Count;
        }
    }
}
