using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Tests.Editor
{
    // Read-back of the prepared boot data whose layouts reach through closed
    // generic types: a dropped list or pointer would still load a typed asset,
    // so each case compares the loaded data with the supplied YAML rows.
    public sealed class OriginalBootDataPreparedAssetTests
    {
        private const string LookupPath = "Assets/MonoBehaviour/TimeCategoryLookup.asset";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        [Test]
        public void PreparedTimeCategoryLookupReadsEveryAuthoredCategoryBack()
        {
            Receipts receipts = Receipts.Load();
            receipts.RequireOwner("Game.Runtime::HardlightProject.TimeCategoryLookup");
            receipts.RequireRewritten(LookupPath);
            string raw = receipts.Raw(LookupPath);
            int[] keys = Regex.Matches(raw, "^    - m_key: (-?[0-9]+)\\r?$", RegexOptions.Multiline).Cast<Match>()
                .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.That(Regex.Matches(raw, "^      m_value: \\{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\\}\\r?$", RegexOptions.Multiline).Count,
                        Is.EqualTo(keys.Length));
            Assert.That(keys.Length, Is.GreaterThan(0)); Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length));

            TimeCategoryLookup lookup = AssetDatabase.LoadAssetAtPath<TimeCategoryLookup>(Receipts.Prepared(LookupPath));
            Assert.That(lookup, Is.Not.Null); Assert.That(lookup.Dictionary, Is.Not.Null);
            Assert.That(lookup.Dictionary.Count, Is.EqualTo(keys.Length), "Every authored m_values row deserializes into the lookup.");
            foreach (int key in keys)
            {
                Assert.That(lookup.Dictionary.TryGetValue((TimeCategory)key, out TimeCategoryObject value), Is.True, "Category " + key);
                Assert.That(value, Is.Not.Null, "Category " + key + " keeps its TimeCategoryObject.");
            }
        }

        [Test]
        public void PreparedHighlightPrefabsKeepTheirDefinitions()
        {
            Receipts receipts = Receipts.Load();
            receipts.RequireOwner("HLModernUI.Runtime::Hardlight.UIHighlight");
            receipts.RequireOwner("HLModernUI.Runtime::Hardlight.UIHighlightDefinition");
            string[] prefabs = receipts.Rewritten.Where(path => path.StartsWith("Assets/GameObject/uihighlight_", StringComparison.Ordinal) &&
                                                                path.EndsWith(".prefab", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(prefabs.Length, Is.EqualTo(10));
            var serialized = new List<SerializedObject>();
            try
            {
                foreach (string path in prefabs)
                {
                    Match authored = Regex.Match(receipts.Raw(path), "^  m_definition: \\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\\}\\r?$", RegexOptions.Multiline);
                    Assert.That(authored.Success, Is.True, path);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Receipts.Prepared(path));
                    Assert.That(prefab, Is.Not.Null, path);
                    UIHighlight highlight = prefab.GetComponent<UIHighlight>();
                    Assert.That(highlight, Is.Not.Null, path); Assert.That(highlight.Definition, Is.Not.Null, path);
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(highlight.Definition, out string guid, out long id), Is.True);
                    Assert.That(guid, Is.EqualTo(authored.Groups[1].Value), path); Assert.That(id, Is.EqualTo(11400000), path);
                    var definition = new SerializedObject(highlight.Definition); serialized.Add(definition); definition.Update();
                    SerializedProperty target = definition.FindProperty("m_prefab");
                    Assert.That(target, Is.Not.Null); Assert.That(target.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Assert.That(target.objectReferenceValue, Is.InstanceOf<UIHighlight>(), path + ": the definition points back at a highlight component.");
                }
            }
            finally
            {
                foreach (SerializedObject value in serialized) value.Dispose();
            }
        }

        private sealed class Receipts
        {
            private string work, project;
            private Bindings proof;
            public string[] Rewritten => proof.assets.Select(a => a.path).ToArray();

            public static Receipts Load()
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
                if (String.IsNullOrEmpty(work)) work = Path.Combine(root, ".cache", "project-lucid");
                work = Path.GetFullPath(work);
                ExportReceipt export = JsonUtility.FromJson<ExportReceipt>(File.ReadAllText(Path.Combine(work, "assets/latest-assets.json"), Utf8));
                PreparationReceipt prepared = JsonUtility.FromJson<PreparationReceipt>(File.ReadAllText(Path.Combine(work, "assets/latest-prepare.json"), Utf8));
                Assert.That(export, Is.Not.Null); Assert.That(prepared, Is.Not.Null);
                Assert.That(export.status, Is.EqualTo("exported")); Assert.That(prepared.status, Is.EqualTo("prepared"));
                Assert.That(prepared.script_bindings, Is.Not.Null); Assert.That(prepared.script_bindings.status, Is.EqualTo("verified_layout"));
                Assert.That(prepared.script_bindings.assets, Is.Not.Null); Assert.That(prepared.script_bindings.bindings, Is.Not.Null);
                return new Receipts { work = work, project = export.project_path, proof = prepared.script_bindings };
            }

            public void RequireOwner(string owner) =>
                Assert.That(proof.bindings.Count(b => b != null && b.assembly + "::" + b.full_name == owner), Is.EqualTo(1), owner);

            public void RequireRewritten(string path)
            {
                BoundAsset[] rows = proof.assets.Where(a => a != null && a.path == path).ToArray();
                Assert.That(rows.Length, Is.EqualTo(1), path); Assert.That(rows[0].pointers_rewritten, Is.EqualTo(1), path);
            }

            public string Raw(string path)
            {
                string exported = Path.GetFullPath(Path.Combine(work, project));
                string file = Path.GetFullPath(Path.Combine(exported, path));
                Assert.That(file.StartsWith(exported + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True, "Supplied evidence stays under the export.");
                return File.ReadAllText(file, Utf8);
            }

            public static string Prepared(string path) => "Assets/Recovered/" + path.Substring("Assets/".Length);
        }

        [Serializable] private sealed class ExportReceipt { public string status = null, project_path = null; }
        [Serializable] private sealed class PreparationReceipt { public string status = null; public Bindings script_bindings = null; }
        [Serializable] private sealed class Bindings
        {
            public string status = null; public Binding[] bindings = null; public BoundAsset[] assets = null;
        }
        [Serializable] private sealed class Binding { public string assembly = null, full_name = null; }
        [Serializable] private sealed class BoundAsset { public string path = null; public int pointers_rewritten; }
    }
}
