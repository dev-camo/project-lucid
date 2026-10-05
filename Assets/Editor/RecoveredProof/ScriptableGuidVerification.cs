// Original HLUnityCore.Runtime 0x0200023e and Game.Runtime 0x02000569.
// Bounded real-Unity checks for the GUID base; this does not bind original assets.
using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class ScriptableGuidVerification
    {
        private static int checks;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static void Check(bool condition, string label)
        {
            ++checks;
            if (!condition) throw new Exception(label);
        }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            ++checks;
            try { action(); }
            catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + label);
        }
        private static FieldInfo GuidField => typeof(ScriptableObjectWithGuid).GetField("m_guid", Fields);
        private static void SetGuid(ScriptableObjectWithGuid value, string guid) => GuidField.SetValue(value, guid);
        private static LevelStartPositionDefinition Create()
        {
            return ScriptableObject.CreateInstance<LevelStartPositionDefinition>();
        }
        private static void LayoutAndConstructor(LevelStartPositionDefinition value)
        {
            Check(typeof(ScriptableObjectWithGuid).IsAbstract && typeof(ScriptableObjectWithGuid).BaseType == typeof(ScriptableObject), "original abstract Unity base");
            Check(typeof(LevelStartPositionDefinition).BaseType == typeof(ScriptableObjectWithGuid), "original concrete definition base");
            Check(typeof(ScriptableObjectWithGuid).Assembly.GetName().Name == "HLUnityCore.Runtime" && typeof(LevelStartPositionDefinition).Assembly.GetName().Name == "Game.Runtime", "original assembly names");
            Check(typeof(ScriptableObjectWithGuid).GetFields(Fields).Length == 1 && typeof(LevelStartPositionDefinition).GetFields(Fields).Length == 0, "exact own field counts");
            FieldInfo field = GuidField;
            Check(field != null && field.IsFamily && field.FieldType == typeof(string) && !field.IsStatic && !field.IsInitOnly, "original protected GUID field");
            Check(field.IsDefined(typeof(SerializeField), false) && field.IsDefined(typeof(InspectorReadOnlyAttribute), false), "original GUID field attributes");
            Check(!field.IsDefined(typeof(SerializeReference), false) && !field.IsDefined(typeof(NonSerializedAttribute), false), "GUID serialization eligibility");
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(InspectorReadOnlyAttribute), typeof(AttributeUsageAttribute));
            Check(typeof(InspectorReadOnlyAttribute).BaseType == typeof(PropertyAttribute) && usage.ValidOn == AttributeTargets.Field && !usage.AllowMultiple && usage.Inherited, "original property-attribute shape");
            Check(new InspectorReadOnlyAttribute().order == 0, "original attribute constructor delegates to Unity");
            var menu = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(typeof(LevelStartPositionDefinition), typeof(CreateAssetMenuAttribute));
            Check(menu.fileName == "LevelStartPositionDefinition" && menu.menuName == "HardlightProject/DefinitionData/Definitions/LevelStartPositionDefinition", "original menu strings");
            // 0x06000e65 loads metadata string literal0 of length0, then calls
            // Unity ScriptableObject's constructor. Null is a different value.
            Check(value.GetGUID() != null && value.GetGUID() == string.Empty, "real Unity constructor assigns empty GUID");
        }
        private static void Serialization(LevelStartPositionDefinition source, LevelStartPositionDefinition target)
        {
            source.name = "Scriptable GUID proof";
            source.hideFlags = HideFlags.HideAndDontSave;
            var serialized = new SerializedObject(source);
            SerializedProperty guid = serialized.FindProperty("m_guid");
            Check(guid != null && guid.propertyType == SerializedPropertyType.String, "inherited serialized GUID property");
            guid.stringValue = "guid-\"quoted\"-\n-unicode-\u03a9";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(source.GetGUID() == "guid-\"quoted\"-\n-unicode-\u03a9", "serialized field updates original getter");
            string json = JsonUtility.ToJson(source);
            Check(json.Contains("\"m_guid\":"), "Unity JSON includes inherited GUID");
            JsonUtility.FromJsonOverwrite(json, target);
            Check(target.GetGUID() == source.GetGUID(), "inherited GUID Unity JSON roundtrip");
            // Reset/OnValidate native methods 0x06000e63/0x06000e64 are RET.
            typeof(ScriptableObjectWithGuid).GetMethod("Reset", Fields).Invoke(source, null);
            typeof(ScriptableObjectWithGuid).GetMethod("OnValidate", Fields).Invoke(source, null);
            Check(source.GetGUID() == target.GetGUID() && source.name == "Scriptable GUID proof" && source.hideFlags == HideFlags.HideAndDontSave, "original RET callbacks preserve GUID/engine values");
        }
        private static void LiveEquality(LevelStartPositionDefinition a, LevelStartPositionDefinition b)
        {
            ScriptableObjectWithGuid same = a;
            Check(a == same && !(a != same), "reference equality precedes GUID/null checks");
            Check(a != null && null != a && !(a == null) && !(null == a), "live Unity null checks");
            Check(!ReferenceEquals(a, b), "distinct real Unity objects");
            SetGuid(a, "same"); SetGuid(b, "same");
            Check(a == b && !(a != b), "distinct GUID equality");
            Check(a.Equals((ScriptableObjectWithGuid)b) && a.Equals((object)b), "typed/object GUID Equals");
            Check(!a.Equals((ScriptableObjectWithGuid)null) && !a.Equals((object)null) && !a.Equals(new object()), "typed/object null and unrelated type");
            Check(a.GetHashCode() == b.GetHashCode(), "equal GUID hash contract");
            var dictionary = new Dictionary<ScriptableObjectWithGuid, string> { { a, "value" } };
            Check(dictionary[b] == "value", "default dictionary uses original object Equals/hash");
            SetGuid(b, "different");
            Check(a != b && !a.Equals((ScriptableObjectWithGuid)b) && !a.Equals((object)b) && !dictionary.ContainsKey(b), "different GUID identity");
            SetGuid(a, null); SetGuid(b, null);
            Check(a == b && a.Equals((ScriptableObjectWithGuid)b) && a.Equals((object)b), "null GUID strings compare equal for live objects");
            Throws<NullReferenceException>(() => a.GetHashCode(), "original null GUID hash failure");
            SetGuid(b, string.Empty);
            Check(a != b && !a.Equals((object)b), "empty and null GUID differ");
        }
        private static void DestroyedEquality(LevelStartPositionDefinition a, LevelStartPositionDefinition b)
        {
            SetGuid(a, "retained"); SetGuid(b, "retained");
            UnityEngine.Object.DestroyImmediate(a);
            Check(!ReferenceEquals(a, null) && a == null && null == a && !(a != null), "destroyed managed reference has Unity null semantics");
            Check(a.GetGUID() == "retained", "destroyed own managed GUID remains readable");
            ScriptableObjectWithGuid same = a;
            Check(a == same, "destroyed same reference still equals itself by operator");
            Check(!a.Equals((ScriptableObjectWithGuid)a) && !a.Equals((object)a), "destroyed typed/object self Equals reject null-like other");
            Check(a != b && b != a, "live versus destroyed operator inequality");
            // Original Equals checks only 'other'; this asymmetry is intentional.
            Check(a.Equals((ScriptableObjectWithGuid)b) && a.Equals((object)b), "destroyed this can Equals live same GUID");
            Check(!b.Equals((ScriptableObjectWithGuid)a) && !b.Equals((object)a), "live this rejects destroyed other");
            SetGuid(b, "different"); UnityEngine.Object.DestroyImmediate(b);
            Check(!ReferenceEquals(a, b) && a == b && !(a != b), "different destroyed objects compare equal before GUID reads");
            Check(!a.Equals((ScriptableObjectWithGuid)b) && !a.Equals((object)b), "destroyed other still rejected by Equals");
            Check(a.GetHashCode() == "retained".GetHashCode(), "destroyed own hash remains GUID hash");
        }
        public static int Run()
        {
            checks = 0;
            LevelStartPositionDefinition a = null, b = null;
            try
            {
                a = Create(); b = Create();
                LayoutAndConstructor(a); Serialization(a, b); LiveEquality(a, b); DestroyedEquality(a, b);
                return checks;
            }
            finally
            {
                // Unity's null test prevents attempting to destroy an object twice.
                if (a != null) UnityEngine.Object.DestroyImmediate(a);
                if (b != null) UnityEngine.Object.DestroyImmediate(b);
            }
        }
    }
}
