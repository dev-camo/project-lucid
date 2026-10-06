using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using HardlightEnumComparers = HardlightProject.HardlightEnumComparers;

namespace ProjectLucid
{
    // Bounded original definition/source proof. Raw managed values never execute
    // Unity object equality; real Unity lifecycle/reference fixtures are separate.
    public static class ProgressionDefinitionVerification
    {
        private static int checks;
        private static readonly Type ThresholdType = typeof(MissionScorerStreakDefinition).GetNestedType("ScoreThreshold", BindingFlags.NonPublic);
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original progression definitions: " + label); checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type owner, string name) => owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static void Set(object value, Type owner, string name, object data) => Field(owner, name).SetValue(value, data);
        private static object Read(object value, Type owner, string name) => Field(owner, name).GetValue(value);
        private static object DictionaryComparer(SerializableDictionary<WaypointContainerType,WaypointSettings> value) => ((Dictionary<WaypointContainerType,WaypointSettings>)Read(value, typeof(SerializableDictionaryBase<WaypointContainerType,WaypointSettings,SerializableKeyValuePair<WaypointContainerType,WaypointSettings>>), "m_dictionary")).Comparer;
        private static object Comparer(object value) => value.GetType().GetMethod("GetKeyComparer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(value, null);
        private static void Throws<T>(Action action, string label) where T : Exception { try { action(); throw new Exception("Expected fault absent: " + label); } catch (T) { Check(true, label); } }
        private static DefinitionDataType<TKey,TData>.DefinitionElement<TData> Row<TKey,TData>(TData data) where TData : ScriptableObject
        { var row = Raw<DefinitionDataType<TKey,TData>.DefinitionElement<TData>>(); row.Data = data; return row; }
        private static object Threshold(float count, float score)
        { object value = Activator.CreateInstance(ThresholdType, true); Set(value, ThresholdType, "m_count", count); Set(value, ThresholdType, "m_score", score); return value; }
        private static void Thresholds(MissionScorerStreakDefinition definition, params float[] pairs)
        {
            IList values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ThresholdType));
            for (int i = 0; i < pairs.Length; i += 2) values.Add(Threshold(pairs[i], pairs[i+1]));
            Set(definition, typeof(MissionScorerStreakDefinition), "m_scoreThresholds", values);
        }
        private static void Enable(DreamPowerStoreBandDefinition value)
        { typeof(DreamPowerStoreBandDefinition).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(value, null); }
        private static object Retained(ManagedAddressableAsset<Sprite> value) => Read(value, typeof(ManagedAddressableAsset<Sprite>), "m_assetReference");
        private static bool Fresh(ManagedAddressableAsset<Sprite> value) => value != null && (int)Read(value, typeof(ManagedAddressableAsset<Sprite>), "m_refCount") == 0
            && ReferenceEquals(Read(value, typeof(ManagedAddressableAsset<Sprite>), "m_loadedAsset"), null);
        public static int RunManaged()
        {
            checks = 0;
            var streak = Raw<MissionScorerStreakDefinition>();
            Check((int)streak.Identifier == 0 && !streak.ExcludeFromAutoGeneration && !streak.DamageableObject && !streak.UsesComboMultiplier && streak.MinCount == 0 && streak.Timeout == 0, "native field getter zero defaults");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_identifier", (MissionScorerStreakIdentifier)(-123));
            Set(streak, typeof(MissionScorerStreakDefinition), "m_excludeFromAutoGeneration", true); Set(streak, typeof(MissionScorerStreakDefinition), "m_damageableObject", true);
            Set(streak, typeof(MissionScorerStreakDefinition), "m_minCount", float.NaN); Set(streak, typeof(MissionScorerStreakDefinition), "m_timeout", float.PositiveInfinity); Set(streak, typeof(MissionScorerStreakDefinition), "m_usesComboMultiplier", true);
            Check((int)streak.Identifier == -123 && streak.ExcludeFromAutoGeneration && streak.DamageableObject && streak.UsesComboMultiplier && float.IsNaN(streak.MinCount) && float.IsPositiveInfinity(streak.Timeout), "all six getters retain authored values without normalization");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_enemyTypes", new List<EnemyType> { (EnemyType)(-11), (EnemyType)22, (EnemyType)(-11) });
            Check(streak.HasEnemyType((EnemyType)(-11)), "enemy list contains arbitrary authored enum");
            Check(!streak.HasEnemyType((EnemyType)33), "missing enemy type returns false");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_collectableTypes", new List<CollectableType> { (CollectableType)44 });
            Check(streak.HasCollectableType((CollectableType)44), "collectable list contains authored value");
            Check(!streak.HasCollectableType((CollectableType)(-44)), "missing collectable false");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_enemyTypes", null); Throws<NullReferenceException>(() => streak.HasEnemyType((EnemyType)0), "null enemy list preserves fault");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_collectableTypes", null); Throws<NullReferenceException>(() => streak.HasCollectableType((CollectableType)0), "null collectable list preserves fault");
            Set(streak, typeof(MissionScorerStreakDefinition), "m_scoreThresholds", null); Throws<NullReferenceException>(() => streak.GetScore(0), "null threshold list preserves fault");
            Thresholds(streak); Check(streak.GetScore(int.MaxValue) == 0f, "empty thresholds zero");
            Thresholds(streak, 2, 10, 5, 20, 8, 30);
            Check(streak.GetScore(1) == 0f, "below first threshold zero"); Check(streak.GetScore(2) == 10f, "exact threshold retains its score");
            Check(streak.GetScore(4) == 10f, "between thresholds keeps prior score"); Check(streak.GetScore(5) == 20f, "second exact threshold selected");
            Check(streak.GetScore(9) == 30f, "past final threshold last score");
            Thresholds(streak, 5, 90, 1, 10); Check(streak.GetScore(2) == 0f, "first greater threshold stops before later lower threshold");
            Thresholds(streak, 1, 10, 1, 20, 0, 30); Check(streak.GetScore(1) == 30f, "unsorted equal thresholds retain last visited score");
            Thresholds(streak, float.NaN, 70, 9, 80); Check(streak.GetScore(0) == 70f, "unordered NaN Count does not break original greater branch");
            Thresholds(streak, float.NegativeInfinity, -50, float.PositiveInfinity, 50); Check(streak.GetScore(int.MaxValue) == -50f, "infinite thresholds use direct ordered branch");
            Thresholds(streak, -5, -40, -1, -20); Check(streak.GetScore(-2) == -40f, "negative streak and scores preserved");
            Thresholds(streak, 16777216f, 61, 16777218f, 62); Check(streak.GetScore(16777217) == 61f, "Int32-to-Single boundary retains native rounding");
            Thresholds(streak, 0, float.NaN); Check(float.IsNaN(streak.GetScore(0)), "NaN Score retained");
            Thresholds(streak, 0, float.PositiveInfinity); Check(float.IsPositiveInfinity(streak.GetScore(0)), "infinite Score retained");
            Thresholds(streak); ((IList)Read(streak, typeof(MissionScorerStreakDefinition), "m_scoreThresholds")).Add(null);
            Throws<NullReferenceException>(() => streak.GetScore(0), "null threshold row not skipped");
            object thresholdDefault = Activator.CreateInstance(ThresholdType, true);
            Check((float)ThresholdType.GetProperty("Count").GetValue(thresholdDefault) == 0 && (float)ThresholdType.GetProperty("Score").GetValue(thresholdDefault) == 0, "real nested threshold constructor/default getters zero");

