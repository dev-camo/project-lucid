using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    // Bounded proof of the maintained original dictionary APIs; no authored
    // game assets, configuration lifecycle, save slots or gameplay acceptance.
    public static class SerializableDictionaryVerification
    {
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks++; }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException(message);
        }
        private static FieldInfo Field(Type type, string name)
            => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static Type Base<TKey, TValue>() => typeof(SerializableDictionaryBase<TKey, TValue, SerializableKeyValuePair<TKey, TValue>>);
        private static List<SerializableKeyValuePair<TKey, TValue>> Rows<TKey, TValue>(SerializableDictionary<TKey, TValue> value)
            => (List<SerializableKeyValuePair<TKey, TValue>>)Field(Base<TKey, TValue>(), "m_values").GetValue(value);
        private static void SetRows<TKey, TValue>(SerializableDictionary<TKey, TValue> value,
            List<SerializableKeyValuePair<TKey, TValue>> rows) => Field(Base<TKey, TValue>(), "m_values").SetValue(value, rows);
        private static Dictionary<TKey, TValue> Backing<TKey, TValue>(SerializableDictionary<TKey, TValue> value)
            => (Dictionary<TKey, TValue>)Field(Base<TKey, TValue>(), "m_dictionary").GetValue(value);
        private static bool Validate<TKey, TValue>(SerializableDictionary<TKey, TValue> value)
        {
            try { return (bool)Base<TKey, TValue>().GetMethod("ValidateValues", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        // These proof-only subclasses exercise real virtual contracts. They are
        // never runtime substitutes for game definitions or consumers.
        private sealed class ConversionProbe : SerializableDictionary<string, int>
        {
            internal readonly List<string> Trace = new List<string>();
            internal string ThrowOn;
            internal bool Mutate;
            internal List<SerializableKeyValuePair<string, int>> ReplacementRows;
            internal int SerializeCalls, DeserializeCalls;
            protected override SerializableKeyValuePair<string, int> SerializeKeyValuePair(string key, int value)
            {
                Trace.Add(key);
                if (ReplacementRows != null)
                {
                    SetRows(this, ReplacementRows);
                    ReplacementRows = null;
                }
                if (key == ThrowOn) throw new ApplicationException("pair conversion");
                if (Mutate) { Mutate = false; Add("added-during-conversion", 99); }
                return base.SerializeKeyValuePair(key, value);
            }
            public override void Serialize() { SerializeCalls++; base.Serialize(); }
            public override void Deserialize() { DeserializeCalls++; base.Deserialize(); }
        }
        private sealed class RemovalKey
        {
            internal readonly int Id;
            internal bool ThrowHash;
            internal RemovalKey(int id) { Id = id; }
            public override int GetHashCode() { if (ThrowHash) throw new ApplicationException("remove hash"); return Id; }
            public override bool Equals(object other) => other is RemovalKey key && key.Id == Id;
        }
        [Serializable] private sealed class JsonHolder
        { public SerializableDictionary<string, int> Values = new SerializableDictionary<string, int>(); }

        private static void Metadata()
        {
            Type[] types = { typeof(SerializableDictionaryBase<,,>), typeof(SerializableDictionary<,>), typeof(SerializableKeyValuePair<,>) };
            foreach (Type type in types)
            {
                Check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly identity");
                Check(type.IsSerializable && (type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original Serializable/BeforeFieldInit flags");
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(options.Length == 2 && options[0].Option == Option.NullChecks && options[1].Option == Option.ArrayBoundsChecks,
                    "ordered original type options");
                Check(options.All(option => option.Value is bool b && !b), "original option values");
                foreach (Type parameter in type.GetGenericArguments())
                {
                    Check(parameter.GenericParameterAttributes == GenericParameterAttributes.None, "unconstrained original type parameter");
                    Check(parameter.GetGenericParameterConstraints().Length == 0, "no invented type constraint");
                }
            }
            Type b = typeof(SerializableDictionaryBase<,,>);
            var fields = b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Check(fields.Length == 2 && fields[0].Name == "m_dictionary" && fields[1].Name == "m_values", "ordered two original base fields");
            Check(fields[0].IsPrivate && fields[0].IsInitOnly && !fields[0].IsDefined(typeof(SerializeField), false), "readonly nonserialized dictionary field API");
            Check(fields[1].IsPrivate && !fields[1].IsInitOnly && fields[1].IsDefined(typeof(SerializeField), false), "mutable serialized row list");
            var attrs = fields[1].GetCustomAttributesData();
            Check(attrs.Count == 2 && attrs[0].AttributeType == typeof(FormerlySerializedAsAttribute) && attrs[1].AttributeType == typeof(SerializeField), "ordered list attributes");
            Check((string)attrs[0].ConstructorArguments[0].Value == "m_listKVPs", "original former list name");
            Check(b.GetCustomAttribute<DefaultMemberAttribute>().MemberName == "Item", "original indexer default member");
            Check(b.GetInterfaces().Length == 9, "all genuine dictionary/callback interfaces");
            Check(b.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length + b.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 43, "43 original base declarations including two contracts");
            var contracts = b.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(method => method.IsAbstract).ToArray();
            Check(contracts.Length == 2 && contracts.All(method => method.IsFamily), "exact protected abstract pair contracts");
            Check(types[1].GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length + types[1].GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 4, "four original derived declarations");
            Check(types[2].GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length + types[2].GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 5, "five original pair declarations");
            foreach (Type type in types.Skip(1))
                Check(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == (type == types[1] ? 0 : 2), "derived/pair original field counts");
            var pairFields = types[2].GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Check(pairFields[0].Name == "m_key" && pairFields[1].Name == "m_value" && pairFields.All(field => field.IsPrivate && !field.IsInitOnly && field.IsDefined(typeof(SerializeField), false)), "ordered serialized pair fields");
            foreach (string name in new[] { "KeyAs", "ValueAs" })
            {
                Type parameter = types[2].GetMethod(name).GetGenericArguments()[0];
                Check(parameter.GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint && parameter.GetGenericParameterConstraints().Length == 0, "pair cast reference constraint only");
            }
            var extensionMethods = typeof(DictionaryExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Check(extensionMethods.Length == 7, "complete original extension type seven declarations; old266 context");
            Check(typeof(DictionaryExtensions).IsAbstract && typeof(DictionaryExtensions).IsSealed && typeof(DictionaryExtensions).IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false), "original static extension type marker");
            Check(extensionMethods.All(method => method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)), "all original extension methods");
            var fallback = extensionMethods.Single(method => method.Name == "TryGetWithDefault").GetParameters()[2];
            Check(fallback.IsOptional && fallback.DefaultValue == null, "generic optional default encoding");
        }

        public static int RunManaged()
        {
            checks = 0; Metadata();
            var d = new SerializableDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Check(d.Count == 0 && Rows(d).Count == 0, "both original containers initialize empty");
            Check(ReferenceEquals(Backing(d).Comparer, StringComparer.OrdinalIgnoreCase), "constructor preserves supplied comparer");
            d.Add("A", 1); d["a"] = 2;
            Check(d.Count == 1 && d["A"] == 2, "generic indexer preserves dictionary comparer");
            var keys = d.Keys; var values = d.Values;
            Check(ReferenceEquals(keys, d.Keys) && ReferenceEquals(values, d.Values), "live collection getter references");
            d.Add("B", 3); Check(keys.Count == 2 && values.Count == 2, "collection references observe later additions");
            Check(!d.IsReadOnly && !d.IsFixedSize && !d.IsSynchronized, "real dictionary interface flags");
            Check(ReferenceEquals(d.SyncRoot, ((ICollection)Backing(d)).SyncRoot), "SyncRoot forwards backing identity");
            Check(d.ContainsKey("b") && d.TryGetValue("a", out int v) && v == 2, "contains/try-get transport");
            Check(!d.TryGetValue("missing", out v) && v == 0, "missing out value default");
            Check(d.Contains(new KeyValuePair<string, int>("A", 2)) && !d.Contains(new KeyValuePair<string, int>("A", 8)), "pair Contains checks value too");
            d.Add(new KeyValuePair<string, int>("C", 4)); Check(d["c"] == 4, "generic pair Add");
            var copy = new KeyValuePair<string, int>[4]; d.CopyTo(copy, 1);
            Check(copy[0].Key == null && copy.Skip(1).Select(pair => pair.Value).OrderBy(x => x).SequenceEqual(new[] { 2, 3, 4 }), "generic pair CopyTo offset");
            Throws<ArgumentException>(() => d.CopyTo(new KeyValuePair<string, int>[1], 0), "copy length validation");
            Check(!d.Remove(new KeyValuePair<string, int>("A", 99)) && d.Remove(new KeyValuePair<string, int>("A", 2)), "pair removal requires value match");
            Check(!d.Remove("absent") && d.Remove("B"), "key removal bool transport");
            var readonlyView = (IReadOnlyDictionary<string, int>)d;
            Check(ReferenceEquals(readonlyView.Keys, d.Keys) && ReferenceEquals(readonlyView.Values, d.Values), "readonly getters retain collections");
            var untyped = (IDictionary)d;
            Check(ReferenceEquals(untyped.Keys, d.Keys) && ReferenceEquals(untyped.Values, d.Values), "nongeneric getters retain collections");
            d.Add((object)"D", (object)5); d[(object)"D"] = 6;
            Check((int)d[(object)"D"] == 6 && d.Contains((object)"D"), "object Add/index/Contains API");
            Check(d[(object)47] == null && !d.Contains((object)47), "incompatible object keys lookup absent");
            d.Remove((object)47); Check(d.Count == 2, "incompatible object key removal ignored");
            Throws<ArgumentException>(() => d.Add((object)47, (object)5), "incompatible object key add fails");
            Throws<ArgumentException>(() => d[(object)"D"] = "wrong value", "object index value type validation");
            Throws<ArgumentNullException>(() => d.Contains((object)null), "object null key fails");
            DictionaryEntry[] entries = new DictionaryEntry[2]; d.CopyTo((Array)entries, 0);
            Check(entries.Select(entry => (int)entry.Value).OrderBy(x => x).SequenceEqual(new[] { 4, 6 }), "nongeneric collection array forwarding");
            IEnumerator<KeyValuePair<string, int>> generic = d.GetEnumerator();
            Check(generic.GetType() == typeof(Dictionary<string, int>.Enumerator) && generic.MoveNext(), "boxed generic dictionary enumerator");
            IEnumerator nongeneric = ((IEnumerable)d).GetEnumerator(); nongeneric.MoveNext();
            Check(nongeneric.Current is KeyValuePair<string, int>, "IEnumerable route retains generic pair current");
            IDictionaryEnumerator dictionaryEnumerator = untyped.GetEnumerator(); dictionaryEnumerator.MoveNext();
            Check(dictionaryEnumerator.Current is DictionaryEntry, "IDictionary route uses dictionary entry current");
            d.Add("new", 9); Throws<InvalidOperationException>(() => generic.MoveNext(), "live enumerator mutation failure"); generic.Dispose();
            d.Serialize(); var priorRows = Rows(d); Check(priorRows.Count == d.Count, "serialization emits each entry");
            d.Clear(); Check(d.Count == 0 && priorRows.Count == 3, "Clear retains serialized rows");
            d.Deserialize(); Check(d.Count == 3 && d["D"] == 6, "deserialize restores exact rows");
            d.Reset(); Check(d.Count == 0 && priorRows.Count == 0 && ReferenceEquals(priorRows, Rows(d)), "Reset clears both without replacing list");
            var nullComparer = new SerializableDictionary<int, string>(null);
            Check(Backing(nullComparer).Comparer.Equals(EqualityComparer<int>.Default), "null comparer follows BCL default");
            var pairObject = new SerializableKeyValuePair<object, object>("key", 12);
            Check((string)pairObject.Key == "key" && (int)pairObject.Value == 12, "pair constructor/getter order");
            Check(pairObject.KeyAs<string>() == "key" && pairObject.KeyAs<Uri>() == null, "KeyAs cast/null mismatch");
            Check(pairObject.ValueAs<ValueType>() is int && pairObject.ValueAs<string>() == null, "ValueAs boxed cast/null mismatch");
            var nullPair = new SerializableKeyValuePair<object, object>(null, null);
            Check(nullPair.KeyAs<object>() == null && nullPair.ValueAs<object>() == null, "cast null preserved");
            Throws<ArgumentException>(() => pairObject.GetType().GetMethod("KeyAs").MakeGenericMethod(typeof(int)), "genuine reference constraint rejects value target");

            var src = new Dictionary<string, int> { ["copied"] = 7 };
            d.Set(src); Check(d.Count == 1 && d["copied"] == 7 && !ReferenceEquals(src, Backing(d)), "Set copies into existing dictionary");
            src["copied"] = 8; Check(d["copied"] == 7, "Set does not alias argument");
            d.Serialize(); priorRows = Rows(d); d.Set(Backing(d));
            Check(d.Count == 0 && priorRows.Count == 1, "Set self clears before enumeration, leaves rows");
            d.Add("old", 1); Throws<NullReferenceException>(() => d.Set(null), "Set null fails after clear");
            Check(d.Count == 0 && priorRows.Count == 1, "Set null retains cleared dictionary and stale rows");

            var invalid = new SerializableDictionary<string, int>();
            var duplicateRows = new List<SerializableKeyValuePair<string, int>> { new SerializableKeyValuePair<string, int>("x", 1), new SerializableKeyValuePair<string, int>("x", 2) };
            SetRows(invalid, duplicateRows); invalid.Add("old", 10);
            Throws<ArgumentException>(() => invalid.Deserialize(), "duplicate serialized key fails");
            Check(invalid.Count == 1 && invalid["x"] == 1 && ReferenceEquals(duplicateRows, Rows(invalid)), "duplicate failure preserves earlier addition/list");
            Check(!Validate(invalid), "final equal keys fail private validation");
            duplicateRows.Add(new SerializableKeyValuePair<string, int>("other", 3));
            Check(Validate(invalid), "validation ignores earlier duplicate");
            SetRows(invalid, new List<SerializableKeyValuePair<string, int>>()); Check(Validate(invalid), "empty validation succeeds");
            SetRows(invalid, new List<SerializableKeyValuePair<string, int>> { new SerializableKeyValuePair<string, int>(null, 1) });
            Check(!Validate(invalid), "last null key validation fails");
            Throws<ArgumentNullException>(() => invalid.Deserialize(), "null serialized key retains BCL failure");
            Check(invalid.Count == 0, "null key failure follows clear");
            SetRows(invalid, new List<SerializableKeyValuePair<string, int>> { new SerializableKeyValuePair<string, int>("first", 1), null });
            Throws<NullReferenceException>(() => invalid.Deserialize(), "null pair not skipped");
            Check(invalid.Count == 1 && invalid["first"] == 1, "null pair failure keeps preceding rows");
            SetRows(invalid, null); Throws<NullReferenceException>(() => invalid.Deserialize(), "null list failure after clear");
            Check(invalid.Count == 0, "null list failure clears dictionary");
            SetRows(invalid, new List<SerializableKeyValuePair<string, int>> { new SerializableKeyValuePair<string, int>("A", 1), new SerializableKeyValuePair<string, int>("a", 2) });
            Check(Validate(invalid), "validation uses key Equals, not arbitrary comparer");
            var conversion = new ConversionProbe(); conversion.Add("first", 1); conversion.Add("second", 2); conversion.ThrowOn = "second";
            Throws<ApplicationException>(() => conversion.Serialize(), "conversion failure propagates");
            Check(conversion.Trace.SequenceEqual(new[] { "first", "second" }) && Rows(conversion).Count == 1 && Rows(conversion)[0].Key == "first", "conversion failure retains earlier appended rows");
            conversion.ThrowOn = null; conversion.Trace.Clear(); conversion.Mutate = true;
            Throws<InvalidOperationException>(() => conversion.Serialize(), "conversion dictionary mutation invalidates enumeration");
            Check(Rows(conversion).Count == 1 && conversion.Count == 3, "mutation failure retains appended row and changed dictionary");
            ((ISerializationCallbackReceiver)conversion).OnBeforeSerialize();
            Check(conversion.SerializeCalls == 3 && Rows(conversion).Count == 3, "before callback dispatches virtual Serialize once");
            conversion.Clear(); ((ISerializationCallbackReceiver)conversion).OnAfterDeserialize();
            Check(conversion.DeserializeCalls == 1 && conversion.Count == 3, "after callback dispatches virtual Deserialize once");

            // Native virtual conversion precedes a fresh read of the mutable
            // row-list field. A real callback can replace that serialized field.
            var replacementProbe = new ConversionProbe();
            replacementProbe.Add("one", 1); replacementProbe.Add("two", 2);
            var oldRows = Rows(replacementProbe);
            oldRows.Add(new SerializableKeyValuePair<string, int>("old", 0));
            var replacementRows = new List<SerializableKeyValuePair<string, int>> {
                new SerializableKeyValuePair<string, int>("existing", 91)
            };
            replacementProbe.ReplacementRows = replacementRows;
            replacementProbe.Serialize();
            Check(ReferenceEquals(Rows(replacementProbe), replacementRows), "converter replacement retains new serialized field identity");
            Check(oldRows.Count == 0, "Serialize clears old list then reloads field after converter, no append to captured receiver");
            Check(replacementRows.Count == 3 && replacementRows[0].Key == "existing" && replacementRows.Skip(1).Select(pair => pair.Key).OrderBy(key => key).SequenceEqual(new[] { "one", "two" }), "new list retains preexisting row and receives both converted pairs");
            Check(replacementProbe.Trace.Count == 2 && replacementProbe.Count == 2, "replacement conversion completes original dictionary enumeration");

            var extensions = new Dictionary<string, string> { ["present"] = "value", ["null"] = null };
            Check(extensions.TryGetWithDefault("present", "fallback") == "value" && extensions.TryGetWithDefault("missing", "fallback") == "fallback", "TryGetWithDefault lookup/fallback");
            Check(extensions.TryGetWithDefault("null", "fallback") == null && extensions.TryGetWithDefault("missing") == null, "existing null/optional default unchanged");
            int ctorCalls = 0;
            Check(extensions.TryGetOrNew("present", () => { ctorCalls++; return "ignored"; }) == "value" && ctorCalls == 0, "constructor not invoked on hit");
            Check(extensions.TryGetOrNew("null", (Func<string>)null) == null, "null constructor ignored on hit");
            Check(extensions.TryGetOrNew("created", () => { ctorCalls++; return "made"; }) == "made" && ctorCalls == 1 && extensions["created"] == "made", "constructor then Add");
            Throws<NullReferenceException>(() => extensions.TryGetOrNew("missing", (Func<string>)null), "missing null constructor fails");
            Throws<ApplicationException>(() => extensions.TryGetOrNew("failure", () => { throw new ApplicationException(); }), "constructor failure propagates");
            Check(!extensions.ContainsKey("failure"), "constructor failure leaves absent key");
            Throws<ArgumentException>(() => extensions.TryGetOrNew("reentrant", () => { extensions.Add("reentrant", "callback"); return "return"; }), "constructor Add collision is preserved");
            Check(extensions["reentrant"] == "callback", "collision retains callback mutation");
            List<string> valueCopy = extensions.GetValues(); extensions.Add("later", "last");
            Check(valueCopy.Count == extensions.Count - 1 && !valueCopy.Contains("last"), "GetValues makes detached list");
            extensions.AddOrReplace("present", "replaced"); extensions.AddOrReplace("added", "new");
            Check(extensions["present"] == "replaced" && extensions["added"] == "new", "AddOrReplace direct index API");
            string[] array = null; extensions.ResizeAndCopyValues(ref array);
            Check(array.Length == extensions.Count && array.SequenceEqual(extensions.Values), "resize-null then copy actual values");
            string[] same = array; extensions.ResizeAndCopyValues(ref array); Check(ReferenceEquals(array, same), "Array.Resize preserves equal-length instance");
            extensions.Clear(); extensions.ResizeAndCopyValues(ref array); Check(array != null && array.Length == 0, "resize shrinks to zero array");
            array = null; extensions.ResizeAndCopyValues(ref array); Check(array != null && array.Length == 0, "zero resize initializes null array");
            var selected = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2, ["three"] = 3 };
            var seenCounts = new List<int>(); selected.RemoveAll(item => { seenCounts.Add(selected.Count); return item.Value != 2; });
            Check(seenCounts.SequenceEqual(new[] { 3, 3, 3 }) && selected.Count == 1 && selected["two"] == 2, "selection completes before removals");
            selected = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };
            Throws<ApplicationException>(() => selected.RemoveAll(item => { if (item.Key == "two") throw new ApplicationException(); return true; }), "predicate failure propagates before removals");
            Check(selected.Count == 2, "predicate failure keeps previously selected keys");
            Throws<NullReferenceException>(() => selected.RemoveAll(null), "null predicate nonempty failure");
            var empty = new Dictionary<string, int>(); empty.RemoveAll(null); Check(empty.Count == 0, "empty dictionary ignores null predicate");
            Throws<InvalidOperationException>(() => selected.RemoveAll(item => { selected.Add("mutation", 3); return true; }), "selection mutation preserves live enumeration failure");
            Check(selected.Count == 3 && selected.ContainsKey("one"), "selection mutation failure causes no collected-key removals");
            var firstKey = new RemovalKey(1); var secondKey = new RemovalKey(2);
            var hashFailure = new Dictionary<RemovalKey, int> { [firstKey] = 1, [secondKey] = 2 };
            Throws<ApplicationException>(() => hashFailure.RemoveAll(item => { if (item.Key == secondKey) secondKey.ThrowHash = true; return true; }), "removal failure propagates");
            secondKey.ThrowHash = false; Check(hashFailure.Count == 1 && !hashFailure.ContainsKey(firstKey) && hashFailure.ContainsKey(secondKey), "second-pass failure retains earlier removal");
            var originalContext = new Dictionary<string, List<int>>();
            Check(ReferenceEquals(originalContext.TryGetOrNew("key"), originalContext["key"]), "unchanged old266 constructor dependency consumed");
            var nativeWrapper = new SerializableDictionary<string, int>(); nativeWrapper.Add("keep", 1); nativeWrapper.Add("remove", 2); nativeWrapper.RemoveAll(item => item.Value == 2);
            Check(nativeWrapper.Count == 1 && nativeWrapper["keep"] == 1, "base RemoveAll forwards original extension");
            return checks;
        }

        public static int Run()
        {
            RunManaged();
            // Actual Unity serializer checks are deliberately unrun in private
            // managed proof; root integration supplies the real Editor verdict.
            var pair = new SerializableKeyValuePair<string, int>("original", 17);
            string json = JsonUtility.ToJson(pair);
            Check(json.Contains("\"m_key\":\"original\"") && json.Contains("\"m_value\":17"), "actual serialized original pair fields");
            Check(!json.Contains("KeyAs") && !json.Contains("\"Key\""), "actual serializer excludes pair methods/properties");
            JsonUtility.FromJsonOverwrite("{\"m_key\":\"overwritten\",\"m_value\":29}", pair);
            Check(pair.Key == "overwritten" && pair.Value == 29, "actual pair overwrite preserves getters");
            var holder = new JsonHolder(); holder.Values.Add("entry", 31);
            ((ISerializationCallbackReceiver)holder.Values).OnBeforeSerialize();
            string holderJson = JsonUtility.ToJson(holder);
            Check(holderJson.Contains("\"m_values\"") && holderJson.Contains("\"m_key\":\"entry\""), "actual inherited generic serialized row list");
            Check(!holderJson.Contains("m_dictionary"), "actual serializer excludes readonly backing dictionary");
            JsonUtility.FromJsonOverwrite("{\"Values\":{\"m_values\":[{\"m_key\":\"changed\",\"m_value\":47}]}}", holder);
            ((ISerializationCallbackReceiver)holder.Values).OnAfterDeserialize();
            Check(holder.Values.Count == 1 && holder.Values["changed"] == 47, "actual overwrite plus original deserialize callback");
            Check(Rows(holder.Values).Count == 1 && Rows(holder.Values)[0].Key == "changed", "actual retained serialized pair identity");
            return checks;
        }
    }
}
