// Bounded original LevelDefinition behavior/metadata proof. No game startup.
using System;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class LevelDefinitionVerification
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
            LevelDefinition source = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelDefinition target = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                Type type = typeof(LevelDefinition);
                Check(type.Assembly.GetName().Name == "Game.Runtime" && typeof(ILevelDefinition).Assembly == type.Assembly, "original level/interface assembly");
                Check(type.BaseType == typeof(ScriptableObjectWithGuid) && typeof(ILevelDefinition).IsAssignableFrom(type), "original GUID base and level contract");
                Check(!type.IsAbstract && !type.IsSealed, "original concrete unsealed type");
                FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                Check(fields.Length == 1 && fields[0].Name == "m_sceneName", "original single declared field");
                FieldInfo field = fields[0];
                Check(field.IsPrivate && !field.IsStatic && !field.IsInitOnly && field.FieldType == typeof(string), "original scene-name field flags/type");
                Check(field.IsDefined(typeof(SerializeField), false) && field.GetCustomAttributes(false).Length == 1 && !field.IsNotSerialized, "original exact scene-name serialization attribute");
                CreateAssetMenuAttribute menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                Check(menu != null && menu.fileName == "LevelDefinition" && menu.menuName == "HardlightProject/DefinitionData/Definitions/LevelDefinition" && menu.order == 0, "original asset menu values");
                object[] options = type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false);
                Check(options.Length == 2, "original two compiler options");
                bool nullChecks = false, boundsChecks = false;
                foreach (Il2CppSetOptionAttribute option in options)
                {
                    Check(option.Value is bool && !(bool)option.Value, "original compiler option value false");
                    nullChecks |= option.Option == Option.NullChecks;
                    boundsChecks |= option.Option == Option.ArrayBoundsChecks;
                }
                Check(nullChecks && boundsChecks, "original distinct NullChecks and ArrayBoundsChecks identities");
                Check(typeof(ILevelDefinition).GetMethods().Length == 2, "original two interface contracts");
                Check(source.GetName() == null && source.GetGUID() == string.Empty, "actual Unity constructor retains null scene and original empty GUID");
                var serialized = new SerializedObject(source);
                SerializedProperty scene = serialized.FindProperty("m_sceneName");
                SerializedProperty guid = serialized.FindProperty("m_guid");
                Check(scene != null && scene.propertyType == SerializedPropertyType.String && guid != null, "actual serialized own/inherited fields");
                scene.stringValue = "s_\"scene\"\n-\u03a9";
                guid.stringValue = "original-authored-level-guid";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(source.GetName() == "s_\"scene\"\n-\u03a9" && source.GetGUID() == "original-authored-level-guid", "real authored field updates original getters");
                string before = JsonUtility.ToJson(source);
                Check(before.Contains("\"m_sceneName\":") && before.Contains("\"m_guid\":"), "actual JSON contains original own/inherited fields");
                source.SetData("different_scene");
                Check(source.GetName() == "s_\"scene\"\n-\u03a9" && JsonUtility.ToJson(source) == before, "native RET SetData preserves all serialized fields");
                ((ILevelDefinition)source).SetData(null);
                Check(((ILevelDefinition)source).GetName() == source.GetName() && JsonUtility.ToJson(source) == before, "original interface dispatch preserves null/no-op contract");
                JsonUtility.FromJsonOverwrite(before, target);
                Check(target.GetName() == source.GetName() && target.GetGUID() == source.GetGUID(), "actual own/base JSON roundtrip");
                field.SetValue(target, null);
                Check(target.GetName() == null, "native getter returns null field directly");
                target.SetData("still_no_write");
                Check(target.GetName() == null && target.GetGUID() == source.GetGUID(), "native SetData does not normalize null or alter GUID");
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
