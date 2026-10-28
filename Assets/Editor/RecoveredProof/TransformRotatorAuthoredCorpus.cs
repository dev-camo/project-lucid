#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectLucid
{
    public static class TransformRotatorAuthoredCorpus
    {
        public const string OriginalGuid = "7aeebac275418b09c654cf4431d0f800";
        public const string Source = "Assets/Scripts/Game/World/TransformAnimation/TransformRotator.cs";
        public const string LookupGuid = "22d0597fd2f52364abe6c04d2cf832bd";
        public static readonly string[] FieldNames = {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier", "m_timeCategoryObject",
            "m_updateOn", "m_enabledByActivator", "m_timeCategoryEnum", "m_synchronise", "m_targetTransform",
            "m_randomiseRotationSpeedOnAwake", "m_rotationSpeed", "m_rotationSpeedMin", "m_rotationSpeedMax"
        };
        [Serializable] public sealed class Fingerprint { public string algorithm, sha256; public int file_count; public long total_bytes; }
        [Serializable] public sealed class Export { public string status, project_path, asset_map_path, run_id; public Fingerprint input_fingerprint; }
        [Serializable] public sealed class Prepare { public string status, recovered_path; public Bindings script_bindings; }
        [Serializable] public sealed class Bindings { public string status, comparison_path, comparison_sha256, original_schema_sha256, source_fingerprint; public bool references_modified; public Binding[] bindings; public BoundAsset[] assets; }
        [Serializable] public sealed class Binding { public string full_name, assembly, exported_guid, target_guid, maintained_path, binary_sha256, metadata_sha256, source_sha256; public long exported_file_id, target_file_id; }
        [Serializable] public sealed class BoundAsset { public string path, exported_sha256, prepared_sha256; }
        [Serializable] public sealed class Comparison { public string command, status, source_fingerprint, original_schema_sha256; public Candidate[] candidates; }
        [Serializable] public sealed class Candidate { public string assembly, full_name, status; public string[] issues; public LoadedScript[] loaded_scripts; }
        [Serializable] public sealed class LoadedScript { public string path, guid, source_sha256; public long file_id; public bool class_resolved; }
        [Serializable] public sealed class Map { public string project_path; public Asset[] assets; }
        [Serializable] public sealed class Asset { public string path, guid; public string[] script_references; public bool code_quarantined; }
        public sealed class Sample
        {
            public string Path, Id, Document, TargetDocument;
            public bool OwnerActive;
            public Dictionary<string, string> Fields;
        }
        public sealed class Corpus
        {
            public Export Export;
            public Prepare Prepare;
            public Sample[] Samples;
            public string[] Paths;
        }
        private static Corpus validatedCorpus;
        private static string validatedExport, validatedPrepare;
        public static readonly Regex Header = new Regex(@"^--- !u!([0-9]+) &(-?[0-9]+)\r?$", RegexOptions.Multiline);
        public static Dictionary<string, string> Fields(string text)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(text, @"^  ([A-Za-z_][A-Za-z_0-9]*):[ \t]*(.*)\r?$", RegexOptions.Multiline))
                fields.Add(m.Groups[1].Value, m.Groups[2].Value.TrimEnd('\r'));
            return fields;
        }
        public static IEnumerable<(int Class, string Id, string Text)> Documents(string text)
        {
            MatchCollection headers = Header.Matches(text);
            for (int i = 0; i < headers.Count; ++i)
            {
                Match h = headers[i];
                int end = i + 1 == headers.Count ? text.Length : headers[i + 1].Index;
                yield return (Int32.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture), h.Groups[2].Value,
                    text.Substring(h.Index, end - h.Index));
            }
        }
        public static void CheckFields(string document)
        {
            string[] names = Regex.Matches(document, @"^  ([A-Za-z_][A-Za-z_0-9]*):", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Require(names.SequenceEqual(FieldNames), "all twenty original fields in order");
            var f = Fields(document);
            foreach (string n in new[] { "m_ObjectHideFlags", "m_EditorHideFlags", "m_enabledByActivator" })
                Require(f[n] == "0", "original authored zero " + n);
            foreach (string n in new[] { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" })
                Require(f[n] == "{fileID: 0}", "original null prefab provenance " + n);
            Require(f["m_Name"] == "" && f["m_EditorClassIdentifier"] == "", "original empty component names");
            Require(new[] { "0", "1" }.Contains(f["m_Enabled"]) && new[] { "0", "1" }.Contains(f["m_randomiseRotationSpeedOnAwake"]), "authored booleans");
            Require(f["m_Script"] == "{fileID: 11500000, guid: " + OriginalGuid + ", type: 3}", "immutable original script pointer");
        }
        public static string ExpectedCategoryGuid(Sample sample)
        {
            string pointer = sample.Fields["m_timeCategoryObject"];
            if (pointer != "{fileID: 0}") return AssetGuid(pointer);
            // Original 06002e45@5F8B30 preserves assigned objects, otherwise uses
            // the enum-keyed authored lookup; these five entries come from that asset.
            switch (sample.Fields["m_timeCategoryEnum"])
            {
                case "886747957": return "0089624ea26a0754b80f64aa90a69794";
                case "-968218126": return "b95a4ebacc46f6248849781cdc7fc1f2";
                case "-870455447": return "bbdd0ebaaf382c8439c65f1c07e09f66";
                case "-2001650353": return "9f3a67e3d7bac7e43994c22c2c72f29c";
                case "962018048": return "5bcb83d83ef5dba4088b4da180bb0a81";
                default: throw new InvalidOperationException("Unexpected authored lookup key");
            }
        }
        public static string AssetGuid(string pointer)
        {
            Match m = Regex.Match(pointer, @"\A\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}\z");
            Require(m.Success, "authored category asset pointer");
            return m.Groups[1].Value;
        }
        public static string LocalPointer(string pointer)
        {
            Match m = Regex.Match(pointer, @"\A\{fileID: (-?[0-9]+)\}\z");
            Require(m.Success, "authored local reference");
            return m.Groups[1].Value;
        }
        public static Corpus Read()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string cache = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            if (String.IsNullOrEmpty(cache)) cache = Path.Combine(root, ".cache/project-lucid");
            cache = Path.GetFullPath(cache);
            string exportText = File.ReadAllText(Path.Combine(cache, "assets/latest-assets.json")), prepareText = File.ReadAllText(Path.Combine(cache, "assets/latest-prepare.json"));
            if (validatedCorpus != null)
            {
                Require(exportText == validatedExport && prepareText == validatedPrepare, "corpus reuse stays in the same export/preparation");
                return Copy(validatedCorpus);
            }
            var export = JsonUtility.FromJson<Export>(exportText);
            var prepare = JsonUtility.FromJson<Prepare>(prepareText);
            Require(export.status == "exported" && prepare.status == "prepared" && prepare.script_bindings.status == "verified_layout", "completed genuine preparation");
            Require(Path.GetFullPath(prepare.recovered_path) == Path.Combine(root, "Assets/Recovered"), "prepared root identity");
            Require(export.input_fingerprint.algorithm == "sha256-content-manifest-v1" &&
                export.input_fingerprint.sha256 == "7f7af864f06ca17d0f7be4aa10b44a4b43d496a4b4af78db7957dc4d507c878e" &&
                export.input_fingerprint.file_count == 3691 && export.input_fingerprint.total_bytes == 2506296102, "immutable canonical export identity");
            byte[] mapBytes = File.ReadAllBytes(OwnedPath(cache, export.asset_map_path));
            // Run id and absolute root are incidental to an extraction; content is pinned by the input fingerprint, the root-normalized map and the asset manifest.
            string mapText = System.Text.Encoding.UTF8.GetString(mapBytes);
            var map = JsonUtility.FromJson<Map>(mapText);
            Require(map.project_path == export.project_path, "original map root");
            const string rootKey = "\n  \"project_path\": \"";
            int rootAt = mapText.IndexOf(rootKey, StringComparison.Ordinal), rootEnd = rootAt < 0 ? -1 : mapText.IndexOf("\",\n", rootAt + rootKey.Length, StringComparison.Ordinal);
            Require(rootAt >= 0 && rootEnd >= 0 && mapText.IndexOf(rootKey, rootAt + 1, StringComparison.Ordinal) < 0, "single asset map root field");
            Require(Hash(System.Text.Encoding.UTF8.GetBytes(mapText.Substring(0, rootAt + rootKey.Length) + "<project>" + mapText.Substring(rootEnd))) == "d025cc509b938eaa43203d2ba895495bdee1293817a0090ba167d3ae4bdd7c37", "immutable asset map rows (root-normalized)");
            Asset script = map.assets.Single(a => a.path == "Assets/Scripts/Game.Runtime/HardlightProject/TransformRotator.cs");
            Require(script.guid == OriginalGuid && script.code_quarantined, "immutable original script row");
            Asset[] assets = map.assets.Where(a => a.script_references != null && a.script_references.Contains(OriginalGuid)).OrderBy(a => a.path, StringComparer.Ordinal).ToArray();
            Require(assets.Length == 516 && assets.Select(a => a.path).Distinct().Count() == 516, "complete original asset set");
            var samples = new List<Sample>();
            var manifest = new System.Text.StringBuilder();
            foreach (Asset asset in assets)
            {
                string path = OwnedPath(export.project_path, Path.Combine(export.project_path, asset.path));
                byte[] bytes = File.ReadAllBytes(path);
                manifest.Append(asset.path).Append('\t').Append(Hash(bytes)).Append('\t').Append(Hash(File.ReadAllBytes(path + ".meta"))).Append('\n');
                string text = System.Text.Encoding.UTF8.GetString(bytes);
                var documents = Documents(text).ToDictionary(d => d.Id);
                foreach (var d in documents.Values)
                {
                    if (d.Class != 114 || !d.Text.Contains("  m_Script: {fileID: 11500000, guid: " + OriginalGuid + ", type: 3}")) continue;
                    CheckFields(d.Text);
                    var sample = new Sample { Path = asset.path, Id = d.Id, Document = d.Text, Fields = Fields(d.Text) };
                    string owner = LocalPointer(sample.Fields["m_GameObject"]);
                    Require(documents.ContainsKey(owner) && documents[owner].Class == 1, "original component owner GameObject");
                    string active = Fields(documents[owner].Text)["m_IsActive"];
                    Require(active == "0" || active == "1", "original owner activity flag"); sample.OwnerActive = active == "1";
                    string target = LocalPointer(sample.Fields["m_targetTransform"]);
                    if (target != "0")
                    {
                        Require(documents.ContainsKey(target) && documents[target].Class == 4, "original local target is a Transform");
                        sample.TargetDocument = documents[target].Text;
                        Require(LocalPointer(Fields(sample.TargetDocument)["m_GameObject"]) == owner, "all fifteen authored targets are the owner's own Transform");
                    }
                    samples.Add(sample);
                }
            }
            // Independently measured immutable baseline: sorted path, asset hash,
            // meta hash and LF per row. The oracle does not trust a prepare receipt.
            Require(Hash(System.Text.Encoding.UTF8.GetBytes(manifest.ToString())) == "5b1aed732dea063dd550e9deaeced810b952550d63475d74c5296fb2a88f6246", "complete original asset and metadata bytes");
            Require(samples.Count == 9656 && samples.Count(s => s.TargetDocument != null) == 15, "complete original component and target counts");
            Require(samples.Count(s => s.OwnerActive) == 9098, "original owner local activity variants");
            Require(samples.Select(s => s.Path + ":" + s.Id).Distinct().Count() == samples.Count, "unique original document provenance");
            Require(samples.Count(s => s.Fields["m_timeCategoryObject"] == "{fileID: 0}") == 9369, "all original missing categories");
            Require(samples.Count(s => s.Fields["m_timeCategoryObject"].Contains("e72b7e5a5557139428b647daf5b0de1e")) == 76 &&
                    samples.Count(s => s.Fields["m_timeCategoryObject"].Contains("3dbf53c896b26ef4bb01a879f3bced0b")) == 211, "both distinct assigned category identities");
            validatedExport = exportText; validatedPrepare = prepareText;
            validatedCorpus = new Corpus { Export = export, Prepare = prepare, Samples = samples.ToArray(), Paths = assets.Select(a => a.path).ToArray() };
            return Copy(validatedCorpus);
        }
        private static Corpus Copy(Corpus original) => new Corpus {
            Export = JsonUtility.FromJson<Export>(validatedExport), Prepare = JsonUtility.FromJson<Prepare>(validatedPrepare), Paths = original.Paths.ToArray(),
            Samples = original.Samples.Select(s => new Sample { Path = s.Path, Id = s.Id, Document = s.Document, TargetDocument = s.TargetDocument,
                OwnerActive = s.OwnerActive, Fields = new Dictionary<string, string>(s.Fields, StringComparer.Ordinal) }).ToArray()
        };
        public static void RequireBinding(Corpus corpus, string targetGuid)
        {
            Binding[] rows = corpus.Prepare.script_bindings.bindings.Where(b => b.full_name == "HardlightProject.TransformRotator").ToArray();
            Require(rows.Length == 1, "exact TransformRotator binding is absent");
            Binding binding = rows[0];
            Require(binding.assembly == "Game.Runtime" && binding.binary_sha256 == "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216" &&
                    binding.metadata_sha256 == "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b" &&
                    binding.source_sha256 == "fb2e574c41fcf9c4368d12ca85c6ac408cf04c04fcabbdf4a1e847795ac7428a" && Hash(File.ReadAllBytes(Source)) == binding.source_sha256,
                    "binding original input and unchanged source identity");
            Require(binding.exported_guid == OriginalGuid && binding.exported_file_id == 11500000 && binding.target_file_id == 11500000 &&
                    binding.target_guid == targetGuid && binding.maintained_path == Source, "exact original-to-maintained binding");
            Require(Hash(File.ReadAllBytes(corpus.Prepare.script_bindings.comparison_path)) == corpus.Prepare.script_bindings.comparison_sha256 &&
                    corpus.Prepare.script_bindings.original_schema_sha256 == "0561bd051ea7df722493df125bfe14323d3b371e5a07e8c5610f83d88448bbd4" &&
                    !String.IsNullOrEmpty(corpus.Prepare.script_bindings.source_fingerprint), "sealed layout receipt");
            string current = ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(Directory.GetParent(Application.dataPath).FullName, false);
            Comparison comparison = JsonUtility.FromJson<Comparison>(File.ReadAllText(corpus.Prepare.script_bindings.comparison_path));
            Require(corpus.Prepare.script_bindings.status == "verified_layout" && corpus.Prepare.script_bindings.references_modified &&
                    corpus.Prepare.script_bindings.source_fingerprint == current && comparison.source_fingerprint == current &&
                    comparison.command == "compare-monoscript-layouts" && comparison.status == "compared" &&
                    comparison.original_schema_sha256 == corpus.Prepare.script_bindings.original_schema_sha256, "current complete layout and binding authority");
            Candidate candidate = comparison.candidates.Single(c => c.assembly == binding.assembly && c.full_name == binding.full_name);
            Require(candidate.status == "compatible_layout" && candidate.issues != null && candidate.issues.Length == 0 &&
                    candidate.loaded_scripts.Length == 1, "one compatible original layout without issues");
            LoadedScript loaded = candidate.loaded_scripts[0];
            Require(loaded.class_resolved && loaded.path == Source && loaded.guid == targetGuid && loaded.file_id == 11500000 &&
                    loaded.source_sha256 == binding.source_sha256, "exact loaded script bound by the layout proof");
            foreach (string path in corpus.Paths)
            {
                BoundAsset row = corpus.Prepare.script_bindings.assets.Single(a => a.path == path);
                byte[] original = File.ReadAllBytes(OwnedPath(corpus.Export.project_path, Path.Combine(corpus.Export.project_path, path)));
                byte[] prepared = File.ReadAllBytes(OwnedPath(corpus.Prepare.recovered_path, Path.Combine(corpus.Prepare.recovered_path, path.Substring(7))));
                Require(Hash(original) == row.exported_sha256 && Hash(prepared) == row.prepared_sha256, "binding receipt exact bytes");
                var mapped = Documents(System.Text.Encoding.UTF8.GetString(prepared)).ToDictionary(d => d.Id);
                var expectedIds = corpus.Samples.Where(s => s.Path == path).Select(s => s.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                var actualIds = mapped.Values.Where(d => d.Class == 114 && Fields(d.Text).TryGetValue("m_Script", out string p) &&
                    p == "{fileID: 11500000, guid: " + targetGuid + ", type: 3}").Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Require(actualIds.SequenceEqual(expectedIds), "no extra or missing mapped TransformRotator documents");
                foreach (Sample sample in corpus.Samples.Where(s => s.Path == path))
                {
                    string expected = sample.Document.Replace("guid: " + OriginalGuid, "guid: " + targetGuid);
                    Require(mapped.ContainsKey(sample.Id) && mapped[sample.Id].Text == expected, "script-only complete prepared document");
                }
            }
            Map originalMap = JsonUtility.FromJson<Map>(File.ReadAllText(corpus.Export.asset_map_path));
            string mappedPointer = "{fileID: 11500000, guid: " + targetGuid + ", type: 3}";
            foreach (Asset asset in originalMap.assets.Where(a => a.script_references != null && a.script_references.Length != 0 && !corpus.Paths.Contains(a.path)))
            {
                string path = OwnedPath(corpus.Prepare.recovered_path, Path.Combine(corpus.Prepare.recovered_path, asset.path.Substring(7)));
                if (!File.Exists(path)) { Require(asset.code_quarantined, "nonquarantined prepared asset remains present"); continue; }
                Require(!Documents(File.ReadAllText(path)).Any(d => d.Class == 114 && Fields(d.Text).TryGetValue("m_Script", out string pointer) && pointer == mappedPointer),
                    "no mapped TransformRotator outside the complete original asset set");
            }
        }
        public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static string OwnedPath(string root, string path)
        {
            path = Path.GetFullPath(path); root = Path.GetFullPath(root);
            Require(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned evidence path");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
                if (File.Exists(part) || Directory.Exists(part)) Require((File.GetAttributes(part) & FileAttributes.ReparsePoint) == 0, "no symlinked evidence path");
            return path;
        }
        public static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Authored TransformRotator: " + message); }
    }
}
#endif