            var streakGroup = Raw<MissionScorerStreakDefinitionGroup>();
            Check(ReferenceEquals(Comparer(streakGroup), HardlightEnumComparers.MissionScorerStreakIdentifierComparer), "exact original streak comparer");
            streakGroup.m_elements = new[] { Row<MissionScorerStreakIdentifier,MissionScorerStreakDefinition>(streak) };
            var streaks = streakGroup.GetData(); Check(ReferenceEquals(streaks[(MissionScorerStreakIdentifier)(-123)], streak) && ReferenceEquals(streaks.Comparer, HardlightEnumComparers.MissionScorerStreakIdentifierComparer), "streak key/getter and comparer feed genuine dictionary");
            streakGroup.m_elements = new[] { Row<MissionScorerStreakIdentifier,MissionScorerStreakDefinition>(streak), Row<MissionScorerStreakIdentifier,MissionScorerStreakDefinition>(streak) };
            Throws<ArgumentException>(() => streakGroup.GetData(), "duplicate streak identifiers original fault");
            streakGroup.m_elements = new[] { Row<MissionScorerStreakIdentifier,MissionScorerStreakDefinition>(null) }; Throws<NullReferenceException>(() => streakGroup.GetData(), "null streak data fault");

            var band = Raw<DreamPowerStoreBandDefinition>(); var widget = Raw<UIWidgetProgression>();
            Set(band, typeof(DreamPowerStoreBandDefinition), "m_type", (DreamPowerStoreBandType)(-456)); Set(band, typeof(DreamPowerStoreBandDefinition), "m_name", (Strings)789);
            Set(band, typeof(DreamPowerStoreBandDefinition), "m_progressionUnlockWidget", widget);
            Check((int)band.Type == -456 && (int)band.Name == 789 && ReferenceEquals(band.ProgressionUnlockWidget, widget), "band getters retain exact authored fields/reference");
            Check(ReferenceEquals(band.ImageAsset, null) && ReferenceEquals(band.SlotLockedBackground, null), "raw native wrapper field zero before enable");
            Enable(band); var oldImage = band.ImageAsset; var oldBackground = band.SlotLockedBackground;
            Check(Fresh(oldImage) && Fresh(oldBackground) && !ReferenceEquals(oldImage, oldBackground), "enable constructs distinct fresh genuine wrappers");
            Check(ReferenceEquals(Retained(oldImage), null) && ReferenceEquals(Retained(oldBackground), null), "null authored references are retained in wrappers");
            var imageRef = new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff"); var backgroundRef = new AssetReferenceAtlasedSprite("ffeeddccbbaa99887766554433221100");
            Set(band, typeof(DreamPowerStoreBandDefinition), "m_image", imageRef); Set(band, typeof(DreamPowerStoreBandDefinition), "m_slotLockedBackground", backgroundRef); Enable(band);
            Check(ReferenceEquals(Retained(band.ImageAsset), imageRef) && ReferenceEquals(Retained(band.SlotLockedBackground), backgroundRef), "enable retains corresponding exact references");
            Check(!ReferenceEquals(oldImage, band.ImageAsset) && !ReferenceEquals(oldBackground, band.SlotLockedBackground), "repeat enable replaces both wrappers");
            Check(Fresh(oldImage) && Fresh(oldBackground) && Fresh(band.ImageAsset) && Fresh(band.SlotLockedBackground), "replacement leaves old/new wrappers unloaded and unreleased");
            typeof(DreamPowerStoreBandDefinition).GetProperty("ImageAsset").GetSetMethod(true).Invoke(band, new object[] { oldBackground });
            Check(ReferenceEquals(band.ImageAsset, oldBackground), "private image setter stores caller wrapper directly");
            typeof(DreamPowerStoreBandDefinition).GetProperty("SlotLockedBackground").GetSetMethod(true).Invoke(band, new object[] { oldImage });
            Check(ReferenceEquals(band.SlotLockedBackground, oldImage), "private background setter stores caller wrapper directly");
            var bands = Raw<DreamPowerStoreBandDefinitionGroup>(); Check(ReferenceEquals(Comparer(bands), HardlightEnumComparers.DreamPowerStoreBandTypeComparer), "band exact readonly registry comparer");
            bands.m_elements = new[] { Row<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>(band) };
            Check(bands.GetIndex(band) == 0, "same reference short-circuits genuine GUID equality before engine operations");
            Check(ReferenceEquals(bands.GetData()[(DreamPowerStoreBandType)(-456)], band), "band own Type dictionary key");
            bands.m_elements = Array.Empty<DefinitionDataType<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>>(); Check(bands.GetIndex(band) == -1, "empty Array.FindIndex returns minus one");
            bands.m_elements = new[] { Row<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>(null) }; Check(bands.GetIndex(null) == 0, "both-null authored operator reference shortcut");
            bands.m_elements = new DefinitionDataType<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>[] { null }; Throws<NullReferenceException>(() => bands.GetIndex(null), "null row callback preserves dereference fault");
            bands.m_elements = null; Throws<ArgumentNullException>(() => bands.GetIndex(null), "Array.FindIndex null array fault");

