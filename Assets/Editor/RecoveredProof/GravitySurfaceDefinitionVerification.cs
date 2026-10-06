using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class GravitySurfaceDefinitionVerification
    {
        private static readonly string[] ReferenceFields = {
            "MetadataGroupKey", "m_metadataGravityKey", "m_metadataInnerFalloffDistanceKey",
            "m_metadataInnerDistanceKey", "m_metadataOuterDistanceKey", "m_metadataOuterFalloffDistanceKey"
        };
        private static void Require(bool value, string label, ref int count)
        { if (!value) throw new InvalidOperationException("GravitySurfaceDefinition: " + label); ++count; }
        private static object Call(string name, GravitySurfaceDefinitionGroup group, params object[] args)
        { return typeof(GravitySurfaceDefinitionGroup).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(group, args); }
        private static void MissingReferences(GravitySurfaceDefinition value, ref int count)
        {
            foreach (string name in ReferenceFields)
                Require(ReferenceEquals(typeof(GravitySurfaceDefinition).GetField(name).GetValue(value), null), "original null field " + name, ref count);
        }

        // Raw instances bypass ScriptableObject construction; no native object/name
        // operators are requested. The real complete class and comparer still run.
        public static int RunManaged()
        {
            int n = 0;
            var fields = typeof(GravitySurfaceDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Require(fields.Length == 7, "seven complete public fields", ref n);
            Require(typeof(GravitySurfaceDefinitionGroup).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 0, "original group has no own fields", ref n);
            Require(typeof(GravitySurfaceDefinitionGroup).BaseType == typeof(DefinitionDataType<GravitySurfaceType, GravitySurfaceDefinition>), "exact original generic base", ref n);
            foreach (var f in fields)
                Require(f.IsPublic && !f.IsInitOnly && f.GetCustomAttribute<TooltipAttribute>() != null, "original mutable tooltip field " + f.Name, ref n);
            Require(typeof(GravitySurfaceDefinition).GetField("Type").IsDefined(typeof(SerializeField), false), "redundant original Type SerializeField retained", ref n);
            var d = (GravitySurfaceDefinition)FormatterServices.GetUninitializedObject(typeof(GravitySurfaceDefinition));
            var group = (GravitySurfaceDefinitionGroup)FormatterServices.GetUninitializedObject(typeof(GravitySurfaceDefinitionGroup));
            MissingReferences(d, ref n);
            Require((int)d.Type == 0, "raw enum defaults to zero, not named Default", ref n);
            Require(group.m_elements == null, "raw inherited elements null", ref n);
            int[] values = { 0, (int)GravitySurfaceType.Default, int.MinValue, int.MaxValue, -1 };
            var comparer = (IEqualityComparer<GravitySurfaceType>)Call("GetKeyComparer", group);
            Require(ReferenceEquals(comparer, HardlightEnumComparers.GravitySurfaceTypeComparer), "original generated comparer singleton", ref n);
            for (int i = 0; i < values.Length; ++i)
            {
                d.Type = (GravitySurfaceType)values[i];
                Require((GravitySurfaceType)Call("GetElementKey", group, d) == d.Type, "raw key bits " + i, ref n);
                Require(comparer.Equals(d.Type, d.Type), "self equal " + i, ref n);
                Require(!comparer.Equals(d.Type, (GravitySurfaceType)values[(i + 1) % values.Length]), "different key " + i, ref n);
                Require(comparer.GetHashCode(d.Type) == values[i], "original enum hash bits " + i, ref n);
            }
            group.m_elements = Array.Empty<DefinitionDataType<GravitySurfaceType, GravitySurfaceDefinition>.DefinitionElement<GravitySurfaceDefinition>>();
            var empty = group.GetData();
            Require(empty.Count == 0, "real empty data dictionary", ref n);
            Require(ReferenceEquals(empty.Comparer, comparer), "real dictionary consumes authored comparer", ref n);
            group.m_elements = null;
            bool nullArray = false;
            try { group.GetData(); } catch (NullReferenceException) { nullArray = true; }
            Require(nullArray, "CLR-only inherited null array behavior", ref n);
            return n;
        }

        public static int RunEngine()
        {
            int n = 0;
            GravitySurfaceDefinition first = null, second = null, copy = null;
            GravitySurfaceDefinitionGroup group = null;
            try
            {
                first = ScriptableObject.CreateInstance<GravitySurfaceDefinition>();
                second = ScriptableObject.CreateInstance<GravitySurfaceDefinition>();
                copy = ScriptableObject.CreateInstance<GravitySurfaceDefinition>();
                group = ScriptableObject.CreateInstance<GravitySurfaceDefinitionGroup>();
                first.name = "Lucid Owned Gravity A"; second.name = "Lucid Owned Gravity B";
                MissingReferences(first, ref n);
                Require((int)first.Type == 0, "real constructor keeps enum zero", ref n);
                Require(group.m_elements == null, "real generic constructor retains null elements", ref n);
                first.Type = GravitySurfaceType.Default; second.Type = (GravitySurfaceType)int.MinValue;
                var a = new DefinitionDataType<GravitySurfaceType, GravitySurfaceDefinition>.DefinitionElement<GravitySurfaceDefinition>(first);
                var b = new DefinitionDataType<GravitySurfaceType, GravitySurfaceDefinition>.DefinitionElement<GravitySurfaceDefinition>(second);
                Require(ReferenceEquals(a.Data, first) && ReferenceEquals(b.Data, second), "genuine elements retain owned definitions", ref n);
                Require(a.Name == first.name, "genuine element generates first native name", ref n);
                Require(b.Name == second.name, "genuine element generates second native name", ref n);
                group.m_elements = new[] { a, b };
                Dictionary<GravitySurfaceType, GravitySurfaceDefinition> values = group.GetData();
                Require(values.Count == 2, "genuine inherited loading creates both entries", ref n);
                Require(ReferenceEquals(values[first.Type], first), "real authored first key", ref n);
                Require(ReferenceEquals(values[second.Type], second), "real unrecognized enum bits retained", ref n);
                Require(ReferenceEquals(values.Comparer, HardlightEnumComparers.GravitySurfaceTypeComparer), "real loaded dictionary comparer", ref n);
                second.Type = first.Type;
                bool duplicate = false;
                try { group.GetData(); } catch (ArgumentException) { duplicate = true; }
                Require(duplicate, "original duplicate Add failure retained", ref n);
                first.Type = (GravitySurfaceType)int.MinValue;
                string json = JsonUtility.ToJson(first);
                JsonUtility.FromJsonOverwrite(json, copy);
                Require(copy.Type == first.Type, "real JSON retains raw enum bits", ref n);
                MissingReferences(copy, ref n);
                Require(JsonUtility.ToJson(copy) == json, "real JSON serialized shape stable", ref n);
                return n;
            }
            finally
            {
                try { if (group != null) UnityEngine.Object.DestroyImmediate(group); }
                finally
                {
                    try { if (copy != null) UnityEngine.Object.DestroyImmediate(copy); }
                    finally
                    {
                        try { if (second != null) UnityEngine.Object.DestroyImmediate(second); }
                        finally { if (first != null) UnityEngine.Object.DestroyImmediate(first); }
                    }
                }
            }
        }
    }
}
