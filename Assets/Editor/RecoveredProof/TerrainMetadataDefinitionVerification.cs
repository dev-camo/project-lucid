using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Editor
{
    public static class TerrainMetadataDefinitionVerification
    {
        private static int checks;
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); ++checks; }
        private static FieldInfo Field(string name) => typeof(TerrainMetadataDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void Set(TerrainMetadataDefinition value, string name, object data) => Field(name).SetValue(value, data);

        public static int RunManaged()
        {
            checks = 0;
            // This deliberately bypasses Unity construction. Actual construction
            // and JsonUtility serialization have separate unrun engine fixtures.
            var value = Raw<TerrainMetadataDefinition>();
            Check(ReferenceEquals(value.MetadataGroupKey, null), "zeroed group is returned unchanged");
            Check(ReferenceEquals(value.MetadataMovementKey, null), "zeroed movement is returned unchanged");
            Check((int)value.MetadataType == 0, "zeroed enum is returned unchanged");
            var a = Raw<MetadataGroupKey>(); var b = Raw<MetadataGroupKey>();
            Set(value, "m_metadataGroupKey", a);
            Check(ReferenceEquals(value.MetadataGroupKey, a), "group reference retained");
            Set(value, "m_metadataGroupKey", b);
            Check(ReferenceEquals(value.MetadataGroupKey, b), "getter reloads current group field");
            Set(value, "m_metadataGroupKey", null);
            Check(ReferenceEquals(value.MetadataGroupKey, null), "null group is not replaced");
            var keyA = Raw<MetadataKeyType>(); var keyB = Raw<MetadataKeyType>();
            Set(value, "m_metadataMovementKey", keyA);
            Check(ReferenceEquals(value.MetadataMovementKey, keyA), "movement reference retained");
            Set(value, "m_metadataMovementKey", keyB);
            Check(ReferenceEquals(value.MetadataMovementKey, keyB), "getter reloads movement field");
            Set(value, "m_metadataMovementKey", null);
            Check(ReferenceEquals(value.MetadataMovementKey, null), "null movement is not replaced");
            foreach (int bits in new[] { 0, 1, -1, int.MinValue, int.MaxValue, 0x12345678 })
            {
                Set(value, "m_metadataType", (TerrainMetadataType)bits);
                Check((int)value.MetadataType == bits, "enum bits retained " + bits);
            }
            Check(ReferenceEquals(value.MetadataGroupKey, null) && ReferenceEquals(value.MetadataMovementKey, null), "enum updates do not alter key fields");
            Check(typeof(TerrainMetadataDefinition).BaseType == typeof(ScriptableObject), "original ScriptableObject base");
            var fields = typeof(TerrainMetadataDefinition).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_metadataGroupKey", "m_metadataType", "m_metadataMovementKey" }), "complete original field order");
            Check(fields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(MetadataGroupKey), typeof(TerrainMetadataType), typeof(MetadataKeyType) }), "complete original field types");
            foreach (var field in fields)
            {
                Check(field.IsPrivate && !field.IsInitOnly && !field.IsStatic, "private mutable field " + field.Name);
                Check(field.IsDefined(typeof(SerializeField), false), "serialized field " + field.Name);
            }
            Check(Field("m_metadataGroupKey").GetCustomAttribute<TooltipAttribute>().tooltip == "Metadata group key", "group tooltip");
            Check(Field("m_metadataType").GetCustomAttribute<TooltipAttribute>().tooltip == "Defines the terrain type.", "terrain tooltip");
            Check(Field("m_metadataMovementKey").GetCustomAttribute<TooltipAttribute>().tooltip == "Key for movement metadata.", "movement tooltip");
            Check(Field("m_metadataType").GetCustomAttribute<FormerlySerializedAsAttribute>().oldName == "metadataType", "original serialized rename");
            var menu = typeof(TerrainMetadataDefinition).GetCustomAttribute<CreateAssetMenuAttribute>();
            Check(menu.fileName == "TerrainMetadataDefinition" && menu.menuName == "HardlightProject/DefinitionData/Definitions/TerrainMetadataDefinition", "original asset menu strings");
            var options = typeof(TerrainMetadataDefinition).GetCustomAttributes<Il2CppSetOptionAttribute>().ToArray();
            Check(options.Select(o => o.Option).SequenceEqual(new[] { Option.ArrayBoundsChecks, Option.NullChecks }) && options.All(o => o.Value is bool && !(bool)o.Value), "exact original IL2CPP option order and values");
            Check(typeof(TerrainMetadataDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).All(v => v.GetMethod != null && v.SetMethod == null), "all original properties are getter only");
            return checks;
        }

        public static int RunEngine()
        {
            checks = 0; TerrainMetadataDefinition value = null; MetadataGroupKey group = null; MetadataKeyType key = null;
            try
            {
                value = ScriptableObject.CreateInstance<TerrainMetadataDefinition>();
                Check(value != null, "real concrete terrain object construction");
                Check(ReferenceEquals(value.MetadataGroupKey, null), "constructor leaves group null");
                Check(ReferenceEquals(value.MetadataMovementKey, null), "constructor leaves movement null");
                Check((int)value.MetadataType == 0, "constructor leaves enum zero");
                group = ScriptableObject.CreateInstance<MetadataGroupKey>(); key = ScriptableObject.CreateInstance<MetadataKeyType>();
                Check(group != null && key != null, "real complete HLS key construction");
                Set(value, "m_metadataGroupKey", group); Set(value, "m_metadataMovementKey", key); Set(value, "m_metadataType", (TerrainMetadataType)(-19));
                Check(ReferenceEquals(value.MetadataGroupKey, group), "real group reference getter");
                Check(ReferenceEquals(value.MetadataMovementKey, key), "real movement reference getter");
                Check((int)value.MetadataType == -19, "real signed enum getter");
                string json = JsonUtility.ToJson(value);
                Check(json.Contains("\"m_metadataGroupKey\"") && json.Contains("\"m_metadataMovementKey\""), "actual serializer includes key fields");
                Check(json.Contains("\"m_metadataType\":-19"), "actual serializer includes raw enum bits");
                Set(value, "m_metadataGroupKey", null); Set(value, "m_metadataMovementKey", null); Set(value, "m_metadataType", (TerrainMetadataType)42);
                JsonUtility.FromJsonOverwrite(json, value);
                Check(ReferenceEquals(value.MetadataGroupKey, group), "JsonUtility restores original group object identity");
                Check(ReferenceEquals(value.MetadataMovementKey, key), "JsonUtility restores original key object identity");
                Check((int)value.MetadataType == -19, "JsonUtility restores signed raw enum");
                string again = JsonUtility.ToJson(value);
                Check(again == json, "actual same-process serialized roundtrip");
                JsonUtility.FromJsonOverwrite("{\"m_metadataType\":2147483647}", value);
                Check((int)value.MetadataType == int.MaxValue, "actual serializer does not normalize unknown enum");
                Check(ReferenceEquals(value.MetadataGroupKey, group) && ReferenceEquals(value.MetadataMovementKey, key), "partial overwrite retains original key references");
                return checks;
            }
            finally
            {
                try { if (!ReferenceEquals(value, null)) UnityEngine.Object.DestroyImmediate(value); }
                finally { try { if (!ReferenceEquals(group, null)) UnityEngine.Object.DestroyImmediate(group); } finally { if (!ReferenceEquals(key, null)) UnityEngine.Object.DestroyImmediate(key); } }
            }
        }
    }
}
