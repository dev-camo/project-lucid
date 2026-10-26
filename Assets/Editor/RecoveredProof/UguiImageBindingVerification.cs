#if UNITY_EDITOR
using System;
using System.Collections;
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
    // Qualifies inactive authored serialization only. Image's original
    // 06000162@27D1C20 normalizes fill values; 06000101@27CC650 still unregisters
    // inherited state on destruction. Neither layout nor this proof is a render test.
    public static class UguiImageBindingVerification
    {
        [Serializable] private sealed class ExportPointer { public string status, project_path, asset_map_path; }
        [Serializable] private sealed class PreparePointer { public string status, recovered_path; public BindingProof script_bindings; }
        [Serializable] private sealed class BindingProof { public string status; public Binding[] bindings; public AssetRow[] assets; }
        [Serializable] private sealed class Binding
        {
            public string full_name, package, maintained_path, exported_guid, target_guid, source_sha256;
            public string binary_sha256, metadata_sha256;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class AssetRow { public string path, exported_sha256, prepared_sha256; }
        [Serializable] private sealed class OriginalMap { public string project_path; public OriginalAsset[] assets; }
        [Serializable] private sealed class OriginalAsset { public string path, guid; public bool code_quarantined; public string[] script_references; }
        private sealed class Sample
        {
            public string Document;
            public Dictionary<string, string> Fields;
        }
        private static readonly string[] OwnFields = {
            "m_Material", "m_Color", "m_RaycastTarget", "m_RaycastPadding", "m_Maskable", "m_OnCullStateChanged",
            "m_Sprite", "m_Type", "m_PreserveAspect", "m_FillCenter", "m_FillMethod", "m_FillAmount",
            "m_FillClockwise", "m_FillOrigin", "m_UseSpriteMesh", "m_PixelsPerUnitMultiplier"
        };
        private static readonly string[] CommonFields = {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"
        };
        private static readonly Regex Header = new Regex(@"^--- !u!([0-9]+) &(-?[0-9]+)\r?$", RegexOptions.Multiline);
        private static readonly Regex Pointer = new Regex(@"\A\{fileID: (-?[0-9]+), guid: ([0-9a-f]{32}), type: ([02])\}\z");

        private const string BuiltinGuid = "0000000000000000f000000000000000";
        private const string BuiltinPath = "Resources/unity_builtin_extra";
        private const string BuiltinResourceHash = "4706a837278deb458991d16823718055f9ec00c16a483bb8c4782405ba32a289";

        public static void CheckBuiltinAssets()
        {
            // Original field/local-ID pairs; installed resource objects retain
            // these identities. No replacement Sprite or default material is used.
            var samples = new List<Sample>();
            foreach (long id in new long[] { 10907, 10911, 10913, 10917 })
                samples.Add(new Sample { Fields = new Dictionary<string, string> {
                    { "m_Sprite", "{fileID: " + id + ", guid: " + BuiltinGuid + ", type: 0}" },
                    { "m_Material", "{fileID: 10754, guid: " + BuiltinGuid + ", type: 0}" }
                } });
            var borrowed = new BorrowedAssets();
            VerificationFixtureOwnership.RunThenCleanUp(() => borrowed.Capture(samples.ToArray()), borrowed.Verify);
        }

        public sealed class OwnedFixture : IDisposable
        {
            public string Folder { get; } = "Assets/LucidImageProof_" + Guid.NewGuid().ToString("N");
            public string AssetPath => Folder + "/images.prefab";
            public GameObject Construction { get; private set; }
            public GameObject Contents { get; private set; }
            private readonly List<GameObject> children = new List<GameObject>();
            private bool ownsFolder;

            public void Create(Action afterFolder = null)
            {
                Check(!Directory.Exists(Folder) && !File.Exists(Folder + ".meta"), "fresh owned fixture folder");
                // Claim the unique absent path before the creation API can fail.
                ownsFolder = true;
                Check(!String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder))), "create fixture folder");
                afterFolder?.Invoke();
                Construction = new GameObject("Inactive authored Image vectors", typeof(RectTransform));
                Construction.SetActive(false);
            }

            public UnityEngine.UI.Image AddImage(string name)
            {
                var child = new GameObject(name, typeof(RectTransform));
                children.Add(child); // custody before parenting or component callbacks
                child.SetActive(false);
                child.transform.SetParent(Construction.transform, false);
                return child.AddComponent<UnityEngine.UI.Image>();
            }

            public void Save()
            {
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(Construction, AssetPath, out saved);
                Check(saved, "save genuine fixture hierarchy");
            }

            public void Load()
            {
                Check(Contents == null, "one owned loaded prefab");
                Contents = PrefabUtility.LoadPrefabContents(AssetPath);
                Check(Contents != null, "load owned prefab contents");
            }

            public void Unload()
            {
                if (Contents == null) return;
                PrefabUtility.UnloadPrefabContents(Contents);
                Contents = null; // clear custody only after successful release
            }

            internal void Release(Action<string> afterRelease)
            {
                VerificationFixtureOwnership.RunThenCleanUp(() => { },
                    () =>
                    {
                        // A failure destroying one child must not skip the others or root.
                        var releases = children.Select<GameObject, Action>(child => () => { if (child != null) Object.DestroyImmediate(child); }).ToList();
                        releases.Add(() => { if (Construction != null) Object.DestroyImmediate(Construction); });
                        VerificationFixtureOwnership.RunThenCleanUp(() => { }, releases.ToArray());
                        afterRelease?.Invoke("construction");
                    },
                    () =>
                    {
                        Unload();
                        afterRelease?.Invoke("contents");
                    },
                    () =>
                    {
                        if (ownsFolder && (AssetDatabase.IsValidFolder(Folder) || Directory.Exists(Folder)))
                            Check(AssetDatabase.DeleteAsset(Folder), "delete owned fixture folder");
                        Check(!ownsFolder || (!Directory.Exists(Folder) && !File.Exists(Folder + ".meta")), "owned files removed");
                        afterRelease?.Invoke("folder");
                    });
            }

            public void Dispose() => Release(null);
        }

        public static void RunOwnedFixture(Action<OwnedFixture> body, Action<string> afterRelease = null)
        {
            var state = new OwnedFixture();
            VerificationFixtureOwnership.RunThenCleanUp(() => body(state), () => state.Release(afterRelease));
        }

        public static int RunAuthored()
        {
            Check(Application.unityVersion == "2022.3.54f1", "qualified Editor");
            string root = Directory.GetParent(Application.dataPath).FullName;
            string configuredCache = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            string cache = Path.GetFullPath(String.IsNullOrEmpty(configuredCache) ? Path.Combine(root, ".cache/project-lucid") : configuredCache);
            var export = JsonUtility.FromJson<ExportPointer>(File.ReadAllText(OwnedPath(cache, Path.Combine(cache, "assets/latest-assets.json"))));
            var prepared = JsonUtility.FromJson<PreparePointer>(File.ReadAllText(OwnedPath(cache, Path.Combine(cache, "assets/latest-prepare.json"))));
            Check(export != null && export.status == "exported" && prepared != null && prepared.status == "prepared" &&
                  prepared.script_bindings != null && prepared.script_bindings.status == "verified_layout", "qualified preparation");
            Check(Path.GetFullPath(prepared.recovered_path) == Path.Combine(root, "Assets/Recovered"), "owned prepared asset root");
            Binding[] bindings = prepared.script_bindings.bindings.Where(b => b.package == "com.unity.ugui" && b.full_name == typeof(UnityEngine.UI.Image).FullName).ToArray();
            Check(bindings.Length == 1, "one separate Image binding");
            Binding binding = bindings[0];
            Check(binding.exported_guid == "3cf5a44414476512e00c3e7a2569a919" && binding.exported_file_id == 11500000,
                  "immutable original Image identity");
            Check(binding.binary_sha256 == "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216" &&
                  binding.metadata_sha256 == "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b", "qualified original input");
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(binding.maintained_path);
            Check(script != null && script.GetClass() == typeof(UnityEngine.UI.Image), "actual Image MonoScript");
            CheckScript(script, binding);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(binding.maintained_path);
            Check(package != null && package.name == "com.unity.ugui" && package.version == "1.0.0" &&
                  package.source == UnityEditor.PackageManager.PackageSource.BuiltIn, "builtin package identity");
            string source = OwnedPath(root, Path.Combine(package.resolvedPath, binding.maintained_path.Substring("Packages/com.unity.ugui/".Length)));
            Check(Hash(File.ReadAllBytes(source)) == binding.source_sha256, "exact inventoried Image source");
            CheckEventDeclaration();

            var originalMap = JsonUtility.FromJson<OriginalMap>(File.ReadAllText(OwnedPath(cache, export.asset_map_path)));
            Check(originalMap != null && originalMap.assets != null && originalMap.project_path == export.project_path, "original asset-map identity");
            OriginalAsset[] originalScript = originalMap.assets.Where(a => a.path == "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/Image.cs").ToArray();
            Check(originalScript.Length == 1 && originalScript[0].guid == binding.exported_guid && originalScript[0].code_quarantined,
                  "original map contains the exact quarantined Image script");
            string[] originalPaths = originalMap.assets.Where(a => a.script_references != null && a.script_references.Contains(binding.exported_guid)).Select(a => a.path).ToArray();
            Check(originalPaths.Length > 0 && originalPaths.Distinct(StringComparer.Ordinal).Count() == originalPaths.Length, "unique original Image asset paths");
            Check(prepared.script_bindings.assets.Select(a => a.path).Distinct(StringComparer.Ordinal).Count() == prepared.script_bindings.assets.Length,
                  "unique prepared asset paths");
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            int documents = 0;
            var samples = new Dictionary<string, Sample>(StringComparer.Ordinal);
            foreach (AssetRow asset in prepared.script_bindings.assets)
            {
                string originalPath = OwnedPath(cache, Path.Combine(export.project_path, asset.path));
                byte[] original = File.ReadAllBytes(originalPath);
                Check(Hash(original) == asset.exported_sha256, "unchanged original asset");
                Check(asset.path.StartsWith("Assets/", StringComparison.Ordinal), "original asset prefix");
                string generatedPath = OwnedPath(prepared.recovered_path, Path.Combine(prepared.recovered_path, asset.path.Substring(7)));
                byte[] generated = File.ReadAllBytes(generatedPath);
                Check(Hash(generated) == asset.prepared_sha256, "qualified prepared asset");
                string text = Encoding.UTF8.GetString(original), mapped = Encoding.UTF8.GetString(generated);
                var mappedDocuments = Documents(mapped).ToDictionary(d => d.Item1, d => d.Item2, StringComparer.Ordinal);
                int selectedDocuments = 0;
                foreach (var document in Documents(text))
                {
                    if (!document.Item2.Contains("  m_Script: " + ScriptPointer(binding.exported_file_id, binding.exported_guid) + "\n")) continue;
                    Dictionary<string, string> fields = Fields(document.Item2);
                    string[] orderedNames = Regex.Matches(document.Item2, @"^  ([A-Za-z_][A-Za-z_0-9]*):", RegexOptions.Multiline)
                        .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
                    Check(orderedNames.SequenceEqual(CommonFields.Concat(OwnFields)), "all 26 authored fields in order");
                    string expected = document.Item2.Replace(ScriptPointer(binding.exported_file_id, binding.exported_guid),
                                                            ScriptPointer(binding.target_file_id, binding.target_guid));
                    Check(mappedDocuments.ContainsKey(document.Item1) && mappedDocuments[document.Item1] == expected, "script-only prepared Image document");
                    CheckStableAuthoredValues(fields);
                    string key = String.Join("\n", new[] { fields["m_Enabled"] }.Concat(OwnFields.Select(n => n + ":" + fields[n])));
                    if (!samples.ContainsKey(key)) samples.Add(key, new Sample { Document = document.Item2, Fields = fields });
                    ++documents;
                    ++selectedDocuments;
                }
                int mappedImages = mappedDocuments.Values.Count(d => d.Contains("  m_Script: " + ScriptPointer(binding.target_file_id, binding.target_guid) + "\n"));
                Check(mappedImages == selectedDocuments, "no added or missing prepared Image documents");
                if (selectedDocuments != 0) seenPaths.Add(asset.path);
            }
            Check(seenPaths.SetEquals(originalPaths), "every original Image-referencing asset represented exactly once");
            Check(documents == 11228 && samples.Count > 0, "all original Image documents represented");
            Sample[] ordered = samples.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToArray();
            Check(ordered.Any(s => s.Fields["m_Material"] != "{fileID: 0}") && ordered.Any(s => s.Fields["m_Enabled"] == "0"), "authored nonnull material and disabled coverage");
            for (int offset = 0; offset < ordered.Length; offset += 64)
                RunBatch(ordered.Skip(offset).Take(64).ToArray(), binding);
            Debug.Log("Authored Image binding: " + documents + " documents, " + ordered.Length + " distinct value/pointer vectors; inactive roundtrip only.");
            return documents;
        }

        private static void RunBatch(Sample[] samples, Binding binding)
        {
            var borrowed = new BorrowedUiState();
            var borrowedAssets = new BorrowedAssets();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                borrowedAssets.Capture(samples);
                RunOwnedFixture(state =>
                {
                    state.Create();
                    for (int i = 0; i < samples.Length; ++i) state.AddImage(i.ToString(CultureInfo.InvariantCulture));
                    state.Save();
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(state.AssetPath);
                    string yaml = File.ReadAllText(state.AssetPath);
                    for (int i = 0; i < samples.Length; ++i)
                    {
                        GameObject child = prefab.transform.GetChild(i).gameObject;
                        long id = LocalId(child.GetComponent<UnityEngine.UI.Image>()), owner = LocalId(child);
                        string replacement = Regex.Replace(samples[i].Document, @"\A--- !u!114 &-?[0-9]+", "--- !u!114 &" + id);
                        replacement = Regex.Replace(replacement, @"^  m_GameObject: \{fileID: -?[0-9]+\}$", "  m_GameObject: {fileID: " + owner + "}", RegexOptions.Multiline);
                        replacement = replacement.Replace(ScriptPointer(binding.exported_file_id, binding.exported_guid), ScriptPointer(binding.target_file_id, binding.target_guid));
                        string current = Documents(yaml).Single(d => d.Item1 == id.ToString(CultureInfo.InvariantCulture)).Item2;
                        int start = yaml.IndexOf(current, StringComparison.Ordinal);
                        yaml = yaml.Substring(0, start) + replacement + yaml.Substring(start + current.Length);
                    }
                    File.WriteAllText(state.AssetPath, yaml, new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(state.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    VerifyImported(state.AssetPath, samples, binding, borrowedAssets);
                    state.Load();
                    bool saved;
                    PrefabUtility.SaveAsPrefabAsset(state.Contents, state.AssetPath, out saved);
                    Check(saved, "save imported authored values");
                    state.Unload();
                    AssetDatabase.ImportAsset(state.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    VerifyImported(state.AssetPath, samples, binding, borrowedAssets);
                });
            }, borrowed.Verify, borrowedAssets.Verify);
        }

        private static void VerifyImported(string path, Sample[] samples, Binding binding, BorrowedAssets borrowedAssets)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var writtenDocuments = Documents(File.ReadAllText(path)).ToDictionary(d => d.Item1, d => Fields(d.Item2), StringComparer.Ordinal);
            Check(prefab != null && !prefab.activeSelf && prefab.transform.childCount == samples.Length, "inactive imported hierarchy");
            for (int i = 0; i < samples.Length; ++i)
            {
                GameObject child = prefab.transform.GetChild(i).gameObject;
                UnityEngine.UI.Image image = child.GetComponent<UnityEngine.UI.Image>();
                Check(!child.activeSelf && image != null && child.GetComponent<RectTransform>() != null &&
                      child.GetComponent<CanvasRenderer>() != null && child.GetComponents<Component>().Length == 3, "genuine required Image components only");
                var serialized = new SerializedObject(image);
                CheckScript((MonoScript)serialized.FindProperty("m_Script").objectReferenceValue, binding);
                Dictionary<string, string> fields = samples[i].Fields;
                Check(serialized.FindProperty("m_Enabled").boolValue == (fields["m_Enabled"] == "1"), "authored enabled state");
                Check(image.hideFlags == HideFlags.None, "authored object hide flags");
                Dictionary<string, string> written = writtenDocuments[LocalId(image).ToString(CultureInfo.InvariantCulture)];
                foreach (string name in new[] { "m_ObjectHideFlags", "m_EditorHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
                                               "m_PrefabAsset", "m_Name", "m_EditorClassIdentifier" })
                    Check(written[name] == fields[name], "authored common field " + name);
                Check(written["m_GameObject"] == "{fileID: " + LocalId(child).ToString(CultureInfo.InvariantCulture) + "}", "owned remapped component owner");
                foreach (string name in OwnFields)
                {
                    SerializedProperty property = serialized.FindProperty(name);
                    Check(property != null, "authored field exists " + name);
                    if (name == "m_OnCullStateChanged")
                    {
                        Check(image.onCullStateChanged != null && image.onCullStateChanged.GetPersistentEventCount() == 0 &&
                              serialized.FindProperty("m_OnCullStateChanged.m_PersistentCalls.m_Calls").arraySize == 0, "empty persistent event survives");
                    }
                    else if (name == "m_Material" || name == "m_Sprite")
                    {
                        CheckAssetPointer(property.objectReferenceValue, fields[name], name);
                        borrowedAssets.CheckReference(property.objectReferenceValue, fields[name], name);
                    }
                    else if (name == "m_Color")
                    {
                        float[] values = Components(fields[name]);
                        Color actual = property.colorValue;
                        Check(actual.r == values[0] && actual.g == values[1] && actual.b == values[2] && actual.a == values[3], "authored Color");
                    }
                    else if (name == "m_RaycastPadding")
                    {
                        float[] values = Components(fields[name]);
                        Vector4 actual = property.vector4Value;
                        Check(actual.x == values[0] && actual.y == values[1] && actual.z == values[2] && actual.w == values[3], "authored padding");
                    }
                    else if (property.propertyType == SerializedPropertyType.Float)
                        Check(property.floatValue == Single.Parse(fields[name], CultureInfo.InvariantCulture), "authored float " + name);
                    else if (property.propertyType == SerializedPropertyType.Boolean)
                        Check(property.boolValue == (fields[name] == "1"), "authored Boolean " + name);
                    else
                        Check((property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum) &&
                              property.intValue == Int32.Parse(fields[name], CultureInfo.InvariantCulture), "authored integer/enum " + name);
                }
            }
        }

        private static void CheckStableAuthoredValues(Dictionary<string, string> fields)
        {
            int method = Int32.Parse(fields["m_FillMethod"], CultureInfo.InvariantCulture), origin = Int32.Parse(fields["m_FillOrigin"], CultureInfo.InvariantCulture);
            float amount = Single.Parse(fields["m_FillAmount"], CultureInfo.InvariantCulture), ppu = Single.Parse(fields["m_PixelsPerUnitMultiplier"], CultureInfo.InvariantCulture);
            // These are observed authored invariants, not restrictions on the byte-only parser.
            Check(origin >= 0 && origin <= (method <= 1 ? 1 : 3) && amount >= 0 && amount <= 1 && ppu >= .01f,
                  "authored values stable under original deserialize and Editor validation");
        }

        private sealed class BorrowedAssets
        {
            private readonly List<Action> checks = new List<Action>();
            private readonly Dictionary<string, Object> objects = new Dictionary<string, Object>(StringComparer.Ordinal);
            public void CheckReference(Object value, string authored, string field)
            {
                if (authored != "{fileID: 0}")
                    Check(ReferenceEquals(value, objects[field + ":" + authored]), "import retains captured borrowed object identity");
            }
            public void Capture(Sample[] samples)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                bool checkedBuiltinBytes = false;
                foreach (Sample sample in samples)
                foreach (string field in new[] { "m_Sprite", "m_Material" })
                {
                    string authored = sample.Fields[field];
                    if (authored == "{fileID: 0}" || !seen.Add(field + ":" + authored)) continue;
                    Match pointer = AssetPointer(authored, field);
                    string guid = pointer.Groups[2].Value;
                    long id = Int64.Parse(pointer.Groups[1].Value, CultureInfo.InvariantCulture);
                    bool builtin = pointer.Groups[3].Value == "0";
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (builtin)
                    {
                        if (String.IsNullOrEmpty(path)) path = BuiltinPath;
                        Check(path == BuiltinPath, "exact installed builtin resource path");
                    }
                    Check(!String.IsNullOrEmpty(path), "authored asset resolves before fixture construction");
                    Object[] matching = AssetDatabase.LoadAllAssetsAtPath(path).Where(value =>
                    {
                        string actualGuid; long actualId;
                        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out actualGuid, out actualId) && actualGuid == guid && actualId == id;
                    }).ToArray();
                    Check(matching.Length == 1, "one real borrowed authored subasset");
                    Object borrowed = matching[0];
                    Check(field == "m_Sprite" ? borrowed is Sprite : borrowed is Material, "actual authored field asset class");
                    objects.Add(field + ":" + authored, borrowed);
                    bool dirty = EditorUtility.IsDirty(borrowed);
                    checks.Add(() =>
                    {
                        Check(borrowed != null && EditorUtility.IsDirty(borrowed) == dirty, "borrowed asset remains alive and unchanged");
                        CheckAssetPointer(borrowed, authored, field);
                    });
                    if (builtin)
                    {
                        if (!checkedBuiltinBytes)
                        {
                            string resource = Path.Combine(EditorApplication.applicationContentsPath, "Resources/unity_builtin_extra");
                            Check(Hash(File.ReadAllBytes(resource)) == BuiltinResourceHash, "pinned installed builtin resource bytes");
                            checks.Add(() => Check(Hash(File.ReadAllBytes(resource)) == BuiltinResourceHash, "builtin resource bytes unchanged"));
                            checkedBuiltinBytes = true;
                        }
                        continue;
                    }
                    string root = Directory.GetParent(Application.dataPath).FullName;
                    string physical = OwnedPath(root, Path.Combine(root, path));
                    string bytes = Hash(File.ReadAllBytes(physical));
                    checks.Add(() => Check(Hash(File.ReadAllBytes(physical)) == bytes, "borrowed asset bytes unchanged"));
                    string meta = Hash(File.ReadAllBytes(physical + ".meta"));
                    checks.Add(() => Check(Hash(File.ReadAllBytes(physical + ".meta")) == meta, "borrowed metadata bytes unchanged"));
                }
            }
            public void Verify() { foreach (Action check in checks) check(); }
        }

        private sealed class EventField
        {
            public readonly string Name, TypeName, Constant;
            public readonly int Attributes;
            public readonly string[] CustomAttributes;
            public EventField(string name, int attributes, string typeName, string constant, string[] customAttributes)
            { Name = name; Attributes = attributes; TypeName = typeName; Constant = constant; CustomAttributes = customAttributes; }
        }

        public static void CheckEventDeclaration()
        {
            // Literal original metadata contracts, including nonserialized fields,
            // ordered CAD and enum constants. No generic admission rule is changed.
            // Original 0x02000057 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.UI", "UnityEngine.UI.MaskableGraphic+CullStateChangedEvent", 1056770, "UnityEngine.CoreModule:UnityEngine.Events.UnityEvent`1<mscorlib:System.Boolean>",
                Array.Empty<string>(), Array.Empty<EventField>());
            // Original 0x0200021d (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.UnityEvent`1", 1056769, "UnityEngine.CoreModule:UnityEngine.Events.UnityEventBase",
                Array.Empty<string>(), new EventField[] {
                    new EventField("m_InvokeArray", 1, "mscorlib:System.Object[]", null, Array.Empty<string>())
                });
            // Original 0x02000219 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.UnityEventBase", 1056897, "mscorlib:System.Object",
                new string[] { "UnityEngine.CoreModule:UnityEngine.Scripting.UsedByNativeCodeAttribute()" }, new EventField[] {
                    new EventField("m_Calls", 1, "UnityEngine.CoreModule:UnityEngine.Events.InvokableCallList", null, Array.Empty<string>()),
                    new EventField("m_PersistentCalls", 1, "UnityEngine.CoreModule:UnityEngine.Events.PersistentCallGroup", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:m_PersistentListeners)" }),
                    new EventField("m_CallsDirty", 1, "mscorlib:System.Boolean", null, Array.Empty<string>())
                });
            // Original 0x02000217 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.PersistentCallGroup", 1056768, "mscorlib:System.Object",
                Array.Empty<string>(), new EventField[] {
                    new EventField("m_Calls", 1, "mscorlib:System.Collections.Generic.List`1<UnityEngine.CoreModule:UnityEngine.Events.PersistentCall>", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:m_Listeners)" })
                });
            // Original 0x02000216 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.PersistentCall", 1056768, "mscorlib:System.Object",
                Array.Empty<string>(), new EventField[] {
                    new EventField("m_Target", 1, "UnityEngine.CoreModule:UnityEngine.Object", null, new string[] { "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:instance)", "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_TargetAssemblyTypeName", 1, "mscorlib:System.String", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_MethodName", 1, "mscorlib:System.String", null, new string[] { "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:methodName)", "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_Mode", 1, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", null, new string[] { "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:mode)", "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_Arguments", 1, "UnityEngine.CoreModule:UnityEngine.Events.ArgumentCache", null, new string[] { "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:arguments)", "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_CallState", 1, "UnityEngine.CoreModule:UnityEngine.Events.UnityEventCallState", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:m_Enabled)", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:enabled)" })
                });
            // Original 0x0200020d (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.ArgumentCache", 1056768, "mscorlib:System.Object",
                Array.Empty<string>(), new EventField[] {
                    new EventField("m_ObjectArgument", 1, "UnityEngine.CoreModule:UnityEngine.Object", null, new string[] { "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:objectArgument)", "UnityEngine.CoreModule:UnityEngine.SerializeField()" }),
                    new EventField("m_ObjectArgumentAssemblyTypeName", 1, "mscorlib:System.String", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:objectArgumentAssemblyTypeName)" }),
                    new EventField("m_IntArgument", 1, "mscorlib:System.Int32", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:intArgument)" }),
                    new EventField("m_FloatArgument", 1, "mscorlib:System.Single", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:floatArgument)" }),
                    new EventField("m_StringArgument", 1, "mscorlib:System.String", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()", "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute(System.String:stringArgument)" }),
                    new EventField("m_BoolArgument", 1, "mscorlib:System.Boolean", null, new string[] { "UnityEngine.CoreModule:UnityEngine.SerializeField()" })
                });
            // Original 0x02000218 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.InvokableCallList", 1048576, "mscorlib:System.Object",
                Array.Empty<string>(), new EventField[] {
                    new EventField("m_PersistentCalls", 33, "mscorlib:System.Collections.Generic.List`1<UnityEngine.CoreModule:UnityEngine.Events.BaseInvokableCall>", null, Array.Empty<string>()),
                    new EventField("m_RuntimeCalls", 33, "mscorlib:System.Collections.Generic.List`1<UnityEngine.CoreModule:UnityEngine.Events.BaseInvokableCall>", null, Array.Empty<string>()),
                    new EventField("m_ExecutingCalls", 1, "mscorlib:System.Collections.Generic.List`1<UnityEngine.CoreModule:UnityEngine.Events.BaseInvokableCall>", null, Array.Empty<string>()),
                    new EventField("m_NeedsUpdate", 1, "mscorlib:System.Boolean", null, Array.Empty<string>())
                });
            // Original 0x0200020b (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.PersistentListenerMode", 8449, "mscorlib:System.Enum",
                Array.Empty<string>(), new EventField[] {
                    new EventField("value__", 1542, "mscorlib:System.Int32", null, Array.Empty<string>()),
                    new EventField("EventDefined", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "0", Array.Empty<string>()),
                    new EventField("Void", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "1", Array.Empty<string>()),
                    new EventField("Object", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "2", Array.Empty<string>()),
                    new EventField("Int", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "3", Array.Empty<string>()),
                    new EventField("Float", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "4", Array.Empty<string>()),
                    new EventField("String", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "5", Array.Empty<string>()),
                    new EventField("Bool", 32854, "UnityEngine.CoreModule:UnityEngine.Events.PersistentListenerMode", "6", Array.Empty<string>())
                });
            // Original 0x02000215 (physical loaded tokens may differ).
            CheckEventType("UnityEngine.CoreModule", "UnityEngine.Events.UnityEventCallState", 257, "mscorlib:System.Enum",
                Array.Empty<string>(), new EventField[] {
                    new EventField("value__", 1542, "mscorlib:System.Int32", null, Array.Empty<string>()),
                    new EventField("Off", 32854, "UnityEngine.CoreModule:UnityEngine.Events.UnityEventCallState", "0", Array.Empty<string>()),
                    new EventField("EditorAndRuntime", 32854, "UnityEngine.CoreModule:UnityEngine.Events.UnityEventCallState", "1", Array.Empty<string>()),
                    new EventField("RuntimeOnly", 32854, "UnityEngine.CoreModule:UnityEngine.Events.UnityEventCallState", "2", Array.Empty<string>())
                });
            Type generic = typeof(UnityEngine.Events.UnityEvent<>);
            Type parameter = generic.GetGenericArguments().Single();
            Check(parameter.Name == "T0" && parameter.GenericParameterAttributes == GenericParameterAttributes.None &&
                  parameter.GetGenericParameterConstraints().Length == 0, "original event generic parameter/constraints");
            Type group = typeof(UnityEngine.Events.UnityEventBase).Assembly.GetType("UnityEngine.Events.PersistentCallGroup", true);
            Type list = group.GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
            Check(list.GetGenericTypeDefinition() == typeof(List<>), "actual closed persistent-call list definition");
        }

        private static void CheckEventType(string assembly, string name, int attributes, string parent, string[] customAttributes, EventField[] expected)
        {
            Assembly module = assembly == "UnityEngine.UI" ? typeof(UnityEngine.UI.Image).Assembly : typeof(UnityEngine.Events.UnityEventBase).Assembly;
            Check(module.GetName().Name == assembly, "event declaration assembly");
            Type type = module.GetType(name, true);
            Check((int)type.Attributes == attributes && DeclarationName(type.BaseType) == parent, "original event flags/ancestry " + name);
            Check(Attributes(CustomAttributeData.GetCustomAttributes(type)).SequenceEqual(customAttributes), "original ordered event type CAD " + name);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .OrderBy(f => f.MetadataToken).ToArray();
            Check(fields.Length == expected.Length, "complete original event fields " + name);
            for (int i = 0; i < fields.Length; ++i)
            {
                FieldInfo field = fields[i]; EventField original = expected[i];
                Check(field.Name == original.Name && (int)field.Attributes == original.Attributes && DeclarationName(field.FieldType) == original.TypeName,
                      "original ordered event field " + name + "." + original.Name);
                bool constant = (field.Attributes & FieldAttributes.HasDefault) != 0;
                Check(constant == (original.Constant != null) && (!constant || Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture) == original.Constant),
                      "original event field constant " + name + "." + original.Name);
                CheckEventAttributeOrder(name, original.Name, original.CustomAttributes,
                                         Attributes(CustomAttributeData.GetCustomAttributes(field)).ToArray());
            }
        }

        public static void CheckEventAttributeOrder(string type, string field, string[] original, string[] loaded)
        {
            // Original and pinned Editor retain the same CAD values but these eight
            // exact orders differ; only current-name, empty-call payloads are admitted.
            int[] permutation = null;
            switch (type + "." + field)
            {
                case "UnityEngine.Events.ArgumentCache.m_ObjectArgument":
                case "UnityEngine.Events.ArgumentCache.m_IntArgument":
                case "UnityEngine.Events.ArgumentCache.m_FloatArgument":
                case "UnityEngine.Events.PersistentCall.m_Target":
                case "UnityEngine.Events.PersistentCall.m_Arguments":
                case "UnityEngine.Events.PersistentCallGroup.m_Calls":
                case "UnityEngine.Events.UnityEventBase.m_PersistentCalls":
                    permutation = new[] { 1, 0 };
                    break;
                case "UnityEngine.Events.PersistentCall.m_CallState":
                    permutation = new[] { 2, 1, 0 };
                    break;
            }
            if (permutation == null)
                Check(loaded.SequenceEqual(original), "exact original CAD order on unlisted field " + type + "." + field);
            else
            {
                Check(original.Length == permutation.Length, "complete original CAD permutation domain");
                Check(loaded.SequenceEqual(permutation.Select(index => original[index])), "exact pinned Editor CAD order " + type + "." + field);
            }
        }

        private static IEnumerable<string> Attributes(IList<CustomAttributeData> attributes)
        {
            foreach (CustomAttributeData attribute in attributes)
            {
                // Reflection synthesizes these from flags; the flags are checked above.
                if (attribute.AttributeType == typeof(SerializableAttribute) || attribute.AttributeType == typeof(NonSerializedAttribute)) continue;
                Check(attribute.NamedArguments.Count == 0, "original event CAD has no named arguments");
                yield return attribute.AttributeType.Assembly.GetName().Name + ":" + attribute.AttributeType.FullName + "(" + String.Join(",", attribute.ConstructorArguments.Select(a => a.ArgumentType.FullName + ":" + a.Value)) + ")";
            }
        }

        private static string DeclarationName(Type type)
        {
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                string rank = type == element.MakeArrayType() ? "[]" : type.GetArrayRank() == 1 ? "[*]" : "[" + new string(',', type.GetArrayRank() - 1) + "]";
                return DeclarationName(element) + rank;
            }
            string prefix = type.Assembly.GetName().Name + ":";
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                return prefix + type.GetGenericTypeDefinition().FullName + "<" + String.Join(",", type.GetGenericArguments().Select(DeclarationName)) + ">";
            return prefix + type.FullName;
        }

        public static Action CaptureBorrowedCollection(IDictionary collection)
        {
            var state = new BorrowedUiState(false);
            state.Capture(() => collection, "test-owned collection");
            return state.Verify;
        }

        // Read-only snapshots: never replace containers, remove foreign rows, or write private events.
        private sealed class BorrowedUiState
        {
            private readonly List<Action> checks = new List<Action>();
            private readonly HashSet<object> visited = new HashSet<object>(ReferenceIdentity.Instance);
            public BorrowedUiState(bool captureRegistries = true)
            {
                if (!captureRegistries) return;
                CaptureStatic(typeof(UnityEngine.UI.GraphicRegistry), "s_Instance");
                CaptureStatic(typeof(UnityEngine.UI.CanvasUpdateRegistry), "s_Instance");
                CaptureStatic(typeof(UnityEngine.UI.GraphicRebuildTracker), "m_Tracked");
                CaptureStatic(typeof(UnityEngine.UI.GraphicRebuildTracker), "s_Initialized");
                CaptureStatic(typeof(UnityEngine.UI.Image), "m_TrackedTexturelessImages");
                CaptureStatic(typeof(UnityEngine.UI.Image), "s_Initialized");
            }
            private void CaptureStatic(Type type, string name)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                Check(field != null, "known registry field " + name);
                Capture(() => field.GetValue(null), type.Name + "." + name);
            }
            public void Capture(Func<object> read, string label)
            {
                object value = read();
                checks.Add(() => Check(Same(value, read()), "borrowed identity/value " + label));
                if (value == null || value.GetType().IsValueType || value is string || !visited.Add(value)) return;
                var dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    DictionaryEntry[] rows = Entries(dictionary);
                    checks.Add(() =>
                    {
                        DictionaryEntry[] actual = Entries(dictionary);
                        Check(rows.Length == actual.Length && rows.Zip(actual, (a, b) => Same(a.Key, b.Key) && Same(a.Value, b.Value)).All(equal => equal), "borrowed dictionary rows " + label);
                    });
                    foreach (DictionaryEntry row in rows) Capture(() => dictionary[row.Key], label + " value");
                }
                else if (value is IList)
                {
                    var list = (IList)value;
                    object[] rows = list.Cast<object>().ToArray();
                    checks.Add(() => Check(rows.Length == list.Count && rows.Zip(list.Cast<object>(), Same).All(equal => equal), "borrowed list entries " + label));
                }
                else if (value.GetType().Namespace != null && value.GetType().Namespace.StartsWith("UnityEngine.UI", StringComparison.Ordinal))
                {
                    foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        Capture(() => field.GetValue(value), label + "." + field.Name);
                }
            }
            private static DictionaryEntry[] Entries(IDictionary dictionary)
            {
                var rows = new List<DictionaryEntry>();
                foreach (DictionaryEntry row in dictionary) rows.Add(row);
                return rows.ToArray();
            }
            public void Verify() { foreach (Action check in checks) check(); }
            private static bool Same(object a, object b) => a == null ? b == null : a.GetType().IsValueType || a is string ? a.Equals(b) : ReferenceEquals(a, b);
        }
        private sealed class ReferenceIdentity : IEqualityComparer<object>
        {
            public static readonly ReferenceIdentity Instance = new ReferenceIdentity();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        private static IEnumerable<Tuple<string, string>> Documents(string text)
        {
            MatchCollection headers = Header.Matches(text);
            for (int i = 0; i < headers.Count; ++i)
            {
                Match header = headers[i];
                if (header.Groups[1].Value != "114") continue;
                int end = i + 1 == headers.Count ? text.Length : headers[i + 1].Index;
                yield return Tuple.Create(header.Groups[2].Value, text.Substring(header.Index, end - header.Index));
            }
        }
        private static Dictionary<string, string> Fields(string document)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(document, @"^  ([A-Za-z_][A-Za-z_0-9]*): ?(.*)$", RegexOptions.Multiline))
                fields.Add(match.Groups[1].Value, match.Groups[2].Value.TrimEnd('\r'));
            Check(document.Contains("  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"), "original empty persistent call shape");
            return fields;
        }
        private static float[] Components(string value) => value.Trim('{', '}').Split(',').Select(part => Single.Parse(part.Substring(part.IndexOf(':') + 1), CultureInfo.InvariantCulture)).ToArray();
        private static Match AssetPointer(string authored, string field)
        {
            Match pointer = Pointer.Match(authored);
            Check(pointer.Success, "authored asset pointer syntax");
            long id = Int64.Parse(pointer.Groups[1].Value, CultureInfo.InvariantCulture);
            if (pointer.Groups[3].Value == "0")
                Check(pointer.Groups[2].Value == BuiltinGuid &&
                      (field == "m_Sprite" ? new long[] { 10907, 10911, 10913, 10917 }.Contains(id) : field == "m_Material" && id == 10754),
                      "exact authored builtin field/local-ID tuple");
            else
                Check(pointer.Groups[2].Value != BuiltinGuid && pointer.Groups[2].Value != "0000000000000000e000000000000000" &&
                      pointer.Groups[2].Value != "00000000000000000000000000000000", "builtin identity retains original pointer kind");
            return pointer;
        }
        private static void CheckAssetPointer(Object value, string authored, string field)
        {
            if (authored == "{fileID: 0}") { Check(value == null, "authored null asset pointer"); return; }
            Match pointer = AssetPointer(authored, field);
            Check(value != null && (field == "m_Sprite" ? value is Sprite : value is Material), "actual nonnull authored asset class");
            string guid; long id;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id) && guid == pointer.Groups[2].Value &&
                  id == Int64.Parse(pointer.Groups[1].Value, CultureInfo.InvariantCulture), "exact authored asset GUID/local ID");
        }
        private static void CheckScript(MonoScript script, Binding binding)
        {
            string guid; long id;
            Check(script != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out id) &&
                  guid == binding.target_guid && id == binding.target_file_id, "loaded MonoScript identity");
        }
        private static long LocalId(Object value)
        {
            string guid; long id;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id) && id != 0, "actual fixture local ID");
            return id;
        }
        private static string ScriptPointer(long id, string guid) => "{fileID: " + id.ToString(CultureInfo.InvariantCulture) + ", guid: " + guid + ", type: 3}";
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string OwnedPath(string owner, string value)
        {
            string path = Path.GetFullPath(value), root = Path.GetFullPath(owner);
            Check(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "owned evidence path");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
                if (File.Exists(part) || Directory.Exists(part)) Check((File.GetAttributes(part) & FileAttributes.ReparsePoint) == 0, "no symlinked evidence");
            return path;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Authored Image verification failed: " + message);
        }
    }
}
#endif
