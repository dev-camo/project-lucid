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
    // and the separate game GUID. It does not exercise level spawning/gameplay.
    public static class StartingPointAssetVerification
    {
        private static int checks;
        [Serializable] private sealed class Export
        {
            public string status = null, project_path = null, asset_map_path = null;
        }
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
        [Serializable] private sealed class Preparation { public string status = null; public Bindings script_bindings = null; }
        [Serializable] private sealed class Receipt
        {
            public int schema_version = 1, asset_count, checks;
            public string command = "verify-starting-point-assets", status = "failed", unity_version;
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
            MatchCollection matches = Regex.Matches(text, "^  " + name + ": (" + pattern + ")\\r?$", RegexOptions.Multiline);
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
            Bindings proof = prepare.script_bindings;
            Check(proof != null && proof.status == "verified_layout" && proof.references_modified && proof.bindings != null && proof.bindings.All(b => b != null) && proof.bindings.Count(b => b.assembly == "Game.Runtime") == 2, "exact two approved maintained definition bindings");
            Check(proof.source_fingerprint == ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(root, false), "fresh maintained source binding evidence");
            Check(Hash(File.ReadAllBytes(CheckedPath(cache, proof.comparison_path))) == proof.comparison_sha256, "unchanged exact layout proof");
            Binding[] selectedBindings = proof.bindings.Where(b => b != null && b.assembly == "Game.Runtime" &&
                b.full_name == typeof(LevelStartPositionDefinition).FullName && b.maintained_path == "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs").ToArray();
            Check(selectedBindings.Length == 1, "one exact approved starting-point binding");
            Binding binding = selectedBindings[0];
            Binding[] levelBindings = proof.bindings.Where(b => b != null && b.assembly == "Game.Runtime" &&
                b.full_name == typeof(LevelDefinition).FullName && b.maintained_path == "Assets/Scripts/Definitions/LevelDefinition.cs").ToArray();
            Check(levelBindings.Length == 1 && binding.exported_guid != levelBindings[0].exported_guid &&
                  binding.target_guid != levelBindings[0].target_guid, "distinct approved definition MonoScript identities");
            Check(binding.assembly == "Game.Runtime" && binding.full_name == typeof(LevelStartPositionDefinition).FullName &&
                  binding.maintained_path == "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs", "exact original type and maintained path");
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(binding.maintained_path);
            Check(script != null && script.GetClass() == typeof(LevelStartPositionDefinition), "loaded maintained concrete MonoScript");
            string scriptGuid; long scriptId;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out scriptGuid, out scriptId) && scriptGuid == binding.target_guid &&
                  scriptId == binding.target_file_id, "exact current script GUID and signed long local ID");
            Map mapping = JsonUtility.FromJson<Map>(File.ReadAllText(CheckedPath(cache, export.asset_map_path)));
            Asset[] originals = mapping.assets.Where(a => a.script_references != null && a.script_references.Contains(binding.exported_guid)).ToArray();
            Asset[] levels = mapping.assets.Where(a => a.script_references != null && a.script_references.Contains(levelBindings[0].exported_guid)).ToArray();
            Check(proof.assets != null, "prepared asset evidence exists");
            var definitionPaths = new HashSet<string>(originals.Concat(levels).Select(a => a.path), StringComparer.Ordinal);
            BoundAsset[] definitionAssets = proof.assets.Where(a => a != null && definitionPaths.Contains(a.path)).ToArray();
            Check(originals.Length == 21 && levels.Length == 26 && definitionAssets.Length == 47, "all supplied starting-point and level definitions accounted for");
            Check(originals.Concat(levels).Select(a => a.path).OrderBy(p => p).SequenceEqual(definitionAssets.Select(a => a.path).OrderBy(p => p)), "no omitted or extra rebound definitions across both approved groups");
            var originalPaths = new HashSet<string>(originals.Select(a => a.path), StringComparer.Ordinal);
            BoundAsset[] selectedAssets = definitionAssets.Where(a => a != null && originalPaths.Contains(a.path)).ToArray();
            Check(selectedAssets.Length == 21 && originals.Select(a => a.path).OrderBy(p => p).SequenceEqual(selectedAssets.Select(a => a.path).OrderBy(p => p)), "all21 starting-point owners remain individually accounted for");
            string runId = Guid.NewGuid().ToString("N");
            string run = CheckedPath(cache, Path.Combine(cache, "proof/starting-points", runId));
            Directory.CreateDirectory(run);
            var receipt = new Receipt { unity_version = Application.unityVersion, source_fingerprint = proof.source_fingerprint,
                comparison_sha256 = proof.comparison_sha256, script_guid = scriptGuid, asset_count = originals.Length };
            string folder = "Assets/LucidStartingPointProof_" + runId;
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
                    Check(Hash(raw) == promoted.exported_sha256 && Hash(generated) == promoted.prepared_sha256, "raw/prepared definition content proof");
                    Check(row.guid == promoted.guid && MetaGuid(File.ReadAllBytes(rawPath + ".meta")) == row.guid &&
                          MetaGuid(File.ReadAllBytes(preparedFile + ".meta")) == row.guid, "original Unity asset GUID remains unchanged");
                    string text = Encoding.UTF8.GetString(raw);
                    string from = "{fileID: " + binding.exported_file_id.ToString(CultureInfo.InvariantCulture) + ", guid: " + binding.exported_guid + ", type: 3}";
                    string to = "{fileID: " + scriptId.ToString(CultureInfo.InvariantCulture) + ", guid: " + scriptGuid + ", type: 3}";
                    Check(promoted.pointers_rewritten == 1 && Field(text, "m_Script", Regex.Escape(from)) == from &&
                          Encoding.UTF8.GetString(generated) == text.Replace("  m_Script: " + from, "  m_Script: " + to), "only original script pointer changes");
                    string authoredGuid = Field(text, "m_guid", "[0-9a-f]{32}"), name = Field(text, "m_Name", "[^\\r\\n]+");
                    int flags = Int32.Parse(Field(text, "m_ObjectHideFlags", "[0-9]+"), CultureInfo.InvariantCulture);
                    Match document = Regex.Match(text, "^--- !u!114 &(-?[0-9]+)\\r?$", RegexOptions.Multiline);
                    Check(document.Success, "original definition object identity");
                    long originalId = Int64.Parse(document.Groups[1].Value, CultureInfo.InvariantCulture);
                    LevelStartPositionDefinition value = AssetDatabase.LoadAssetAtPath<LevelStartPositionDefinition>(preparedPath);
                    Check(value != null && value.GetType() == typeof(LevelStartPositionDefinition), "original definition loads as maintained type");
                    string assetGuid; long assetId;
                    Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out assetGuid, out assetId) && assetGuid == row.guid && assetId == originalId,
                          "original asset GUID and object local ID survive import");
                    Check(value.GetGUID() == authoredGuid && value.name == name && (int)value.hideFlags == flags, "authored game GUID and engine fields survive import");
                    var serialized = new SerializedObject(value);
                    Check(serialized.FindProperty("m_guid").stringValue == authoredGuid && serialized.FindProperty("m_Script").objectReferenceValue == script,
                          "actual inherited serialized field and script reference");
                    // Preserve the authored filename as well as serialized
                    // m_Name; Unity imports ScriptableObject names from it.
                    Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, row.guid)), "create distinct original-name roundtrip path");
                    string fixture = folder + "/" + row.guid + "/" + Path.GetFileName(row.path);
                    LevelStartPositionDefinition clone = Object.Instantiate(value);
                    clone.name = name;
                    AssetDatabase.CreateAsset(clone, fixture);
                    AssetDatabase.SaveAssetIfDirty(clone);
                    AssetDatabase.ForceReserializeAssets(new[] { fixture });
                    AssetDatabase.ImportAsset(fixture, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    LevelStartPositionDefinition roundtrip = AssetDatabase.LoadAssetAtPath<LevelStartPositionDefinition>(fixture);
                    Check(roundtrip != null && roundtrip.GetGUID() == authoredGuid && roundtrip.name == name && (int)roundtrip.hideFlags == flags,
                          "complete definition Unity save/reimport roundtrip: " + row.path + "; actual " +
                          (roundtrip == null ? "null" : roundtrip.GetGUID() + "/" + roundtrip.name + "/" + (int)roundtrip.hideFlags));
                    Check(MonoScript.FromScriptableObject(roundtrip) == script, "roundtrip retains maintained MonoScript");
                    File.Copy(Path.Combine(root, fixture), Path.Combine(run, row.guid + ".asset"));
                    Check(File.ReadAllBytes(rawPath).SequenceEqual(raw) && File.ReadAllBytes(preparedFile).SequenceEqual(generated), "proof never modifies original or prepared definition");
                }
                receipt.status = "verified";
                receipt.asset_guids_preserved = receipt.authored_guids_preserved = receipt.roundtrip_verified = true;
                Debug.Log("Starting-point asset verification: " + originals.Length + " definitions, " + checks + " checks");
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
