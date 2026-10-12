using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
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

    [Serializable]
    private sealed class ConverterFixture
    {
        public int schema_version;
        public string generated_by;
        public string carrier_script_guid;
        public string converter_sha256;
        public string nonce;
        public string[] keys;
        public ConverterVariant[] variants;
    }

    [Serializable]
    private sealed class ConverterVariant
    {
        public string name;
        public string original_base64;
        public string converted_base64;
        public string original_sha256;
        public string converted_sha256;
        public int fragment_count;
        public bool script_references_modified;
        public bool player_serialized_profile_verified;
        public bool runtime_verified;
    }

    [Test]
    public void PythonConverterProducedFontCollectionOwnersImportTheirExactBytes()
    {
        string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
        string supplied = Environment.GetEnvironmentVariable("LUCID_FONT_REFERENCE_IMPORT_FIXTURE");
        string nonce = Environment.GetEnvironmentVariable("LUCID_FONT_REFERENCE_IMPORT_NONCE");
        Assert.That(work, Is.Not.Null.And.Not.Empty, "Run EditMode through tools/lucid.py test to generate its fresh converter fixture.");
        Assert.That(supplied, Is.Not.Null.And.Not.Empty);
        Assert.That(nonce, Does.Match("^[a-f0-9]{32}$"));
        Assert.That(Path.GetFullPath(supplied), Is.EqualTo(Path.GetFullPath(Path.Combine(work, "reports", "font-reference-import.json"))));
        byte[] contextBytes = File.ReadAllBytes(supplied);
        Assert.That(contextBytes.Length, Is.LessThan(65536));
        TestContext.Out.WriteLine("Python converter import fixture (complete base64): " + Convert.ToBase64String(contextBytes));
        var fixture = JsonUtility.FromJson<ConverterFixture>(new UTF8Encoding(false, true).GetString(contextBytes));
        Assert.That(fixture, Is.Not.Null);
        Assert.That(fixture.schema_version, Is.EqualTo(1));
        Assert.That(fixture.generated_by, Is.EqualTo("ProjectLucid.fontreferences.import-fixture"));
        Assert.That(fixture.nonce, Is.EqualTo(nonce));
        string converterPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "tools", "lucidlib", "fontreferences.py");
        Assert.That(fixture.converter_sha256, Is.EqualTo(Sha256(File.ReadAllBytes(converterPath))));
        var expectedKeys = Enumerable.Range(1, 4).Select(value => value.ToString("x32", CultureInfo.InvariantCulture)).ToArray();
        Assert.That(fixture.keys, Is.EqualTo(expectedKeys));
        Assert.That(fixture.variants, Has.Length.EqualTo(2));
        Assert.That(fixture.variants.Select(variant => variant.name).ToArray(), Is.EqualTo(new[] { "LF", "CRLF" }));

        WithOwnedFolder((folder, enroll) =>
        {
            var owner = ScriptableObject.CreateInstance<ProjectLucidFontReferenceConversionCarrier>();
            enroll(owner);
            MonoScript script = MonoScript.FromScriptableObject(owner);
            Assert.That(script, Is.Not.Null);
            Assert.That(fixture.carrier_script_guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script))));
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            foreach (var variant in fixture.variants)
            {
                Assert.That(variant.fragment_count, Is.EqualTo(expectedKeys.Length));
                Assert.That(variant.script_references_modified, Is.False);
                Assert.That(variant.player_serialized_profile_verified, Is.False);
                Assert.That(variant.runtime_verified, Is.False);
                foreach (bool converted in new[] { false, true })
                {
                    string encoded = converted ? variant.converted_base64 : variant.original_base64;
                    byte[] source = Convert.FromBase64String(encoded);
                    Assert.That(Convert.ToBase64String(source), Is.EqualTo(encoded));
                    Assert.That(Sha256(source), Is.EqualTo(converted ? variant.converted_sha256 : variant.original_sha256));
                    string path = folder + "/Python" + variant.name + (converted ? "Converted" : "OriginalProfile") + ".asset";
                    Assert.That(File.Exists(path) || File.Exists(path + ".meta"), Is.False);
                    File.WriteAllBytes(path, source);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(source));
                    var imported = AssetDatabase.LoadAssetAtPath<ProjectLucidFontReferenceConversionCarrier>(path);
                    Assert.That(imported, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(imported), Is.EqualTo(path));
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Has.Length.EqualTo(32));
                    Assert.That(imported.CollectionReferences.Count, Is.EqualTo(expectedKeys.Length));
                    using (var serialized = new SerializedObject(imported))
                    {
                        serialized.Update();
                        var list = serialized.FindProperty("m_fontAssetsAndFallBackCollectionRefs");
                        Assert.That(list, Is.Not.Null);
                        Assert.That(list.arraySize, Is.EqualTo(expectedKeys.Length));
                        for (int i = 0; i < expectedKeys.Length; i++)
                        {
                            var reference = imported.CollectionReferences[i];
                            Assert.That(reference.GetType(), Is.EqualTo(typeof(AssetReferenceT<FontAssetAndFallbacks>)));
                            Assert.That(reference.AssetGUID, Is.EqualTo(expectedKeys[i]));
                            Assert.That(reference.RuntimeKey, Is.EqualTo(expectedKeys[i]));
                            Assert.That(reference.IsValid(), Is.False);
                            AssertReference(list.GetArrayElementAtIndex(i), expectedKeys[i]);
                        }
                    }
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(source));
                }
            }
        });
    }

    private static string Sha256(byte[] bytes)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
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
