using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class TrackerDefinitionGroupsVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); ++checks; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo TypeField(Type owner) => owner.GetField("m_type", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static object Invoke(MethodInfo method, object instance, params object[] args) => method.Invoke(instance, args);
        private static void Expect<T>(Action action, string label) where T : Exception
        { try { action(); } catch (T) { ++checks; return; } throw new InvalidOperationException(label); }

        private static void Managed<TKey, TData, TGroup>(IEqualityComparer<TKey> expected)
            where TData : MetadataDefinition where TGroup : DefinitionDataType<TKey, TData>
        {
            var group = Raw<TGroup>(); var first = Raw<TData>(); var second = Raw<TData>(); var field = TypeField(typeof(TData));
            var keyMethod = typeof(TGroup).GetMethod("GetElementKey", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var comparerMethod = typeof(TGroup).GetMethod("GetKeyComparer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(typeof(TGroup).BaseType == typeof(DefinitionDataType<TKey, TData>), typeof(TGroup).Name + " exact original closed base");
            Check(typeof(TGroup).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0, "original group owns zero fields");
            Check(group.m_elements == null, "constructor-free inherited array zero state only; engine constructors separate");
            Check(ReferenceEquals(Invoke(comparerMethod, group), expected), "exact genuine comparer registry instance");
            Check(ReferenceEquals(Invoke(comparerMethod, group), expected), "repeated comparer reload retains shared instance");
            foreach (int bits in new[] { -1, int.MinValue, int.MaxValue, 0x12345678 })
            { field.SetValue(first, Enum.ToObject(typeof(TKey), bits)); Check(Convert.ToInt32(Invoke(keyMethod, group, first)) == bits, "original key keeps raw signed enum bits " + bits); }
            group.m_elements = new DefinitionDataType<TKey, TData>.DefinitionElement<TData>[0];
            var empty = group.GetData(); Check(empty.Count == 0, "real generic base empty dictionary"); Check(ReferenceEquals(empty.Comparer, expected), "empty dictionary exact registry comparer"); Check(!ReferenceEquals(group.GetData(), empty), "original base creates fresh dictionary each call");
            var a = Raw<DefinitionDataType<TKey, TData>.DefinitionElement<TData>>(); var b = Raw<DefinitionDataType<TKey, TData>.DefinitionElement<TData>>(); a.Data = first; b.Data = second;
            var keyA = (TKey)Enum.ToObject(typeof(TKey), -19); var keyB = (TKey)Enum.ToObject(typeof(TKey), 37); field.SetValue(first, keyA); field.SetValue(second, keyB); group.m_elements = new[] { a, b };
            var result = group.GetData(); Check(result.Count == 2, "two original typed definitions"); Check(ReferenceEquals(result[keyA], first) && ReferenceEquals(result[keyB], second), "exact key/object pairs from maintained base"); Check(ReferenceEquals(result.Comparer, expected), "populated dictionary original registry comparer");
            field.SetValue(first, keyB); field.SetValue(second, keyA); var changed = group.GetData(); Check(ReferenceEquals(changed[keyB], first) && ReferenceEquals(changed[keyA], second), "each original key accessor reloads changed field"); Check(ReferenceEquals(result[keyA], first), "prior dictionary snapshot is retained");
            field.SetValue(second, keyB); Expect<ArgumentException>(() => group.GetData(), "original duplicate Add failure"); Check(ReferenceEquals(group.m_elements[0], a) && ReferenceEquals(group.m_elements[1], b), "duplicate failure does not rewrite authored array");
            group.m_elements = null; Expect<NullReferenceException>(() => group.GetData(), "managed null array fault; no native invalid-input equivalence claim");
            group.m_elements = new DefinitionDataType<TKey, TData>.DefinitionElement<TData>[] { null }; Expect<NullReferenceException>(() => group.GetData(), "managed null wrapper fault");
            a.Data = null; group.m_elements = new[] { a }; Expect<NullReferenceException>(() => group.GetData(), "managed null definition fault");
            try { Invoke(keyMethod, group, new object[] { null }); throw new InvalidOperationException("managed direct null getter did not fault"); }
            catch (TargetInvocationException exception) { Check(exception.InnerException is NullReferenceException, "managed protected getter fault preserves reflection wrapper"); }
        }

        public static int RunManaged()
        {
            checks = 0;
            Managed<HalfPipeType, HalfPipeDefinition, HalfPipeDefinitionGroup>(HardlightEnumComparers.HalfPipeTypeComparer);
            Managed<LightspeedDashType, LightspeedDashDefinition, LightspeedDashDefinitionGroup>(HardlightEnumComparers.LightspeedDashTypeComparer);
            Managed<RailType, RailDefinition, RailDefinitionGroup>(HardlightEnumComparers.RailTypeComparer);
            Managed<TransporterType, TransporterDefinition, TransporterDefinitionGroup>(HardlightEnumComparers.TransporterTypeComparer);
            return checks;
        }

        // Every owned object is attempted even when earlier disposal throws.
        private static void DisposeAll(List<ScriptableObject> owned, int index)
        { if (index < 0) return; try { if (!ReferenceEquals(owned[index], null)) UnityEngine.Object.DestroyImmediate(owned[index]); } finally { DisposeAll(owned, index - 1); } }
        private static void Engine<TKey, TData, TGroup>(IEqualityComparer<TKey> expected)
            where TData : MetadataDefinition where TGroup : DefinitionDataType<TKey, TData>
        {
            var owned = new List<ScriptableObject>();
            try
            {
                var group = (TGroup)ScriptableObject.CreateInstance(typeof(TGroup)); owned.Add(group);
                var first = (TData)ScriptableObject.CreateInstance(typeof(TData)); owned.Add(first);
                var second = (TData)ScriptableObject.CreateInstance(typeof(TData)); owned.Add(second);
                Check(group != null && first != null && second != null, "genuine original concrete group/definition SO construction");
                Check(group.m_elements == null, "original group constructor leaves inherited array null");
                var field = TypeField(typeof(TData)); var keyA = (TKey)Enum.ToObject(typeof(TKey), -19); var keyB = (TKey)Enum.ToObject(typeof(TKey), 37); field.SetValue(first, keyA); field.SetValue(second, keyB);
                first.name = "first"; second.name = "second";
                var a = new DefinitionDataType<TKey, TData>.DefinitionElement<TData>(first); var b = new DefinitionDataType<TKey, TData>.DefinitionElement<TData>(second);
                Check(a.Name == "first" && b.Name == "second", "real original wrapper constructor names from actual Unity objects");
                first.name = "renamed"; a.OnBeforeSerialize(); Check(a.Name == "renamed", "maintained wrapper real callback reloads engine name");
                group.m_elements = new[] { a, b }; var data = group.GetData();
                Check(data.Count == 2 && ReferenceEquals(data[keyA], first) && ReferenceEquals(data[keyB], second), "real concrete inherited dictionary key/reference pairs");
                Check(ReferenceEquals(data.Comparer, expected), "actual engine dictionary same genuine registry instance");
                string json = JsonUtility.ToJson(group);
                Check(json.Contains("\"m_elements\"") && json.Contains("\"Data\"") && json.Contains("\"Name\""), "actual inherited original generic wrapper fields serialize");
                group.m_elements = null; JsonUtility.FromJsonOverwrite(json, group);
                Check(group.m_elements != null && group.m_elements.Length == 2, "actual inherited generic array roundtrip");
                Check(ReferenceEquals(group.m_elements[0].Data, first) && ReferenceEquals(group.m_elements[1].Data, second), "actual same-process original reference roundtrip");
                Check(group.m_elements[0].Name == "renamed" && group.m_elements[1].Name == "second", "actual original wrapper string roundtrip");
                Check(JsonUtility.ToJson(group) == json, "actual same-process original group serialization stable");
                var roundtrip = group.GetData(); Check(roundtrip.Count == 2 && ReferenceEquals(roundtrip[keyA], first) && ReferenceEquals(roundtrip[keyB], second), "roundtripped genuine group dictionary behavior");
            }
            finally { DisposeAll(owned, owned.Count - 1); }
        }
        public static int RunEngine()
        {
            checks = 0;
            Engine<HalfPipeType, HalfPipeDefinition, HalfPipeDefinitionGroup>(HardlightEnumComparers.HalfPipeTypeComparer);
            Engine<LightspeedDashType, LightspeedDashDefinition, LightspeedDashDefinitionGroup>(HardlightEnumComparers.LightspeedDashTypeComparer);
            Engine<RailType, RailDefinition, RailDefinitionGroup>(HardlightEnumComparers.RailTypeComparer);
            Engine<TransporterType, TransporterDefinition, TransporterDefinitionGroup>(HardlightEnumComparers.TransporterTypeComparer);
            return checks;
        }
    }
}
