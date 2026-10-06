using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class TrackerMetadataDefinitionsVerification
    {
        private static int checks;
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); ++checks; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private readonly struct Pair
        {
            public readonly Type Owner; public readonly string FieldName, PropertyName;
            public Pair(Type owner, string fieldName, string propertyName) { Owner = owner; FieldName = fieldName; PropertyName = propertyName; }
            public FieldInfo Field => Owner.GetField(FieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            public PropertyInfo Property => Owner.GetProperty(PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        }
        private readonly struct Row
        { public readonly Type Type; public readonly Pair[] Pairs; public Row(Type type, Pair[] pairs) { Type = type; Pairs = pairs; } }
        private static Row[] Rows() => new Row[]
        {
                new Row(typeof(TerrainTrackerDefinition), new Pair[] { new Pair(typeof(TerrainTrackerDefinition), "m_metadataGroupKey", "MetadataGroupKey"), new Pair(typeof(TerrainTrackerDefinition), "m_metadataType", "MetadataType") }),
                new Row(typeof(HalfPipeDefinition), new Pair[] { new Pair(typeof(HalfPipeDefinition), "m_metadataGroupKey", "MetadataGroupKey"), new Pair(typeof(HalfPipeDefinition), "m_metadataTrajectoryKey", "MetadataTrajectoryKey"), new Pair(typeof(HalfPipeDefinition), "m_metadataIgnoreKey", "MetadataIgnoreKey"), new Pair(typeof(HalfPipeDefinition), "m_type", "Type") }),
                new Row(typeof(LightspeedDashDefinition), new Pair[] { new Pair(typeof(LightspeedDashDefinition), "m_type", "Type") , new Pair(typeof(TrackerMetadataDefinition), "m_metadataGroupKey", "MetadataGroupKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataMotionKey", "MetadataMotionKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataEnterKey", "MetadataEnterKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataExitKey", "MetadataExitKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataForwardKey", "MetadataForwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataBackwardKey", "MetadataBackwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataCameraKey", "MetadataCameraKey") }),
                new Row(typeof(RailDefinition), new Pair[] { new Pair(typeof(RailDefinition), "m_metadataAutoRailKey", "MetadataAutoRailKey"), new Pair(typeof(RailDefinition), "m_metadataControlsKey", "MetadataControlsKey"), new Pair(typeof(RailDefinition), "m_metadataGravityKey", "MetadataGravityKey"), new Pair(typeof(RailDefinition), "m_metadataTargetableKey", "MetadataTargetableKey"), new Pair(typeof(RailDefinition), "m_type", "Type") , new Pair(typeof(TrackerMetadataDefinition), "m_metadataGroupKey", "MetadataGroupKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataMotionKey", "MetadataMotionKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataEnterKey", "MetadataEnterKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataExitKey", "MetadataExitKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataForwardKey", "MetadataForwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataBackwardKey", "MetadataBackwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataCameraKey", "MetadataCameraKey") }),
                new Row(typeof(TransporterDefinition), new Pair[] { new Pair(typeof(TransporterDefinition), "m_type", "Type") , new Pair(typeof(TrackerMetadataDefinition), "m_metadataGroupKey", "MetadataGroupKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataMotionKey", "MetadataMotionKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataEnterKey", "MetadataEnterKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataExitKey", "MetadataExitKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataForwardKey", "MetadataForwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataBackwardKey", "MetadataBackwardKey"), new Pair(typeof(TrackerMetadataDefinition), "m_metadataCameraKey", "MetadataCameraKey") }),
        };

        public static int RunManaged()
        {
            checks = 0;
            // Constructor-free objects isolate field access and virtual dispatch.
            // Actual original SO construction and serialization remain RunEngine.
            foreach (var row in Rows())
            {
                object value = FormatterServices.GetUninitializedObject(row.Type);
                foreach (var pair in row.Pairs)
                {
                    if (pair.Field.FieldType.IsEnum)
                    {
                        Check(Convert.ToInt32(pair.Property.GetValue(value)) == 0, row.Type.Name + pair.PropertyName + " default enum load");
                        foreach (int bits in new[] { -1, int.MinValue, int.MaxValue, 0x12345678 })
                        { pair.Field.SetValue(value, Enum.ToObject(pair.Field.FieldType, bits)); Check(Convert.ToInt32(pair.Property.GetValue(value)) == bits, row.Type.Name + pair.PropertyName + " raw enum bits " + bits); }
                    }
                    else
                    {
                        Check(ReferenceEquals(pair.Property.GetValue(value), null), row.Type.Name + pair.PropertyName + " null remains null");
                        object first = FormatterServices.GetUninitializedObject(pair.Field.FieldType);
                        object second = FormatterServices.GetUninitializedObject(pair.Field.FieldType);
                        pair.Field.SetValue(value, first); Check(ReferenceEquals(pair.Property.GetValue(value), first), row.Type.Name + pair.PropertyName + " exact first reference");
                        pair.Field.SetValue(value, second); Check(ReferenceEquals(pair.Property.GetValue(value), second), row.Type.Name + pair.PropertyName + " reload changed reference");
                    }
                }
                if (value is MetadataDefinition definition)
                {
                    var pair = row.Pairs.Single(p => p.PropertyName == "MetadataGroupKey");
                    Check(ReferenceEquals(definition.MetadataGroupKey, pair.Field.GetValue(value)), row.Type.Name + " genuine original base virtual slot dispatch");
                }
            }
            Check(typeof(MetadataDefinition).IsAbstract && typeof(TrackerMetadataDefinition).IsAbstract, "original abstract owners remain abstract");
            Check(typeof(MetadataDefinition).BaseType == typeof(ScriptableObject) && typeof(TrackerMetadataDefinition).BaseType == typeof(MetadataDefinition), "original complete abstract base hierarchy");
            Check(typeof(TerrainTrackerDefinition).BaseType == typeof(ScriptableObject), "terrain tracker original SO base");
            Check(typeof(HalfPipeDefinition).BaseType == typeof(MetadataDefinition), "half pipe original direct metadata base");
            Check(new[] { typeof(RailDefinition), typeof(LightspeedDashDefinition), typeof(TransporterDefinition) }.All(t => t.BaseType == typeof(TrackerMetadataDefinition)), "tracker descendants retain exact original immediate base");
            var groupGetter = typeof(TrackerMetadataDefinition).GetProperty("MetadataGroupKey").GetMethod;
            Check(groupGetter.GetBaseDefinition().DeclaringType == typeof(MetadataDefinition) && !groupGetter.IsAbstract, "original virtual slot overridden by abstract tracker owner body");
            Check(typeof(TerrainTrackerDefinition).GetField("SplineTurnAngleMax").FieldType == typeof(AnimationCurve), "complete original public curve field retained");
            return checks;
        }

        // Attempt every owned object even if an earlier disposal fails.
        private static void DisposeAll(List<ScriptableObject> owned, int index)
        { if (index < 0) return; try { if (!ReferenceEquals(owned[index], null)) UnityEngine.Object.DestroyImmediate(owned[index]); } finally { DisposeAll(owned, index - 1); } }

        public static int RunEngine()
        {
            checks = 0; var values = new List<ScriptableObject>(); MetadataGroupKey group = null; MetadataKeyType key = null;
            try
            {
                foreach (var row in Rows())
                {
                    var value = ScriptableObject.CreateInstance(row.Type); values.Add(value);
                    Check(value != null, row.Type.Name + " genuine concrete SO constructor");
                    foreach (var pair in row.Pairs) Check(pair.Field.FieldType.IsEnum ? Convert.ToInt32(pair.Property.GetValue(value)) == 0 : ReferenceEquals(pair.Property.GetValue(value), null), row.Type.Name + pair.PropertyName + " original constructor zero/null");
                }
                var terrain = (TerrainTrackerDefinition)values.Single(v => v is TerrainTrackerDefinition);
                Check(terrain.SplineTurnAngleMax == null, "native terrain constructor leaves AnimationCurve null");
                group = ScriptableObject.CreateInstance<MetadataGroupKey>(); key = ScriptableObject.CreateInstance<MetadataKeyType>();
                Check(group != null && key != null, "real maintained HLS key constructors");
                terrain.SplineTurnAngleMax = new AnimationCurve(new Keyframe(0f, 5f), new Keyframe(1f, 17f));
                var rows = Rows();
                for (int i = 0; i < rows.Length; ++i)
                {
                    var row = rows[i]; var value = values[i];
                    foreach (var pair in row.Pairs) pair.Field.SetValue(value, pair.Field.FieldType.IsEnum ? Enum.ToObject(pair.Field.FieldType, -19) : pair.Field.FieldType == typeof(MetadataGroupKey) ? (object)group : key);
                    string json = JsonUtility.ToJson(value);
                    Check(row.Pairs.All(p => json.Contains("\"" + p.FieldName + "\"")), row.Type.Name + " serializer includes complete own and inherited private fields");
                    foreach (var pair in row.Pairs) pair.Field.SetValue(value, pair.Field.FieldType.IsEnum ? Enum.ToObject(pair.Field.FieldType, 0) : null);
                    JsonUtility.FromJsonOverwrite(json, value);
                    Check(row.Pairs.All(p => p.Field.FieldType.IsEnum ? Convert.ToInt32(p.Property.GetValue(value)) == -19 : ReferenceEquals(p.Property.GetValue(value), p.Field.FieldType == typeof(MetadataGroupKey) ? (object)group : key)), row.Type.Name + " actual same-process enum/reference roundtrip");
                    Check(JsonUtility.ToJson(value) == json, row.Type.Name + " stable serialized roundtrip");
                }
                Check(terrain.SplineTurnAngleMax != null && terrain.SplineTurnAngleMax.length == 2, "public AnimationCurve actual serializer preserves keys");
                Check(terrain.SplineTurnAngleMax.Evaluate(0f) == 5f && terrain.SplineTurnAngleMax.Evaluate(1f) == 17f, "actual public curve serialization endpoints");
                return checks;
            }
            finally
            {
                try { DisposeAll(values, values.Count - 1); }
                finally { try { if (!ReferenceEquals(group, null)) UnityEngine.Object.DestroyImmediate(group); } finally { if (!ReferenceEquals(key, null)) UnityEngine.Object.DestroyImmediate(key); } }
            }
        }
    }
}