            var settings = new WaypointSettings();
            Check(ReferenceEquals(settings.Icon, null) && settings.ShowHeightIndicatorThreshold == 0 && settings.MinDrawDistance == 0 && settings.MaxDrawDistance == 0 && settings.ShowDistanceIndicator, "real settings constructor preserves original true bool and zero fields");
            var icon = Raw<Sprite>(); Set(settings, typeof(WaypointSettings), "m_icon", icon); Set(settings, typeof(WaypointSettings), "m_showHeightIndicatorThreshold", -2f); Set(settings, typeof(WaypointSettings), "m_minDrawDistance", float.NaN); Set(settings, typeof(WaypointSettings), "m_maxDrawDistance", float.PositiveInfinity); Set(settings, typeof(WaypointSettings), "m_showDistanceIndicator", false);
            Check(ReferenceEquals(settings.Icon, icon) && settings.ShowHeightIndicatorThreshold == -2 && float.IsNaN(settings.MinDrawDistance) && float.IsPositiveInfinity(settings.MaxDrawDistance) && !settings.ShowDistanceIndicator, "all settings getters retain distinct raw values");
            var waypoint = Raw<WaypointDefinition>(); Set(waypoint, typeof(WaypointDefinition), "m_type", (WaypointType)(-321));
            var dictionary = new SerializableDictionary<WaypointContainerType,WaypointSettings>(HardlightEnumComparers.WaypointContainerTypeComparer);
            Set(waypoint, typeof(WaypointDefinition), "m_settingsByContainerType", dictionary); dictionary.Add((WaypointContainerType)123, settings); dictionary.Add((WaypointContainerType)(-123), null);
            Check((int)waypoint.Type == -321, "waypoint Type direct field"); Check(ReferenceEquals(DictionaryComparer(dictionary), HardlightEnumComparers.WaypointContainerTypeComparer), "waypoint real dictionary comparer");
            WaypointSettings found; Check(waypoint.TryGetSettingsByContainerType((WaypointContainerType)123, out found) && ReferenceEquals(found, settings), "TryGet forwards successful exact value");
            found = settings; Check(!waypoint.TryGetSettingsByContainerType((WaypointContainerType)999, out found) && ReferenceEquals(found, null), "missing TryGet clears out value as genuine dictionary");
            Check(waypoint.TryGetSettingsByContainerType((WaypointContainerType)(-123), out found) && ReferenceEquals(found, null), "existing null value returns true");
            Set(waypoint, typeof(WaypointDefinition), "m_settingsByContainerType", null); found = settings;
            Throws<NullReferenceException>(() => waypoint.TryGetSettingsByContainerType((WaypointContainerType)123, out found), "null dictionary retains fault"); Check(ReferenceEquals(found, settings), "null receiver fault happens before out value is modified");
            var waypoints = Raw<WaypointDefinitionGroup>(); Check(ReferenceEquals(Comparer(waypoints), HardlightEnumComparers.WaypointTypeComparer), "waypoint group exact readonly comparer");
            waypoints.m_elements = new[] { Row<WaypointType,WaypointDefinition>(waypoint) }; Check(ReferenceEquals(waypoints.GetData()[(WaypointType)(-321)], waypoint), "waypoint own Type dictionary key");
            waypoints.m_elements = new[] { Row<WaypointType,WaypointDefinition>(waypoint), Row<WaypointType,WaypointDefinition>(waypoint) }; Throws<ArgumentException>(() => waypoints.GetData(), "duplicate waypoint key not overwritten");
            return checks;
        }
        public static int RunEngine()
        {
            checks = 0; var owned = new List<UnityEngine.Object>();
            try
            {
                var streak = ScriptableObject.CreateInstance<MissionScorerStreakDefinition>(); owned.Add(streak);
                var streakCopy = ScriptableObject.CreateInstance<MissionScorerStreakDefinition>(); owned.Add(streakCopy);
                var streakGroup = ScriptableObject.CreateInstance<MissionScorerStreakDefinitionGroup>(); owned.Add(streakGroup);
                var band = ScriptableObject.CreateInstance<DreamPowerStoreBandDefinition>(); owned.Add(band);
                var bandCopy = ScriptableObject.CreateInstance<DreamPowerStoreBandDefinition>(); owned.Add(bandCopy);
                var bandOther = ScriptableObject.CreateInstance<DreamPowerStoreBandDefinition>(); owned.Add(bandOther);
                var bandGroup = ScriptableObject.CreateInstance<DreamPowerStoreBandDefinitionGroup>(); owned.Add(bandGroup);
                var waypoint = ScriptableObject.CreateInstance<WaypointDefinition>(); owned.Add(waypoint);
                var waypointGroup = ScriptableObject.CreateInstance<WaypointDefinitionGroup>(); owned.Add(waypointGroup);
                Check(streak != null && streakCopy != null && streakGroup != null && band != null && bandCopy != null && bandOther != null && bandGroup != null && waypoint != null && waypointGroup != null, "all genuine concrete definitions/groups create");
                Check(((IList)Read(streak, typeof(MissionScorerStreakDefinition), "m_enemyTypes")).Count == 0 && ((IList)Read(streak, typeof(MissionScorerStreakDefinition), "m_collectableTypes")).Count == 0 && ((IList)Read(streak, typeof(MissionScorerStreakDefinition), "m_scoreThresholds")).Count == 0, "actual streak constructor creates three independent empty lists");
                Check(!ReferenceEquals(Read(streak, typeof(MissionScorerStreakDefinition), "m_enemyTypes"), Read(streakCopy, typeof(MissionScorerStreakDefinition), "m_enemyTypes")) && streak.GetGUID() == "", "actual per-instance lists and genuine GUID base");
                Check(streak.GetScore(100) == 0 && !streak.HasEnemyType((EnemyType)0) && !streak.HasCollectableType((CollectableType)0), "actual empty authored lists return native defaults");
                Set(streak, typeof(MissionScorerStreakDefinition), "m_identifier", (MissionScorerStreakIdentifier)(-76)); Set(streak, typeof(MissionScorerStreakDefinition), "m_excludeFromAutoGeneration", true); Set(streak, typeof(MissionScorerStreakDefinition), "m_damageableObject", true);
                Set(streak, typeof(MissionScorerStreakDefinition), "m_enemyTypes", new List<EnemyType> { (EnemyType)27 }); Set(streak, typeof(MissionScorerStreakDefinition), "m_collectableTypes", new List<CollectableType> { (CollectableType)(-28) });
                Set(streak, typeof(MissionScorerStreakDefinition), "m_minCount", 2.5f); Set(streak, typeof(MissionScorerStreakDefinition), "m_timeout", 8.5f); Set(streak, typeof(MissionScorerStreakDefinition), "m_usesComboMultiplier", true); Thresholds(streak, 2, 17, 5, 35);
                string streakJson = JsonUtility.ToJson(streak); JsonUtility.FromJsonOverwrite(streakJson, streakCopy);
                Check((int)streakCopy.Identifier == -76 && streakCopy.ExcludeFromAutoGeneration && streakCopy.DamageableObject && streakCopy.UsesComboMultiplier && streakCopy.MinCount == 2.5f && streakCopy.Timeout == 8.5f, "actual private scalar streak field JSON roundtrip");
                Check(streakCopy.HasEnemyType((EnemyType)27) && streakCopy.HasCollectableType((CollectableType)(-28)), "actual private enum lists JSON roundtrip");
                Check(streakCopy.GetScore(1) == 0 && streakCopy.GetScore(2) == 17 && streakCopy.GetScore(5) == 35, "actual private Serializable threshold graph JSON roundtrip");
                streakGroup.m_elements = new[] { new DefinitionDataType<MissionScorerStreakIdentifier,MissionScorerStreakDefinition>.DefinitionElement<MissionScorerStreakDefinition>(streakCopy) };
                Check(ReferenceEquals(streakGroup.GetData()[(MissionScorerStreakIdentifier)(-76)], streakCopy), "actual streak group uses authored identifier");

                Check(Fresh(band.ImageAsset) && Fresh(band.SlotLockedBackground), "actual OnEnable initializes both wrappers without loads");
                Check(band.GetGUID() == "" && bandOther.GetGUID() == "", "actual band GUID empty initializer");
                var imageRef = new AssetReferenceAtlasedSprite("00112233445566778899aabbccddeeff"); var backgroundRef = new AssetReferenceAtlasedSprite("ffeeddccbbaa99887766554433221100");
                Set(band, typeof(DreamPowerStoreBandDefinition), "m_type", (DreamPowerStoreBandType)(-71)); Set(band, typeof(DreamPowerStoreBandDefinition), "m_name", (Strings)72); Set(band, typeof(DreamPowerStoreBandDefinition), "m_image", imageRef); Set(band, typeof(DreamPowerStoreBandDefinition), "m_slotLockedBackground", backgroundRef);
                var widgetObject = new GameObject("ProjectLucid_ProgressionDefinitionFixture"); owned.Add(widgetObject); var widget = widgetObject.AddComponent<UIWidgetProgression>();
                Set(band, typeof(DreamPowerStoreBandDefinition), "m_progressionUnlockWidget", widget);
                var firstImage = band.ImageAsset; var firstBackground = band.SlotLockedBackground; Enable(band);
                Check(ReferenceEquals(Retained(band.ImageAsset), imageRef) && ReferenceEquals(Retained(band.SlotLockedBackground), backgroundRef), "actual enable retains corresponding authored AssetReferences");
                Check(!ReferenceEquals(firstImage, band.ImageAsset) && !ReferenceEquals(firstBackground, band.SlotLockedBackground) && Fresh(firstImage) && Fresh(firstBackground), "actual replacement preserves old wrapper state without release");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(band), bandCopy);
                Check((int)bandCopy.Type == -71 && (int)bandCopy.Name == 72, "actual private band type/localization key JSON");
                Check(ReferenceEquals(bandCopy.ProgressionUnlockWidget, widget), "actual private serialized widget field retains genuine original Component");
                Check(((AssetReferenceAtlasedSprite)Read(bandCopy, typeof(DreamPowerStoreBandDefinition), "m_image")).AssetGUID == imageRef.AssetGUID && ((AssetReferenceAtlasedSprite)Read(bandCopy, typeof(DreamPowerStoreBandDefinition), "m_slotLockedBackground")).AssetGUID == backgroundRef.AssetGUID, "actual complete package AssetReference serialized graph keeps distinct keys");
                Enable(bandCopy); Check(ReferenceEquals(Retained(bandCopy.ImageAsset), Read(bandCopy, typeof(DreamPowerStoreBandDefinition), "m_image")), "explicit enable after deserialization captures current reference");
                bandGroup.m_elements = new[] { new DefinitionDataType<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>(band) };
                Check(bandGroup.GetIndex(bandOther) == 0, "different real objects with same empty GUID compare equal");
                Set(bandOther, typeof(ScriptableObjectWithGuid), "m_guid", "other"); Check(bandGroup.GetIndex(bandOther) == -1, "different real GUID values do not match");
                Set(band, typeof(ScriptableObjectWithGuid), "m_guid", "other"); Check(bandGroup.GetIndex(bandOther) == 0, "same nonempty GUID compares equal across instances");
                Check(bandGroup.GetIndex(null) == -1, "live authored object versus actual null retains operator rule");
                bandGroup.m_elements = new[] { new DefinitionDataType<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>(band), new DefinitionDataType<DreamPowerStoreBandType,DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>(bandOther) };
                Check(bandGroup.GetIndex(bandOther) == 0, "first GUID-equivalent row wins authored array order");

                var map = (SerializableDictionary<WaypointContainerType,WaypointSettings>)Read(waypoint, typeof(WaypointDefinition), "m_settingsByContainerType");
                Check(map != null && map.Count == 0 && ReferenceEquals(DictionaryComparer(map), HardlightEnumComparers.WaypointContainerTypeComparer), "actual waypoint constructor real empty dictionary/exact comparer");
                var texture = new Texture2D(2, 2); owned.Add(texture); var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f)); owned.Add(sprite);
                var settings = new WaypointSettings(); Set(settings, typeof(WaypointSettings), "m_icon", sprite); Set(settings, typeof(WaypointSettings), "m_showHeightIndicatorThreshold", 3); Set(settings, typeof(WaypointSettings), "m_minDrawDistance", -4); Set(settings, typeof(WaypointSettings), "m_maxDrawDistance", 5); Set(settings, typeof(WaypointSettings), "m_showDistanceIndicator", false);
                var copy = JsonUtility.FromJson<WaypointSettings>(JsonUtility.ToJson(settings));
                Check(ReferenceEquals(copy.Icon, sprite) && copy.ShowHeightIndicatorThreshold == 3 && copy.MinDrawDistance == -4 && copy.MaxDrawDistance == 5 && !copy.ShowDistanceIndicator, "actual settings JSON retains all five private fields/real Sprite");
                map.Add((WaypointContainerType)(-99), copy); WaypointSettings found;
                Check(waypoint.TryGetSettingsByContainerType((WaypointContainerType)(-99), out found) && ReferenceEquals(found, copy), "actual dictionary TryGet retains owned settings");
                Set(waypoint, typeof(WaypointDefinition), "m_type", (WaypointType)101);
                waypointGroup.m_elements = new[] { new DefinitionDataType<WaypointType,WaypointDefinition>.DefinitionElement<WaypointDefinition>(waypoint) };
                Check(ReferenceEquals(waypointGroup.GetData()[(WaypointType)101], waypoint), "actual waypoint group authored type key");
                Check(streakGroup.m_elements[0].Name == streakCopy.name && bandGroup.m_elements[0].Name == band.name && waypointGroup.m_elements[0].Name == waypoint.name, "genuine inherited row constructors use current engine names");
                return checks;
            }
            finally
            { for (int i = owned.Count - 1; i >= 0; --i) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]); }
        }
        public static void Run()
        { int managed = RunManaged(); int engine = RunEngine(); Debug.Log("PASS original progression/waypoint definitions: " + managed + " managed + " + engine + " engine; supplied content/App startup remains unverified."); }
    }
}
