using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Real rebuilt assets created normally. These cases do not register systems,
    // query gameplay caches, load Addressables or invoke the original bootstrap.
    public sealed class OriginalConfigurationAssetSerializationTests
    {
        private sealed class OwnedAssets
        {
            public readonly string Directory = "Assets/LucidConfigurationTests_" + Guid.NewGuid().ToString("N");
            public readonly List<Object> Objects = new List<Object>();
            public readonly List<SerializedObject> Serialized = new List<SerializedObject>();

            public void Initialize()
            {
                string leaf = Directory.Substring("Assets/".Length);
                Assert.IsNotEmpty(AssetDatabase.CreateFolder("Assets", leaf));
            }

            public T Create<T>(string name) where T : ScriptableObject
            {
                T asset = ScriptableObject.CreateInstance<T>();
                Objects.Add(asset);
                asset.name = name;
                AssetDatabase.CreateAsset(asset, Directory + "/" + name + ".asset");
                return asset;
            }

            public T Reimport<T>(T asset) where T : ScriptableObject
            {
                string path = AssetDatabase.GetAssetPath(asset);
                Assert.IsTrue(path.StartsWith(Directory + "/", StringComparison.Ordinal));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                T imported = AssetDatabase.LoadAssetAtPath<T>(path);
                Assert.IsNotNull(imported);
                return imported;
            }

            public SerializedObject Inspect(Object asset)
            {
                SerializedObject value = new SerializedObject(asset);
                Serialized.Add(value);
                value.Update();
                return value;
            }
        }

        private static void WithOwnedAssets(Action<OwnedAssets> body)
        {
            OwnedAssets owned = new OwnedAssets();
            Exception primary = null;
            List<Exception> cleanup = new List<Exception>();
            try { owned.Initialize(); body(owned); }
            catch (Exception failure) { primary = failure; }
            for (int i = owned.Serialized.Count - 1; i >= 0; i--)
                try { owned.Serialized[i].Dispose(); }
                catch (Exception failure) { cleanup.Add(failure); }
            try
            {
                if (AssetDatabase.IsValidFolder(owned.Directory))
                    Assert.IsTrue(AssetDatabase.DeleteAsset(owned.Directory), "Delete the owned configuration test assets.");
            }
            catch (Exception failure) { cleanup.Add(failure); }
            for (int i = owned.Objects.Count - 1; i >= 0; i--)
                try { if (owned.Objects[i] != null) Object.DestroyImmediate(owned.Objects[i]); }
                catch (Exception failure) { cleanup.Add(failure); }
            if (primary != null)
            {
                if (cleanup.Count != 0) primary.Data["OwnedConfigurationCleanupFailures"] = new AggregateException(cleanup);
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            if (cleanup.Count != 0) throw new AggregateException(cleanup);
        }

        private static SerializedProperty Required(SerializedObject owner, string name)
        {
            SerializedProperty value = owner.FindProperty(name);
            Assert.IsNotNull(value, "Required original serialized field: " + name);
            return value;
        }

        private static void References(SerializedProperty array, params Object[] expected)
        {
            Assert.IsTrue(array.isArray);
            array.arraySize = expected.Length;
            for (int i = 0; i < expected.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                Assert.AreEqual(SerializedPropertyType.ObjectReference, element.propertyType);
                element.objectReferenceValue = expected[i];
            }
        }

        private static void AssertReferences(SerializedProperty array, params Object[] expected)
        {
            Assert.IsTrue(array.isArray);
            Assert.AreEqual(expected.Length, array.arraySize);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreSame(expected[i], array.GetArrayElementAtIndex(i).objectReferenceValue, "Original reference slot " + i);
        }

        [Test]
        public void ActiveConfigurationPreservesItsSerializedRuntimeReferenceAndGetter()
        {
            WithOwnedAssets(owned =>
            {
                ActiveRuntimeConfiguration active = owned.Create<ActiveRuntimeConfiguration>("OwnedActiveConfiguration");
                RuntimeConfiguration runtime = owned.Create<RuntimeConfiguration>("OwnedRuntimeConfiguration");
                SerializedObject serialized = owned.Inspect(active);
                SerializedProperty field = Required(serialized, "ActiveConfig");
                Assert.AreEqual(SerializedPropertyType.ObjectReference, field.propertyType);
                field.objectReferenceValue = runtime;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                active = owned.Reimport(active);
                serialized = owned.Inspect(active);
                Assert.AreSame(runtime, active.ActiveConfig);
                Assert.AreSame(runtime, active.Config);
                Assert.AreEqual("Assets/Configuration/Resources/", active.AssetLocation());
                Assert.AreEqual("ActiveRuntimeConfiguration", active.GetFileName());
                serialized.Update();
                Assert.AreSame(runtime, Required(serialized, "ActiveConfig").objectReferenceValue);
                Assert.IsNull(serialized.FindProperty("Config"));
            });
        }

        [Test]
        public void RuntimeConfigurationKeepsTwelveOrderedSlotsAndCreatesReadonlyWrappers()
        {
            WithOwnedAssets(owned =>
            {
                RuntimeConfiguration runtime = owned.Create<RuntimeConfiguration>("OwnedRuntimeConfiguration");
                GameLevels first = owned.Create<GameLevels>("OwnedFirstConfiguration");
                GameLevels second = owned.Create<GameLevels>("OwnedSecondConfiguration");
                Object[] expected = { first, null, second, first, second, null, first, second, first, null, second, first };
                SerializedObject serialized = owned.Inspect(runtime);
                References(Required(serialized, "m_configurationAssets"), expected);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                runtime = owned.Reimport(runtime);
                serialized = owned.Inspect(runtime);
                AssertReferences(Required(serialized, "m_configurationAssets"), expected);
                var firstWrapper = runtime.Assets;
                var secondWrapper = runtime.Assets;
                Assert.AreNotSame(firstWrapper, secondWrapper);
                Assert.AreEqual(12, firstWrapper.Count);
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreSame(expected[i], firstWrapper[i]);
                    Assert.AreSame(expected[i], secondWrapper[i]);
                }
                Assert.Throws<NotSupportedException>(() => ((IList<SystemConfigurationAsset>)firstWrapper)[0] = second);
                Assert.IsNull(serialized.FindProperty("Assets"));
            });
        }

        [Test]
        public void GameLevelsPreservesBaseOverridesAndAuthoredReferenceOrderWithoutCaching()
        {
            WithOwnedAssets(owned =>
            {
                GameLevels parent = owned.Create<GameLevels>("OwnedBaseLevels");
                GameLevels levels = owned.Create<GameLevels>("OwnedLevels");
                LevelDefinition common = owned.Create<LevelDefinition>("OwnedCommonScene");
                LevelDefinition gui = owned.Create<LevelDefinition>("OwnedGuiScene");
                GameplayLevelDefinition entry = owned.Create<GameplayLevelDefinition>("OwnedGameplayEntry");
                SerializedObject serialized = owned.Inspect(levels);
                Required(serialized, "m_baseAsset").objectReferenceValue = parent;
                SerializedProperty overrides = Required(serialized, "m_overriddenFields");
                string[] names = { "m_loadableLevels", "m_gameScenes", "m_gameplayEntries", "m_gameCenter" };
                overrides.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++) overrides.GetArrayElementAtIndex(i).stringValue = names[i];
                References(Required(serialized, "m_systemScenes"), common, gui, common, null);
                References(Required(serialized, "m_uiScenes"));
                References(Required(serialized, "m_gameplayEntries"), entry, null, entry);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                levels = owned.Reimport(levels);
                serialized = owned.Inspect(levels);
                Assert.AreSame(parent, Required(serialized, "m_baseAsset").objectReferenceValue);
                Assert.AreEqual(names.Length, Required(serialized, "m_overriddenFields").arraySize);
                for (int i = 0; i < names.Length; i++)
                    Assert.AreEqual(names[i], Required(serialized, "m_overriddenFields").GetArrayElementAtIndex(i).stringValue);
                AssertReferences(Required(serialized, "m_systemScenes"), common, gui, common, null);
                AssertReferences(Required(serialized, "m_uiScenes"));
                AssertReferences(Required(serialized, "m_gameplayEntries"), entry, null, entry);
                foreach (string name in new[] { "m_allLevels", "m_allZones", "m_zoneForLevel", "m_levelsPerZone", "m_levelsCached" })
                    Assert.IsNull(serialized.FindProperty(name), "Original nonserialized cache: " + name);
            });
        }

        // Tool receipts are inert identity evidence, not replacement game providers.
        [Serializable] private sealed class ImportFingerprint { public string sha256 = null; }
        [Serializable] private sealed class ImportExport
        {
            public string status = null, project_path = null, asset_map_path = null;
            public ImportFingerprint input_fingerprint = null;
        }
        [Serializable] private sealed class ImportMap { public ImportAsset[] assets = null; }
        [Serializable] private sealed class ImportAsset
        {
            public string path = null, guid = null;
            public string[] script_references = null;
        }
        [Serializable] private sealed class ImportPreparation
        {
            public string status = null, recovered_path = null;
            public ImportFingerprint input_fingerprint = null;
            public ImportBindings script_bindings = null;
        }
        [Serializable] private sealed class ImportBindings
        {
            public string status = null, source_fingerprint = null, comparison_path = null, comparison_sha256 = null;
            public bool references_modified;
            public ImportBinding[] bindings = null;
            public ImportBoundAsset[] assets = null;
        }
        [Serializable] private sealed class ImportBinding
        {
            public string assembly = null, full_name = null, maintained_path = null;
            public string exported_guid = null, target_guid = null, source_sha256 = null;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class ImportBoundAsset
        {
            public string path = null, guid = null, exported_sha256 = null, prepared_sha256 = null;
            public int pointers_rewritten;
        }
        private sealed class ImportSnapshot
        {
            public string Path, Original, Prepared;
            public byte[] Raw, PreparedBytes, RawMeta, PreparedMeta;
        }

        [Test]
        public void ActualPreparedConfigurationOwnersRetainOriginalPointersAndScopedLoadedFields()
        {
            // This gate qualifies four actual owner imports and their bounded
            // fields. Eleven other runtime settings and gameplay-entry payloads
            // remain unresolved; their raw ordered references are preserved.
            string root = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
            if (String.IsNullOrEmpty(work)) work = System.IO.Path.Combine(root, ".cache", "project-lucid");
            work = System.IO.Path.GetFullPath(work);
            string CheckedPath(string ownedRoot, string candidate)
            {
                string basePath = System.IO.Path.GetFullPath(ownedRoot);
                string path = System.IO.Path.GetFullPath(candidate);
                Assert.IsTrue(path.StartsWith(basePath + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal), "Identity evidence stays under its owned root.");
                for (string part = path; part != basePath; part = System.IO.Path.GetDirectoryName(part))
                    if (System.IO.File.Exists(part) || System.IO.Directory.Exists(part))
                        Assert.AreEqual(0, (int)(System.IO.File.GetAttributes(part) & System.IO.FileAttributes.ReparsePoint), "Identity evidence cannot follow symlinks.");
                return path;
            }
            string Hash(byte[] value)
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
            }
            bool IsHash(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-f]{64}$");
            string MetaGuid(byte[] bytes)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.UTF8.GetString(bytes), "^guid: ([0-9a-f]{32})\\r?$", System.Text.RegularExpressions.RegexOptions.Multiline);
                Assert.AreEqual(1, matches.Count, "One exact original asset metadata GUID.");
                return matches[0].Groups[1].Value;
            }
            string[] Sequence(string text, string name)
            {
                string[] lines = text.Split('\n');
                string prefix = "  " + name + ":";
                int found = -1;
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].TrimEnd('\r').StartsWith(prefix, StringComparison.Ordinal))
                    {
                        Assert.AreEqual(-1, found, "One qualified original field: " + name);
                        found = i;
                    }
                Assert.GreaterOrEqual(found, 0, "Required qualified original field: " + name);
                string value = lines[found].TrimEnd('\r').Substring(prefix.Length);
                if (value == " []")
                {
                    Assert.IsTrue(found + 1 == lines.Length || !lines[found + 1].StartsWith("  - ", StringComparison.Ordinal));
                    return new string[0];
                }
                Assert.AreEqual("", value, "Qualified sequence uses the observed block shape.");
                var entries = new List<string>();
                for (int i = found + 1; i < lines.Length && lines[i].StartsWith("  - ", StringComparison.Ordinal); i++)
                    entries.Add(lines[i].TrimEnd('\r').Substring(4));
                Assert.Greater(entries.Count, 0, "An empty qualified sequence must use [].");
                return entries.ToArray();
            }
            string PointerGuid(string value)
            {
                var match = System.Text.RegularExpressions.Regex.Match(value, "^\\{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\\}$");
                Assert.IsTrue(match.Success, "Exact original nonnull configuration asset pointer.");
                Assert.AreNotEqual(new string('0', 32), match.Groups[1].Value);
                return match.Groups[1].Value;
            }
            ImportExport export = JsonUtility.FromJson<ImportExport>(System.IO.File.ReadAllText(CheckedPath(work, System.IO.Path.Combine(work, "assets/latest-assets.json"))));
            ImportPreparation prepared = JsonUtility.FromJson<ImportPreparation>(System.IO.File.ReadAllText(CheckedPath(work, System.IO.Path.Combine(work, "assets/latest-prepare.json"))));
            Assert.IsNotNull(export); Assert.IsNotNull(prepared);
            Assert.AreEqual("exported", export.status); Assert.AreEqual("prepared", prepared.status);
            Assert.IsNotNull(export.input_fingerprint); Assert.IsNotNull(prepared.input_fingerprint);
            Assert.IsTrue(IsHash(export.input_fingerprint.sha256));
            Assert.AreEqual(export.input_fingerprint.sha256, prepared.input_fingerprint.sha256, "Immutable supplied release identity.");
            ImportBindings proof = prepared.script_bindings;
            Assert.IsNotNull(proof); Assert.AreEqual("verified_layout", proof.status); Assert.IsTrue(proof.references_modified);
            Assert.IsNotNull(proof.bindings); Assert.AreEqual(12, proof.bindings.Length);
            Assert.IsNotNull(proof.assets); Assert.AreEqual(529, proof.assets.Length);
            var expectedOwners = new HashSet<string>(new[] {
                "Game.Runtime::HardlightProject.LevelStartPositionDefinition",
                "Game.Runtime::HardlightProject.LevelDefinition",
                "UnityEngine.UI::UnityEngine.UI.LayoutElement",
                "UnityEngine.UI::UnityEngine.UI.ContentSizeFitter",
                "UnityEngine.UI::UnityEngine.UI.AspectRatioFitter",
                "HLUnityCore.Runtime::Hardlight.ActiveRuntimeConfiguration",
                "HLUnityCore.Runtime::Hardlight.RuntimeConfiguration",
                "Game.Runtime::HardlightProject.GameLevels",
                "Game.Runtime::HardlightProject.CoreGameConfiguration",
                "HLLocalisation.Runtime::Hardlight.HLLocalisationConfigurationAsset",
                "Game.Runtime::HardlightProject.HashedGroupConfiguration",
                "Game.Runtime::HardlightProject.DataDefinitions" }, StringComparer.Ordinal);
            var actualOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImportBinding item in proof.bindings)
            {
                Assert.IsNotNull(item);
                string owner = item.assembly + "::" + item.full_name;
                Assert.IsTrue(expectedOwners.Contains(owner), "Unexpected exact binding owner: " + owner);
                Assert.IsTrue(actualOwners.Add(owner), "Duplicate exact binding owner: " + owner);
            }
            CollectionAssert.AreEquivalent(expectedOwners, actualOwners, "Every exact binding owner must be present.");
            Assert.AreEqual(ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(root, false), proof.source_fingerprint, "Fresh maintained source binding evidence.");
            Assert.IsTrue(IsHash(proof.comparison_sha256));
            Assert.AreEqual(proof.comparison_sha256, Hash(System.IO.File.ReadAllBytes(CheckedPath(work, proof.comparison_path))), "Exact current layout evidence bytes.");
            Assert.AreEqual(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "Assets/Recovered")), System.IO.Path.GetFullPath(prepared.recovered_path));
            ImportMap mapping = JsonUtility.FromJson<ImportMap>(System.IO.File.ReadAllText(CheckedPath(work, export.asset_map_path)));
            Assert.IsNotNull(mapping); Assert.IsNotNull(mapping.assets);
            Type[] types = { typeof(ActiveRuntimeConfiguration), typeof(RuntimeConfiguration), typeof(GameLevels) };
            string[] assemblies = { "HLUnityCore.Runtime", "HLUnityCore.Runtime", "Game.Runtime" };
            string[] paths = {
                "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/ActiveRuntimeConfiguration.cs",
                "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/RuntimeConfiguration.cs",
                "Assets/Scripts/Game/Configuration/GameLevels.cs" };
            string[] originalScripts = { "beb5b3fe49e0cf7630d751a80e74b246", "043202340fb68357bcc9620084f5ad91", "de1a71a6eb50fa30619ff8d202f18f95" };
            string[] currentScripts = { "15909dda3294429b9bffe33ec5b0dd91", "115cb95a5da94420bc577783827149fc", "f7ea6aa109a4a844605ac741f01e9606" };
            var bindingRows = new ImportBinding[3]; var scripts = new MonoScript[3];
            for (int i = 0; i < 3; i++)
            {
                var selected = new List<ImportBinding>();
                foreach (ImportBinding item in proof.bindings)
                {
                    Assert.IsNotNull(item);
                    if (item.assembly == assemblies[i] && item.full_name == types[i].FullName) selected.Add(item);
                }
                Assert.AreEqual(1, selected.Count); ImportBinding approved = selected[0]; bindingRows[i] = approved;
                Assert.AreEqual(paths[i], approved.maintained_path); Assert.AreEqual(originalScripts[i], approved.exported_guid);
                Assert.AreEqual(11500000, approved.exported_file_id); Assert.AreEqual(11500000, approved.target_file_id);
                Assert.AreEqual(currentScripts[i], approved.target_guid); Assert.IsTrue(IsHash(approved.source_sha256));
                Assert.AreEqual(approved.source_sha256, Hash(System.IO.File.ReadAllBytes(CheckedPath(root, System.IO.Path.Combine(root, paths[i])))));
                Assert.AreEqual(currentScripts[i], MetaGuid(System.IO.File.ReadAllBytes(CheckedPath(root, System.IO.Path.Combine(root, paths[i] + ".meta")))));
                scripts[i] = AssetDatabase.LoadAssetAtPath<MonoScript>(paths[i]);
                Assert.IsNotNull(scripts[i]); Assert.AreEqual(types[i], scripts[i].GetClass());
                Assert.AreEqual(assemblies[i], scripts[i].GetClass().Assembly.GetName().Name);
                string scriptGuid; long scriptId;
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(scripts[i], out scriptGuid, out scriptId));
                Assert.AreEqual(currentScripts[i], scriptGuid); Assert.AreEqual(11500000, scriptId);
            }
            string[] ownerGuids = { "48ed56d7f0d5ec149a74db9c8ab52f55", "be0c4994985f7c84d9569675aa53e016", "203f48a8a3b537a4fa01f7d2736e4767", "671cfbca72eaf6e45a906b11506a9691" };
            int[] ownerKinds = { 0, 1, 2, 2 };
            var loaded = new Object[4]; var snapshots = new List<ImportSnapshot>(); var serialized = new List<SerializedObject>();
            Exception primary = null; var cleanup = new List<Exception>();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var originalRows = new List<ImportAsset>(); var boundRows = new List<ImportBoundAsset>();
                    foreach (ImportAsset item in mapping.assets) { Assert.IsNotNull(item); if (item.guid == ownerGuids[i]) originalRows.Add(item); }
                    foreach (ImportBoundAsset item in proof.assets) { Assert.IsNotNull(item); if (item.guid == ownerGuids[i]) boundRows.Add(item); }
                    Assert.AreEqual(1, originalRows.Count); Assert.AreEqual(1, boundRows.Count);
                    ImportAsset original = originalRows[0]; ImportBoundAsset bound = boundRows[0]; ImportBinding binding = bindingRows[ownerKinds[i]];
                    Assert.AreEqual(original.path, bound.path); Assert.IsTrue(original.path.StartsWith("Assets/", StringComparison.Ordinal) && original.path.EndsWith(".asset", StringComparison.Ordinal));
                    Assert.IsNotNull(original.script_references); CollectionAssert.AreEqual(new[] { binding.exported_guid }, original.script_references);
                    string rawPath = CheckedPath(work, System.IO.Path.Combine(export.project_path, original.path));
                    string importedPath = "Assets/Recovered/" + original.path.Substring("Assets/".Length);
                    string importedFile = CheckedPath(prepared.recovered_path, System.IO.Path.Combine(root, importedPath));
                    var snapshot = new ImportSnapshot { Path = importedPath, Raw = System.IO.File.ReadAllBytes(rawPath), PreparedBytes = System.IO.File.ReadAllBytes(importedFile),
                        RawMeta = System.IO.File.ReadAllBytes(CheckedPath(work, rawPath + ".meta")), PreparedMeta = System.IO.File.ReadAllBytes(CheckedPath(prepared.recovered_path, importedFile + ".meta")) };
                    snapshot.Original = System.Text.Encoding.UTF8.GetString(snapshot.Raw); snapshot.Prepared = System.Text.Encoding.UTF8.GetString(snapshot.PreparedBytes); snapshots.Add(snapshot);
                    Assert.AreEqual(bound.exported_sha256, Hash(snapshot.Raw)); Assert.AreEqual(bound.prepared_sha256, Hash(snapshot.PreparedBytes));
                    Assert.AreEqual(ownerGuids[i], MetaGuid(snapshot.RawMeta)); Assert.AreEqual(ownerGuids[i], MetaGuid(snapshot.PreparedMeta));
                    CollectionAssert.AreEqual(snapshot.RawMeta, snapshot.PreparedMeta, "Prepared metadata stays byte-identical to the original.");
                    string from = "  m_Script: {fileID: 11500000, guid: " + binding.exported_guid + ", type: 3}";
                    string to = "  m_Script: {fileID: 11500000, guid: " + binding.target_guid + ", type: 3}";
                    Assert.AreEqual(1, bound.pointers_rewritten);
                    Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(snapshot.Original, "^" + System.Text.RegularExpressions.Regex.Escape(from) + "\\r?$", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
                    Assert.AreEqual(snapshot.Original.Replace(from, to), snapshot.Prepared, "Whole original document changes only m_Script.");
                    Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(snapshot.Original, "^--- !u!114 &11400000\\r?$", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
                    loaded[i] = AssetDatabase.LoadAssetAtPath(importedPath, types[ownerKinds[i]]);
                    Assert.IsNotNull(loaded[i]); Assert.AreEqual(types[ownerKinds[i]], loaded[i].GetType());
                    string assetGuid; long fileId;
                    Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(loaded[i], out assetGuid, out fileId));
                    Assert.AreEqual(ownerGuids[i], assetGuid); Assert.AreEqual(11400000, fileId);
                    SerializedObject value = new SerializedObject(loaded[i]); serialized.Add(value); value.Update();
                    SerializedProperty script = Required(value, "m_Script");
                    Assert.AreEqual(SerializedPropertyType.ObjectReference, script.propertyType); Assert.AreSame(scripts[ownerKinds[i]], script.objectReferenceValue);
                }
                ActiveRuntimeConfiguration active = (ActiveRuntimeConfiguration)loaded[0]; RuntimeConfiguration runtime = (RuntimeConfiguration)loaded[1];
                Assert.AreSame(runtime, active.ActiveConfig); Assert.AreSame(runtime, active.Config);
                Assert.AreEqual(SerializedPropertyType.ObjectReference, Required(serialized[0], "ActiveConfig").propertyType);
                Assert.AreSame(runtime, Required(serialized[0], "ActiveConfig").objectReferenceValue);
                SerializedProperty runtimeSlots = Required(serialized[1], "m_configurationAssets");
                string[] runtimeRaw = Sequence(snapshots[1].Original, "m_configurationAssets");
                Assert.IsTrue(runtimeSlots.isArray); Assert.AreEqual(12, runtimeRaw.Length); Assert.AreEqual(12, runtimeSlots.arraySize);
                for (int i = 0; i < runtimeRaw.Length; i++)
                {
                    PointerGuid(runtimeRaw[i]);
                    Assert.AreEqual(SerializedPropertyType.ObjectReference, runtimeSlots.GetArrayElementAtIndex(i).propertyType);
                }
                Assert.AreEqual(ownerGuids[2], PointerGuid(runtimeRaw[4]));
                Assert.AreSame(loaded[2], runtimeSlots.GetArrayElementAtIndex(4).objectReferenceValue, "The qualified GameLevels slot is genuinely resolved.");
                // Do not require unresolved other settings to load as providers,
                // and do not interpret missing references as readiness evidence.
                for (int owner = 2; owner < 4; owner++)
                {
                    SerializedObject value = serialized[owner];
                    string[] sceneRaw = Sequence(snapshots[owner].Original, "m_systemScenes");
                    SerializedProperty scenes = Required(value, "m_systemScenes");
                    Assert.IsTrue(scenes.isArray); Assert.AreEqual(13, sceneRaw.Length); Assert.AreEqual(13, scenes.arraySize);
                    for (int i = 0; i < 13; i++)
                    {
                        SerializedProperty scene = scenes.GetArrayElementAtIndex(i);
                        Assert.AreEqual(SerializedPropertyType.ObjectReference, scene.propertyType);
                        Object definition = scene.objectReferenceValue; Assert.IsNotNull(definition); Assert.AreEqual(typeof(LevelDefinition), definition.GetType());
                        string guid; long id; Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(definition, out guid, out id));
                        Assert.AreEqual(PointerGuid(sceneRaw[i]), guid); Assert.AreEqual(11400000, id);
                    }
                    string[] overridesRaw = Sequence(snapshots[owner].Original, "m_overriddenFields");
                    string[] expected = owner == 2 ? new[] { "m_loadableLevels", "m_gameScenes", "m_gameplayEntries", "m_gameCenter" } : new[] { "m_systemScenes", "m_gameplayEntries" };
                    CollectionAssert.AreEqual(expected, overridesRaw); SerializedProperty overrides = Required(value, "m_overriddenFields");
                    Assert.IsTrue(overrides.isArray); Assert.AreEqual(expected.Length, overrides.arraySize);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        Assert.AreEqual(SerializedPropertyType.String, overrides.GetArrayElementAtIndex(i).propertyType);
                        Assert.AreEqual(expected[i], overrides.GetArrayElementAtIndex(i).stringValue);
                    }
                    CollectionAssert.AreEqual(new string[0], Sequence(snapshots[owner].Original, "m_uiScenes"));
                    Assert.IsTrue(Required(value, "m_uiScenes").isArray); Assert.AreEqual(0, Required(value, "m_uiScenes").arraySize);
                    SerializedProperty parent = Required(value, "m_baseAsset"); Assert.AreEqual(SerializedPropertyType.ObjectReference, parent.propertyType);
                    if (owner == 2) Assert.AreSame(loaded[3], parent.objectReferenceValue); else Assert.IsNull(parent.objectReferenceValue);
                    string[] entryRaw = Sequence(snapshots[owner].Original, "m_gameplayEntries");
                    Assert.AreEqual(owner == 2 ? 1 : 0, entryRaw.Length);
                    SerializedProperty entries = Required(value, "m_gameplayEntries"); Assert.IsTrue(entries.isArray); Assert.AreEqual(entryRaw.Length, entries.arraySize);
                    for (int i = 0; i < entryRaw.Length; i++)
                    {
                        PointerGuid(entryRaw[i]); Assert.AreEqual(SerializedPropertyType.ObjectReference, entries.GetArrayElementAtIndex(i).propertyType);
                    }
                    // Gameplay-entry contents are preserved as raw pointer bytes;
                    // no category/definition payload or CacheLevels is invoked.
                }
                for (int i = 0; i < 4; i++)
                {
                    string raw = CheckedPath(work, System.IO.Path.Combine(export.project_path, "Assets/" + snapshots[i].Path.Substring("Assets/Recovered/".Length)));
                    string preparedFile = CheckedPath(prepared.recovered_path, System.IO.Path.Combine(root, snapshots[i].Path));
                    CollectionAssert.AreEqual(snapshots[i].Raw, System.IO.File.ReadAllBytes(raw));
                    CollectionAssert.AreEqual(snapshots[i].PreparedBytes, System.IO.File.ReadAllBytes(preparedFile));
                    CollectionAssert.AreEqual(snapshots[i].RawMeta, System.IO.File.ReadAllBytes(CheckedPath(work, raw + ".meta")));
                    CollectionAssert.AreEqual(snapshots[i].PreparedMeta, System.IO.File.ReadAllBytes(CheckedPath(prepared.recovered_path, preparedFile + ".meta")));
                }
                TestContext.Out.WriteLine("Qualified only four configuration owner imports and bounded fields. Eleven other runtime settings and gameplay-entry payloads remain unresolved; their raw ordered references were retained without provider invocation.");
            }
            catch (Exception failure) { primary = failure; }
            finally
            {
                for (int i = serialized.Count - 1; i >= 0; i--)
                    try { serialized[i].Dispose(); }
                    catch (Exception failure) { cleanup.Add(failure); }
            }
            if (primary != null)
            {
                if (cleanup.Count != 0)
                    try { primary.Data["ReadonlyConfigurationImportCleanupFailures"] = new AggregateException(cleanup); }
                    catch { }
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            if (cleanup.Count != 0) throw new AggregateException(cleanup);
        }
    }
}
