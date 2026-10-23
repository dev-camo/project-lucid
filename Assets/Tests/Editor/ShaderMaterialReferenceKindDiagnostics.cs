using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Diagnostic only: an imported ShaderLab target replaces a serialized Shader asset.
    // Unexpected Unity logs stay failures; the receipt records both reference kinds first.
    public sealed class ShaderMaterialReferenceKindDiagnostics
    {
        private const string Source = "Assets/Shaders/Recovered/HL_Sky_Cubemap.shader";
        private const string MaterialSource = "Assets/Recovered/Data/Art/Shaders/Shaders/Sky/HL_Sky_Cubemap.mat";
        private const string OriginalGuid = "c24466ea4a824924e820666d8ffb14a4";
        private const string OriginalName = "HardLight/Sky/HL_Sky_Cubemap";
        // Immutable release material; all its texture pointers are null.
        private const string MaterialSha256 = "d3c9f74e2abdcfcbb9b013abf5cbdfb8f7f8a7b5a3285d894653a4b9f8768e29";
        private Fixture active;

        [Test]
        public void MeasureSerializedAndImportedShaderReferenceKinds()
        {
            Assert.That(active, Is.Null, "An earlier diagnostic still owns its assets.");
            active = new Fixture();
            active.Run();
        }

        // NUnit owns this synchronous cleanup even when an unexpected log aborts the test.
        [TearDown]
        public void RemoveOwnedProbeAssetsAndWriteReceipt()
        {
            Fixture owned = active;
            if (owned == null) return;
            try { owned.Cleanup(); }
            finally { active = null; }
        }

        [Serializable]
        private sealed class Identity
        {
            public bool unity_null;
            public string name, path, managed_type, guid, file_path;
            public long local_id;
            public bool identifier_found;
            public int file_type;
        }

        [Serializable]
        private sealed class Arm
        {
            public int reference_type;
            public string material_path, material_guid, yaml_sha256, meta_sha256;
            public string[] resident_before_import, resident_before_shader_read;
            public Identity material, shader_before_direct_load;
            public bool exact_target_before_direct_load;
            public string yaml_after_import_sha256, meta_after_import_sha256, serialized_shader_after_import, exception;
            [NonSerialized] public byte[] yaml, meta;
        }

        [Serializable]
        private sealed class Order
        {
            public string label, shader_path, shader_name, target_guid, source_sha256, meta_sha256;
            public string[] resident_after_source_import, resident_before_direct_load;
            public string importer_type, exception;
            public Identity direct_shader;
            public List<Arm> arms = new List<Arm>();
        }

        [Serializable]
        private sealed class Log
        {
            public string stage, type, message, stack;
        }

        [Serializable]
        private sealed class FileHash
        {
            public string path, sha256;
        }

        [Serializable]
        private sealed class Receipt
        {
            public int schema_version = 1;
            public string purpose = "Measure type 2 versus type 3 material shader references; not a reconstruction or merge verdict.";
            public string cold_limit = "Source import and URP material postprocessing may load the target before the explicit material.shader read. Residency observations do not prove absence from Unity native caches. Each order uses an independent fresh target; neither order is a fresh Editor process.";
            public string project, unity_version, started_utc, completed_utc, owned_folder;
            public bool cleanup_complete, originals_unchanged, manifest_unchanged;
            public int manifest_before_count, manifest_after_count;
            public string manifest_before_sha256, manifest_after_sha256;
            public List<FileHash> originals = new List<FileHash>();
            public List<string> manifest_changes = new List<string>();
            public List<string> failures = new List<string>();
            public List<Log> logs = new List<Log>();
            public List<Order> orders = new List<Order>();
        }

        private sealed class Fixture
        {
            private readonly Receipt receipt = new Receipt();
            private readonly List<Exception> failures = new List<Exception>();
            private Dictionary<string, string> before;
            private string project, reportPath, folder, stage = "initialization";
            private bool subscribed;

            public void Run()
            {
                project = Path.GetDirectoryName(Application.dataPath);
                receipt.project = project;
                receipt.unity_version = Application.unityVersion;
                receipt.started_utc = DateTime.UtcNow.ToString("o");
                string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
                if (string.IsNullOrEmpty(work)) work = Path.Combine(project, ".cache", "project-lucid");
                reportPath = Path.Combine(work, "reports", "shader-reference-kind-diagnostic.json");
                folder = "Assets/ShaderReferenceKindProbe_" + Guid.NewGuid().ToString("N");
                receipt.owned_folder = folder;
                Application.logMessageReceived += ObserveLog;
                subscribed = true;
                foreach (string path in new[] { Source, Source + ".meta", MaterialSource, MaterialSource + ".meta" })
                    receipt.originals.Add(new FileHash { path = path, sha256 = Hash(Full(path)) });
                Assert.That(Hash(Full(MaterialSource)), Is.EqualTo(MaterialSha256), "The control must use the immutable release material.");
                Assert.That(File.Exists(Full(folder)) || Directory.Exists(Full(folder)) || File.Exists(Full(folder + ".meta")), Is.False);
                stage = "baseline manifest";
                before = Manifest(project);
                receipt.manifest_before_count = before.Count;
                receipt.manifest_before_sha256 = ManifestHash(before);
                stage = "create owned folder";
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                RunOrder("type2_then_type3", new[] { 2, 3 });
                RunOrder("type3_then_type2", new[] { 3, 2 });
                if (failures.Count != 0) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            private void RunOrder(string label, int[] types)
            {
                var order = new Order { label = label, target_guid = Guid.NewGuid().ToString("N") };
                order.shader_name = "Hidden/ProjectLucid/ShaderReferenceProbe/" + order.target_guid;
                order.shader_path = folder + "/" + label + ".shader";
                foreach (int type in types)
                    order.arms.Add(new Arm { reference_type = type,
                        material_path = folder + "/" + label + "_type" + type + "/HL_Sky_Cubemap.mat",
                        material_guid = Guid.NewGuid().ToString("N") });
                receipt.orders.Add(order);
                try
                {
                    stage = label + ": write maintained ShaderLab copy";
                    Assert.That(AssetDatabase.GUIDToAssetPath(order.target_guid), Is.Empty);
                    Assert.That(Resident(order.shader_name), Is.Empty);
                    byte[] source = ReplaceOnce(File.ReadAllBytes(Full(Source)), "Shader \"" + OriginalName + "\"", "Shader \"" + order.shader_name + "\"");
                    byte[] meta = ReplaceOnce(File.ReadAllBytes(Full(Source + ".meta")), "guid: " + OriginalGuid, "guid: " + order.target_guid);
                    Write(order.shader_path, source);
                    Write(order.shader_path + ".meta", meta);
                    order.source_sha256 = Hash(Full(order.shader_path));
                    order.meta_sha256 = Hash(Full(order.shader_path + ".meta"));
                    stage = label + ": import owned ShaderLab target";
                    Import(order.shader_path);
                    order.resident_after_source_import = Resident(order.shader_name);
                    Assert.That(AssetDatabase.AssetPathToGUID(order.shader_path), Is.EqualTo(order.target_guid));

                    string originalReference = "m_Shader: {fileID: 4800000, guid: " + OriginalGuid + ", type: 2}";
                    string type2Reference = "m_Shader: {fileID: 4800000, guid: " + order.target_guid + ", type: 2}";
                    string type3Reference = type2Reference.Replace("type: 2", "type: 3");
                    byte[] type2 = ReplaceOnce(File.ReadAllBytes(Full(MaterialSource)), originalReference, type2Reference);
                    byte[] type3 = ReplaceOnce(type2, type2Reference, type3Reference);
                    Assert.That(type3.Length, Is.EqualTo(type2.Length));
                    int differences = type2.Where((value, index) => value != type3[index]).Count();
                    Assert.That(differences, Is.EqualTo(1), "The material pair varies only the reference-kind digit.");
                    foreach (Arm arm in order.arms)
                    {
                        try
                        {
                            string directory = Path.GetDirectoryName(arm.material_path).Replace('\\', '/');
                            stage = label + ": create type" + arm.reference_type + " material";
                            Assert.That(AssetDatabase.GUIDToAssetPath(arm.material_guid), Is.Empty);
                            Assert.That(AssetDatabase.CreateFolder(folder, Path.GetFileName(directory)), Is.Not.Empty);
                            arm.yaml = arm.reference_type == 2 ? type2 : type3;
                            // Fresh owned metadata avoids registering the original bundle label.
                            arm.meta = Encoding.UTF8.GetBytes("fileFormatVersion: 2\nguid: " + arm.material_guid
                                + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
                        }
                        catch (Exception failure) { arm.exception = failure.ToString(); Record(failure); }
                    }
                }
                catch (Exception failure)
                {
                    order.exception = failure.ToString();
                    Record(failure);
                }

                // A log or exception in the first arm must not discard the second observation.
                foreach (Arm arm in order.arms) Measure(order, arm);
                try
                {
                    stage = label + ": direct shader load AFTER both material reads";
                    order.resident_before_direct_load = Resident(order.shader_name);
                    Shader direct = AssetDatabase.LoadAssetAtPath<Shader>(order.shader_path);
                    order.direct_shader = Describe(direct);
                    AssetImporter importer = AssetImporter.GetAtPath(order.shader_path);
                    order.importer_type = importer == null ? null : importer.GetType().FullName;
                    Assert.That(direct, Is.Not.Null);
                    Assert.That(order.direct_shader.guid, Is.EqualTo(order.target_guid));
                    Assert.That(order.direct_shader.local_id, Is.EqualTo(4800000));
                    Assert.That(order.direct_shader.file_type, Is.EqualTo((int)FileType.MetaAssetType));
                    Assert.That(importer, Is.TypeOf<ShaderImporter>());
                    Assert.That(Hash(Full(order.shader_path)), Is.EqualTo(order.source_sha256));
                    Assert.That(Hash(Full(order.shader_path + ".meta")), Is.EqualTo(order.meta_sha256));
                }
                catch (Exception failure) { order.exception += "\n" + failure; Record(failure); }
            }

            private void Measure(Order order, Arm arm)
            {
                try
                {
                    // Write just before measurement so the first arm cannot import the second early.
                    stage = order.label + ": type" + arm.reference_type + " write material";
                    Write(arm.material_path, arm.yaml);
                    Write(arm.material_path + ".meta", arm.meta);
                    arm.yaml_sha256 = Hash(Full(arm.material_path));
                    arm.meta_sha256 = Hash(Full(arm.material_path + ".meta"));
                    stage = order.label + ": type" + arm.reference_type + " material import";
                    arm.resident_before_import = Resident(order.shader_name);
                    Import(arm.material_path);
                    arm.yaml_after_import_sha256 = Hash(Full(arm.material_path));
                    arm.meta_after_import_sha256 = Hash(Full(arm.material_path + ".meta"));
                    arm.serialized_shader_after_import = File.ReadAllLines(Full(arm.material_path))
                        .Single(line => line.StartsWith("  m_Shader:", StringComparison.Ordinal));
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(arm.material_path);
                    arm.material = Describe(material);
                    Assert.That(material, Is.Not.Null);
                    stage = order.label + ": type" + arm.reference_type + " material.shader BEFORE direct shader load";
                    arm.resident_before_shader_read = Resident(order.shader_name);
                    Shader shader = material.shader;
                    arm.shader_before_direct_load = Describe(shader);
                    arm.exact_target_before_direct_load = shader != null && arm.shader_before_direct_load.guid == order.target_guid
                        && arm.shader_before_direct_load.local_id == 4800000 && arm.shader_before_direct_load.name == order.shader_name;
                    Assert.That(arm.material.guid, Is.EqualTo(arm.material_guid));
                    Assert.That(arm.material.file_type, Is.EqualTo((int)FileType.SerializedAssetType));
                    // Type 3 is the documented imported-asset control; type 2 is a measurement.
                    if (arm.reference_type == 3) Assert.That(arm.exact_target_before_direct_load, Is.True);
                    Assert.That(Hash(Full(arm.material_path)), Is.EqualTo(arm.yaml_sha256));
                    Assert.That(Hash(Full(arm.material_path + ".meta")), Is.EqualTo(arm.meta_sha256));
                }
                catch (Exception failure) { arm.exception += "\n" + failure; Record(failure); }
            }

            public void Cleanup()
            {
                var cleanupFailures = new List<Exception>();
                stage = "cleanup owned assets";
                TryCleanup(() =>
                {
                    if (folder != null && (Directory.Exists(Full(folder)) || File.Exists(Full(folder + ".meta"))))
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Delete only the diagnostic's unique owned root.");
                    Assert.That(folder == null || (!Directory.Exists(Full(folder)) && !File.Exists(Full(folder + ".meta"))), Is.True);
                    receipt.cleanup_complete = true;
                }, cleanupFailures);
                TryCleanup(() =>
                {
                    foreach (FileHash original in receipt.originals)
                        Assert.That(Hash(Full(original.path)), Is.EqualTo(original.sha256), original.path);
                    receipt.originals_unchanged = receipt.originals.Count == 4;
                }, cleanupFailures);
                if (before != null) TryCleanup(() =>
                {
                    stage = "final manifest";
                    Dictionary<string, string> after = Manifest(project);
                    receipt.manifest_after_count = after.Count;
                    receipt.manifest_after_sha256 = ManifestHash(after);
                    foreach (string path in before.Keys.Union(after.Keys).OrderBy(path => path, StringComparer.Ordinal))
                        if (!before.TryGetValue(path, out string oldHash) || !after.TryGetValue(path, out string newHash) || oldHash != newHash)
                            receipt.manifest_changes.Add(path);
                    receipt.manifest_unchanged = receipt.manifest_changes.Count == 0;
                    Assert.That(receipt.manifest_changes, Is.Empty, "Whole Assets/Packages/ProjectSettings content changed.");
                }, cleanupFailures);
                if (subscribed) { Application.logMessageReceived -= ObserveLog; subscribed = false; }
                receipt.completed_utc = DateTime.UtcNow.ToString("o");
                TryCleanup(() =>
                {
                    Assert.That(reportPath, Is.Not.Null.And.Not.Empty);
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    string json = JsonUtility.ToJson(receipt, true);
                    File.WriteAllText(reportPath, json, new UTF8Encoding(false));
                    TestContext.Out.WriteLine("Shader reference diagnostic receipt: " + reportPath);
                    // The unchanged native runner retains test output, not arbitrary reports.
                    TestContext.Out.WriteLine(json);
                }, cleanupFailures);
                if (cleanupFailures.Count != 0) throw new AggregateException("Shader reference diagnostic cleanup failed.", cleanupFailures);
            }

            private void TryCleanup(Action action, List<Exception> secondary)
            {
                try { action(); }
                catch (Exception failure) { receipt.failures.Add(stage + ": " + failure); secondary.Add(failure); }
            }

            private void Record(Exception failure)
            {
                receipt.failures.Add(stage + ": " + failure);
                failures.Add(failure);
            }

            private void ObserveLog(string message, string stack, LogType type)
            {
                receipt.logs.Add(new Log { stage = stage, type = type.ToString(), message = message, stack = stack });
            }

            private string Full(string path) { return Path.Combine(project, path); }
            private void Write(string path, byte[] bytes) { File.WriteAllBytes(Full(path), bytes); }
            private static void Import(string path) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate); }
        }

        private static Identity Describe(UnityEngine.Object value)
        {
            var identity = new Identity { unity_null = value == null };
            if (value == null) return identity;
            identity.name = value.name;
            identity.path = AssetDatabase.GetAssetPath(value);
            identity.managed_type = value.GetType().FullName;
            identity.identifier_found = ObjectIdentifier.TryGetObjectIdentifier(value, out ObjectIdentifier id);
            if (identity.identifier_found)
            {
                identity.guid = id.guid.ToString();
                identity.local_id = id.localIdentifierInFile;
                identity.file_type = (int)id.fileType;
                identity.file_path = id.filePath;
            }
            return identity;
        }

        private static string[] Resident(string name)
        {
            return Resources.FindObjectsOfTypeAll<Shader>().Where(shader => shader.name == name)
                .Select(shader => shader.name + " | " + AssetDatabase.GetAssetPath(shader) + " | instance " + shader.GetInstanceID())
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static byte[] ReplaceOnce(byte[] bytes, string oldValue, string newValue)
        {
            var utf8 = new UTF8Encoding(false, true);
            string text = utf8.GetString(bytes);
            int start = text.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), oldValue);
            Assert.That(text.IndexOf(oldValue, start + oldValue.Length, StringComparison.Ordinal), Is.EqualTo(-1));
            return utf8.GetBytes(text.Substring(0, start) + newValue + text.Substring(start + oldValue.Length));
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static Dictionary<string, string> Manifest(string project)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string top in new[] { "Assets", "Packages", "ProjectSettings" })
                AddDirectory(project, Path.Combine(project, top), result);
            return result;
        }

        private static string ManifestHash(Dictionary<string, string> manifest)
        {
            string rows = string.Join("\n", manifest.OrderBy(row => row.Key, StringComparer.Ordinal)
                .Select(row => row.Key + "\0" + row.Value));
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(rows))).Replace("-", "").ToLowerInvariant();
        }

        private static void AddDirectory(string project, string directory, Dictionary<string, string> manifest)
        {
            foreach (string file in Directory.EnumerateFiles(directory))
                manifest.Add(file.Substring(project.Length + 1).Replace('\\', '/'), Hash(file));
            foreach (string child in Directory.EnumerateDirectories(directory))
                if (!Path.GetFileName(child).StartsWith(".lucid-assets-", StringComparison.Ordinal)) AddDirectory(project, child, manifest);
        }
    }
}
