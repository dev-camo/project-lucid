using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Serialization host only: no runtime systems, handles, lifecycle callbacks or loading services.
public sealed class ProjectLucidAssetReferenceProfileCarrier : ScriptableObject
{
    public AssetReferenceT<FontAssetAndFallbacks> reference =
        new AssetReferenceT<FontAssetAndFallbacks>(string.Empty);
}

public sealed class OriginalAssetReferenceProfileTests
{
    private static readonly string[] EditorChildren =
        { "m_AssetGUID", "m_SubObjectName", "m_SubObjectType", "m_EditorAssetChanged" };

    [Test]
    public void OwnedClosedAssetReferenceExposesFourEditorChildrenWithoutAnOperation()
    {
        var owner = ScriptableObject.CreateInstance<ProjectLucidAssetReferenceProfileCarrier>();
        try
        {
            Assert.That(owner.reference.GetType(), Is.EqualTo(typeof(AssetReferenceT<FontAssetAndFallbacks>)));
            Assert.That(owner.reference.AssetGUID, Is.EqualTo(string.Empty));
            Assert.That(owner.reference.SubObjectName, Is.Null);
            Assert.That(owner.reference.IsValid(), Is.False);
            using (var serialized = new SerializedObject(owner))
            {
                serialized.Update();
                AssertReference(serialized, string.Empty, string.Empty, false);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    // Addressables package contract (SetEditorAsset and editor serialization of
    // a genuine AssetDatabase asset), not original game behavior. It needs a
    // temporary Assets/ folder, removed in WithOwnedAsset's finally blocks.
    [Test]
    public void AddressablesContractRealAssetAssignmentPreservesGuidAndTrueEditorFlagThroughSerialization()
    {
        WithOwnedAsset((owner, asset, guid) =>
        {
            Assert.That(owner.reference.SetEditorAsset(asset), Is.True);
            Assert.That(owner.reference.AssetGUID, Is.EqualTo(guid));
            Assert.That(owner.reference.editorAsset, Is.SameAs(asset));
            Assert.That(owner.reference.RuntimeKey, Is.EqualTo(guid));
            Assert.That(owner.reference.IsValid(), Is.False);
            using (var serialized = new SerializedObject(owner))
            {
                serialized.Update();
                AssertReference(serialized, guid, string.Empty, true);
            }
            var copy = ScriptableObject.CreateInstance<ProjectLucidAssetReferenceProfileCarrier>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(owner), copy);
                Assert.That(copy.reference.GetType(), Is.EqualTo(typeof(AssetReferenceT<FontAssetAndFallbacks>)));
                Assert.That(copy.reference.AssetGUID, Is.EqualTo(guid));
                Assert.That(copy.reference.editorAsset, Is.SameAs(asset));
                Assert.That(copy.reference.IsValid(), Is.False);
                using (var serialized = new SerializedObject(copy))
                {
                    serialized.Update();
                    AssertReference(serialized, guid, string.Empty, true);
                }
                Assert.That(owner.reference.AssetGUID, Is.EqualTo(guid));
                Assert.That(owner.reference.editorAsset, Is.SameAs(asset));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        });
    }

    [Test]
    public void OwnedPublicNullClearSetsEditorFlagAndClearsGuidAndSubObjectName()
    {
        WithOwnedAsset((owner, asset, guid) =>
        {
            // A normal constructor supplies a real GUID; the public null assignment sets the
            // Editor flag without a private write, load operation or addressable registration.
            owner.reference = new AssetReferenceT<FontAssetAndFallbacks>(guid);
            owner.reference.SubObjectName = "owned subobject name";
            using (var serialized = new SerializedObject(owner))
            {
                serialized.Update();
                AssertReference(serialized, guid, "owned subobject name", false);
            }
            Assert.That(owner.reference.SetEditorAsset(null), Is.True);
            Assert.That(owner.reference.AssetGUID, Is.EqualTo(string.Empty));
            Assert.That(owner.reference.SubObjectName, Is.Null);
            Assert.That(owner.reference.editorAsset, Is.Null);
            Assert.That(owner.reference.RuntimeKey, Is.EqualTo(string.Empty));
            Assert.That(owner.reference.IsValid(), Is.False);
            using (var serialized = new SerializedObject(owner))
            {
                serialized.Update();
                AssertReference(serialized, string.Empty, string.Empty, true);
            }
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(AssetDatabase.GetAssetPath(asset)));
        });
    }

    private static void AssertReference(SerializedObject serialized, string guid, string subObjectName, bool changed)
    {
        SerializedProperty reference = serialized.FindProperty("reference");
        Assert.That(reference, Is.Not.Null);
        Assert.That(reference.propertyType, Is.EqualTo(SerializedPropertyType.Generic));
        var children = new List<string>();
        using (SerializedProperty cursor = reference.Copy())
        using (SerializedProperty end = reference.GetEndProperty())
        {
            bool enter = true;
            while (cursor.Next(enter) && !SerializedProperty.EqualContents(cursor, end))
            {
                enter = false;
                if (cursor.depth == reference.depth + 1) children.Add(cursor.name);
            }
        }
        Assert.That(children, Is.EqualTo(EditorChildren));
        Assert.That(Relative(reference, "m_AssetGUID", SerializedPropertyType.String).stringValue, Is.EqualTo(guid));
        Assert.That(Relative(reference, "m_SubObjectName", SerializedPropertyType.String).stringValue, Is.EqualTo(subObjectName));
        Assert.That(Relative(reference, "m_SubObjectType", SerializedPropertyType.String).stringValue, Is.EqualTo(string.Empty));
        Assert.That(Relative(reference, "m_EditorAssetChanged", SerializedPropertyType.Boolean).boolValue, Is.EqualTo(changed));
        foreach (string absent in new[] { "m_Operation", "m_ActiveAssetReferences", "m_CachedAsset", "m_CachedGUID", "<DerivedClassType>k__BackingField" })
            Assert.That(reference.FindPropertyRelative(absent), Is.Null, absent);
    }

    private static SerializedProperty Relative(SerializedProperty owner, string name, SerializedPropertyType type)
    {
        SerializedProperty property = owner.FindPropertyRelative(name);
        Assert.That(property, Is.Not.Null, name);
        Assert.That(property.propertyType, Is.EqualTo(type), name);
        return property;
    }

    private static void WithOwnedAsset(Action<ProjectLucidAssetReferenceProfileCarrier, FontAssetAndFallbacks, string> observe)
    {
        string folder = "Assets/ProjectLucidOwnedAssetReference_" + Guid.NewGuid().ToString("N");
        bool created = false;
        ProjectLucidAssetReferenceProfileCarrier owner = null;
        FontAssetAndFallbacks asset = null;
        try
        {
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            string folderGuid = AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            created = !string.IsNullOrEmpty(folderGuid);
            Assert.That(created, Is.True);
            Assert.That(AssetDatabase.GUIDToAssetPath(folderGuid), Is.EqualTo(folder));
            asset = ScriptableObject.CreateInstance<FontAssetAndFallbacks>();
            // The genuine original serialization callback iterates this public collection.
            asset.m_fallbackCollection = new List<FontAssetAndFallbacks.FontAssetFallbackPairing>();
            string path = folder + "/OwnedFontAssetAndFallbacks.asset";
            AssetDatabase.CreateAsset(asset, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Has.Length.EqualTo(32));
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));
            Assert.That(AssetDatabase.LoadAssetAtPath<FontAssetAndFallbacks>(path), Is.SameAs(asset));
            Assert.That(asset.m_fallbackCollection, Is.Empty);
            owner = ScriptableObject.CreateInstance<ProjectLucidAssetReferenceProfileCarrier>();
            observe(owner, asset, guid);
        }
        finally
        {
            try
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            }
            finally
            {
                try
                {
                    if (created) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                }
                finally
                {
                    if (asset != null && !AssetDatabase.Contains(asset)) UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }
    }
}
