// Bounded real-Unity evidence for original HLUnityCore.Runtime field 0x04000200.
// No FSM acquisition, authored state execution, file writes or source mutation.
using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class DelegateSerializationVerification
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException(message);
        }
        public static int Run()
        {
            checks = 0;
            FiniteStateMachineScriptableObject source = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            FiniteStateMachineScriptableObject target = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            try
            {
                Type type = typeof(FiniteStateMachineScriptableObject);
                FieldInfo field = type.GetField("OnInitialisationComplete", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original FSM asset assembly");
                Check(field != null && field.IsPublic && !field.IsStatic && !field.IsInitOnly && !field.IsLiteral, "original callback field flags");
                Check(field.Attributes == FieldAttributes.Public, "callback retains exact original public-only metadata");
                Check(field.FieldType == typeof(Action<FiniteStateMachineScriptableObject>), "original closed Action callback type");
                Check(field.GetCustomAttributes(false).Length == 0 && !field.IsNotSerialized, "original callback has no serialization attributes");
                Check(field.FieldType.IsGenericType && !field.FieldType.ContainsGenericParameters, "original closed delegate generic identity");
                Check(field.FieldType.GetGenericTypeDefinition() == typeof(Action<>), "original BCL Action generic definition");
                Check(field.FieldType.GetGenericArguments().Length == 1 && field.FieldType.GetGenericArguments()[0] == type, "original delegate argument identity");
                Check(field.FieldType.BaseType == typeof(MulticastDelegate) && typeof(MulticastDelegate).BaseType == typeof(Delegate), "actual loaded delegate ancestry");
                Check(typeof(Delegate).IsAssignableFrom(field.FieldType) && typeof(MulticastDelegate).IsAssignableFrom(field.FieldType), "actual loaded callback is a delegate");
                Check(!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && !field.FieldType.IsArray && !field.FieldType.IsEnum, "delegate is not a Unity object/container/enum");

                int sourceCalls = 0, targetCalls = 0;
                FiniteStateMachineScriptableObject sourceArgument = null, targetArgument = null;
                source.OnInitialisationComplete = value => { ++sourceCalls; sourceArgument = value; };
                target.OnInitialisationComplete = value => { ++targetCalls; targetArgument = value; };
                Action<FiniteStateMachineScriptableObject> sourceCallback = source.OnInitialisationComplete;
                Action<FiniteStateMachineScriptableObject> targetCallback = target.OnInitialisationComplete;
                var serializedSource = new SerializedObject(source);
                var serializedTarget = new SerializedObject(target);
                Check(serializedSource.FindProperty("OnInitialisationComplete") == null && serializedTarget.FindProperty("OnInitialisationComplete") == null, "actual Unity excludes nonnull original public delegates");
                Check(serializedSource.FindProperty("m_fsmDictionary") == null && serializedSource.FindProperty("m_fsm") == null && serializedSource.FindProperty("m_refCount") == null, "private runtime state remains excluded");
                string[] authored = { "m_name", "m_isOverwriteOk", "m_reuseExisting", "m_releaseOnDestroy", "m_referencedScriptableObjects", "m_relativePathToJSON", "m_embeddedJSON" };
                foreach (string name in authored)
                    Check(serializedSource.FindProperty(name) != null, "actual original authored field remains serialized: " + name);
                var visible = new HashSet<string>();
                SerializedProperty iterator = serializedSource.GetIterator();
                bool descend = true;
                while (iterator.NextVisible(descend)) { visible.Add(iterator.name); descend = false; }
                Check(!visible.Contains("OnInitialisationComplete"), "callback absent from actual visible serialized tree");
                foreach (string name in authored)
                    Check(visible.Contains(name), "authored field remains in actual visible tree: " + name);
                serializedSource.FindProperty("m_name").stringValue = "original delegate serialization proof";
                serializedSource.FindProperty("m_relativePathToJSON").stringValue = "FSM/Application/Application_Splash.json";
                serializedSource.FindProperty("m_isOverwriteOk").boolValue = true;
                serializedSource.FindProperty("m_reuseExisting").boolValue = true;
                serializedSource.ApplyModifiedPropertiesWithoutUndo();
                string json = JsonUtility.ToJson(source);
                Check(!json.Contains("OnInitialisationComplete"), "actual JsonUtility excludes nonnull original public delegate");
                foreach (string name in authored)
                    Check(json.Contains("\"" + name + "\":"), "actual JsonUtility retains authored field: " + name);
                JsonUtility.FromJsonOverwrite(json, target);
                serializedTarget.Update();
                Check(serializedTarget.FindProperty("m_name").stringValue == "original delegate serialization proof", "actual authored name JSON roundtrip");
                Check(serializedTarget.FindProperty("m_relativePathToJSON").stringValue == "FSM/Application/Application_Splash.json", "actual authored JSON path roundtrip");
                Check(serializedTarget.FindProperty("m_isOverwriteOk").boolValue && serializedTarget.FindProperty("m_reuseExisting").boolValue, "actual authored flags JSON roundtrip");
                Check(ReferenceEquals(source.OnInitialisationComplete, sourceCallback) && ReferenceEquals(target.OnInitialisationComplete, targetCallback), "serialized changes/JSON overwrite retain runtime callback identity");
                Check(sourceCalls == 0 && targetCalls == 0, "serialization does not invoke runtime callbacks");
                source.OnInitialisationComplete(source);
                target.OnInitialisationComplete(target);
                Check(sourceCalls == 1 && targetCalls == 1 && ReferenceEquals(sourceArgument, source) && ReferenceEquals(targetArgument, target), "original public delegates remain callable after serialization");
                source.OnInitialisationComplete = null;
                serializedSource.Update();
                Check(serializedSource.FindProperty("OnInitialisationComplete") == null && !JsonUtility.ToJson(source).Contains("OnInitialisationComplete"), "actual null delegate is excluded identically");
                return checks;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
