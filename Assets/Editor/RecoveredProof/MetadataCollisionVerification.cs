using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    /// <summary>Bounds metadata evidence to actual original types; live Unity resources remain separate.</summary>
    public static class MetadataCollisionVerification
    {
        private static void Require(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }
        private static void Throws<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); } catch (T ex) { if (ex.GetType() != typeof(T)) throw new InvalidOperationException(label + ": unexpected exception type", ex); checks++; return; }
            throw new InvalidOperationException(label);
        }
        private static void Unhandled(Action action, string label, ref int checks)
        {
            try { action(); } catch (Exception ex)
            {
                Require(ex.GetType() == typeof(Exception) && ex.Message == "Unhandled interpolation type.", label, ref checks);
                return;
            }
            throw new InvalidOperationException(label);
        }
        private static T Raw<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            throw new InvalidOperationException("original field missing: " + name);
        }
        private static void Set(object obj, string name, object value) => Field(obj.GetType(), name).SetValue(obj, value);
        private static T Get<T>(object obj, string name) => (T)Field(obj.GetType(), name).GetValue(obj);

        // A BCL collection observation fixture, not a substitute game type or metadata implementation.
        private sealed class ObservedList : IReadOnlyList<Metadata>
        {
            public List<Metadata> Rows = new List<Metadata>();
            public Action OnCount;
            public Action<int> OnIndex;
            public int CountReads;
            public List<int> Indices = new List<int>();
            public int Count
            {
                get { CountReads++; OnCount?.Invoke(); return Rows.Count; }
            }
            public Metadata this[int index]
            {
                get { Indices.Add(index); OnIndex?.Invoke(index); return Rows[index]; }
            }
            public IEnumerator<Metadata> GetEnumerator() => throw new InvalidOperationException("original lookup must use captured Count/indexed access");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static void DestroyOwned(List<UnityEngine.Object> owned)
        {
            Exception first = null;
            for (int i = owned.Count - 1; i >= 0; --i)
            {
                try { if (!ReferenceEquals(owned[i], null)) UnityEngine.Object.DestroyImmediate(owned[i]); }
                catch (Exception ex) { if (first == null) first = ex; }
            }
            if (first != null) throw first;
        }
        private static T CreateOwned<T>(List<UnityEngine.Object> owned) where T : ScriptableObject
        {
            T result = ScriptableObject.CreateInstance<T>();
            owned.Add(result);
            return result;
        }

        // Real Unity object/resource checks are intentionally never invoked by managed hosts.
        public static int RunEngine()
        {
            int checks = 0;
            List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            try
            {
                MetadataValueTypeInteger integerType = CreateOwned<MetadataValueTypeInteger>(owned);
                // Unity CreateInstance requests defaults and Reset; the original override
                // changes the constructor label from Value to Integer.
                Require(Get<string>(integerType, "m_label") == "Integer" && Get<string>(integerType, "m_helpText") == string.Empty, "real Unity creation applies original Reset label and retains empty help", ref checks);
                Require(!Get<bool>(integerType, "m_hasMinimum") && Get<int>(integerType, "m_minimum") == 0 && !Get<bool>(integerType, "m_hasMaximum") && Get<int>(integerType, "m_maximum") == 0, "real integer constructor keeps bound defaults zero", ref checks);
                Require(!Get<bool>(integerType, "m_noDefaultHelpText") && Get<int>(integerType, "m_default") == 0 && integerType.GetDefaultAsString() == "0", "real integer default fields/conversion", ref checks);
                Require(integerType.GetGUID() == string.Empty, "real original Guid base initialization", ref checks);
                MetadataKeyType k1 = CreateOwned<MetadataKeyType>(owned);
                MetadataKeyType same = CreateOwned<MetadataKeyType>(owned);
                MetadataKeyType other = CreateOwned<MetadataKeyType>(owned);
                Require(ReferenceEquals(k1.ValueType, null), "real key constructor default null value type", ref checks);
                Set(k1, "m_guid", "lucid-metadata-first"); Set(same, "m_guid", "lucid-metadata-first"); Set(other, "m_guid", "lucid-metadata-other");
                Require(k1 == same && k1 != other, "lookups retain genuine Guid key semantics", ref checks);
                Metadata row1 = new Metadata(k1, "10"); Metadata row2 = new Metadata(same, "20"); Metadata row3 = new Metadata(other, "30");
                List<Metadata> rows = new List<Metadata> { row1, row2, row3 };
                Require(MetadataUtilities.TryGetMetadata(rows, same, out Metadata first) && ReferenceEquals(first, row1), "first lookup compares real distinct Guid-equal objects", ref checks);
                Require(MetadataUtilities.TryGetMetadata(rows, k1, out List<Metadata> all) && all.Count == 2 && ReferenceEquals(all[0], row1) && ReferenceEquals(all[1], row2), "multi lookup real key duplicates", ref checks);
                Require(!MetadataUtilities.TryGetMetadata(rows, null, out first) && ReferenceEquals(first, null), "non-null real keys do not match null query", ref checks);
                MetadataFlexiGroup group = CreateOwned<MetadataFlexiGroup>(owned);
                MetadataFlexiGroup group2 = CreateOwned<MetadataFlexiGroup>(owned);
                Require(group.Metadata != null && group.Metadata.Count == 0 && Get<MetadataFlexiGroupData>(group, "m_data") != null, "real Flexi group constructs genuine data/list before base", ref checks);
                Require(!ReferenceEquals(Get<MetadataFlexiGroupData>(group, "m_data"), Get<MetadataFlexiGroupData>(group2, "m_data")), "real group default data is per instance", ref checks);
                Get<List<MetadataFlexi>>(Get<MetadataFlexiGroupData>(group, "m_data"), "m_metadata").Add(new MetadataFlexi(k1, "42"));
                Require(group.TryGetValue(same, out int integer) && integer == 42, "real group dispatch with Guid aliases", ref checks);
                Require(!group.TryGetValue(other, out integer) && integer == 0, "real group distinct key miss", ref checks);
                Set(k1, "m_valueType", integerType);
                Require(ReferenceEquals(k1.ValueType, integerType), "real key exposes actual value type", ref checks);
                Metadata result = MetadataUtilities.Interpolate(row1, row2, 0.5f, MetadataInterpolationType.Linear);
                Require(result.Value == "15" && ReferenceEquals(result.Key, k1), "real outer integer interpolation", ref checks);
                result = MetadataUtilities.Interpolate(row1, row2, float.NaN, MetadataInterpolationType.Constant);
                Require(result.Value == "20" && ReferenceEquals(result.Key, k1), "real constant NaN selects b value and a key", ref checks);
                typeof(MetadataValueTypeInteger).GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(integerType, null);
                Require(Get<string>(integerType, "m_label") == "Integer" && Get<string>(integerType, "m_helpText") == string.Empty, "real Reset preserves help while updating label", ref checks);
                MetadataGroupKey groupKey = CreateOwned<MetadataGroupKey>(owned);
                MetadataGroupKey groupAlias = CreateOwned<MetadataGroupKey>(owned);
                MetadataGroupKey groupOther = CreateOwned<MetadataGroupKey>(owned);
                Set(groupKey, "m_guid", "lucid-group-first"); Set(groupAlias, "m_guid", "lucid-group-first"); Set(groupOther, "m_guid", "lucid-group-other");
                MetadataGroups groups = new MetadataGroups();
                List<MetadataKeyGroupPair> pairs = Get<List<MetadataKeyGroupPair>>(groups, "m_groups");
                pairs.Add(new MetadataKeyGroupPair { Key = groupKey, Group = group });
                pairs.Add(new MetadataKeyGroupPair { Key = groupAlias, Group = group2 });
                pairs.Add(default);
                Require(ReferenceEquals(groups.GetGroup(groupKey), group2), "real dictionary duplicates overwrite earlier group", ref checks);
                Require(ReferenceEquals(groups.GetGroup(groupAlias), group2), "real dictionary aliases share Guid hash/equality", ref checks);
                Require(ReferenceEquals(groups.GetGroup(groupOther), null), "real dictionary missing returns null", ref checks);
                Dictionary<MetadataGroupKey, MetadataGroup> cache = Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary");
                Require(cache.Count == 1, "real dictionary skips null pair keys", ref checks);
                pairs.Add(new MetadataKeyGroupPair { Key = groupOther, Group = group });
                Require(ReferenceEquals(groups.GetGroup(groupOther), null) && cache.Count == 1, "cached dictionary never rebuilds after source list change", ref checks);
                MetadataGroupKey bad = CreateOwned<MetadataGroupKey>(owned);
                Set(bad, "m_guid", null);
                groups = new MetadataGroups(); pairs = Get<List<MetadataKeyGroupPair>>(groups, "m_groups");
                pairs.Add(new MetadataKeyGroupPair { Key = groupKey, Group = group });
                pairs.Add(new MetadataKeyGroupPair { Key = bad, Group = group2 });
                Throws<NullReferenceException>(() => groups.GetGroup(groupKey), "bad original key GUID faults during cache construction", ref checks);
                cache = Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary");
                Require(cache != null && cache.Count == 1 && ReferenceEquals(cache[groupKey], group), "cache is published and retains prior insertion after fault", ref checks);
                Set(bad, "m_guid", "lucid-group-repaired");
                Require(ReferenceEquals(groups.GetGroup(groupKey), group) && ReferenceEquals(groups.GetGroup(bad), null), "subsequent lookup uses partial cache without retrying repaired data", ref checks);
                string uniqueName = "lucid-metadata-resource-" + Guid.NewGuid().ToString("N");
                k1.name = uniqueName; same.name = uniqueName;
                MetadataKeyType expected = null;
                foreach (MetadataKeyType candidate in Resources.FindObjectsOfTypeAll<MetadataKeyType>())
                    if (candidate.name == uniqueName) { expected = candidate; break; }
                Require(!ReferenceEquals(expected, null), "owned original resource candidates exist", ref checks);
                Require(ReferenceEquals(MetadataUtilities.ConvertToScriptableObject<MetadataKeyType>(uniqueName), expected), "resource lookup returns first matching real Unity enumeration row", ref checks);
                Require(ReferenceEquals(MetadataUtilities.ConvertToScriptableObject<MetadataKeyType>(null), null) && ReferenceEquals(MetadataUtilities.ConvertToScriptableObject<MetadataKeyType>(string.Empty), null), "real resource empty inputs bypass lookup/logging", ref checks);
                string json = JsonUtility.ToJson(new Metadata(k1, "authored"));
                Require(json.Contains("m_key") && json.Contains("m_value") && json.Contains("m_valueObject") && json.Contains("authored"), "real Unity Metadata serialization fields", ref checks);
                json = JsonUtility.ToJson(new MetadataFlexiGroupData());
                Require(json.Contains("m_metadata"), "real Unity authored Flexi list serialization", ref checks);
                return checks;
            }
            finally { DestroyOwned(owned); }
        }

        public static int RunManaged()
        {
            int checks = 0;
            CultureInfo priorCulture = CultureInfo.CurrentCulture;
            CultureInfo priorUI = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                Metadata first = new Metadata(null, "17");
                Metadata second = new Metadata(null, "invalid");
                Require(first.Key == null && first.Value == "17" && ReferenceEquals(first.ValueObject, null), "string constructor keeps inactive object field null", ref checks);
                Require(first.AsString() == "17" && first.AsInt() == 17 && first.AsFloat() == 17f, "original string/int/float conversions", ref checks);
                Require(first.AsBool() == false, "numeric text is not a Boolean", ref checks);
                foreach (string text in new[] { null, "", "invalid", "2147483648", "1.2" })
                    Require(MetadataUtilities.ConvertToInt(text) == 0, "integer parse failure defaults: " + text, ref checks);
                Require(MetadataUtilities.ConvertToInt(" -2147483648 ") == int.MinValue, "integer signed minimum and whitespace", ref checks);
                Require(MetadataUtilities.ConvertToInt("+2147483647") == int.MaxValue, "integer signed maximum", ref checks);
                Require(MetadataUtilities.ConvertToFloat("-1.25e2") == -125f && MetadataUtilities.ConvertToFloat("1,234") == 1234f, "default BCL float styles retained", ref checks);
                Require(MetadataUtilities.ConvertToFloat("invalid") == 0f && MetadataUtilities.ConvertToFloat(null) == 0f, "float failure defaults", ref checks);
                Require(float.IsNaN(MetadataUtilities.ConvertToFloat("NaN")), "float NaN text retained", ref checks);
                Require(MetadataUtilities.ConvertToBool("  tRuE ") && !MetadataUtilities.ConvertToBool("1"), "Boolean parser case/whitespace and numeric rejection", ref checks);
                Require(!MetadataUtilities.ConvertToBool("invalid") && !MetadataUtilities.ConvertToBool(null), "Boolean failure defaults", ref checks);
                Require(MetadataUtilities.ConvertToEnum<MetadataInterpolationType>("Linear") == MetadataInterpolationType.Linear, "enum named case-sensitive parse", ref checks);
                Require(MetadataUtilities.ConvertToEnum<MetadataInterpolationType>("linear") == MetadataInterpolationType.Constant, "enum wrong case becomes zero", ref checks);
                Require((int)MetadataUtilities.ConvertToEnum<MetadataInterpolationType>("7") == 7, "undefined numeric enum is accepted by BCL", ref checks);
                Require(new Metadata(null, "Linear").AsEnum<MetadataInterpolationType>() == MetadataInterpolationType.Linear, "actual generic instance conversion", ref checks);
                Require(ReferenceEquals(MetadataUtilities.ConvertToScriptableObject<MetadataKeyType>(null), null) && ReferenceEquals(MetadataUtilities.ConvertToScriptableObject<MetadataKeyType>(""), null), "empty object names return before Unity resource lookup", ref checks);
                MetadataKeyType rawKey = Raw<MetadataKeyType>();
                Metadata objectRow = new Metadata(null, rawKey);
                Require(objectRow.Value == null && ReferenceEquals(objectRow.ValueObject, rawKey), "object constructor keeps string null", ref checks);
                Require(ReferenceEquals(objectRow.AsObject<MetadataKeyType>(), rawKey), "generic object cast retains real compatible object", ref checks);
                Require(ReferenceEquals(objectRow.AsObject<MetadataGroupKey>(), null), "generic object mismatch returns null", ref checks);

                List<Metadata> rows = new List<Metadata> { first, second, objectRow };
                Require(MetadataUtilities.TryGetMetadata(rows, null, out Metadata selected) && ReferenceEquals(selected, first), "single lookup selects first authored match", ref checks);
                Require(MetadataUtilities.TryGetMetadata(rows, null, out List<Metadata> all) && all.Count == 3 && ReferenceEquals(all[0], first) && ReferenceEquals(all[2], objectRow), "multi lookup retains duplicates/order", ref checks);
                Require(MetadataUtilities.TryGetValue(rows, null, out int integer) && integer == 17, "int return is metadata presence", ref checks);
                Require(MetadataUtilities.TryGetValue(new[] { second }, null, out integer) && integer == 0, "malformed int remains present", ref checks);
                Require(MetadataUtilities.TryGetValue(new[] { second }, null, out bool boolean) && !boolean, "malformed bool remains present", ref checks);
                Require(MetadataUtilities.TryGetValue(new[] { objectRow }, null, out string value) && value == null, "present null string is retained", ref checks);
                Require(MetadataUtilities.TryGetObject(rows, null, out MetadataKeyType obj) && ReferenceEquals(obj, null), "present nonobject row returns true/null", ref checks);
                Require(MetadataUtilities.TryGetObject(new[] { objectRow }, null, out obj) && ReferenceEquals(obj, rawKey), "present compatible real object", ref checks);
                Require(MetadataUtilities.TryGetObject(new[] { objectRow }, null, out MetadataGroupKey wrong) && ReferenceEquals(wrong, null), "present wrong object type returns true/null", ref checks);
                Require(MetadataUtilities.TryGetValue(new[] { new Metadata(null, "invalid") }, null, out MetadataInterpolationType enumValue) && enumValue == 0, "malformed generic enum remains present", ref checks);
                Require(!MetadataUtilities.TryGetValue(null, null, out string absentString) && absentString == string.Empty, "absent string uses Empty", ref checks);
                Require(!MetadataUtilities.TryGetValue(null, null, out integer) && integer == 0, "absent int defaults", ref checks);
                Require(!MetadataUtilities.TryGetValue(null, null, out float absentFloat) && absentFloat == 0f, "absent float defaults", ref checks);
                Require(!MetadataUtilities.TryGetValue(null, null, out boolean) && !boolean, "absent bool defaults", ref checks);
                Require(!MetadataUtilities.TryGetValue(null, null, out enumValue) && enumValue == 0, "absent generic enum defaults", ref checks);
                Require(!MetadataUtilities.TryGetObject(null, null, out obj) && ReferenceEquals(obj, null), "absent object defaults", ref checks);
                Require(!MetadataUtilities.TryGetMetadata(null, null, out all) && all == null, "absent multi-list remains null", ref checks);
                List<Metadata> filled = new List<Metadata> { second };
                Require(MetadataUtilities.TryGetMetadata(null, null, filled) && filled.Count == 1, "nonnull fill list succeeds even without input", ref checks);
                Require(MetadataUtilities.TryGetMetadata(Array.Empty<Metadata>(), null, filled) && filled.Count == 1, "empty input does not clear fill list", ref checks);
                Require(MetadataUtilities.TryGetMetadata(rows, null, filled) && filled.Count == 4 && ReferenceEquals(filled[0], second) && ReferenceEquals(filled[1], first), "fill appends and retains prior entries", ref checks);
                Require(!MetadataUtilities.TryGetMetadata(null, null, (List<Metadata>)null), "null input and fill returns false", ref checks);
                Throws<NullReferenceException>(() => MetadataUtilities.TryGetMetadata(new[] { first }, null, (List<Metadata>)null), "matched null fill fault", ref checks);

                ObservedList observed = new ObservedList();
                observed.Rows.Add(first);
                observed.OnIndex = index => { if (index == 0) observed.Rows.Add(second); };
                Require(MetadataUtilities.TryGetMetadata(observed, null, out all) && all.Count == 1 && observed.CountReads == 1 && observed.Indices.Count == 1, "lookup snapshots Count before indexed callback changes the real list", ref checks);
                observed = new ObservedList();
                observed.Rows.Add(null);
                Metadata retained = first;
                Throws<NullReferenceException>(() => MetadataUtilities.TryGetMetadata(observed, null, out retained), "null row faults rather than being skipped", ref checks);
                Require(ReferenceEquals(retained, first), "single out prior value retained on early row fault", ref checks);
                all = new List<Metadata> { first };
                Throws<NullReferenceException>(() => MetadataUtilities.TryGetMetadata(observed, null, out all), "multi null row fault", ref checks);
                Require(all == null, "multi out clears before early row fault", ref checks);
                observed.Rows[0] = first;
                observed.Rows.Add(null);
                Throws<NullReferenceException>(() => MetadataUtilities.TryGetMetadata(observed, null, out all), "multi later row fault", ref checks);
                Require(all.Count == 1 && ReferenceEquals(all[0], first), "multi out publishes partial results before later row fault", ref checks);

                // Exercise real concrete original class behavior; raw ScriptableObject receiver is not a native constructor fixture.
                MetadataFlexiGroupData data = new MetadataFlexiGroupData();
                Require(data.Metadata != null && data.Metadata.Count == 0, "genuine Flexi data allocates its mutable list", ref checks);
                MetadataFlexi flexi = new MetadataFlexi(null, "42");
                Get<List<MetadataFlexi>>(data, "m_metadata").Add(flexi);
                MetadataFlexiGroup group = Raw<MetadataFlexiGroup>();
                Set(group, "m_data", data);
                Require(ReferenceEquals(group.Metadata, data.Metadata), "genuine group returns authored data list without copying", ref checks);
                Require(group.TryGetMetadata(null, out selected) && ReferenceEquals(selected, flexi), "real abstract-group virtual dispatch closes with genuine Flexi subclass", ref checks);
                Require(group.TryGetMetadata(null, out all) && all.Count == 1, "real group multi lookup", ref checks);
                Require(group.KeyTypeExists(null), "real group presence lookup", ref checks);
                Require(group.TryGetValue(null, out integer) && integer == 42, "real group int lookup", ref checks);
                Require(group.TryGetValue(null, out absentFloat) && absentFloat == 42f, "real group float lookup", ref checks);
                Require(group.TryGetValue(null, out value) && value == "42", "real group string lookup", ref checks);
                Require(group.TryGetValue(null, out boolean) && !boolean, "real group bool parse-presence", ref checks);
                Require(group.TryGetObject(null, out obj) && ReferenceEquals(obj, null), "real group object parse-presence", ref checks);
                Require(group.TryGetValue(null, out enumValue) && (int)enumValue == 42, "real group generic numeric enum", ref checks);
                Set(group, "m_data", null);
                Throws<NullReferenceException>(() => group.KeyTypeExists(null), "real group null authored data is not normalized", ref checks);

                MetadataGroups groups = new MetadataGroups();
                Require(!groups.HasData && Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary") == null, "list and cache constructor defaults", ref checks);
                Throws<ArgumentNullException>(() => groups.GetGroup(null), "empty original dictionary null-key rejection", ref checks);
                Dictionary<MetadataGroupKey, MetadataGroup> cache = Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary");
                Require(cache != null && cache.Count == 0, "cache published before lookup fault", ref checks);
                Get<List<MetadataKeyGroupPair>>(groups, "m_groups").Add(default);
                Require(groups.HasData, "HasData reflects source list despite skipped null key/cache", ref checks);
                Throws<ArgumentNullException>(() => groups.GetGroup(null), "cached null-key rejection", ref checks);
                Require(ReferenceEquals(cache, Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary")) && cache.Count == 0, "lazy cache not rebuilt after authored list changes", ref checks);
                groups = new MetadataGroups();
                Get<List<MetadataKeyGroupPair>>(groups, "m_groups").Add(default);
                Throws<ArgumentNullException>(() => groups.GetGroup(null), "null group pair key skipped before dictionary query", ref checks);
                Require(Get<Dictionary<MetadataGroupKey, MetadataGroup>>(groups, "m_groupDictionary").Count == 0, "skip leaves published cache empty", ref checks);

                foreach (float t in new[] { float.NegativeInfinity, -3f, 0f, 0.75f, 1f, 2f, float.PositiveInfinity, float.NaN })
                {
                    bool earlier = t < 1f;
                    Require(MetadataUtilities.InterpolateValue("a", "b", t, MetadataInterpolationType.Constant) == (earlier ? "a" : "b"), "constant string threshold including NaN", ref checks);
                    Require(MetadataUtilities.InterpolateValue(true, false, t, MetadataInterpolationType.Constant) == earlier, "constant bool threshold including NaN", ref checks);
                    Require(MetadataUtilities.InterpolateValue(3, 9, t, MetadataInterpolationType.Constant) == (earlier ? 3 : 9), "constant integer threshold including NaN", ref checks);
                    Require(MetadataUtilities.InterpolateValue(3f, 9f, t, MetadataInterpolationType.Constant) == (earlier ? 3f : 9f), "constant float threshold including NaN", ref checks);
                }
                Require(MetadataUtilities.InterpolateValue(10, -10, 0.25f, MetadataInterpolationType.Linear) == 5, "integer lerp truncates the binary32 result", ref checks);
                Require(MetadataUtilities.InterpolateValue(-3, -10, 0.5f, MetadataInterpolationType.Linear) == -6, "negative integer truncation toward zero", ref checks);
                Require(MetadataUtilities.InterpolateValue(0f, 16f, 0.25f, MetadataInterpolationType.Linear) == 4f, "float linear interpolation", ref checks);
                Require(MetadataUtilities.InterpolateValue(3f, 9f, -2f, MetadataInterpolationType.Linear) == 3f && MetadataUtilities.InterpolateValue(3f, 9f, 2f, MetadataInterpolationType.Linear) == 9f, "linear clamp retains endpoints", ref checks);
                Require(float.IsNaN(MetadataUtilities.InterpolateValue(3f, 9f, float.NaN, MetadataInterpolationType.Linear)), "linear NaN remains NaN", ref checks);
                Unhandled(() => MetadataUtilities.InterpolateValue("a", "b", 0.5f, MetadataInterpolationType.Linear), "string rejects linear", ref checks);
                Unhandled(() => MetadataUtilities.InterpolateValue(false, true, 0.5f, MetadataInterpolationType.Linear), "Boolean rejects linear", ref checks);
                try { MetadataUtilities.InterpolateValue(3f, 9f, 0.5f, (MetadataInterpolationType)7); throw new InvalidOperationException("unknown mode accepted"); }
                catch (Exception ex) { Require(ex.GetType() == typeof(Exception) && ex.Message == "Unhandled interpolation type.", "exact unknown interpolation error", ref checks); }

                MetadataValueTypeInteger integerType = Raw<MetadataValueTypeInteger>();
                Set(integerType, "m_default", -17);
                Require(integerType.GetDefaultAsString() == "-17", "real default integer conversion", ref checks);
                Set(integerType, "m_label", "old");
                typeof(MetadataValueTypeInteger).GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(integerType, null);
                Require(Get<string>(integerType, "m_label") == "Integer", "real Reset calls genuine base and restores label", ref checks);
                Set(integerType, "m_hasMinimum", true); Set(integerType, "m_minimum", 1000);
                Set(integerType, "m_hasMaximum", true); Set(integerType, "m_maximum", 1001);
                integerType.Interpolate(new Metadata(null, "10"), new Metadata(null, "20"), 0.5f, MetadataInterpolationType.Linear, out Metadata interpolated);
                Require(interpolated.Value == "15" && interpolated.Key == null && ReferenceEquals(interpolated.ValueObject, null), "real integer value type ignores inspector bounds and constructs metadata", ref checks);
                Set(rawKey, "m_valueType", integerType);
                Metadata a = new Metadata(rawKey, "10"); Metadata b = new Metadata(null, "20");
                interpolated = MetadataUtilities.Interpolate(a, b, 3f, MetadataInterpolationType.Linear);
                Require(interpolated.Value == "20" && ReferenceEquals(interpolated.Key, rawKey), "outer interpolation clamps and selects a's real value type/key", ref checks);
                Metadata prior = interpolated;
                Unhandled(() => integerType.Interpolate(a, b, 0.5f, (MetadataInterpolationType)7, out interpolated), "integer unknown mode throws before out publication", ref checks);
                Require(ReferenceEquals(interpolated, prior), "integer interpolation failure retains prior out value", ref checks);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Require(MetadataUtilities.ConvertToFloat("1,5") == 1.5f, "default float parsing follows current culture", ref checks);
            }
            finally
            {
                CultureInfo.CurrentCulture = priorCulture;
                CultureInfo.CurrentUICulture = priorUI;
            }
            return checks;
        }
    }
}
