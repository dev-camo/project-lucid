using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HardlightProject;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid
{
    // Checks complete authored definitions, including both the Unity asset GUID
    // and the separate game GUID and original scene key. It does not run scene loading/gameplay.
    public static class LevelDefinitionAssetVerification
    {
        private static int checks;
        [Serializable] private sealed class Export
        {
            public string status = null, project_path = null, asset_map_path = null;
            public Fingerprint input_fingerprint = null;
        }
        [Serializable] private sealed class Fingerprint { public string sha256 = null; }
        [Serializable] private sealed class Map { public Asset[] assets = null; }
        [Serializable] private sealed class Asset
        {
            public string path = null, guid = null;
            public string[] script_references = null;
        }
        [Serializable] private sealed class Binding
        {
            public string assembly = null, full_name = null, maintained_path = null;
            public string exported_guid = null, target_guid = null;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class BoundAsset
        {
            public string path = null, guid = null, exported_sha256 = null, prepared_sha256 = null;
            public int pointers_rewritten;
        }
        [Serializable] private sealed class Bindings
        {
            public string status = null, source_fingerprint = null, comparison_path = null, comparison_sha256 = null;
            public bool references_modified;
            public Binding[] bindings = null;
            public BoundAsset[] assets = null;
        }
        [Serializable] private sealed class Preparation
        {
            public string status = null;
            public Fingerprint input_fingerprint = null;
            public Bindings script_bindings = null;
        }
        [Serializable] private sealed class Receipt
        {
            public int schema_version = 1, asset_count, checks;
            public string command = "verify-level-definition-assets", status = "failed", unity_version;
            public string source_fingerprint, comparison_sha256, script_guid, error;
            public bool asset_guids_preserved, authored_guids_preserved, roundtrip_verified;
        }

        private static void Check(bool value, string label)
        {
            ++checks;
            if (!value) throw new Exception(label);
        }
        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string CheckedPath(string root, string candidate)
        {
            string full = Path.GetFullPath(candidate), prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            Check(full.StartsWith(prefix, StringComparison.Ordinal), "proof path remains under its owned root");
            for (string path = full; path != Path.GetFullPath(root); path = Path.GetDirectoryName(path))
                if (File.Exists(path) || Directory.Exists(path))
                    Check((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "proof does not follow symlinks");
            return full;
        }
        private static string Field(string text, string name, string pattern)
        {
            MatchCollection matches = Regex.Matches(text, "^  " + name + ": ?(" + pattern + ")\\r?$", RegexOptions.Multiline);
            Check(matches.Count == 1, "exact original " + name + " field");
            return matches[0].Groups[1].Value;
        }
        private static string MetaGuid(byte[] bytes)
        {
            MatchCollection matches = Regex.Matches(Encoding.UTF8.GetString(bytes), "^guid: ([0-9a-f]{32})\\r?$", RegexOptions.Multiline);
            Check(matches.Count == 1, "unique original meta GUID");
            return matches[0].Groups[1].Value;
        }

        public static void Run()
        {
            checks = 0;
            Check(Application.unityVersion == "2022.3.54f1", "matching original Unity version");
            string root = Directory.GetParent(Application.dataPath).FullName;
            string cache = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            if (String.IsNullOrEmpty(cache)) cache = Path.Combine(root, ".cache", "project-lucid");
            cache = Path.GetFullPath(cache);
            Export export = JsonUtility.FromJson<Export>(File.ReadAllText(CheckedPath(cache, Path.Combine(cache, "assets/latest-assets.json"))));
            Preparation prepare = JsonUtility.FromJson<Preparation>(File.ReadAllText(CheckedPath(cache, Path.Combine(cache, "assets/latest-prepare.json"))));
            Check(export != null && export.status == "exported" && prepare != null && prepare.status == "prepared", "complete extraction and preparation");
            Check(export.input_fingerprint != null && prepare.input_fingerprint != null &&
                  Regex.IsMatch(export.input_fingerprint.sha256 ?? "", "^[0-9a-f]{64}$") &&
                  export.input_fingerprint.sha256 == prepare.input_fingerprint.sha256, "preparation retains the same immutable supplied bundle fingerprint");
            Bindings proof = prepare.script_bindings;
            Check(proof != null && proof.status == "verified_layout" && proof.references_modified && proof.bindings != null && proof.bindings.All(b => b != null) && proof.bindings.Count(b => b.assembly == "Game.Runtime") == 2, "exact two approved maintained definition bindings");
            Check(proof.source_fingerprint == ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(root, false), "fresh maintained source binding evidence");
            Check(Hash(File.ReadAllBytes(CheckedPath(cache, proof.comparison_path))) == proof.comparison_sha256, "unchanged exact layout proof");
            Binding[] selectedBindings = proof.bindings.Where(b => b != null && b.assembly == "Game.Runtime" &&
                b.full_name == typeof(LevelDefinition).FullName && b.maintained_path == "Assets/Scripts/Definitions/LevelDefinition.cs").ToArray();
            Check(selectedBindings.Length == 1, "one exact approved level-definition binding");
            Binding binding = selectedBindings[0];
            Binding[] startingBindings = proof.bindings.Where(b => b != null && b.assembly == "Game.Runtime" &&
                b.full_name == typeof(LevelStartPositionDefinition).FullName && b.maintained_path == "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs").ToArray();
            Check(startingBindings.Length == 1 && binding.exported_guid != startingBindings[0].exported_guid &&
                  binding.target_guid != startingBindings[0].target_guid, "distinct approved starting-point and level MonoScript identities");
            Check(binding.assembly == "Game.Runtime" && binding.full_name == typeof(LevelDefinition).FullName &&
                  binding.maintained_path == "Assets/Scripts/Definitions/LevelDefinition.cs", "exact original type and maintained path");
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(binding.maintained_path);
            Check(script != null && script.GetClass() == typeof(LevelDefinition), "loaded maintained concrete MonoScript");
            string scriptGuid; long scriptId;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out scriptGuid, out scriptId) && scriptGuid == binding.target_guid &&
                  scriptId == binding.target_file_id, "exact current script GUID and signed long local ID");
            Check(binding.exported_file_id == 11500000 && scriptId != 0 && scriptGuid != binding.exported_guid,
                  "maintained script keeps a fresh identity rather than adopting original MonoScript GUID");
            Map mapping = JsonUtility.FromJson<Map>(File.ReadAllText(CheckedPath(cache, export.asset_map_path)));
            Asset[] originals = mapping.assets.Where(a => a.script_references != null && a.script_references.Contains(binding.exported_guid)).ToArray();
            Asset[] startingOriginals = mapping.assets.Where(a => a.script_references != null && a.script_references.Contains(startingBindings[0].exported_guid)).ToArray();
            Check(proof.assets != null, "prepared asset evidence exists");
            var definitionPaths = new HashSet<string>(originals.Concat(startingOriginals).Select(a => a.path), StringComparer.Ordinal);
            BoundAsset[] definitionAssets = proof.assets.Where(a => a != null && definitionPaths.Contains(a.path)).ToArray();
            Check(originals.Length == 26 && startingOriginals.Length == 21 && definitionAssets.Length == 47, "all supplied approved definition groups accounted for");
            Check(originals.Concat(startingOriginals).Select(a => a.path).OrderBy(p => p).SequenceEqual(definitionAssets.Select(a => a.path).OrderBy(p => p)),
                  "no omitted or extra rebound definitions across both approved groups");
            Check(originals.Select(a => a.path).Distinct(StringComparer.Ordinal).Count() == originals.Length &&
                  originals.Select(a => a.guid).Distinct(StringComparer.Ordinal).Count() == originals.Length, "each original definition has a unique path and Unity GUID");
            var originalPaths = new HashSet<string>(originals.Select(a => a.path), StringComparer.Ordinal);
            BoundAsset[] selectedAssets = definitionAssets.Where(a => a != null && originalPaths.Contains(a.path)).ToArray();
            Check(selectedAssets.Length == originals.Length && originals.Select(a => a.path).OrderBy(p => p).SequenceEqual(selectedAssets.Select(a => a.path).OrderBy(p => p)), "no omitted or duplicate rebound level definitions");
            string runId = Guid.NewGuid().ToString("N");
            string run = CheckedPath(cache, Path.Combine(cache, "proof/level-definitions", runId));
            Directory.CreateDirectory(run);
            var receipt = new Receipt { unity_version = Application.unityVersion, source_fingerprint = proof.source_fingerprint,
                comparison_sha256 = proof.comparison_sha256, script_guid = scriptGuid, asset_count = originals.Length };
            string folder = "Assets/LucidLevelDefinitionProof_" + runId;
            bool ownsFolder = false;
            try
            {
                Check(!Directory.Exists(Path.Combine(root, folder)) && !File.Exists(Path.Combine(root, folder + ".meta")), "fresh temporary roundtrip path");
                Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder))), "create owned roundtrip folder");
                ownsFolder = true;
                foreach (Asset row in originals)
                {
                    BoundAsset promoted = selectedAssets.Single(a => a.path == row.path);
                    Check(row.path.StartsWith("Assets/", StringComparison.Ordinal) && row.path.EndsWith(".asset", StringComparison.Ordinal), "authored definition path");
                    string rawPath = CheckedPath(cache, Path.Combine(export.project_path, row.path));
                    string preparedPath = "Assets/Recovered/" + row.path.Substring("Assets/".Length);
                    string preparedFile = CheckedPath(Path.Combine(root, "Assets/Recovered"), Path.Combine(root, preparedPath));
                    byte[] raw = File.ReadAllBytes(rawPath), generated = File.ReadAllBytes(preparedFile);
                    string rawMetaPath = CheckedPath(cache, rawPath + ".meta");
                    string preparedMetaPath = CheckedPath(Path.Combine(root, "Assets/Recovered"), preparedFile + ".meta");
                    byte[] rawMeta = File.ReadAllBytes(rawMetaPath), preparedMeta = File.ReadAllBytes(preparedMetaPath);
                    Check(Hash(raw) == promoted.exported_sha256 && Hash(generated) == promoted.prepared_sha256, "raw/prepared definition content proof");
                    Check(row.guid == promoted.guid && MetaGuid(rawMeta) == row.guid &&
                          MetaGuid(preparedMeta) == row.guid, "original Unity asset GUID remains unchanged");
                    string text = Encoding.UTF8.GetString(raw);
                    string from = "{fileID: " + binding.exported_file_id.ToString(CultureInfo.InvariantCulture) + ", guid: " + binding.exported_guid + ", type: 3}";
                    string to = "{fileID: " + scriptId.ToString(CultureInfo.InvariantCulture) + ", guid: " + scriptGuid + ", type: 3}";
                    Check(promoted.pointers_rewritten == 1 && Field(text, "m_Script", Regex.Escape(from)) == from &&
                          Encoding.UTF8.GetString(generated) == text.Replace("  m_Script: " + from, "  m_Script: " + to), "only original script pointer changes");
                    string authoredGuid = Field(text, "m_guid", "[0-9a-f]{32}"), name = Field(text, "m_Name", "[^\\r\\n]+");
                    string sceneName = Field(text, "m_sceneName", "[a-z0-9_]+");
                    int flags = Int32.Parse(Field(text, "m_ObjectHideFlags", "[0-9]+"), CultureInfo.InvariantCulture);
                    string[] engineFields = { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject" };
                    foreach (string engineField in engineFields)
                        Check(Field(text, engineField, Regex.Escape("{fileID: 0}")) == "{fileID: 0}", "original null engine pointer: " + engineField);
                    string enabled = Field(text, "m_Enabled", "[01]"), editorFlags = Field(text, "m_EditorHideFlags", "[0-9]+");
                    string classIdentifier = Field(text, "m_EditorClassIdentifier", "[^\\r\\n]*");
                    MatchCollection documents = Regex.Matches(text, "^--- !u!114 &(-?[0-9]+)\\r?$", RegexOptions.Multiline);
                    Check(documents.Count == 1 && Regex.Matches(text, "^--- !u!", RegexOptions.Multiline).Count == 1, "one exact original definition object identity");
                    Match document = documents[0];
                    long originalId = Int64.Parse(document.Groups[1].Value, CultureInfo.InvariantCulture);
                    LevelDefinition value = AssetDatabase.LoadAssetAtPath<LevelDefinition>(preparedPath);
                    Check(value != null && value.GetType() == typeof(LevelDefinition), "original definition loads as maintained type");
                    string assetGuid; long assetId;
                    Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out assetGuid, out assetId) && assetGuid == row.guid && assetId == originalId,
                          "original asset GUID and object local ID survive import");
                    Check(value.GetGUID() == authoredGuid && value.GetName() == sceneName && value.name == name && (int)value.hideFlags == flags, "authored game GUID, scene key and engine fields survive import");
                    var serialized = new SerializedObject(value);
                    Check(serialized.FindProperty("m_guid").stringValue == authoredGuid && serialized.FindProperty("m_sceneName").stringValue == sceneName && serialized.FindProperty("m_Script").objectReferenceValue == script,
                          "actual inherited serialized field and script reference");
                    // Preserve the authored filename as well as serialized
                    // m_Name; Unity imports ScriptableObject names from it.
                    Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, row.guid)), "create distinct original-name roundtrip path");
                    string fixture = folder + "/" + row.guid + "/" + Path.GetFileName(row.path);
                    LevelDefinition clone = Object.Instantiate(value);
                    clone.name = name;
                    clone.SetData("replacement_scene_must_be_ignored");
                    ((ILevelDefinition)clone).SetData(null);
                    Check(clone.GetName() == sceneName && clone.GetGUID() == authoredGuid, "original native RET SetData preserves complete cloned authored identity");
                    AssetDatabase.CreateAsset(clone, fixture);
                    AssetDatabase.SaveAssetIfDirty(clone);
                    AssetDatabase.ForceReserializeAssets(new[] { fixture });
                    AssetDatabase.ImportAsset(fixture, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    LevelDefinition roundtrip = AssetDatabase.LoadAssetAtPath<LevelDefinition>(fixture);
                    Check(roundtrip != null && roundtrip.GetGUID() == authoredGuid && roundtrip.GetName() == sceneName && roundtrip.name == name && (int)roundtrip.hideFlags == flags,
                          "complete definition Unity save/reimport roundtrip: " + row.path + "; actual " +
                          (roundtrip == null ? "null" : roundtrip.GetGUID() + "/" + roundtrip.GetName() + "/" + roundtrip.name + "/" + (int)roundtrip.hideFlags));
                    Check(MonoScript.FromScriptableObject(roundtrip) == script, "roundtrip retains maintained MonoScript");
                    string roundtripGuid; long roundtripId;
                    Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(roundtrip, out roundtripGuid, out roundtripId) &&
                          roundtripId == originalId && roundtripGuid != row.guid && !originals.Any(a => a.guid == roundtripGuid),
                          "owned copy retains the original main object local ID with a fresh asset GUID");
                    string saved = File.ReadAllText(Path.Combine(root, fixture));
                    Check(Field(saved, "m_guid", "[0-9a-f]{32}") == authoredGuid && Field(saved, "m_sceneName", "[a-z0-9_]+") == sceneName &&
                          Field(saved, "m_Name", "[^\\r\\n]+") == name && Field(saved, "m_ObjectHideFlags", "[0-9]+") == flags.ToString(CultureInfo.InvariantCulture),
                          "actual Unity save retains all authored and engine scalar values");
                    foreach (string engineField in engineFields)
                        Check(Field(saved, engineField, Regex.Escape("{fileID: 0}")) == "{fileID: 0}", "actual Unity save retains original null engine pointer: " + engineField);
                    Check(Field(saved, "m_Enabled", "[01]") == enabled && Field(saved, "m_EditorHideFlags", "[0-9]+") == editorFlags &&
                          Field(saved, "m_EditorClassIdentifier", "[^\\r\\n]*") == classIdentifier, "actual Unity save retains engine enabled/editor fields");
                    File.Copy(Path.Combine(root, fixture), Path.Combine(run, row.guid + ".asset"));
                    Check(File.ReadAllBytes(rawPath).SequenceEqual(raw) && File.ReadAllBytes(preparedFile).SequenceEqual(generated) &&
                          File.ReadAllBytes(rawMetaPath).SequenceEqual(rawMeta) && File.ReadAllBytes(preparedMetaPath).SequenceEqual(preparedMeta),
                          "proof never modifies original/prepared definition or metadata");
                }
                receipt.status = "verified";
                receipt.asset_guids_preserved = receipt.authored_guids_preserved = receipt.roundtrip_verified = true;
                Debug.Log("Level-definition asset verification: " + originals.Length + " definitions, " + checks + " checks");
            }
            catch (Exception error) { receipt.error = error.ToString(); throw; }
            finally
            {
                if (ownsFolder) Check(AssetDatabase.DeleteAsset(folder), "remove owned temporary roundtrip assets");
                receipt.checks = checks;
                File.WriteAllText(Path.Combine(run, "report.json"), JsonUtility.ToJson(receipt, true) + "\n");
            }
        }
    }
}
