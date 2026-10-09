#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid
{
    // Shared upstream code stays in Unity.ugui. This fixture imports authored
    // scalar component documents into an isolated, inactive hierarchy. Fixture
    // object IDs and owners are new; shipping assets change only m_Script.
    public static class UguiLayoutBindingVerification
    {
        [Serializable] private sealed class ExportPointer { public string status; public string project_path; }
        [Serializable] private sealed class PreparePointer { public string status; public string recovered_path; public BindingProof script_bindings; }
        [Serializable] private sealed class BindingProof { public string status; public Binding[] bindings; public AssetRow[] assets; }
        [Serializable] private sealed class Binding
        {
            public string full_name, package, maintained_path, exported_guid, target_guid, source_sha256;
            public string binary_sha256, metadata_sha256;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class AssetRow { public string path, exported_sha256, prepared_sha256; }
        [Serializable] private sealed class Receipt
        {
            public string status = "failed", unity_version, error, cleanup_error;
            public int authored_documents, distinct_vectors, imported_vectors, checks;
            public bool authored_script_pointers_verified, scalar_values_survive_roundtrip;
            public bool original_hierarchy_or_full_UI_verified = false;
        }
        private sealed class Sample
        {
            public Binding Binding;
            public string Original, Key;
            public Dictionary<string, string> Fields;
            public MonoScript Script;
        }
        private static readonly Regex Documents = new Regex(@"^--- !u!(\d+) &(-?\d+)\r?\n\w+:\r?\n.*?(?=^--- !u!|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        // Large asset scans retain offsets instead of copying every document.
        // Keep the original envelope grammar; the small header regex sees only
        // the header and label lines, never the whole asset or document body.
        private static readonly Regex DocumentHeader = new Regex(@"\A--- !u!(\d+) &(-?\d+)\r?\n\w+:\r?\n\z");
        private readonly struct DocumentSpan
        {
            public readonly string Class, Id;
            public readonly int Start, Length;
            public DocumentSpan(string kind, string id, int start, int length)
            { Class = kind; Id = id; Start = start; Length = length; }
        }
        private static readonly string[] Common = { "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier" };
        private static int checks;

        public static void RunAuthored()
        {
            checks = 0;
            Check(Application.unityVersion == "2022.3.54f1", "matching Editor");
            string root = Directory.GetParent(Application.dataPath).FullName;
            string cache = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            if (String.IsNullOrEmpty(cache)) cache = Path.Combine(root, ".cache/project-lucid");
            cache = Path.GetFullPath(cache);
            var export = JsonUtility.FromJson<ExportPointer>(File.ReadAllText(OwnedPath(cache, Path.Combine(cache, "assets/latest-assets.json"))));
            var prepared = JsonUtility.FromJson<PreparePointer>(File.ReadAllText(OwnedPath(cache, Path.Combine(cache, "assets/latest-prepare.json"))));
            Check(export != null && export.status == "exported" && prepared != null && prepared.status == "prepared" &&
                  prepared.script_bindings != null && prepared.script_bindings.status == "verified_layout", "complete export and preparation proof");
            Check(Path.GetFullPath(prepared.recovered_path) == Path.Combine(root, "Assets/Recovered"), "owned generated assets");
            Binding[] bindings = prepared.script_bindings.bindings.Where(b => b.package == "com.unity.ugui").ToArray();
            Check(bindings.Length == 3 && bindings.Select(b => b.full_name).Distinct().Count() == 3 &&
                  bindings.Select(b => b.full_name).OrderBy(n => n).SequenceEqual(new[] { typeof(AspectRatioFitter).FullName, typeof(ContentSizeFitter).FullName, typeof(LayoutElement).FullName }.OrderBy(n => n)), "exact reviewed three components");
            var scripts = new Dictionary<string, MonoScript>();
            foreach (Binding binding in bindings)
            {
                Check(binding.binary_sha256 == "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216" &&
                      binding.metadata_sha256 == "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b", "reviewed shipping release");
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(binding.maintained_path);
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(binding.maintained_path);
                Check(script != null && script.GetClass() != null && script.GetClass().FullName == binding.full_name &&
                      script.GetClass().Assembly.GetName().Name == "UnityEngine.UI" && package != null &&
                      package.name == "com.unity.ugui" && package.version == "1.0.0" &&
                      package.source == UnityEditor.PackageManager.PackageSource.BuiltIn, "actual loaded builtin component");
                string source = OwnedPath(root, Path.Combine(package.resolvedPath, binding.maintained_path.Substring("Packages/com.unity.ugui/".Length)));
                Check(Hash(File.ReadAllBytes(source)) == binding.source_sha256, "exact loaded component source");
                CheckIdentity(script, binding);
                scripts.Add(binding.full_name, script);
            }

            var samples = new Dictionary<string, Sample>(StringComparer.Ordinal);
            var counts = bindings.ToDictionary(b => b.full_name, b => 0);
            foreach (AssetRow asset in prepared.script_bindings.assets)
            {
                string originalPath = OwnedPath(cache, Path.Combine(export.project_path, asset.path));
                byte[] original = File.ReadAllBytes(originalPath);
                Check(Hash(original) == asset.exported_sha256, "unchanged exported asset " + asset.path);
                Check(asset.path.StartsWith("Assets/", StringComparison.Ordinal), "exported asset prefix");
                string generatedPath = OwnedPath(prepared.recovered_path, Path.Combine(prepared.recovered_path, asset.path.Substring(7)));
                byte[] generated = File.ReadAllBytes(generatedPath);
                Check(Hash(generated) == asset.prepared_sha256, "exact prepared asset " + asset.path);
                string originalText = Encoding.UTF8.GetString(original), generatedText = Encoding.UTF8.GetString(generated);
                var mapped = ScanDocuments(generatedText).ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);
                foreach (DocumentSpan document in ScanDocuments(originalText))
                {
                    if (document.Class != "114") continue;
                    Binding binding = bindings.SingleOrDefault(b => originalText.IndexOf("  m_Script: " + Pointer(b.exported_file_id, b.exported_guid) + "\n",
                        document.Start, document.Length, StringComparison.Ordinal) >= 0);
                    if (binding == null) continue;
                    string originalDocument = originalText.Substring(document.Start, document.Length);
                    Dictionary<string, string> fields = ParseFields(originalDocument);
                    string[] own = OwnFields(binding.full_name);
                    Check(fields.Keys.OrderBy(n => n).SequenceEqual(Common.Concat(own).OrderBy(n => n)), "exact scalar field set");
                    Check(fields["m_ObjectHideFlags"] == "0" && fields["m_Enabled"] == "1" && fields["m_EditorHideFlags"] == "0" &&
                          fields["m_Name"] == "" && fields["m_EditorClassIdentifier"] == "" &&
                          new[] { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" }.All(n => fields[n] == "{fileID: 0}"), "authored common component fields");
                    DocumentSpan preparedDocument = mapped[document.Id];
                    string expectedDocument = originalDocument.Replace(Pointer(binding.exported_file_id, binding.exported_guid), Pointer(binding.target_file_id, binding.target_guid));
                    Check(preparedDocument.Length == expectedDocument.Length &&
                          String.CompareOrdinal(generatedText, preparedDocument.Start, expectedDocument, 0, expectedDocument.Length) == 0,
                          "prepared document changes only its script pointer");
                    ++counts[binding.full_name];
                    string key = binding.full_name + ":" + String.Join(";", own.Select(n => n + "=" + fields[n]));
                    if (!samples.ContainsKey(key)) samples.Add(key, new Sample { Binding = binding, Original = originalDocument, Fields = fields, Key = key, Script = scripts[binding.full_name] });
                }
            }
            Check(counts[typeof(LayoutElement).FullName] == 4188 && counts[typeof(ContentSizeFitter).FullName] == 291 && counts[typeof(AspectRatioFitter).FullName] == 63,
                  "all 4542 authored component documents accounted for");
            Check(samples.Count == 116, "all observed distinct scalar vectors");
            string id = Guid.NewGuid().ToString("N");
            string run = OwnedPath(cache, Path.Combine(cache, "proof/ugui-layout", id));
            Directory.CreateDirectory(run);
            var receipt = new Receipt { unity_version = Application.unityVersion, authored_documents = counts.Values.Sum(), distinct_vectors = samples.Count, authored_script_pointers_verified = true };
            string folder = "Assets/LucidLayoutProof_" + id, assetPath = folder + "/layout.prefab";
            bool ownsFolder = false;
            GameObject construction = null, contents = null;
            try
            {
                Check(!Directory.Exists(Path.Combine(root, folder)) && !File.Exists(Path.Combine(root, folder) + ".meta"), "fresh temporary import path");
                Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder))), "temporary import folder");
                ownsFolder = true;
                Sample[] ordered = samples.Values.OrderBy(s => s.Key, StringComparer.Ordinal).ToArray();
                construction = new GameObject("Isolated authored layout vectors", typeof(RectTransform));
                construction.SetActive(false);
                for (int i = 0; i < ordered.Length; ++i)
                {
                    var child = new GameObject(i.ToString(CultureInfo.InvariantCulture), typeof(RectTransform));
                    child.SetActive(false);
                    child.transform.SetParent(construction.transform, false);
                    child.AddComponent(ordered[i].Script.GetClass());
                }
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(construction, assetPath, out saved);
                Check(saved, "Editor-generated fixture hierarchy");
                Object.DestroyImmediate(construction); construction = null;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                string yaml = File.ReadAllText(Path.Combine(root, assetPath));
                for (int i = 0; i < ordered.Length; ++i)
                {
                    Sample sample = ordered[i];
                    GameObject child = prefab.transform.GetChild(i).gameObject;
                    Component component = child.GetComponent(sample.Script.GetClass());
                    long componentId = LocalId(component), ownerId = LocalId(child);
                    string replacement = Regex.Replace(sample.Original, @"\A--- !u!114 &-?\d+", "--- !u!114 &" + componentId);
                    replacement = Regex.Replace(replacement, @"^  m_GameObject: \{fileID: -?\d+\}$", "  m_GameObject: {fileID: " + ownerId + "}", RegexOptions.Multiline);
                    replacement = replacement.Replace(Pointer(sample.Binding.exported_file_id, sample.Binding.exported_guid), Pointer(sample.Binding.target_file_id, sample.Binding.target_guid));
                    Match current = Documents.Matches(yaml).Cast<Match>().Single(m => m.Groups[1].Value == "114" && m.Groups[2].Value == componentId.ToString(CultureInfo.InvariantCulture));
                    yaml = yaml.Substring(0, current.Index) + replacement + yaml.Substring(current.Index + current.Length);
                }
                File.WriteAllText(Path.Combine(run, "mapped-fixture.prefab"), yaml, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, assetPath), yaml, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyImported(assetPath, ordered);
                contents = PrefabUtility.LoadPrefabContents(assetPath);
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath, out saved);
                Check(saved, "authored scalar serialization roundtrip");
                PrefabUtility.UnloadPrefabContents(contents); contents = null;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VerifyImported(assetPath, ordered);
                File.Copy(Path.Combine(root, assetPath), Path.Combine(run, "roundtrip.prefab"));
                receipt.imported_vectors = ordered.Length;
                receipt.scalar_values_survive_roundtrip = true;
                receipt.status = "complete";
            }
            catch (Exception error) { receipt.error = error.ToString(); throw; }
            finally
            {
                try
                {
                    if (construction != null) Object.DestroyImmediate(construction);
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                    if (ownsFolder) Check(AssetDatabase.DeleteAsset(folder), "temporary imported fixture removed");
                }
                catch (Exception cleanup)
                {
                    receipt.status = "failed";
                    receipt.cleanup_error = cleanup.ToString();
                    // Preserve the original failure and record cleanup separately.
                    if (receipt.error == null) throw;
                }
                finally
                {
                    receipt.checks = checks;
                    try { File.WriteAllText(Path.Combine(run, "report.json"), JsonUtility.ToJson(receipt, true)); }
                    catch (Exception reportError)
                    {
                        if (receipt.error == null && receipt.cleanup_error == null) throw;
                        Debug.LogError("Could not write uGUI fixture failure receipt: " + reportError);
                    }
                }
            }
        }

        public static void RunGeometry()
        {
            checks = 0;
            var parent = new GameObject("Controlled layout parent", typeof(RectTransform));
            try
            {
                var parentRect = (RectTransform)parent.transform;
                parentRect.sizeDelta = new Vector2(600, 400);
                var child = new GameObject("Controlled content", typeof(RectTransform));
                child.transform.SetParent(parent.transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                var low = child.AddComponent<LayoutElement>();
                low.layoutPriority = 1; low.minWidth = 30; low.minHeight = 40; low.preferredWidth = 150; low.preferredHeight = 80;
                var high = child.AddComponent<LayoutElement>();
                high.layoutPriority = 2; high.minWidth = 10; high.preferredWidth = -1;
                Check(LayoutUtility.GetMinWidth(rect) == 10 && LayoutUtility.GetPreferredWidth(rect) == 150 && LayoutUtility.GetPreferredHeight(rect) == 80,
                      "priority selection and negative-value fallback");
                var content = child.AddComponent<ContentSizeFitter>();
                content.horizontalFit = ContentSizeFitter.FitMode.MinSize; content.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                content.SetLayoutHorizontal(); content.SetLayoutVertical();
                Check(rect.rect.size == new Vector2(10, 80), "minimum and preferred axes");
                content.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 75);
                content.SetLayoutHorizontal();
                Check(rect.rect.width == 75, "unconstrained axis stays authored");
                Object.DestroyImmediate(child);
                child = new GameObject("Controlled aspect", typeof(RectTransform));
                child.transform.SetParent(parent.transform, false);
                rect = (RectTransform)child.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(300, 200);
                var aspect = child.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight; aspect.aspectRatio = 2; aspect.SetLayoutHorizontal();
                Check(rect.rect.size == new Vector2(300, 150), "width controls height");
                aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth; rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80); aspect.SetLayoutVertical();
                Check(rect.rect.size == new Vector2(160, 80), "height controls width");
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.SetLayoutHorizontal();
                Check(rect.rect.size == new Vector2(600, 300) && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.anchoredPosition == Vector2.zero,
                      "fit in parent and driven anchors");
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.SetLayoutVertical();
                Check(rect.rect.size == new Vector2(800, 400), "envelope parent");
                parentRect.sizeDelta = new Vector2(600, 200);
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.SetLayoutHorizontal();
                Check(rect.rect.size == new Vector2(400, 200), "fit after parent resize");
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.SetLayoutVertical();
                Check(rect.rect.size == new Vector2(600, 300), "envelope after parent resize");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        private static int NextDocumentBoundary(string text, int start)
        {
            const string marker = "--- !u!";
            if (start <= text.Length - marker.Length && (start == 0 || text[start - 1] == '\n') &&
                String.CompareOrdinal(text, start, marker, 0, marker.Length) == 0) return start;
            int newline = text.IndexOf("\n" + marker, start, StringComparison.Ordinal);
            return newline < 0 ? -1 : newline + 1;
        }
        private static IEnumerable<DocumentSpan> ScanDocuments(string text)
        {
            int start = NextDocumentBoundary(text, 0);
            while (start >= 0)
            {
                // Every marker terminates the preceding document, even if its
                // own header is malformed, stripped, or otherwise unsupported.
                int next = NextDocumentBoundary(text, start + 1);
                int end = next < 0 ? text.Length : next;
                int headerNewline = text.IndexOf('\n', start, end - start);
                if (headerNewline >= 0)
                {
                    int labelNewline = text.IndexOf('\n', headerNewline + 1, end - headerNewline - 1);
                    if (labelNewline >= 0)
                    {
                        Match header = DocumentHeader.Match(text.Substring(start, labelNewline + 1 - start));
                        if (header.Success)
                            yield return new DocumentSpan(header.Groups[1].Value, header.Groups[2].Value, start, end - start);
                    }
                }
                start = next;
            }
        }

        private static string[] OwnFields(string type)
        {
            if (type == typeof(LayoutElement).FullName) return new[] { "m_IgnoreLayout", "m_MinWidth", "m_MinHeight", "m_PreferredWidth", "m_PreferredHeight", "m_FlexibleWidth", "m_FlexibleHeight", "m_LayoutPriority" };
            if (type == typeof(ContentSizeFitter).FullName) return new[] { "m_HorizontalFit", "m_VerticalFit" };
            Check(type == typeof(AspectRatioFitter).FullName, "reviewed component type");
            return new[] { "m_AspectMode", "m_AspectRatio" };
        }
        private static Dictionary<string, string> ParseFields(string document)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in document.Split('\n').Skip(2))
            {
                if (line.Length == 0) continue;
                Match match = Regex.Match(line, @"^  (m_\w+): ?(.*)$");
                Check(match.Success, "bounded scalar component syntax");
                fields.Add(match.Groups[1].Value, match.Groups[2].Value);
            }
            return fields;
        }
        private static void VerifyImported(string path, Sample[] samples)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab != null && !prefab.activeSelf && prefab.transform.childCount == samples.Length, "isolated inactive imported fixture");
            for (int i = 0; i < samples.Length; ++i)
            {
                Sample sample = samples[i];
                GameObject child = prefab.transform.GetChild(i).gameObject;
                Component component = child.GetComponent(sample.Script.GetClass());
                Check(!child.activeSelf && component != null && child.GetComponents<Component>().Length == 2, "only RectTransform and verified component");
                var serialized = new SerializedObject(component);
                CheckIdentity((MonoScript)serialized.FindProperty("m_Script").objectReferenceValue, sample.Binding);
                Check(serialized.FindProperty("m_Enabled").boolValue && serialized.FindProperty("m_ObjectHideFlags").intValue == 0, "authored enabled/hide fields");
                foreach (string name in OwnFields(sample.Binding.full_name))
                {
                    SerializedProperty property = serialized.FindProperty(name);
                    Check(property != null, "authored scalar field exists " + name);
                    if (property.propertyType == SerializedPropertyType.Float)
                        Check(property.floatValue == Single.Parse(sample.Fields[name], CultureInfo.InvariantCulture), "exact authored float32 " + name);
                    else if (property.propertyType == SerializedPropertyType.Boolean)
                        Check(property.boolValue == (Int32.Parse(sample.Fields[name], CultureInfo.InvariantCulture) != 0), "exact authored Boolean " + name);
                    else
                        Check((property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum) &&
                              property.intValue == Int32.Parse(sample.Fields[name], CultureInfo.InvariantCulture), "exact authored integer/enum " + name);
                }
            }
        }
        private static void CheckIdentity(MonoScript script, Binding binding)
        {
            string guid; long id;
            Check(script != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out id) && guid == binding.target_guid && id == binding.target_file_id,
                  "actual script GUID and long local ID");
        }
        private static long LocalId(Object value)
        {
            string guid; long id;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id) && id != 0, "real fixture local ID"); return id;
        }
        private static string Pointer(long id, string guid) => "{fileID: " + id.ToString(CultureInfo.InvariantCulture) + ", guid: " + guid + ", type: 3}";
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string OwnedPath(string owner, string value)
        {
            string path = Path.GetFullPath(value), root = Path.GetFullPath(owner);
            Check(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "evidence stays in owned path");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
                if (File.Exists(part) || Directory.Exists(part)) Check((File.GetAttributes(part) & FileAttributes.ReparsePoint) == 0, "no symlinked evidence path");
            return path;
        }
        private static void Check(bool condition, string message) { ++checks; if (!condition) throw new InvalidOperationException("uGUI layout verification failed: " + message); }
    }
}
#endif
