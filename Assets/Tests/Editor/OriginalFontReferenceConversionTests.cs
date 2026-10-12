using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

// Genuine owned YAML imports of the original closed collection-reference type.
// No FontAssetLanguageSetter/App/system instance, font load, readiness or catalog is supplied.
public sealed class OriginalFontReferenceConversionTests
{
    private static readonly string[] Collections = {
        "c6bc5f88793a44caaab2e67bd2a45af1", "00411bdfebdc243b5b97ae0f721d16b0",
        "824c341753e3d4732af8fb81d6d0b65a", "36555d6dfb0164ae2982ca7fc963c182" };
    private static readonly string[] EditorChildren = { "m_AssetGUID", "m_SubObjectName", "m_SubObjectType", "m_EditorAssetChanged" };

    [Test]
    public void OwnedOriginalAndExplicitConvertedFontCollectionReferencesImportWithOriginalKeys()
    {
        WithOwnedFolder((folder, enroll) =>
        {
            var owner = ScriptableObject.CreateInstance<ProjectLucidFontReferenceConversionCarrier>();
            enroll(owner);
            {
                using (var serialized = new SerializedObject(owner))
                {
                    serialized.Update();
                    var references = serialized.FindProperty("m_fontAssetsAndFallBackCollectionRefs");
                    Assert.That(references, Is.Not.Null); references.arraySize = Collections.Length;
                    for (int i = 0; i < Collections.Length; i++)
                    {
                        var row = references.GetArrayElementAtIndex(i);
                        row.FindPropertyRelative("m_AssetGUID").stringValue = Collections[i];
                        row.FindPropertyRelative("m_SubObjectName").stringValue = string.Empty;
                        row.FindPropertyRelative("m_SubObjectType").stringValue = string.Empty;
                        Assert.That(row.FindPropertyRelative("m_EditorAssetChanged").boolValue, Is.False);
                    }
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                ImportBoth(folder, owner, Collections, imported =>
                {
                    Assert.That(imported, Is.TypeOf<ProjectLucidFontReferenceConversionCarrier>());
                    var carrier = (ProjectLucidFontReferenceConversionCarrier)imported;
                    Assert.That(carrier.CollectionReferences.Count, Is.EqualTo(Collections.Length));
                    for (int i = 0; i < Collections.Length; i++)
                    {
                        var reference = carrier.CollectionReferences[i];
                        Assert.That(reference.GetType(), Is.EqualTo(typeof(AssetReferenceT<FontAssetAndFallbacks>)));
                        Assert.That(reference.AssetGUID, Is.EqualTo(Collections[i]));
                        Assert.That(reference.RuntimeKey, Is.EqualTo(Collections[i]));
                        Assert.That(reference.IsValid(), Is.False);
                    }
                    using (var serialized = new SerializedObject(imported))
                    {
                        serialized.Update();
                        var list = serialized.FindProperty("m_fontAssetsAndFallBackCollectionRefs");
                        for (int i = 0; i < Collections.Length; i++) AssertReference(list.GetArrayElementAtIndex(i), Collections[i]);
                    }
                });
            }
        });
    }

    private static void ImportBoth(string folder, ScriptableObject owner, string[] keys, Action<ScriptableObject> observe)
    {
        Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
        string seed = folder + "/Seed.asset";
        AssetDatabase.CreateAsset(owner, seed);
        byte[] seedBytes = File.ReadAllBytes(seed);
        string seedYaml = new UTF8Encoding(false, true).GetString(seedBytes);
        TestContext.Out.WriteLine("Owned font seed YAML (complete):\n" + seedYaml);
        Assert.That(seedYaml.StartsWith("%YAML 1.1", StringComparison.Ordinal), Is.True);
        var editorLine = new Regex("(?m)^ *m_EditorAssetChanged: 0\\r?\\n");
        Assert.That(editorLine.Matches(seedYaml).Count, Is.EqualTo(keys.Length));
        string originalYaml = editorLine.Replace(seedYaml, string.Empty);
        // Unity writes one space after each empty scalar colon in this owned seed.
        // Keep those exact bytes while adding only the false Editor field.
        var reference = new Regex("(?m)^( *)- m_AssetGUID: ([a-f0-9]{32})(\\r?\\n)\\1  m_SubObjectName: \\3\\1  m_SubObjectType: \\3");
        var matches = reference.Matches(originalYaml);
        Assert.That(matches.Cast<Match>().Select(match => match.Groups[2].Value).ToArray(), Is.EqualTo(keys));
        Assert.That(matches.Count, Is.EqualTo(keys.Length));
        string convertedYaml = reference.Replace(originalYaml, match => match.Value + match.Groups[1].Value +
            "  m_EditorAssetChanged: 0" + match.Groups[3].Value);
        Assert.That(convertedYaml, Is.EqualTo(seedYaml));
        foreach (var variant in new[] { new KeyValuePair<string, string>("OriginalProfile", originalYaml),
                                       new KeyValuePair<string, string>("ExplicitEditorProfile", convertedYaml) })
        {
            string path = folder + "/" + variant.Key + ".asset";
            Assert.That(File.Exists(path) || File.Exists(path + ".meta"), Is.False);
            byte[] source = new UTF8Encoding(false).GetBytes(variant.Value);
            File.WriteAllBytes(path, source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(source));
            var imported = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Assert.That(imported, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(imported), Is.EqualTo(path));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Has.Length.EqualTo(32));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(seed)));
            observe(imported);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(source));
        }
    }

    private static void AssertReference(SerializedProperty reference, string guid)
    {
        var names = new List<string>();
        using (var cursor = reference.Copy()) using (var end = reference.GetEndProperty())
        {
            if (cursor.Next(true)) do
            {
                if (SerializedProperty.EqualContents(cursor, end) || cursor.depth <= reference.depth) break;
                if (cursor.depth == reference.depth + 1) names.Add(cursor.name);
            } while (cursor.Next(false));
        }
        Assert.That(names, Is.EqualTo(EditorChildren));
        Assert.That(reference.FindPropertyRelative("m_AssetGUID").stringValue, Is.EqualTo(guid));
        Assert.That(reference.FindPropertyRelative("m_SubObjectName").stringValue, Is.EqualTo(string.Empty));
        Assert.That(reference.FindPropertyRelative("m_SubObjectType").stringValue, Is.EqualTo(string.Empty));
        Assert.That(reference.FindPropertyRelative("m_EditorAssetChanged").boolValue, Is.False);
        Assert.That(reference.FindPropertyRelative("m_Operation"), Is.Null);
    }

    private static void WithOwnedFolder(Action<string, Action<ScriptableObject>> action)
    {
        string folder = "Assets/ProjectLucidOwnedFontConversion_" + Guid.NewGuid().ToString("N");
        bool created = false;
        var owners = new List<ScriptableObject>();
        var cleanupFailures = new List<Exception>();
        ExceptionDispatchInfo primary = null;
        try
        {
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
            created = !string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length)));
            Assert.That(created, Is.True);
            action(folder, owner => owners.Add(owner));
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            // Deleting the owned folder handles its persistent seed/imported
            // assets. A failed deletion is reported and does not stop cleanup
            // of independently owned objects that remain transient.
            try { if (created) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True); }
            catch (Exception error) { cleanupFailures.Add(error); }
            for (int i = owners.Count - 1; i >= 0; i--)
            {
                try
                {
                    var owner = owners[i];
                    if (owner != null && !AssetDatabase.Contains(owner)) UnityEngine.Object.DestroyImmediate(owner);
                }
                catch (Exception error) { cleanupFailures.Add(error); }
            }
        }
        if (primary != null)
        {
            try
            {
                foreach (var failure in cleanupFailures)
                    TestContext.Error.WriteLine("Secondary owned font cleanup failure: " + failure);
            }
            finally { primary.Throw(); }
        }
        if (cleanupFailures.Count != 0)
            throw new AggregateException("Owned font cleanup failed.", cleanupFailures);
    }
}
