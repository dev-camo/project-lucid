#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid
{
    // Cinemachine.CinemachinePipeline .ctor, original token 0x06000120:
    // _CinemachinePipeline__ctor_m88F828229691C91F2DFF546084A80215324B9BF2,
    // x64 0x44d840, byte SHA256 a35359f486587db326d424d7bee44f6f55c75882a0c4d3d750617ccc46a4f192.
    // Its only action tail-calls MonoBehaviour..ctor at 0x26ce660. The empty sealed
    // marker source matches official Cinemachine 2.10.3 commit
    // fa616229e5cf36a8c640e862aff6eb686dbef444. This checks one isolated marker's
    // engine serialization; its parent camera components are excluded.
    public static class CinemachinePipelineVerification
    {
        private const string OriginalPrefab = "Assets/Data/Art/Cutscenes/Cutscenes/Zone3_Boss/CS_Zone3_Boss_H_3D.prefab";
        private const string OriginalHash = "d29d44f63392e16bc39d859e8064a233b162e40eb8cba687de014f8bc46e4c46";
        private const string ScriptPath = "Packages/com.unity.cinemachine/Runtime/Behaviours/CinemachinePipeline.cs";
        private const string ScriptHash = "a2c792c6452471ffb4753145a755b287d2d734b25c3e0e31a5dc85633f27f4e6";
        private const string OriginalScript = "{fileID: 11500000, guid: c8e8583478ed25a29d6b06ab06a00c08, type: 3}";
        private const string YamlHeader = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
        private static int checks;

        [Serializable] private sealed class ExportPointer { public string status = null; public string project_path = null; }
        [Serializable] private sealed class Receipt
        {
            public int schema_version = 1;
            public string command = "verify-cinemachine-pipeline-roundtrip";
            public string status = "failed";
            public string unity_version;
            public string original_prefab;
            public string original_prefab_sha256 = OriginalHash;
            public string package_source_sha256 = ScriptHash;
            public string loaded_script_guid;
            public long loaded_script_file_id;
            public long original_marker_file_id;
            public long[] isolated_document_ids;
            public string projected_fixture_sha256;
            public string mapped_fixture_sha256;
            public string roundtrip_sha256;
            public int checks;
            public bool isolated_marker_roundtrip_verified;
            public bool recovered_assets_modified = false;
            public bool camera_behavior_verified = false;
            public string error;
        }

        private sealed class Document
        {
            public int ClassId;
            public long Id;
            public string Label;
            public string Original;
            public readonly List<string> Order = new List<string>();
            public readonly Dictionary<string, List<string>> Fields = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            public string Value(string key) => Fields[key][0];
            public string Render()
            {
                var text = new StringBuilder("--- !u!" + ClassId + " &" + Id + "\n" + Label + ":\n");
                foreach (string key in Order)
                {
                    List<string> value = Fields[key];
                    text.Append("  ").Append(key).Append(':');
                    if (value[0].Length != 0) text.Append(' ').Append(value[0]);
                    text.Append('\n');
                    foreach (string line in value.Skip(1)) text.Append(line).Append('\n');
                }
                return text.ToString();
            }
        }

        public static void Run()
        {
            checks = 0;
            Check(Application.unityVersion == "2022.3.54f1", "matching original Unity version");
            string root = Directory.GetParent(Application.dataPath).FullName;
            string cache = Path.Combine(root, ".cache", "project-lucid");
            string pointerPath = CheckedPath(cache, Path.Combine(cache, "assets", "latest-assets.json"));
            ExportPointer pointer = JsonUtility.FromJson<ExportPointer>(File.ReadAllText(pointerPath));
            Check(pointer != null && pointer.status == "exported" && !String.IsNullOrEmpty(pointer.project_path), "complete asset export pointer");
            string sourcePath = CheckedPath(cache, Path.Combine(pointer.project_path, OriginalPrefab));
            byte[] source = File.ReadAllBytes(sourcePath);
            Check(Hash(source) == OriginalHash, "exact original prefab evidence");

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
            Check(script != null, "maintained upstream MonoScript is loaded");
            Type markerType = script.GetClass();
            Check(markerType != null && markerType.FullName == "Cinemachine.CinemachinePipeline" &&
                  markerType.Assembly.GetName().Name == "Cinemachine" && markerType.IsSealed &&
                  markerType.BaseType == typeof(MonoBehaviour), "exact sealed marker type and engine base");
            const BindingFlags declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Check(markerType.GetFields(declared).Length == 0 && markerType.GetMethods(declared).Length == 0 &&
                  markerType.GetConstructors(declared).Length == 1 && markerType.TypeInitializer == null,
                  "marker has only its native-corroborated constructor");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(ScriptPath);
            Check(package != null && package.name == "com.unity.cinemachine" && package.version == "2.10.3", "pinned maintained package");
            string installedSource = Path.Combine(package.resolvedPath, "Runtime", "Behaviours", "CinemachinePipeline.cs");
            Check(Hash(File.ReadAllBytes(installedSource)) == ScriptHash, "exact official marker source");
            string scriptGuid;
            long scriptId;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out scriptGuid, out scriptId) &&
                  scriptGuid == "ac0b09e7857660247b1477e93731de29" && scriptId != 0, "real upstream GUID and long local file ID");

            string runId = Guid.NewGuid().ToString("N");
            string run = CheckedPath(cache, Path.Combine(cache, "proof", "cinemachine-pipeline", runId));
            Check(!Directory.Exists(run), "fresh cache fixture directory");
            Directory.CreateDirectory(run);
            var receipt = new Receipt { unity_version = Application.unityVersion, original_prefab = OriginalPrefab,
                                        loaded_script_guid = scriptGuid, loaded_script_file_id = scriptId };
            string assetFolder = "Assets/LucidPipelineProof_" + runId;
            string assetPath = assetFolder + "/" + Path.GetFileName(OriginalPrefab);
            string assetFile = Path.Combine(root, assetPath);
            bool ownsFolder = false;
            GameObject contents = null;
            try
            {
                List<Document> documents = Parse(Encoding.UTF8.GetString(source));
                Dictionary<long, Document> all = documents.ToDictionary(d => d.Id);
                Document marker = documents.Single(d => d.ClassId == 114 && d.Fields.ContainsKey("m_Script") && d.Value("m_Script") == OriginalScript);
                receipt.original_marker_file_id = marker.Id;
                Check(marker.ClassId == 114 && marker.Label == "MonoBehaviour" && marker.Value("m_Script") == OriginalScript,
                      "exact original marker document and script pointer");
                List<Document> selected = SelectHierarchy(documents, all, marker);
                receipt.isolated_document_ids = selected.Select(d => d.Id).OrderBy(id => id).ToArray();
                File.WriteAllText(Path.Combine(run, "selected-original-documents.txt"), String.Concat(selected.Select(d => d.Original)));
                // Keep original parent pointers and nonzero ancestor local transforms.
                // Ancestor GameObjects retain only their Transform; their camera/game
                // components and unrelated branches are deliberately omitted here.
                ProjectHierarchy(selected, marker);
                string projected = YamlHeader + String.Concat(selected.Select(d => d.Render()));
                File.WriteAllText(Path.Combine(run, "projected-original.prefab"), projected, new UTF8Encoding(false));
                receipt.projected_fixture_sha256 = Hash(Encoding.UTF8.GetBytes(projected));
                marker.Fields["m_Script"] = new List<string> { "{fileID: " + scriptId.ToString(CultureInfo.InvariantCulture) +
                    ", guid: " + scriptGuid + ", type: 3}" };
                string mapped = YamlHeader + String.Concat(selected.Select(d => d.Render()));
                Check(mapped == projected.Replace("  m_Script: " + OriginalScript + "\n", "  m_Script: " + marker.Value("m_Script") + "\n"),
                      "mapping changes only the selected exact script pointer");
                string mappedPath = Path.Combine(run, "mapped-fixture.prefab");
                File.WriteAllText(mappedPath, mapped, new UTF8Encoding(false));
                receipt.mapped_fixture_sha256 = Hash(Encoding.UTF8.GetBytes(mapped));
                Check(!Directory.Exists(Path.Combine(root, assetFolder)) && !File.Exists(Path.Combine(root, assetFolder) + ".meta") &&
                      !AssetDatabase.IsValidFolder(assetFolder), "fresh temporary import path");
                Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "LucidPipelineProof_" + runId)), "temporary import folder created");
                ownsFolder = true;
                File.Copy(mappedPath, assetFile, false);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyLoaded(assetPath, selected, markerType, scriptGuid, scriptId);
                contents = PrefabUtility.LoadPrefabContents(assetPath);
                Check(contents != null, "isolated prefab contents load");
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath, out saved);
                Check(saved, "explicit prefab serialization roundtrip");
                PrefabUtility.UnloadPrefabContents(contents);
                contents = null;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyLoaded(assetPath, selected, markerType, scriptGuid, scriptId);
                byte[] roundtrip = File.ReadAllBytes(assetFile);
                File.WriteAllBytes(Path.Combine(run, "roundtrip.prefab"), roundtrip);
                VerifyRoundtrip(selected, Parse(Encoding.UTF8.GetString(roundtrip), marker.Id));
                receipt.roundtrip_sha256 = Hash(roundtrip);
                receipt.isolated_marker_roundtrip_verified = true;
                receipt.status = "complete";
            }
            catch (Exception error) { receipt.error = error.ToString(); throw; }
            finally
            {
                try
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                    if (ownsFolder) Check(AssetDatabase.DeleteAsset(assetFolder), "temporary imported assets removed");
                    Check(!Directory.Exists(Path.Combine(root, assetFolder)) && !File.Exists(Path.Combine(root, assetFolder) + ".meta"), "temporary import leaves no source files");
                    Check(Hash(File.ReadAllBytes(sourcePath)) == OriginalHash, "original export evidence remains immutable");
                }
                catch (Exception error) { receipt.status = "failed"; receipt.error = error.ToString(); throw; }
                finally
                {
                    receipt.checks = checks;
                    File.WriteAllText(Path.Combine(run, "report.json"), JsonUtility.ToJson(receipt, true) + "\n");
                }
            }
            Debug.Log("CinemachinePipeline isolated engine serialization roundtrip passed: " + checks + " checks; camera behavior remains unverified.");
        }

        private static List<Document> SelectHierarchy(List<Document> documents, Dictionary<long, Document> all, Document marker)
        {
            long gameObjectId = Pointer(marker.Value("m_GameObject"));
            Document leaf = all[gameObjectId];
            Check(leaf.ClassId == 1 && leaf.Value("m_Name") == "cinemachine_pipeline", "original marker GameObject");
            long[] components = ListPointers(leaf, "m_Component");
            Check(components.Length == 2 && components.Contains(marker.Id), "leaf contains only Transform and marker");
            long transformId = components.Single(id => all[id].ClassId == 4);
            var ids = new HashSet<long> { marker.Id };
            while (transformId != 0)
            {
                Check(ids.Add(transformId), "acyclic ancestor transform chain");
                Document transform = all[transformId];
                Check(transform.ClassId == 4, "ordinary Transform ancestor");
                long go = Pointer(transform.Value("m_GameObject"));
                Check(ids.Add(go) && all[go].ClassId == 1 && ListPointers(all[go], "m_Component").Contains(transformId), "original Transform owner identity");
                long parent = Pointer(transform.Value("m_Father"));
                if (parent != 0) Check(ListPointers(all[parent], "m_Children").Contains(transformId), "original reciprocal parent pointer");
                transformId = parent;
            }
            Check(ids.Count == 9, "four original GameObjects/Transforms and one marker");
            return documents.Where(d => ids.Contains(d.Id)).ToList();
        }

        private static void ProjectHierarchy(List<Document> documents, Document marker)
        {
            var kept = new HashSet<long>(documents.Select(d => d.Id));
            foreach (Document doc in documents)
            {
                if (doc.ClassId == 1)
                {
                    long[] ids = ListPointers(doc, "m_Component").Where(kept.Contains).ToArray();
                    doc.Fields["m_Component"] = new List<string> { "" };
                    doc.Fields["m_Component"].AddRange(ids.Select(id => "  - component: {fileID: " + id + "}"));
                }
                else if (doc.ClassId == 4)
                {
                    long[] ids = ListPointers(doc, "m_Children").Where(kept.Contains).ToArray();
                    doc.Fields["m_Children"] = new List<string> { ids.Length == 0 ? "[]" : "" };
                    doc.Fields["m_Children"].AddRange(ids.Select(id => "  - {fileID: " + id + "}"));
                }
            }
            Check(ListPointers(documents.Single(d => d.Id == Pointer(marker.Value("m_GameObject"))), "m_Component").Length == 2,
                  "marker owner component list remains intact");
        }

        private static void VerifyLoaded(string assetPath, List<Document> expected, Type markerType, string scriptGuid, long scriptId)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Check(root != null, "imported prefab exists");
            string prefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var actual = new Dictionary<long, Object>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                AddIdentity(actual, transform.gameObject, prefabGuid);
                foreach (Component component in transform.GetComponents<Component>())
                {
                    Check(component != null && (component is Transform || component.GetType() == markerType), "only verified marker and engine Transform components imported");
                    AddIdentity(actual, component, prefabGuid);
                }
            }
            Check(actual.Keys.OrderBy(id => id).SequenceEqual(expected.Select(d => d.Id).OrderBy(id => id)), "original long object identities preserved");
            foreach (Document doc in expected)
            {
                Object obj = actual[doc.Id];
                Check((int)obj.hideFlags == Integer(doc.Value("m_ObjectHideFlags")), "original engine hide flags " + doc.Id);
                if (obj is GameObject go)
                {
                    Check(go.name == doc.Value("m_Name") && go.activeSelf == (Integer(doc.Value("m_IsActive")) != 0) &&
                          go.layer == Integer(doc.Value("m_Layer")) && go.tag == doc.Value("m_TagString"), "GameObject engine fields " + doc.Id);
                    Check((int)GameObjectUtility.GetStaticEditorFlags(go) == Integer(doc.Value("m_StaticEditorFlags")), "GameObject static editor flags");
                }
                else if (obj is Transform transform)
                {
                    var serialized = new SerializedObject(transform);
                    CheckVector(Property(serialized, "m_LocalPosition").vector3Value, doc.Value("m_LocalPosition"));
                    CheckVector(Property(serialized, "m_LocalScale").vector3Value, doc.Value("m_LocalScale"));
                    CheckVector(Property(serialized, "m_LocalEulerAnglesHint").vector3Value, doc.Value("m_LocalEulerAnglesHint"));
                    Check(Property(serialized, "m_ConstrainProportionsScale").boolValue == (Integer(doc.Value("m_ConstrainProportionsScale")) != 0), "original proportional-scale flag");
                    Quaternion q = Property(serialized, "m_LocalRotation").quaternionValue;
                    float[] v = Scalars(doc.Value("m_LocalRotation"));
                    Check(v.Length == 4 && q.x == v[0] && q.y == v[1] && q.z == v[2] && q.w == v[3], "exact original serialized rotation");
                    Check(transform.parent == (Pointer(doc.Value("m_Father")) == 0 ? null : actual[Pointer(doc.Value("m_Father"))]), "original parent object pointer");
                    Check(transform.gameObject == actual[Pointer(doc.Value("m_GameObject"))], "Transform owner pointer");
                }
                else
                {
                    var behaviour = (MonoBehaviour)obj;
                    var serialized = new SerializedObject(behaviour);
                    MonoScript script = Property(serialized, "m_Script").objectReferenceValue as MonoScript;
                    string guid;
                    long id;
                    Check(behaviour.GetType() == markerType && script != null && script.GetClass() == markerType &&
                          AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out id) && guid == scriptGuid && id == scriptId,
                          "exact component class and script GUID/local file ID");
                    Check(behaviour.enabled == (Integer(doc.Value("m_Enabled")) != 0), "original enabled field");
                    Check(behaviour.gameObject == actual[Pointer(doc.Value("m_GameObject"))], "marker owner pointer");
                    Check(Property(serialized, "m_EditorHideFlags").intValue == Integer(doc.Value("m_EditorHideFlags")), "original editor hide flags");
                }
            }
        }

        private static void AddIdentity(Dictionary<long, Object> objects, Object obj, string prefabGuid)
        {
            string guid;
            long id;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out id) && guid == prefabGuid && id != 0,
                  "persistent object's full GUID/long local identity");
            Check(!objects.ContainsKey(id), "unique imported object local ID");
            objects.Add(id, obj);
        }

        private static void VerifyRoundtrip(List<Document> expected, List<Document> saved)
        {
            Dictionary<long, Document> actual = saved.ToDictionary(d => d.Id);
            Check(actual.Keys.OrderBy(id => id).SequenceEqual(expected.Select(d => d.Id).OrderBy(id => id)), "roundtrip YAML document identities");
            foreach (Document doc in expected)
            {
                Document other = actual[doc.Id];
                Check(doc.ClassId == other.ClassId && doc.Label == other.Label, "roundtrip engine object kind");
                Check(doc.Fields.Keys.OrderBy(k => k).SequenceEqual(other.Fields.Keys.OrderBy(k => k)), "roundtrip engine field set " + doc.Id +
                      ": original=" + String.Join(",", doc.Fields.Keys) + "; saved=" + String.Join(",", other.Fields.Keys));
                foreach (string key in doc.Order)
                {
                    List<string> left = doc.Fields[key], right = other.Fields[key];
                    if (key.StartsWith("m_Local", StringComparison.Ordinal) && left[0].StartsWith("{x:", StringComparison.Ordinal))
                        Check(Scalars(left[0]).SequenceEqual(Scalars(right[0])), "roundtrip exact float32 field " + key);
                    else Check(left.SequenceEqual(right), "roundtrip field value/pointer " + key);
                }
            }
        }

        private static List<Document> Parse(string yaml, long selectedMarkerId = 0)
        {
            var result = new List<Document>();
            var headers = Regex.Matches(yaml, @"^--- !u!(\d+) &(\d+)\r?\n", RegexOptions.Multiline);
            Check(yaml.StartsWith(YamlHeader, StringComparison.Ordinal), "expected Unity YAML encoding/header");
            for (int i = 0; i < headers.Count; i++)
            {
                Match header = headers[i];
                int end = i + 1 == headers.Count ? yaml.Length : headers[i + 1].Index;
                string original = yaml.Substring(header.Index, end - header.Index);
                string[] lines = yaml.Substring(header.Index + header.Length, end - header.Index - header.Length).Split('\n');
                var doc = new Document { ClassId = Integer(header.Groups[1].Value), Id = Int64.Parse(header.Groups[2].Value, CultureInfo.InvariantCulture),
                                         Label = lines[0].TrimEnd('\r', ':'), Original = original };
                if (doc.ClassId == 1 || doc.ClassId == 4 || doc.ClassId == 114 &&
                    (doc.Id == selectedMarkerId || original.Contains("  m_Script: " + OriginalScript + "\n")))
                {
                    List<string> field = null;
                    foreach (string raw in lines.Skip(1))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length == 0) continue;
                        Match key = Regex.Match(line, @"^  ([A-Za-z_][A-Za-z0-9_]*):(.*)$");
                        if (key.Success)
                        {
                            field = new List<string> { key.Groups[2].Value.TrimStart() };
                            doc.Order.Add(key.Groups[1].Value);
                            doc.Fields.Add(key.Groups[1].Value, field);
                        }
                        else
                        {
                            Check(field != null && line.StartsWith("  - ", StringComparison.Ordinal), "bounded engine field syntax");
                            field.Add(line);
                        }
                    }
                }
                result.Add(doc);
            }
            Check(result.Count != 0 && result.Select(d => d.Id).Distinct().Count() == result.Count, "unique original YAML document identities");
            return result;
        }

        private static long[] ListPointers(Document doc, string key) => doc.Fields[key].Skip(1).Select(Pointer).ToArray();
        private static long Pointer(string value)
        {
            Match match = Regex.Match(value, @"\{fileID: (-?\d+)\}");
            Check(match.Success, "exact engine object pointer");
            return Int64.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        private static int Integer(string value) => Int32.Parse(value, CultureInfo.InvariantCulture);
        private static SerializedProperty Property(SerializedObject owner, string name)
        {
            SerializedProperty property = owner.FindProperty(name);
            Check(property != null, "loaded serialized field exists: " + name);
            return property;
        }
        private static float[] Scalars(string value) => Regex.Matches(value, @"[xyzw]: ([^,}]+)").Cast<Match>()
            .Select(m => Single.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
        private static void CheckVector(Vector3 actual, string expected)
        {
            float[] v = Scalars(expected);
            Check(v.Length == 3 && actual.x == v[0] && actual.y == v[1] && actual.z == v[2], "exact original serialized vector");
        }
        private static string Hash(byte[] data)
        {
            using (var digest = SHA256.Create()) return BitConverter.ToString(digest.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
        private static string CheckedPath(string cache, string candidate)
        {
            cache = Path.GetFullPath(cache);
            string path = Path.GetFullPath(candidate);
            Check(path.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.Ordinal), "fixture/evidence stays inside generated cache");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
            {
                if (File.Exists(part) || Directory.Exists(part)) Check((File.GetAttributes(part) & FileAttributes.ReparsePoint) == 0, "no symlinked fixture/evidence path");
            }
            return path;
        }
        private static void Check(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException("CinemachinePipeline serialization verification failed: " + message);
        }
    }
}
#endif
