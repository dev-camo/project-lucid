using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CutsceneShared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace ProjectLucid.Editor
{
    // Compiled field attributes must match the original 1.10.1 Game.Runtime attribute spans.
    // Owned components stay inactive, so no Awake, OnEnable or system registration runs.
    public sealed class OriginalSerializedFieldAttributesTests
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string RespawnTooltip =
            "Check this if you never want this to become the player respawn point through proximity and only want to set it manually.";

        [Test]
        public void CutsceneTimelineDirectorRetainsOriginalSerializeField() // field token 0x04000e37
        {
            FieldInfo field = typeof(Cutscene).GetField("m_timelineDirector", Declared);
            Assert.That(field.IsFamily && field.FieldType == typeof(PlayableDirector));
            AssertAttributes(field, typeof(SerializeField));
        }

        [Test]
        public void EffectListEffectsRetainsOriginalSerializeField() // field token 0x04001572
        {
            FieldInfo field = typeof(EffectList).GetField("Effects", Declared);
            Assert.That(field.IsPublic && field.FieldType == typeof(SequencedEffect[]));
            AssertAttributes(field, typeof(SerializeField));
        }

        [Test]
        public void RespawnPointFlagRetainsOriginalTooltipThenSerializeField() // field token 0x04002d77
        {
            FieldInfo field = typeof(RespawnPoint).GetField("disableSetOnCollsion", Declared);
            Assert.That(field.IsPrivate && field.FieldType == typeof(bool));
            CustomAttributeData[] attributes = AssertAttributes(field, typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(attributes[0].ConstructorArguments.Single().Value, Is.EqualTo(RespawnTooltip));
        }

        [Test]
        public void OwnedRespawnPointSerializesTrueDisableFlag()
        {
            var owner = new GameObject("OwnedRespawnPointSerialization") { hideFlags = HideFlags.HideAndDontSave };
            var copyOwner = new GameObject("OwnedRespawnPointSerializationCopy") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                owner.SetActive(false);
                copyOwner.SetActive(false);
                owner.AddComponent<BoxCollider>();
                copyOwner.AddComponent<BoxCollider>();
                RespawnPoint point = owner.AddComponent<RespawnPoint>();
                RespawnPoint copy = copyOwner.AddComponent<RespawnPoint>();
                FieldInfo field = typeof(RespawnPoint).GetField("disableSetOnCollsion", Declared);
                using (var serialized = new SerializedObject(point))
                {
                    SerializedProperty property = serialized.FindProperty("disableSetOnCollsion");
                    Assert.That(property, Is.Not.Null);
                    Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.Boolean));
                    Assert.That(property.boolValue, Is.False);
                    property.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.That(field.GetValue(point), Is.True);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(point), copy);
                Assert.That(field.GetValue(copy), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(copyOwner);
            }
        }

        [Test]
        public void OwnedCutsceneSerializesDirectorThroughPrefabRoundTrip()
        {
            string folder = "Assets/ProjectLucidOwnedCutscene_" + Guid.NewGuid().ToString("N");
            var owner = new GameObject("OwnedCutsceneSerialization");
            bool created = false;
            try
            {
                owner.SetActive(false);
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
                string folderGuid = AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                created = !string.IsNullOrEmpty(folderGuid);
                Assert.That(created, Is.True);
                Assert.That(AssetDatabase.GUIDToAssetPath(folderGuid), Is.EqualTo(folder));
                PlayableDirector director = owner.AddComponent<PlayableDirector>();
                OwnedCutsceneProbe probe = owner.AddComponent<OwnedCutsceneProbe>();
                using (var serialized = new SerializedObject(probe))
                {
                    SerializedProperty property = serialized.FindProperty("m_timelineDirector");
                    Assert.That(property, Is.Not.Null);
                    Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Assert.That(property.objectReferenceValue, Is.Null);
                    property.objectReferenceValue = director;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Assert.That(probe.TimelineDirector, Is.SameAs(director));

                // The saved prefab is a separate serialized copy; its probe must reference its own director.
                string path = folder + "/OwnedCutscene.prefab";
                PrefabUtility.SaveAsPrefabAsset(owner, path, out bool saved);
                Assert.That(saved, Is.True);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded, Is.Not.SameAs(owner));
                Assert.That(loaded.activeSelf, Is.False);
                PlayableDirector loadedDirector = loaded.GetComponent<PlayableDirector>();
                Assert.That(loadedDirector, Is.Not.Null.And.Not.SameAs(director));
                Assert.That(loaded.GetComponent<OwnedCutsceneProbe>().TimelineDirector, Is.SameAs(loadedDirector));
            }
            finally
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }
                finally
                {
                    // Delete only the scratch folder this case created; the
                    // Assets root is not ignored, so a failed delete must fail.
                    if (created)
                    {
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Delete only this case's owned scratch.");
                        Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
                    }
                }
            }
        }

        private static CustomAttributeData[] AssertAttributes(FieldInfo field, params Type[] types)
        {
            Assert.That(field, Is.Not.Null);
            Assert.That(field.DeclaringType.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            CustomAttributeData[] attributes = field.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Select(attribute => attribute.AttributeType), Is.EqualTo(types));
            Assert.That(attributes.All(attribute => attribute.NamedArguments.Count == 0));
            Assert.That(attributes.Last().ConstructorArguments, Is.Empty);
            return attributes;
        }
    }
}
