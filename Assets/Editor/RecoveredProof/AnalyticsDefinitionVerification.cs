using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid
{
    public static class AnalyticsDefinitionVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Analytics definition verification: " + label); checks++; }
        private static void Set(object value, string name, object fieldValue) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, fieldValue);
        private static void Fail<TError>(Action action, string label) where TError : Exception
        {
            try { action(); } catch (TError) { checks++; return; }
            throw new Exception("Expected " + typeof(TError).Name + ": " + label);
        }
        private static AnalyticsConfiguration.AnalyticsBand Band(float minimum, float maximum)
        {
            var band = new AnalyticsConfiguration.AnalyticsBand(); Set(band, "m_minimum", minimum); Set(band, "m_maximum", maximum); return band;
        }
        private static void Options(Type type, params Option[] order)
        {
            var attrs = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(attrs.Select(x => x.Option).SequenceEqual(order) && attrs.All(x => Equals(x.Value, false)), type.Name + " exact ordered original IL2CPP options");
        }
        private static void Declarations()
        {
            Type config = typeof(AnalyticsConfiguration), band = typeof(AnalyticsConfiguration.AnalyticsBand), group = typeof(AnalyticsConfigurationGroup), evt = typeof(UIModernEvent), utils = typeof(ObjectUtils);
            Check((int)config.Attributes == 1048833 && config.BaseType == typeof(ScriptableObject), "original sealed configuration base/flags");
            Check((int)band.Attributes == 1057026 && band.BaseType == typeof(object), "original sealed serializable nested band");
            Check((int)group.Attributes == 1048577 && group.BaseType == typeof(DefinitionDataType<string, AnalyticsConfiguration>), "original group complete genuine base");
            Check((int)evt.Attributes == 1048577 && evt.BaseType == typeof(ScriptableObjectWithGuid), "original event genuine GUID base");
            Check((int)utils.Attributes == 1048577 && utils.BaseType == typeof(object), "original utility class flags; API subset explicitly incomplete");
            Options(config, Option.ArrayBoundsChecks, Option.NullChecks); Options(band, Option.NullChecks, Option.ArrayBoundsChecks); Options(group, Option.ArrayBoundsChecks, Option.NullChecks); Options(evt, Option.NullChecks, Option.ArrayBoundsChecks);
            var fields = config.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken).ToArray();
            Check(fields.Select(x => x.Name).SequenceEqual(new[] { "m_blueCoinsBands", "m_rewardTrackXPBands", "m_buttonsToTrack" }) && fields.Select(x => x.FieldType).SequenceEqual(new[] { typeof(AnalyticsConfiguration.AnalyticsBand[]), typeof(AnalyticsConfiguration.AnalyticsBand[]), typeof(UIModernEvent[]) }), "full original three ordered configuration fields");
            Check(fields.All(x => x.IsPrivate && !x.IsInitOnly && x.GetCustomAttributes(false).Length == 1 && x.IsDefined(typeof(SerializeField), false)), "exact mutable serialized fields without additional attrs");
            var bandFields = band.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(x => x.MetadataToken).ToArray();
            Check(bandFields.Select(x => x.Name).SequenceEqual(new[] { "m_minimum", "m_maximum" }) && bandFields.All(x => x.FieldType == typeof(float) && x.IsPrivate && !x.IsInitOnly && x.IsDefined(typeof(SerializeField), false)), "full original nested two-field layout surface");
            Check(evt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0 && utils.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "genuine zero own fields event and utility");
            foreach (var row in new[] { new { Type = config, File = "AnalyticsConfiguration", Menu = "HardlightProject/DefinitionData/Definitions/AnalyticsConfiguration", Order = 0 }, new { Type = group, File = "AnalyticsConfigurationGroup", Menu = "HardlightProject/DefinitionData/Groups/AnalyticsConfigurationGroup", Order = 0 }, new { Type = evt, File = "UIModernEvent", Menu = "Hardlight/HLModernUI/UIModernEvent", Order = 1 } })
            {
                var attr = row.Type.GetCustomAttribute<CreateAssetMenuAttribute>(); Check(attr != null && attr.fileName == row.File && attr.menuName == row.Menu && attr.order == row.Order, row.Type.Name + " exact asset menu metadata");
            }
            foreach (MethodInfo method in utils.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Check(method.IsGenericMethodDefinition && method.GetGenericArguments().Single().GetGenericParameterConstraints().SequenceEqual(new[] { typeof(UObject) }) && method.GetParameters().Single().Name == "name", "genuine helper Unity object constraint and original parameter name");
            Check(utils.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(x => x.Name).SequenceEqual(new[] { "FindByName", "FindByNameSafe" }), "only traced two static original helper methods; remaining32 original methods unresolved");
        }
        public static int RunManaged()
        {
            checks = 0; Declarations();
            var band = new AnalyticsConfiguration.AnalyticsBand(); Check(band.Minimum == 0f && band.Maximum == 0f, "original nested Object constructor numeric defaults");
            var config = (AnalyticsConfiguration)FormatterServices.GetUninitializedObject(typeof(AnalyticsConfiguration));
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                foreach (bool xp in new[] { false, true })
                {
                    string field = xp ? "m_rewardTrackXPBands" : "m_blueCoinsBands"; Func<int, string> read = xp ? new Func<int, string>(config.GetXPBand) : config.GetBlueCoinsBand;
                    Set(config, field, new[] { Band(0, 10), Band(10, 20) });
                    Check(read(5) == "0,10", "first inclusive band for " + field); Check(read(10) == "0,10", "first overlapping band wins for " + field);
                    Check(read(20) == "20,More", "last maximum itself enters More for " + field); Check(read(21) == "20,More", "above last maximum for " + field); Check(read(-1) == "0,0", "no negative fallback band for " + field);
                    Set(config, field, new[] { Band(0, 10), Band(20, 30) }); Check(read(15) == "0,0", "gap fallback for " + field);
                    Set(config, field, new[] { Band(0, 100), Band(0, 20) }); Check(read(25) == "20,More", "last maximum dominates earlier matching range for " + field);
                    Set(config, field, new[] { Band(float.NaN, 10), Band(0, 20) }); Check(read(5) == "0,20", "unordered minimum skips first range for " + field);
                    Set(config, field, new[] { Band(0, float.NaN) }); Check(read(0) == "0,0", "unordered maximum takes no range for " + field);
                    Set(config, field, new[] { Band(float.NaN, 10) }); Check(read(0) == "0,0", "sole unordered minimum has no match for " + field);
                    Set(config, field, new[] { Band(0, float.PositiveInfinity) }); Check(read(int.MaxValue) == "0,Infinity", "positive infinity does not become More for " + field);
                    Set(config, field, new[] { Band(0, float.NegativeInfinity) }); Check(read(0) == "-Infinity,More", "negative infinity last maximum precedes range search for " + field);
                    Set(config, field, new[] { Band(0, 16777216) }); Check(read(16777217).EndsWith(",More", StringComparison.Ordinal), "native Int32-to-Single rounding affects threshold for " + field);
                    Set(config, field, Array.Empty<AnalyticsConfiguration.AnalyticsBand>()); Fail<IndexOutOfRangeException>(() => read(0), "original empty array last lookup for " + field);
                    Set(config, field, null); Fail<NullReferenceException>(() => read(0), "original null array for " + field);
                    Set(config, field, new AnalyticsConfiguration.AnalyticsBand[] { null }); Fail<NullReferenceException>(() => read(0), "original null last band for " + field);
                    Set(config, field, new[] { null, Band(0, 20) }); Check(read(20) == "20,More", "More branch avoids earlier null for " + field); Fail<NullReferenceException>(() => read(1), "earlier null range faults below last maximum for " + field);
                }
                Set(config, "m_blueCoinsBands", new[] { Band(1.5f, 2.5f), Band(100, 200) }); Check(config.GetBlueCoinsBand(2) == "1.5,2.5", "default culture format preserves fractional comma payload");
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Check(config.GetBlueCoinsBand(2) == "1,5,2,5", "original string format uses current culture, not invariant serialization");
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Set(config, "m_buttonsToTrack", Array.Empty<UIModernEvent>()); Check(!config.IsButtonTracked(null), "empty button array returns false");
            Set(config, "m_buttonsToTrack", new UIModernEvent[] { null }); Check(config.IsButtonTracked(null), "original GUID operator handles identical null references");
            var same = (UIModernEvent)FormatterServices.GetUninitializedObject(typeof(UIModernEvent)); Set(config, "m_buttonsToTrack", new[] { same }); Check(config.IsButtonTracked(same), "same event reference uses original GUID equality fast path without engine claim");
            Set(config, "m_buttonsToTrack", null); Fail<NullReferenceException>(() => config.IsButtonTracked(null), "original null button array");
            Check(ObjectUtils.FindByName<GameObject>(null) == null && ObjectUtils.FindByName<GameObject>("") == null, "genuine wrapper null/empty path returns before resources");
            Check(ObjectUtils.FindByNameSafe<UIModernEvent>(null) == null && ObjectUtils.FindByNameSafe<UIModernEvent>("") == null, "genuine safe search null/empty path returns before resources");
            return checks;
        }
        public static void Run()
        {
            int managed = RunManaged(); CheckEngine(); Debug.Log("PASS genuine analytics/event definition checks=" + checks + "; managed=" + managed + "; owned fixtures only, no authored data/App approval.");
        }
        private static void CheckEngine()
        {
            AnalyticsConfiguration config = null, copy = null; AnalyticsConfigurationGroup group = null; UIModernEvent first = null, sameGuid = null; GameObject named = null;
            try
            {
                config = ScriptableObject.CreateInstance<AnalyticsConfiguration>(); copy = ScriptableObject.CreateInstance<AnalyticsConfiguration>(); group = ScriptableObject.CreateInstance<AnalyticsConfigurationGroup>(); first = ScriptableObject.CreateInstance<UIModernEvent>(); sameGuid = ScriptableObject.CreateInstance<UIModernEvent>(); named = new GameObject("Lucid analytics unique object " + Guid.NewGuid().ToString("N")); named.SetActive(false);
                Check(config != null && group != null && first != null, "genuine engine constructors and maintained base types");
                Check(config.GetType().GetField("m_blueCoinsBands", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(config) == null && group.Default == null, "native constructors leave own arrays/default null");
                first.name = "Lucid original UI event fixture " + Guid.NewGuid().ToString("N"); sameGuid.name = first.name + " distinct";
                typeof(ScriptableObjectWithGuid).GetField("m_guid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(first, "lucid-fixture-guid"); typeof(ScriptableObjectWithGuid).GetField("m_guid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sameGuid, "lucid-fixture-guid");
                Set(config, "m_buttonsToTrack", new[] { first }); Check(config.IsButtonTracked(sameGuid), "original GUID equality tracks distinct genuine engine objects");
                typeof(ScriptableObjectWithGuid).GetField("m_guid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sameGuid, "different-fixture-guid"); Check(!config.IsButtonTracked(sameGuid), "different GUID event excluded");
                Check(ReferenceEquals(UIModernEvent.FindByName(first.name), first), "full genuine event lookup finds owned resource by exact name");
                Check(UIModernEvent.FindByName(first.name.ToUpperInvariant()) == null, "original resource lookup is case sensitive");
                Check(UIModernEvent.FindByName(first.name + " absent") == null, "genuine unmatched resource search returns null");
                Check(ReferenceEquals(ObjectUtils.FindByName<GameObject>(named.name), named), "resource search includes owned inactive GameObject");
                Set(config, "m_blueCoinsBands", new[] { Band(1.5f, 3.5f) }); Set(config, "m_rewardTrackXPBands", new[] { Band(10, 30) });
                string json = JsonUtility.ToJson(config); Check(json.Contains("m_blueCoinsBands") && json.Contains("m_rewardTrackXPBands") && json.Contains("m_minimum") && json.Contains("m_maximum") && json.Contains("m_buttonsToTrack"), "actual engine serializes all original own/nested fields");
                JsonUtility.FromJsonOverwrite(json, copy); Check(copy.GetBlueCoinsBand(2).StartsWith("1") && copy.GetXPBand(20) == "10,30", "actual JSON overwrite retains genuine numeric ranges");
                Check(copy.IsButtonTracked(first), "actual JSON overwrite retains owned original event reference");
                Set(group, "m_default", config); Check(ReferenceEquals(group.Default, config), "original default getter retrieves owned configuration");
                string key = (string)typeof(AnalyticsConfigurationGroup).GetMethod("GetElementKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(group, new object[] { config }); Check(key == config.name, "genuine inherited group key is Unity object name");
                var comparer = typeof(AnalyticsConfigurationGroup).GetMethod("GetKeyComparer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(group, null); Check(comparer == null, "original group comparer returns null without substitute comparer");
                string groupJson = JsonUtility.ToJson(group); Set(group, "m_default", null); JsonUtility.FromJsonOverwrite(groupJson, group); Check(ReferenceEquals(group.Default, config), "actual group JSON preserves original own default reference");
            }
            finally
            {
                try { if (named != null) UObject.DestroyImmediate(named); }
                finally { try { if (sameGuid != null) UObject.DestroyImmediate(sameGuid); }
                finally { try { if (first != null) UObject.DestroyImmediate(first); }
                finally { try { if (group != null) UObject.DestroyImmediate(group); }
                finally { try { if (copy != null) UObject.DestroyImmediate(copy); }
                finally { if (config != null) UObject.DestroyImmediate(config); } } } } }
            }
        }
    }
}
