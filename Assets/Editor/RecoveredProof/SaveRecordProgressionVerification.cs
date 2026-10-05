using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded native-derived original record checks. This helper does not open
    // original saves, load content, register fake managers or prove save slots.
    public static class SaveRecordProgressionVerification
    {
        private static int checks;
        private static void Check(bool condition, string label)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException("Save record proof: " + label);
        }
        private static FieldInfo Field(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException(name);
        }
        private static T Get<T>(object instance, string name) => (T)Field(instance, name).GetValue(instance);
        private static void Set(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);
        private static void Clean(SaveDataItem item) => Set(item, "m_dirty", false);
        private static bool Dirty(SaveDataItem item) => Get<bool>(item, "m_dirty");
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            bool threw = false;
            try { action(); } catch (T) { threw = true; }
            Check(threw, label);
        }
        private static SaveDataLevelMission Mission(string guid, params int[] objectives)
        {
            var result = new SaveDataLevelMission(guid);
            Set(result, "m_completedObjectiveIndices", new List<int>(objectives));
            return result;
        }

        public static int RunManaged()
        {
            checks = 0;
            var complete = new SaveDataLevelMission("complete");
            DateTime before = DateTime.Now;
            complete.MarkComplete(-7);
            DateTime after = DateTime.Now;
            Check(complete.Complete && complete.Progress == -7 && Dirty(complete), "completion preserves arbitrary progress and dirty");
            Check(complete.CompletedAt.Kind == DateTimeKind.Local && complete.CompletedAt >= before && complete.CompletedAt <= after, "completion local Now");
            Check(Get<long>(complete, "m_completedAt") == 0, "completion leaves serialized timestamp until callback");
            DateTime originalTime = complete.CompletedAt;
            Clean(complete); complete.MarkComplete(99);
            Check(complete.CompletedAt == originalTime && complete.Progress == -7 && !Dirty(complete), "completed record no-op");
            Check(complete.IsObjectiveComplete(int.MinValue) && complete.IsObjectiveComplete(int.MaxValue), "complete implies all indices");
            complete.MarkObjectiveComplete(4); complete.MarkObjectiveIncomplete(4);
            Check(Get<List<int>>(complete, "m_completedObjectiveIndices") == null && !Dirty(complete), "completed objectives no allocation or dirty");

            var objective = new SaveDataLevelMission("objectives");
            Check(!objective.IsObjectiveComplete(0), "null objective list is incomplete");
            objective.MarkObjectiveIncomplete(0);
            Check(!Dirty(objective) && objective.CompletedObjectiveIndices == null, "null remove no-op");
            objective.MarkObjectiveComplete(-2);
            Check(objective.CompletedObjectiveIndices.Count == 1 && objective.CompletedObjectiveIndices[0] == -2 && Dirty(objective), "negative objective allocated/appended");
            Clean(objective); objective.MarkObjectiveComplete(-2);
            Check(!Dirty(objective) && objective.CompletedObjectiveIndices.Count == 1, "duplicate objective clean");
            objective.MarkObjectiveComplete(int.MaxValue);
            Check(objective.CompletedObjectiveIndices.Count == 2 && objective.CompletedObjectiveIndices[1] == int.MaxValue, "objective insertion order");
            Clean(objective); objective.MarkObjectiveIncomplete(7);
            Check(!Dirty(objective), "missing objective remove clean");
            objective.MarkObjectiveIncomplete(-2);
            Check(Dirty(objective) && !objective.IsObjectiveComplete(-2) && objective.IsObjectiveComplete(int.MaxValue), "objective removal marks dirty");
            Set(objective, "m_completedObjectiveIndices", new List<int> { 3, 3 });
            Clean(objective); objective.MarkObjectiveIncomplete(3);
            Check(objective.CompletedObjectiveIndices.Count == 1 && objective.CompletedObjectiveIndices[0] == 3 && Dirty(objective), "remove only first duplicate");
            Set(objective, "m_attempts", int.MaxValue); Clean(objective); objective.MarkNewAttempt();
            Check(objective.Attempts == int.MinValue && Dirty(objective), "attempt overflow wraps int32");
            objective.DisableSaving(); objective.MarkNewAttempt();
            Check(objective.Attempts == int.MinValue + 1 && Dirty(objective) && !objective.HasChangesToSave(), "attempt mutation respects separate saving gate");

            var local = Mission("same", 2); var remote = Mission("same", 4, 2, 4, -1);
            Set(local, "m_completedAt", 90L); Set(remote, "m_completedAt", 15L);
            Set(local, "m_completedAtTime", new DateTime(2011, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            Set(remote, "m_complete", true); Set(remote, "m_levelSelectUnlockSeen", true); Set(remote, "m_timeTrialUnlockSeen", true);
            Set(remote, "m_isNewState", MissionIsNewState.IsNew); Set(local, "m_attempts", 3); Set(remote, "m_attempts", 8);
            Set(local, "m_xpEarned", 7); Set(remote, "m_xpEarned", 6); Set(local, "m_bestTimeSeconds", 12f); Set(remote, "m_bestTimeSeconds", 9f);
            DateTime cached = local.CompletedAt; local.ResolveNewData(remote);
            Check(local.CompletedObjectiveIndices.Count == 3 && local.CompletedObjectiveIndices[0] == 2 && local.CompletedObjectiveIndices[1] == 4 && local.CompletedObjectiveIndices[2] == -1, "merge objective ordered union");
            Check(local.Complete && local.LevelSelectUnlockSeen && local.TimeTrialUnlockSeen, "merge boolean OR");
            Check(Get<long>(local, "m_completedAt") == 15 && local.CompletedAt == cached, "merge min serialized timestamp preserves cache");
            Check(local.Progress == 3 && local.IsNewState == MissionIsNewState.IsNew && local.Attempts == 8 && local.XPEarned == 7 && local.BestTimeSeconds == 9f, "merge derived count/state/max/min");
            Check(Dirty(local), "objective additions make merged record dirty");
            foreach (MissionIsNewState starting in new[] { MissionIsNewState.None, MissionIsNewState.IsNew, MissionIsNewState.Seen, (MissionIsNewState)17 })
                foreach (MissionIsNewState incomingState in new[] { MissionIsNewState.None, MissionIsNewState.IsNew, MissionIsNewState.Seen, (MissionIsNewState)23 })
                {
                    var left = Mission("state"); var right = Mission("state");
                    Set(left, "m_isNewState", starting); Set(right, "m_isNewState", incomingState); Clean(left);
                    left.ResolveNewData(right);
                    var expected = incomingState == MissionIsNewState.Seen ? MissionIsNewState.Seen :
                        incomingState == MissionIsNewState.IsNew && starting == MissionIsNewState.None ? MissionIsNewState.IsNew : starting;
                    Check(left.IsNewState == expected && !Dirty(left), "state precedence without dirty " + starting + "/" + incomingState);
                }
            foreach (float oldTime in new[] { -1f, 0f, 5f, float.NaN, float.PositiveInfinity })
                foreach (float newTime in new[] { -2f, 0f, 3f, float.NaN, float.PositiveInfinity })
                {
                    var left = Mission("time"); var right = Mission("time");
                    Set(left, "m_bestTimeSeconds", oldTime); Set(right, "m_bestTimeSeconds", newTime);
                    float expected = newTime > 0f ? oldTime > 0f ? Math.Min(oldTime, newTime) : newTime : oldTime;
                    left.ResolveNewData(right);
                    Check((float.IsNaN(expected) ? float.IsNaN(left.BestTimeSeconds) : left.BestTimeSeconds == expected) && !Dirty(left), "positive best-time merge " + oldTime + "/" + newTime);
                }
            var invalid = new SaveDataLevelMission("null"); var untouched = Mission("target", 1);
            Set(invalid, "m_complete", true);
            Throws<NullReferenceException>(() => untouched.ResolveNewData(invalid), "null incoming objective list fails before fields");
            Check(!untouched.Complete && !Dirty(untouched), "merge failure retains earlier fields");
            var partial = new SaveDataLevelMission("partial"); var emptyIncoming = Mission("partial");
            Set(partial, "m_completedAt", 99L); Set(emptyIncoming, "m_completedAt", 13L); Set(emptyIncoming, "m_complete", true);
            Throws<NullReferenceException>(() => partial.ResolveNewData(emptyIncoming), "null local objective list fails at derived count");
            Check(partial.Complete && Get<long>(partial, "m_completedAt") == 13 && !Dirty(partial), "derived-count failure retains OR/min writes");
            var terminal = Mission("terminal", 1); var added = Mission("terminal", 2);
            Set(terminal, "m_complete", true); Clean(terminal); terminal.ResolveNewData(added);
            Check(terminal.Progress == 1 && terminal.CompletedObjectiveIndices.Count == 1 && !Dirty(terminal), "complete local prevents objective union");
            var copiedFrom = Mission("copy", -3, 7);
            Set(copiedFrom, "m_complete", true); Set(copiedFrom, "m_completedAt", 123L); Set(copiedFrom, "m_attempts", 5);
            Set(copiedFrom, "m_progress", 8); Set(copiedFrom, "m_isNewState", MissionIsNewState.Seen); Set(copiedFrom, "m_xpEarned", 99);
            Set(copiedFrom, "m_bestTimeSeconds", 12f); Set(copiedFrom, "m_completedAtTime", cached); Set(copiedFrom, "m_levelSelectUnlockSeen", true); Set(copiedFrom, "m_timeTrialUnlockSeen", true);
            copiedFrom.MarkDirty(); copiedFrom.DisableSaving(); var copy = copiedFrom.CreateCopy();
            Check(copy.GUID == "copy" && copy.Complete && copy.Progress == 8 && copy.Attempts == 5 && copy.IsNewState == MissionIsNewState.Seen, "copy selected scalar fields");
            Check(Get<long>(copy, "m_completedAt") == 123 && copy.CompletedAt == cached && copy.BestTimeSeconds == 12f && copy.LevelSelectUnlockSeen && copy.TimeTrialUnlockSeen, "copy timestamp/cache/unlocks/time");
            Check(copy.XPEarned == 0 && !Dirty(copy) && Get<bool>(copy, "m_savingEnabled"), "copy intentionally resets XP/dirty/saving gate");
            Check(!ReferenceEquals(copy.CompletedObjectiveIndices, copiedFrom.CompletedObjectiveIndices) && copy.CompletedObjectiveIndices.Count == 2, "copy objective list independent");
            Get<List<int>>(copy, "m_completedObjectiveIndices").Add(99);
            Check(copiedFrom.CompletedObjectiveIndices.Count == 2, "copy list mutation isolated");
            Check(new SaveDataLevelMission("empty-copy").CreateCopy().CompletedObjectiveIndices.Count == 0, "copy null list becomes empty list");

            var id = (PersistentObjectIdentifierType)17;
            var persistent = new SaveDataLevelPersistentObject(id, HardlightProject.ValueType.Integer);
            persistent.Set(true); Check(persistent.GetBooleanValue() && Dirty(persistent), "bool access/set ignores declared type");
            Clean(persistent); persistent.Set(true); Check(!Dirty(persistent), "same bool set clean");
            persistent.Set(false); Check(!persistent.GetBooleanValue() && Dirty(persistent), "changed bool set dirty");
            var incomingPersistent = new SaveDataLevelPersistentObject(id, HardlightProject.ValueType.Float); incomingPersistent.Set(true);
            Clean(persistent); persistent.ResolveNewData(incomingPersistent);
            Check(!persistent.GetBooleanValue() && !Dirty(persistent), "nonboolean local merge ignored");
            persistent.ResolveNewData(null); Check(!Dirty(persistent), "nonboolean local avoids null incoming access");
            var boolean = new SaveDataLevelPersistentObject(id, HardlightProject.ValueType.Boolean); boolean.ResolveNewData(incomingPersistent);
            Check(boolean.GetBooleanValue() && !Dirty(boolean), "boolean local OR accepts incoming mismatched type without dirty");
            Throws<NullReferenceException>(() => boolean.ResolveNewData(null), "Boolean local null incoming fails");
            Check(SaveDataLevelPersistentObject.GetValueType<bool>() == HardlightProject.ValueType.Boolean &&
                (int)SaveDataLevelPersistentObject.GetValueType<int>() == 0 && (int)SaveDataLevelPersistentObject.GetValueType<string>() == 0 &&
                (int)SaveDataLevelPersistentObject.GetValueType<bool?>() == 0, "generic type identifies only exact Boolean");
            MethodInfo assertion = typeof(SaveDataLevelPersistentObject).GetMethod("AssertType");
            Check(assertion.GetCustomAttribute<System.Diagnostics.ConditionalAttribute>().ConditionString == "BUILD_DEVELOPMENT", "original conditional assertion attr");
            assertion.Invoke(persistent, new object[] { HardlightProject.ValueType.String });
            Check(!Dirty(persistent) && !persistent.GetBooleanValue(), "original assertion native-empty body");

            var level = new SaveDataLevel("level"); Clean(level);
            var newMission = level.GetOrCreateMissionData("mission");
            Check(newMission.GUID == "mission" && Dirty(level) && !Dirty(newMission) && level.Missions.Count == 0, "get-create mutates dictionary not serialized list");
            Clean(level); Check(ReferenceEquals(newMission, level.GetOrCreateMissionData("mission")) && !Dirty(level), "existing mission identity/clean");
            Throws<ArgumentNullException>(() => level.GetOrCreateMissionData(null), "null mission key dictionary exception");
            Check(!Dirty(level), "failed key does not mark dirty");
            var newPersistent = level.GetOrCreatePersistentObjectData(id, true);
            Check(newPersistent.GetBooleanValue() && Dirty(newPersistent) && Dirty(level), "new persistent applies default and both dirty");
            Clean(level); Clean(newPersistent);
            Check(ReferenceEquals(newPersistent, level.GetOrCreatePersistentObjectData(id, false)) && newPersistent.GetBooleanValue() && !Dirty(level) && !Dirty(newPersistent), "existing persistent ignores default");
            var falseDefault = level.GetOrCreatePersistentObjectData((PersistentObjectIdentifierType)18, false);
            Check(!falseDefault.GetBooleanValue() && !Dirty(falseDefault) && Dirty(level), "false new default leaves child clean");
            level.AddSeenCutsceneGuid(null); Check(level.SeenCutsceneOnLevelGuids.Count == 1 && Dirty(level), "null cutscene accepted");
            Clean(level); level.AddSeenCutsceneGuid(null); Check(level.SeenCutsceneOnLevelGuids.Count == 1 && Dirty(level), "duplicate cutscene still dirty");
            level.ClearAllSeenCutsceneGuids(); Clean(level); level.ClearAllSeenCutsceneGuids();
            Check(level.SeenCutsceneOnLevelGuids.Count == 0 && Dirty(level), "empty clear still dirty");
            level.AddMissionGroupUnlockSeen("g"); Clean(level); level.AddMissionGroupUnlockSeen("g");
            Check(level.MissionGroupsUnlockSeen.Count == 1 && Dirty(level), "duplicate group unlock still dirty");
            newMission.MarkComplete(3); Check(level.GetCompleteMissionCount() == 1 && level.Missions.Count == 0, "completion count uses dictionary while list stale");
            newMission.IsNewState = MissionIsNewState.IsNew;
            Check(level.HasAnyNewContent(new[] { "mission" }) && !level.HasAnyNewContent(new[] { "other" }), "new content checks valid GUID then state");
            Throws<NullReferenceException>(() => level.HasAnyNewContent(null), "null valid list read before is-new test");
            Check(!new SaveDataLevel("empty").HasAnyNewContent(null), "empty dictionary avoids null valid list access");
            var target = new SaveDataLevel("target"); var incoming = new SaveDataLevel("incoming");
            var sharedMission = incoming.GetOrCreateMissionData("shared");
            var sharedPersistent = incoming.GetOrCreatePersistentObjectData(id, true);
            incoming.UnlockSeen = true; incoming.LevelSelectUnlockSeen = true; Set(incoming, "m_missionIntrosSeen", true);
            incoming.AddSeenCutsceneGuid("cut"); incoming.AddMissionGroupUnlockSeen("group"); Clean(target);
            target.ResolveNewData(incoming);
            Check(ReferenceEquals(sharedMission, target.GetOrCreateMissionData("shared")) && ReferenceEquals(sharedPersistent, target.GetOrCreatePersistentObjectData(id, false)), "merge new keys aliases incoming records");
            Check(target.UnlockSeen && !target.LevelSelectUnlockSeen && Get<bool>(target, "m_missionIntrosSeen"), "level merge omitted select-unlock flag");
            Check(target.SeenCutsceneOnLevelGuids.Count == 1 && target.MissionGroupsUnlockSeen.Count == 1 && !Dirty(target), "level merge ranges/fields do not mark parent dirty");
            var sameKey = new SaveDataLevel("merge"); var same = sameKey.GetOrCreateMissionData("shared");
            Set(same, "m_completedObjectiveIndices", new List<int>()); Set(sharedMission, "m_completedObjectiveIndices", new List<int> { 7 });
            Clean(sameKey); sameKey.ResolveNewData(incoming);
            Check(ReferenceEquals(same, sameKey.GetOrCreateMissionData("shared")) && same.IsObjectiveComplete(7) && !Dirty(sameKey) && Dirty(same), "existing mission merges in place/child-only dirty");
            Clean(target); target.ResolveNewData(target);
            Check(!Dirty(target) && target.SeenCutsceneOnLevelGuids.Count == 1,
                "self merge existing dictionaries/lists preserves identity without parent dirty");

            var collection = new RangeCollection(new[] { "a", "b", "c" }); var list = new List<string>();
            Check(ListExtensions.AddUniqueFromRange(list, collection) && list.Count == 3 && collection.Disposed && collection.CountReads == 0, "range adds all after true/no Count/finally");
            collection = new RangeCollection(new[] { "b", "a", "c" });
            Check(!ListExtensions.AddUniqueFromRange(list, collection) && list[0] == "a" && collection.Disposed, "range duplicate no change/order retained");
            collection = new RangeCollection(new[] { "x", "y" }, 1);
            Throws<InvalidOperationException>(() => ListExtensions.AddUniqueFromRange(list, collection), "range enumeration error escapes");
            Check(list.Contains("x") && !list.Contains("y") && collection.Disposed, "range error retains earlier add/disposes");
            Throws<NullReferenceException>(() => ListExtensions.AddUniqueFromRange(list, (IReadOnlyCollection<string>)null), "range null source throws");
            Check(!ListExtensions.AddUniqueFromRange((IList<string>)null, Array.Empty<string>()), "range empty source never touches null destination");
            var nullDestination = new RangeCollection(new[] { "z" });
            Throws<NullReferenceException>(() => ListExtensions.AddUniqueFromRange((IList<string>)null, nullDestination), "range nonempty null destination");
            Check(nullDestination.Disposed, "range null destination still disposes");
            VerifyOriginalSerializationClosure();
            return checks;
        }

        private static void VerifyOriginalSerializationClosure()
        {
            // The original member order generates these names naturally. These
            // existing serialization callbacks are context and receive no new
            // body credit. Scope forwarding and original metadata tokens are
            // not inferred from these bounded reflection checks.
            Type closure = typeof(SaveDataLevel).GetNestedType("<>c", BindingFlags.NonPublic);
            Check(closure != null && closure.FullName == "HardlightProject.SaveDataLevel+<>c" &&
                closure.Assembly == typeof(SaveDataLevel).Assembly && (int)closure.Attributes == 1057027,
                "original generated closure name/local assembly/type flags");
            object[] attributes = closure.GetCustomAttributes(false);
            bool compilerGenerated = false, serializableFlag = false;
            foreach (object attribute in attributes)
            {
                compilerGenerated |= attribute.GetType() == typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute);
                serializableFlag |= attribute.GetType() == typeof(SerializableAttribute);
            }
            // Reflection exposes SerializableAttribute from the original type
            // flag; the original custom-attribute table contains only
            // CompilerGeneratedAttribute.
            Check(attributes.Length == 2 && compilerGenerated && serializableFlag,
                "original generated attribute and Serializable flag reflection projection");
            FieldInfo[] fields = closure.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Array.Sort(fields, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            Check(fields.Length == 3 && fields[0].Name == "<>9" && fields[1].Name == "<>9__35_0" && fields[2].Name == "<>9__35_1",
                "original generated field count/order/names");
            Check((int)fields[0].Attributes == 54 && fields[0].FieldType == closure && fields[0].GetCustomAttributes(false).Length == 0,
                "original generated singleton field flags/type/attributes");
            Check((int)fields[1].Attributes == 22 && fields[1].FieldType == typeof(Func<SaveDataLevelMission, string>) && fields[1].GetCustomAttributes(false).Length == 0,
                "original mission cache field flags/exact delegate arguments");
            Check((int)fields[2].Attributes == 22 && fields[2].FieldType == typeof(Func<SaveDataLevelPersistentObject, PersistentObjectIdentifierType>) && fields[2].GetCustomAttributes(false).Length == 0,
                "original persistent cache field flags/exact delegate arguments");
            ConstructorInfo[] constructors = closure.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(constructors.Length == 1 && (int)constructors[0].Attributes == 6278 && constructors[0].GetParameters().Length == 0 &&
                closure.TypeInitializer != null && (int)closure.TypeInitializer.Attributes == 6289,
                "original generated constructor signatures/flags");
            MethodInfo[] methods = closure.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Array.Sort(methods, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            Check(methods.Length == 2 && methods[0].Name == "<OnAfterDeserialize>b__35_0" && methods[1].Name == "<OnAfterDeserialize>b__35_1" &&
                (int)methods[0].Attributes == 131 && (int)methods[1].Attributes == 131 &&
                methods[0].ReturnType == typeof(string) && methods[0].GetParameters().Length == 1 && methods[0].GetParameters()[0].ParameterType == typeof(SaveDataLevelMission) &&
                methods[1].ReturnType == typeof(PersistentObjectIdentifierType) && methods[1].GetParameters().Length == 1 && methods[1].GetParameters()[0].ParameterType == typeof(SaveDataLevelPersistentObject),
                "original generated callback order/names/flags/arguments/returns");
        }

        public static int Run()
        {
            RunManaged();
            var level = new SaveDataLevel("json-level");
            var mission = level.GetOrCreateMissionData("json-mission"); mission.MarkObjectiveComplete(4); mission.MarkComplete(9);
            var persistent = level.GetOrCreatePersistentObjectData((PersistentObjectIdentifierType)29, true);
            level.OnBeforeSerialize(); string json = JsonUtility.ToJson(level);
            Check(json.Contains("json-level") && json.Contains("json-mission"), "actual Unity serializes original level/mission fields");
            var restored = JsonUtility.FromJson<SaveDataLevel>(json);
            Check(restored.GUID == "json-level" && restored.GetCompleteMissionCount() == 1, "actual Unity deserialization dictionary callbacks");
            var restoredMission = restored.GetOrCreateMissionData("json-mission");
            Check(restoredMission.Complete && restoredMission.Progress == 9 && restoredMission.IsObjectiveComplete(4), "actual Unity completion/objective roundtrip");
            Check(restoredMission.CompletedAt == TimeUtils.FromUnixTime(TimeUtils.ToUnixTimeMs(mission.CompletedAt)) &&
                restoredMission.CompletedAt.Kind == DateTimeKind.Utc, "actual Unity timestamp millisecond/UTC cache reconstruction");
            Check(restored.GetOrCreatePersistentObjectData((PersistentObjectIdentifierType)29, false).GetBooleanValue(), "actual Unity persistent bool roundtrip");
            var copy = restoredMission.CreateCopy();
            Check(!ReferenceEquals(copy.CompletedObjectiveIndices, restoredMission.CompletedObjectiveIndices), "actual Unity loaded mission copy independent");
            Check(persistent.GetBooleanValue(), "source persistence record retained");
            return checks;
        }

        private sealed class RangeCollection : IReadOnlyCollection<string>
        {
            private readonly string[] values; private readonly int failAt;
            public bool Disposed; public int CountReads;
            public int Count { get { ++CountReads; return values.Length; } }
            public RangeCollection(string[] values, int failAt = -1) { this.values = values; this.failAt = failAt; }
            public IEnumerator<string> GetEnumerator()
            {
                try { for (int i = 0; i < values.Length; ++i) { if (i == failAt) throw new InvalidOperationException("range fixture"); yield return values[i]; } }
                finally { Disposed = true; }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
