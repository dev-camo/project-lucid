#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Corpus = ProjectLucid.TransformRotatorAuthoredCorpus;

namespace ProjectLucid
{
    public static class TransformRotatorBindingVerification
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static FieldInfo ConfigField => typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", PrivateStatic);
        private static IDictionary Registry => (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", PrivateStatic).GetValue(null);
        private static object Read(object value, string name)
        {
            for (Type t = value.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(value);
            }
            throw new MissingFieldException(value.GetType().FullName, name);
        }
        private static string TimeName => typeof(TimeManager).FullName;

        public static string ScriptGuid()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(Corpus.Source);
            Corpus.Require(script != null && script.GetClass() == typeof(TransformRotator), "genuine maintained MonoScript");
            string guid; long id;
            Corpus.Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out id) && id == 11500000 && guid != Corpus.OriginalGuid, "maintained script retains fresh GUID");
            return guid;
        }
        public static void CheckBinding() => Corpus.RequireBinding(Corpus.Read(), ScriptGuid());

        public sealed class BorrowedAssets
        {
            private readonly Dictionary<string, Object> assets = new Dictionary<string, Object>();
            private readonly List<Action> checks = new List<Action>();
            public T Get<T>(string guid) where T : Object
            {
                if (assets.TryGetValue(guid, out Object existing))
                { Corpus.Require(existing is T, "cached prepared asset has exact requested class"); return (T)existing; }
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Corpus.Require(!String.IsNullOrEmpty(path), "prepared asset GUID resolves " + guid);
                Object[] matches = AssetDatabase.LoadAllAssetsAtPath(path).Where(o => Identity(o, guid)).ToArray();
                Corpus.Require(matches.Length == 1 && matches[0] is T, "one exact prepared asset class/local ID " + guid);
                T value = (T)matches[0]; assets.Add(guid, value);
                string physical = Path.GetFullPath(path), bytes = Corpus.Hash(File.ReadAllBytes(physical)), meta = Corpus.Hash(File.ReadAllBytes(physical + ".meta"));
                bool dirty = EditorUtility.IsDirty(value);
                checks.Add(() => Corpus.Require(value != null && Identity(value, guid) && ReferenceEquals(AssetDatabase.LoadAssetAtPath<T>(path), value) &&
                    EditorUtility.IsDirty(value) == dirty && Corpus.Hash(File.ReadAllBytes(physical)) == bytes &&
                    Corpus.Hash(File.ReadAllBytes(physical + ".meta")) == meta, "borrowed asset identity/bytes/dirty preserved " + guid));
                return value;
            }
            public void Verify() => VerificationFixtureOwnership.RunThenCleanUp(() => { }, checks.ToArray());
        }
        public static bool Identity(Object value, string guid)
        {
            string actual; long id;
            return value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out actual, out id) && actual == guid && id == 11400000;
        }
        public static void RequireCacheEntry(object entry, TimeCategoryLookup lookup)
        {
            Corpus.Require(lookup != null && Identity(lookup, Corpus.LookupGuid), "genuine authored lookup identity");
            Corpus.Require(ReferenceEquals(entry, null) || ReferenceEquals(entry, lookup), "cache entry is null or exact live authored asset");
        }

        // Reads the private configuration data graph without getters or repair.
        // Cache values and dirty flags are mutable even when container identities stay equal.
        public static Action CaptureConfiguration(SystemConfiguration system)
        {
            var checks = new List<Action>();
            var seen = new HashSet<object>(ReferenceComparer.Instance);
            Action<object> visit = null;
            visit = value =>
            {
                if (value == null || value is Object || value is string || value is Delegate || value.GetType().IsValueType || !seen.Add(value)) return;
                if (value is IDictionary map)
                {
                    var rows = new List<DictionaryEntry>(); foreach (DictionaryEntry row in map) rows.Add(row);
                    checks.Add(() => { var current = new List<DictionaryEntry>(); foreach (DictionaryEntry row in map) current.Add(row);
                        Corpus.Require(current.Count == rows.Count && current.Zip(rows, (a, b) => Same(a.Key, b.Key) && Same(a.Value, b.Value)).All(v => v), "borrowed configuration dictionary identity/order"); });
                    foreach (DictionaryEntry row in rows) visit(row.Value);
                }
                else if (value is IList list)
                {
                    object[] rows = list.Cast<object>().ToArray();
                    checks.Add(() => Corpus.Require(list.Count == rows.Length && list.Cast<object>().Zip(rows, Same).All(v => v), "borrowed configuration list contents"));
                    foreach (object row in rows) visit(row);
                }
                else
                    for (Type t = value.GetType(); t != null && t != typeof(object); t = t.BaseType)
                        foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            object prior = f.GetValue(value);
                            checks.Add(() => Corpus.Require(Same(f.GetValue(value), prior), "borrowed configuration field " + f.Name));
                            visit(prior);
                        }
            };
            visit(system);
            return () => VerificationFixtureOwnership.RunThenCleanUp(() => { }, checks.ToArray());
        }
        private static bool Same(object a, object b) => ReferenceEquals(a, b) || (a != null && b != null && a.GetType().IsValueType && a.Equals(b));
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        public sealed class OwnedConfiguration : IDisposable
        {
            public readonly SystemConfiguration System = new SystemConfiguration();
            private VerificationFixtureOwnership.RegistryRow row;
            private object registeredRow;
            private readonly List<StackableDataHandle> handles = new List<StackableDataHandle>();
            public void Register()
            {
                Corpus.Require(row == null && registeredRow == null, "configuration lease registers only once");
                row = new VerificationFixtureOwnership.RegistryRow(typeof(SystemConfiguration).FullName);
                try { row.RegisterUsing(System, () => ProcessManager.RegisterSystem(System)); }
                finally
                {
                    object actual = Registry[typeof(SystemConfiguration).FullName];
                    if (actual != null && ReferenceEquals(((SystemRef)Read(actual, "SystemRef")).GetSafe(), System)) registeredRow = actual;
                }
            }
            public void Add(TimeCategoryLookup lookup)
            {
                object actual = Registry[typeof(SystemConfiguration).FullName];
                Corpus.Require(row != null && registeredRow != null && ReferenceEquals(actual, registeredRow) &&
                    ReferenceEquals(((SystemRef)Read(actual, "SystemRef")).GetSafe(), System), "configuration add requires its exact owned live registration");
                var dictionary = (IDictionary)Read(System.StackableData, "m_dataDictionary");
                object[] before = dictionary.Keys.Cast<object>().ToArray();
                try { SystemConfiguration.AddConfig<SystemConfigurationAsset>(lookup); }
                finally
                {
                    // AddOverride publishes before notifying observers and returning its handle.
                    foreach (object key in dictionary.Keys)
                        if (!before.Any(old => ReferenceEquals(old, key))) handles.Add((StackableDataHandle)key);
                }
            }
            public int OwnedHandleCount => handles.Count(h => ((IDictionary)Read(System.StackableData, "m_dataDictionary")).Contains(h));
            public void Release(Action<string> after = null)
            {
                var steps = handles.Select<StackableDataHandle, Action>(h => () => System.StackableData.RemoveOverrides(h)).ToList();
                steps.Add(() => { Corpus.Require(OwnedHandleCount == 0, "owned overrides removed even after observer failure"); after?.Invoke("overrides"); });
                steps.Add(() => { row?.Dispose(); after?.Invoke("configuration"); });
                VerificationFixtureOwnership.RunThenCleanUp(() => { }, steps.ToArray());
            }
            public void Dispose() => Release();
        }

        private sealed class EnvironmentLease
        {
            public readonly BorrowedAssets Assets = new BorrowedAssets();
            public TimeCategoryLookup Lookup;
            private object entry;
            private OwnedConfiguration owned;
            private Action borrowedConfiguration = () => { };
            private Action lookupContents;
            private ProjectLucid.Tests.SharedStateSnapshot before;
            private bool rowExisted, typedExisted, componentWork;
            private object priorRow, priorTyped;
            private UnityEngine.Random.State random;
            public void Acquire()
            {
                before = ProjectLucid.Tests.SharedStateSnapshot.Capture();
                random = UnityEngine.Random.state;
                rowExisted = Registry.Contains(TimeName);
                if (rowExisted)
                {
                    priorRow = Registry[TimeName];
                    Corpus.Require(((SystemRef)Read(priorRow, "SystemRef")).IsNull(), "inactive fixture requires no live TimeManager");
                    IDictionary typed = (IDictionary)Read(priorRow, "SystemRefDictionary");
                    typedExisted = typed.Contains(TimeName); priorTyped = typed[TimeName];
                }
                string configurationName = typeof(SystemConfiguration).FullName;
                if (Registry.Contains(configurationName))
                {
                    var system = ((SystemRef)Read(Registry[configurationName], "SystemRef")).GetSafe() as SystemConfiguration;
                    if (system != null) borrowedConfiguration = CaptureConfiguration(system);
                }
                Lookup = Assets.Get<TimeCategoryLookup>(Corpus.LookupGuid);
                lookupContents = UguiImageBindingVerification.CaptureBorrowedCollection(Lookup.Dictionary);
                entry = ConfigField.GetValue(null);
                RequireCacheEntry(entry, Lookup);
                if (ReferenceEquals(entry, null))
                {
                    owned = new OwnedConfiguration(); owned.Register(); owned.Add(Lookup);
                }
            }
            public void ComponentWorkBegins() { componentWork = true; }
            public void VerifyCache(bool completed = false)
            {
                object actual = ConfigField.GetValue(null);
                Corpus.Require(completed ? ReferenceEquals(actual, Lookup) : ReferenceEquals(actual, entry) || (componentWork && ReferenceEquals(actual, Lookup)),
                    "only original cache transition retained without static repair");
            }
            public void ReleaseTemporaryConfiguration()
            {
                if (owned != null)
                {
                    owned.Dispose();
                    Corpus.Require(!Registry.Contains(typeof(SystemConfiguration).FullName), "temporary configuration is absent before the second callback");
                }
                VerifyCache(true);
                Debug.Log("Authored TransformRotator cache entry observed: " +
                    (ReferenceEquals(entry, null) ? "true null; original assignment retained after owned configuration teardown" : "matching authored lookup borrowed unchanged"));
            }
            public void Release()
            {
                VerificationFixtureOwnership.RunThenCleanUp(() => { },
                    () => owned?.Dispose(), borrowedConfiguration, () => lookupContents?.Invoke(), Assets.Verify,
                    () => { if (Lookup != null) VerifyCache(); },
                    () => Corpus.Require(UnityEngine.Random.state.Equals(random), "inactive lifecycle draws no random values"), VerifyReference);
            }
            private void VerifyReference()
            {
                if (before == null) return;
                var changes = ProjectLucid.Tests.SharedStateSnapshot.Capture().Differences(before);
                if (!componentWork) { Corpus.Require(changes.Count == 0, "failed acquisition leaves shared state unchanged"); return; }
                // A failed import may not have constructed a component. Inspect
                // the registry first so observing failure cannot initialize it.
                if (!Registry.Contains(TimeName) || !((IDictionary)Read(Registry[TimeName], "SystemRefDictionary")).Contains(TimeName))
                { Corpus.Require(changes.Count == 0, "failed import leaves shared state unchanged"); return; }
                // Original 06000ec7@1B19FF0 retains GetSystemRef(null,true).
                // Observe only after real component work; never force or undo initialization.
                object reference = typeof(TimeScaledComponent).GetField("m_timeManagerRef", PrivateStatic).GetValue(null);
                Corpus.Require(Registry.Contains(TimeName), "permanent TimeManager row retained");
                object row = Registry[TimeName]; IDictionary typed = (IDictionary)Read(row, "SystemRefDictionary");
                Corpus.Require(ReferenceEquals(reference, typed[TimeName]), "readonly reference remains attached to real registry");
                if (rowExisted) Corpus.Require(ReferenceEquals(priorRow, row), "foreign row identity preserved");
                if (typedExisted) Corpus.Require(ReferenceEquals(priorTyped, reference), "foreign typed reference preserved");
                var expected = new List<string>();
                string key = "system row '" + TimeName + "'", tr = key + " typed reference '" + TimeName + "'";
                if (!rowExisted)
                {
                    object untyped = Read(row, "SystemRef");
                    foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                        Corpus.Require(Read(untyped, name) == null, "new untyped reference has no system/observer/deferred work");
                    expected.Add("changed system row order");
                    expected.Add("added " + key);
                    foreach (string suffix in new[] { "", " OnSystemStartup observers", " OnSystemShutdown observers", " deferred InvokeOnValid work" })
                        expected.Add("added " + key + " untyped reference" + suffix);
                }
                else if (!typedExisted) expected.Add("changed " + key + " typed reference names");
                if (!typedExisted)
                {
                    foreach (string suffix in new[] { "", " OnSystemStartup observers", " OnSystemShutdown observers", " deferred InvokeOnValid work" }) expected.Add("added " + tr + suffix);
                    if (!rowExisted) expected.Add("added " + key + " typed reference names");
                    foreach (string name in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                        Corpus.Require(Read(reference, name) == null, "new permanent reference has no system/observer/deferred work");
                }
                Corpus.Require(changes.SequenceEqual(expected), "only exact original first-reference transition: " + String.Join(";", changes));
            }
        }

        public sealed class OwnedPrefab : IDisposable
        {
            public string Folder { get; } = "Assets/LucidRotatorProof_" + Guid.NewGuid().ToString("N");
            public string Path => Folder + "/rotators.prefab";
            public GameObject Root { get; private set; }
            public GameObject Contents { get; private set; }
            private readonly List<GameObject> objects = new List<GameObject>();
            private bool claimed;
            public void Create()
            {
                Corpus.Require(!Directory.Exists(Folder) && !File.Exists(Folder + ".meta"), "absent fixture folder");
                claimed = true;
                Corpus.Require(!String.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(Folder))), "owned folder creation");
                Root = new GameObject("Inactive authored rotators"); Root.SetActive(false);
            }
            public GameObject Add(string name)
            {
                var child = new GameObject(name); objects.Add(child);
                child.SetActive(false); child.transform.SetParent(Root.transform, false); return child;
            }
            public void Save()
            { bool saved; PrefabUtility.SaveAsPrefabAsset(Root, Path, out saved); Corpus.Require(saved, "save owned skeleton"); }
            public void Load() { Contents = PrefabUtility.LoadPrefabContents(Path); Corpus.Require(Contents != null, "load actual prefab contents"); }
            public void SaveContents()
            { bool saved; PrefabUtility.SaveAsPrefabAsset(Contents, Path, out saved); Corpus.Require(saved, "save imported values"); }
            public void Unload() { if (Contents != null) { PrefabUtility.UnloadPrefabContents(Contents); Contents = null; } }
            public void Release(Action<string> after = null)
            {
                VerificationFixtureOwnership.RunThenCleanUp(() => { },
                    () => { var releases = objects.Select<GameObject, Action>(o => () => { if (o != null) Object.DestroyImmediate(o); }).ToList();
                        releases.Add(() => { if (Root != null) Object.DestroyImmediate(Root); });
                        VerificationFixtureOwnership.RunThenCleanUp(() => { }, releases.ToArray()); after?.Invoke("construction"); },
                    () => { Unload(); after?.Invoke("contents"); },
                    () => { if (claimed && (Directory.Exists(Folder) || AssetDatabase.IsValidFolder(Folder))) Corpus.Require(AssetDatabase.DeleteAsset(Folder), "remove owned folder");
                        Corpus.Require(!claimed || (!Directory.Exists(Folder) && !File.Exists(Folder + ".meta")), "owned folder fully released"); after?.Invoke("folder"); });
            }
            public void Dispose() => Release();
        }

        public static int RunAuthored()
        {
            Corpus.Require(Application.unityVersion == "2022.3.54f1", "qualified Editor version");
            Corpus.Corpus corpus = Corpus.Read();
            var environment = new EnvironmentLease();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                environment.Acquire();
                string guid = ScriptGuid();
                foreach (Corpus.Sample sample in corpus.Samples)
                {
                    var category = environment.Assets.Get<TimeCategoryObject>(Corpus.ExpectedCategoryGuid(sample));
                    if (sample.Fields["m_timeCategoryObject"] == "{fileID: 0}")
                    {
                        TimeCategoryObject mapped;
                        Corpus.Require(environment.Lookup.Dictionary.TryGetValue((TimeCategory)Int32.Parse(sample.Fields["m_timeCategoryEnum"], CultureInfo.InvariantCulture), out mapped) &&
                            ReferenceEquals(category, mapped), "real authored lookup matches independent original mapping");
                    }
                }
                for (int offset = 0; offset < corpus.Samples.Length; offset += 64)
                    RunBatch(corpus.Samples.Skip(offset).Take(64).ToArray(), guid, environment.Assets, environment.ComponentWorkBegins, null, null);
                environment.VerifyCache(true);
                environment.ReleaseTemporaryConfiguration();
                var beforeRetainedLookupCallback = ProjectLucid.Tests.SharedStateSnapshot.Capture();
                // A fresh null-field document forces a real second OnValidate
                // after the temporary configuration has been removed.
                RunBatch(new[] { corpus.Samples.First(s => s.Fields["m_timeCategoryObject"] == "{fileID: 0}") }, guid,
                    environment.Assets, environment.ComponentWorkBegins, null, null);
                Corpus.Require(ProjectLucid.Tests.SharedStateSnapshot.Capture().Differences(beforeRetainedLookupCallback).Count == 0,
                    "retained authored lookup callback creates no configuration row or other shared change");
                environment.VerifyCache(true);
            }, environment.Release);
            Debug.Log("Authored TransformRotator: 9656 documents/516 assets/15 local targets; inactive serialization only.");
            return corpus.Samples.Length;
        }

        public static void RunFailureControl(Corpus.Sample sample, Action<string> at, Action<string> after, Action<OwnedPrefab> capture)
        {
            var environment = new EnvironmentLease();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                environment.Acquire(); at?.Invoke("configuration");
                environment.Assets.Get<TimeCategoryObject>(Corpus.ExpectedCategoryGuid(sample));
                RunBatch(new[] { sample }, ScriptGuid(), environment.Assets, environment.ComponentWorkBegins, at, after, capture);
            }, environment.Release);
        }
        public static void RunMissingKeyControl(Corpus.Sample sample)
        {
            var environment = new EnvironmentLease();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                environment.Acquire();
                // Original TryGetValue miss leaves the null field untouched. This
                // undefined Int32 control is not counted as an authored corpus row.
                Corpus.Require(!environment.Lookup.Dictionary.ContainsKey((TimeCategory)Int32.MinValue), "independent missing-key precondition");
                sample.Fields["m_timeCategoryObject"] = "{fileID: 0}";
                sample.Fields["m_timeCategoryEnum"] = Int32.MinValue.ToString(CultureInfo.InvariantCulture);
                sample.Document = ReplaceField(ReplaceField(sample.Document, "m_timeCategoryObject", "{fileID: 0}"), "m_timeCategoryEnum", sample.Fields["m_timeCategoryEnum"]);
                RunBatch(new[] { sample }, ScriptGuid(), environment.Assets, environment.ComponentWorkBegins, null, null, missingKey: true);
                environment.VerifyCache(true);
            }, environment.Release);
        }
        private static void RunBatch(Corpus.Sample[] samples, string script, BorrowedAssets borrowed, Action componentObserved,
            Action<string> at, Action<string> after, Action<OwnedPrefab> capture = null, bool missingKey = false)
        {
            var state = new OwnedPrefab(); capture?.Invoke(state);
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                state.Create(); at?.Invoke("folder");
                var transforms = new TransformState[samples.Length];
                for (int i = 0; i < samples.Length; ++i)
                {
                    GameObject child = state.Add("rotator_" + i);
                    child.SetActive(samples[i].OwnerActive);
                    if (samples[i].TargetDocument != null)
                    {
                        var target = Corpus.Fields(samples[i].TargetDocument);
                        child.transform.localPosition = Vector(target["m_LocalPosition"]);
                        child.transform.localRotation = Rotation(target["m_LocalRotation"]);
                        child.transform.localScale = Vector(target["m_LocalScale"]);
                    }
                    transforms[i] = new TransformState(child.transform);
                }
                state.Save();
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(state.Path);
                string yaml = File.ReadAllText(state.Path);
                var documents = Corpus.Documents(yaml).ToDictionary(d => d.Id);
                var used = new HashSet<long>(documents.Keys.Select(id => Int64.Parse(id, CultureInfo.InvariantCulture)));
                long next = 1;
                for (int i = 0; i < samples.Length; ++i)
                {
                    GameObject child = prefab.transform.GetChild(i).gameObject;
                    while (used.Contains(next)) ++next;
                    string owner = LocalId(child), component = next.ToString(CultureInfo.InvariantCulture); used.Add(next);
                    string ownerDoc = documents[owner].Text;
                    string changedOwner = ownerDoc.Replace("  m_Layer:", "  - component: {fileID: " + component + "}\n  m_Layer:");
                    Corpus.Require(changedOwner != ownerDoc, "owned component list injection");
                    yaml = yaml.Replace(ownerDoc, changedOwner);
                    string doc = samples[i].Document;
                    doc = Regex.Replace(doc, @"\A--- !u!114 &-?[0-9]+", "--- !u!114 &" + component);
                    doc = ReplaceField(doc, "m_GameObject", "{fileID: " + owner + "}");
                    doc = ReplaceField(doc, "m_Script", "{fileID: 11500000, guid: " + script + ", type: 3}");
                    if (samples[i].TargetDocument != null)
                        doc = ReplaceField(doc, "m_targetTransform", "{fileID: " + LocalId(child.transform) + "}");
                    yaml += doc;
                }
                File.WriteAllText(state.Path, yaml, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(state.Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(state.Path);
                Corpus.Require(imported != null && imported.GetComponentsInChildren<TransformRotator>(true).Length == samples.Length,
                    "real components observed before lifecycle-state observation");
                componentObserved();
                at?.Invoke("import"); Verify(state.Path, samples, script, borrowed, missingKey, transforms);
                state.Load(); at?.Invoke("load"); state.SaveContents(); at?.Invoke("save"); state.Unload();
                AssetDatabase.ImportAsset(state.Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Verify(state.Path, samples, script, borrowed, missingKey, transforms); at?.Invoke("reload");
            }, () => state.Release(after));
        }
        private readonly struct TransformState
        {
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            public TransformState(Transform transform)
            { position = transform.localPosition; scale = transform.localScale; rotation = transform.localRotation; }
            public void Verify(Transform transform)
            {
                // Ownership observation after genuine setters, not an assertion
                // that raw authored quaternion text bypasses engine normalization.
                Corpus.Require(transform.localPosition.Equals(position) && transform.localScale.Equals(scale) && transform.localRotation.Equals(rotation),
                    "owned target transform state unchanged by inactive lifecycle");
            }
        }
        private static void Verify(string path, Corpus.Sample[] samples, string script, BorrowedAssets borrowed, bool missingKey, TransformState[] transforms)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Corpus.Require(prefab != null && !prefab.activeSelf && prefab.transform.childCount == samples.Length, "inactive owned hierarchy");
            var written = Corpus.Documents(File.ReadAllText(path)).ToDictionary(d => d.Id, d => Corpus.Fields(d.Text));
            for (int i = 0; i < samples.Length; ++i)
            {
                GameObject child = prefab.transform.GetChild(i).gameObject;
                TransformRotator component = child.GetComponent<TransformRotator>();
                Corpus.Require(component != null && !child.activeInHierarchy && child.activeSelf == samples[i].OwnerActive && child.GetComponents<Component>().Length == 2,
                    "actual inactive TransformRotator with original local activity");
                var serialized = new SerializedObject(component); var f = samples[i].Fields;
                var output = written[LocalId(component)];
                foreach (string name in Corpus.FieldNames.Take(10).Except(new[] { "m_GameObject", "m_Script" }))
                    Corpus.Require(output[name] == f[name], "authored common field " + name);
                Corpus.Require(output["m_GameObject"] == "{fileID: " + LocalId(child) + "}" && output["m_Script"] == "{fileID: 11500000, guid: " + script + ", type: 3}", "owned owner and maintained script identity");
                foreach (string name in Corpus.FieldNames.Skip(10))
                {
                    SerializedProperty p = serialized.FindProperty(name); Corpus.Require(p != null, "serialized original field " + name);
                    if (name == "m_timeCategoryObject")
                        Corpus.Require(ReferenceEquals(p.objectReferenceValue, missingKey ? null : borrowed.Get<TimeCategoryObject>(Corpus.ExpectedCategoryGuid(samples[i]))),
                            "original normalization, missing-key null, or exact assigned category identity");
                    else if (name == "m_targetTransform")
                        Corpus.Require(ReferenceEquals(p.objectReferenceValue, samples[i].TargetDocument == null ? null : child.transform), "local target identity");
                    else if (name.StartsWith("m_rotationSpeed", StringComparison.Ordinal))
                    {
                        Vector3 expected = Vector(f[name]); Vector3 actual = p.vector3Value;
                        Corpus.Require(actual.x.Equals(expected.x) && actual.y.Equals(expected.y) && actual.z.Equals(expected.z), "exact authored vector " + name);
                    }
                    else if (p.propertyType == SerializedPropertyType.Boolean) Corpus.Require(p.boolValue == (f[name] == "1"), "authored Boolean " + name);
                    else Corpus.Require(p.intValue == Int32.Parse(f[name], CultureInfo.InvariantCulture), "authored integer " + name);
                }
                Corpus.Require(Read(component, "m_transform") == null && ((Vector3)Read(component, "m_rotationSpeedInUse")).Equals(Vector3.zero) &&
                    ((Quaternion)Read(component, "m_startRotation")).Equals(default(Quaternion)), "inactive import did not execute original Awake");
                transforms[i].Verify(child.transform);
            }
        }
        public static Vector3 Vector(string yaml)
        {
            Match m = Regex.Match(yaml, @"\A\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}\z");
            Corpus.Require(m.Success, "original vector syntax");
            return new Vector3(Single.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Single.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), Single.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
        }
        private static Quaternion Rotation(string yaml)
        {
            Match m = Regex.Match(yaml, @"\A\{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}\z");
            Corpus.Require(m.Success, "original quaternion syntax");
            return new Quaternion(Single.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Single.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                Single.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), Single.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
        }
        private static string ReplaceField(string doc, string name, string value)
        {
            string pattern = @"^  " + name + @":[^\r\n]*";
            Corpus.Require(Regex.Matches(doc, pattern, RegexOptions.Multiline).Count == 1, "unique authored field replacement");
            return Regex.Replace(doc, pattern, "  " + name + ": " + value, RegexOptions.Multiline);
        }
        private static string LocalId(Object value)
        { string guid; long id; Corpus.Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out id) && id != 0, "genuine owned local ID"); return id.ToString(CultureInfo.InvariantCulture); }
    }
}
#endif
