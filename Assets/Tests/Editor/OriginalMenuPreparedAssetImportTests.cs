using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.Editor
{
    // Observe supplied data and genuine imports without resolving configuration
    // overrides, registering providers or invoking original initialization.
    public sealed class OriginalMenuPreparedAssetImportTests
    {
        private static readonly string[] ExpectedOwners = {
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
            "Game.Runtime::HardlightProject.DataDefinitions",
            "HLUnityCore.Runtime::Hardlight.FiniteStateMachineBehaviour",
            "HLUnityCore.Runtime::Hardlight.FSMUserBehaviour",
            "HLUnityCore.Runtime::Hardlight.FiniteStateMachineScriptableObject",
            "Unity.TextMeshPro::TMPro.TMP_StyleSheet",
            "Unity.TextMeshPro::TMPro.TMP_SpriteAsset",
            "HLUnityUI.Runtime::Hardlight.GuiInitializer",
            "Game.Runtime::HardlightProject.VisualAssetManager",
            "HLUnityCore.Runtime::Hardlight.TimeCategoryConfiguration",
            "HLUnityCore.Runtime::Hardlight.TimeCategoryObject",
            "HLUnityCore.Runtime::Hardlight.HLUnityCoreConfigurationAsset",
            "HLModernUI.Runtime::Hardlight.UINavigationConfiguration",
            "Game.Runtime::Hardlight.Analytics.Analytics_SystemConfig",
            "Unity.TextMeshPro::TMPro.TMP_Settings",
            "Unity.TextMeshPro::TMPro.TMP_FontAsset",
            "Game.Runtime::HardlightProject.TimeCategoryLookup",
            "Game.Runtime::HardlightProject.DevicePerformanceGroup",
            "Game.Runtime::HardlightProject.DevicePerformance_iOS",
            "Game.Runtime::HardlightProject.DevicePerformance_macOS",
            "Game.Runtime::HardlightProject.DevicePerformance_tvOS",
            "HLModernUI.Runtime::Hardlight.UIHighlightDefinition",
            "HLModernUI.Runtime::Hardlight.UIHighlight" };
        private static readonly string[] DevOverrides = {
            "m_useFastValidation", "m_sendCrashReports", "m_gameCenter", "m_deviceConsoleEnabled",
            "m_logHandlerEnabled", "m_calculateFPSCounter", "m_applicationVersionDefinition",
            "m_showGameCenterChallenges", "m_hashedComponentGroupDefaults" };
        private static readonly string[] ProdOverrides = {
            "m_sendCrashReports", "m_gameCenter", "m_useExportedScenes", "m_deviceConsoleEnabled",
            "m_logHandlerEnabled", "m_calculateFPSCounter" };
        private static readonly string[] LocOverrides = {
            "m_nodeDataExportPath", "m_charactersOutputDirectory", "m_localisedDefinitionsDirectory" };
        private static readonly string[] RuntimeGuids = {
            "74f5b05cc784b67468279ff0fb33cdd6", "8f0750d5cce5f394895662ec47a76168",
            "1fa725bb8aeb116409dc6630d494e378", "83586e556e94a4c4a806d0fd0867f11b",
            "203f48a8a3b537a4fa01f7d2736e4767", "4b6b95cc96912224691158c770774484",
            "15de4602110a71444a4f8334673784f6", "c7befb42a12846847903231153dc8c32",
            "a9a6ec419bc3af6409c01cb9c0658978", "2050b495636a39e4cb16c1082cb7d5e7",
            "22d0597fd2f52364abe6c04d2cf832bd", "70047e1d6e803934e99ca5840b21774f" };

        [Serializable] private sealed class Fingerprint { public string sha256 = null; }
        [Serializable] private sealed class ExportReceipt
        {
            public string status = null, project_path = null, asset_map_path = null;
            public Fingerprint input_fingerprint = null;
        }
        [Serializable] private sealed class AssetMap { public ExportAsset[] assets = null; }
        [Serializable] private sealed class ExportAsset
        {
            public string path = null, guid = null;
            public string[] script_references = null;
        }
        [Serializable] private sealed class PreparationReceipt
        {
            public string status = null, recovered_path = null;
            public Fingerprint input_fingerprint = null;
            public Bindings script_bindings = null;
        }
        [Serializable] private sealed class Bindings
        {
            public string status = null, source_fingerprint = null, comparison_path = null, comparison_sha256 = null;
            public bool references_modified;
            public Binding[] bindings = null;
            public BoundAsset[] assets = null;
        }
        [Serializable] private sealed class Binding
        {
            public string assembly = null, full_name = null, maintained_path = null;
            public string exported_guid = null, target_guid = null, source_sha256 = null;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class BoundAsset
        {
            public string path = null, guid = null, exported_sha256 = null, prepared_sha256 = null;
            public int pointers_rewritten;
        }
        private sealed class FileObservation
        {
            public string Root, Path;
            public byte[] Bytes;
        }
        private sealed class Document
        {
            public string Raw;
            public Object Loaded;
            public SerializedObject Serialized;
        }
        private sealed class Observation
        {
            public readonly List<SerializedObject> Serialized = new List<SerializedObject>();
            private readonly Dictionary<string, FileObservation> files = new Dictionary<string, FileObservation>(StringComparer.Ordinal);

            public byte[] Read(string root, string candidate)
            {
                string path = CheckedPath(root, candidate);
                byte[] bytes = File.ReadAllBytes(path);
                if (files.TryGetValue(path, out FileObservation prior))
                    CollectionAssert.AreEqual(prior.Bytes, bytes, "Observed source or asset bytes stay unchanged: " + path);
                else files.Add(path, new FileObservation { Root = root, Path = path, Bytes = bytes });
                return bytes;
            }

            public string Text(string root, string candidate) => new UTF8Encoding(false, true).GetString(Read(root, candidate));

            public List<Exception> Finish()
            {
                var failures = new List<Exception>();
                for (int i = Serialized.Count - 1; i >= 0; i--)
                    try { Serialized[i].Dispose(); }
                    catch (Exception failure) { failures.Add(failure); }
                foreach (FileObservation file in files.Values)
                    try { CollectionAssert.AreEqual(file.Bytes, File.ReadAllBytes(CheckedPath(file.Root, file.Path)), "Read-only import retained exact bytes: " + file.Path); }
                    catch (Exception failure) { failures.Add(failure); }
                return failures;
            }
        }

        [Test]
        public void ActualSuppliedMenuAssetsRetainOriginalScriptsFieldsGraphAndRuntimeOrder()
        {
            var observed = new Observation();
            ExceptionDispatchInfo primary = null;
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
                if (String.IsNullOrEmpty(work)) work = Path.Combine(root, ".cache", "project-lucid");
                work = Path.GetFullPath(work);
                ExportReceipt export = JsonUtility.FromJson<ExportReceipt>(observed.Text(work, Path.Combine(work, "assets/latest-assets.json")));
                PreparationReceipt prepared = JsonUtility.FromJson<PreparationReceipt>(observed.Text(work, Path.Combine(work, "assets/latest-prepare.json")));
                Assert.That(export, Is.Not.Null); Assert.That(prepared, Is.Not.Null);
                Assert.That(export.status, Is.EqualTo("exported")); Assert.That(prepared.status, Is.EqualTo("prepared"));
                Assert.That(export.input_fingerprint, Is.Not.Null); Assert.That(prepared.input_fingerprint, Is.Not.Null);
                Assert.That(IsHash(export.input_fingerprint.sha256), Is.True);
                Assert.That(prepared.input_fingerprint.sha256, Is.EqualTo(export.input_fingerprint.sha256), "Immutable supplied release identity.");
                Bindings proof = prepared.script_bindings;
                Assert.That(proof, Is.Not.Null); Assert.That(proof.status, Is.EqualTo("verified_layout")); Assert.That(proof.references_modified, Is.True);
                AssertBindingOwners(proof);
                Assert.That(proof.assets, Is.Not.Null); Assert.That(proof.assets.Length, Is.EqualTo(765));
                Assert.That(proof.source_fingerprint, Is.EqualTo(ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(root, false)), "Binding proof belongs to the complete current source.");
                Assert.That(IsHash(proof.comparison_sha256), Is.True);
                Assert.That(Hash(observed.Read(work, proof.comparison_path)), Is.EqualTo(proof.comparison_sha256));
                Assert.That(Path.GetFullPath(prepared.recovered_path), Is.EqualTo(Path.GetFullPath(Path.Combine(root, "Assets/Recovered"))));
                AssetMap map = JsonUtility.FromJson<AssetMap>(observed.Text(work, export.asset_map_path));
                Assert.That(map, Is.Not.Null); Assert.That(map.assets, Is.Not.Null);

                Type[] types = { typeof(CoreGameConfiguration), typeof(HLLocalisationConfigurationAsset), typeof(HashedGroupConfiguration), typeof(RuntimeConfiguration) };
                string[] assemblies = { "Game.Runtime", "HLLocalisation.Runtime", "Game.Runtime", "HLUnityCore.Runtime" };
                string[] paths = {
                    "Assets/Scripts/Game/Configuration/CoreGameConfiguration.cs",
                    "Packages/com.hardlight.hllocalisation/Runtime/HLLocalisationConfigurationAsset.cs",
                    "Assets/Scripts/RecoveredProof/HashedGroupConfiguration.cs",
                    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/RuntimeConfiguration.cs" };
                string[] exportedGuids = { "fc8be15104d99a9879962cad4d763ac3", "3be70450225d95a6c963b5b568cab31c", "157a6b053faad8c749f26c6f2d91797c", "043202340fb68357bcc9620084f5ad91" };
                string[] targetGuids = { "440b8d9aa1906c95609bf5b917cef73f", "cf2ad3c483ed4e46b57f6108b7e2c558", "8548ba757ae253f892f7f7600a798f47", "115cb95a5da94420bc577783827149fc" };
                var bindings = new Binding[4]; var scripts = new MonoScript[4];
                for (int i = 0; i < types.Length; i++)
                {
                    bindings[i] = SelectBinding(proof, assemblies[i], types[i].FullName);
                    Binding binding = bindings[i];
                    Assert.That(binding.maintained_path, Is.EqualTo(paths[i]));
                    Assert.That(binding.exported_guid, Is.EqualTo(exportedGuids[i])); Assert.That(binding.exported_file_id, Is.EqualTo(11500000));
                    Assert.That(binding.target_guid, Is.EqualTo(targetGuids[i])); Assert.That(binding.target_file_id, Is.GreaterThan(0));
                    Assert.That(IsHash(binding.source_sha256), Is.True);
                    Assert.That(Hash(observed.Read(root, Path.Combine(root, paths[i]))), Is.EqualTo(binding.source_sha256));
                    Assert.That(MetaGuid(observed.Read(root, Path.Combine(root, paths[i] + ".meta"))), Is.EqualTo(targetGuids[i]));
                    scripts[i] = AssetDatabase.LoadAssetAtPath<MonoScript>(paths[i]);
                    Assert.That(scripts[i], Is.Not.Null); Assert.That(scripts[i].GetClass(), Is.EqualTo(types[i]));
                    Assert.That(types[i].Assembly.GetName().Name, Is.EqualTo(assemblies[i]));
                    string guid; long id;
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(scripts[i], out guid, out id), Is.True);
                    Assert.That(guid, Is.EqualTo(binding.target_guid)); Assert.That(id, Is.EqualTo(binding.target_file_id), "Observe the genuine loaded script ID; do not infer it from source metadata.");
                }

                string[] names = { "ProdGameConfiguration", "DevGameConfiguration", "HashedComponentGroupDefaults", "SDTLocsConfig", "ProdRuntimeConfiguration" };
                string[] guids = { RuntimeGuids[0], "ca6aaeab30323114696bf3141d7f3f44", "7a4a450d53440c140a009c472a1f24d5", RuntimeGuids[1], "be0c4994985f7c84d9569675aa53e016" };
                int[] kinds = { 0, 0, 2, 1, 3 };
                string[] rawHashes = {
                    "0ebe7c9735a1f41fb4274e062c21c5677f59db0498fe0af124182cb9bfe069f6",
                    "b6f4b9973de9a5424dff5dc197cf8b3eb7829b8a90fb2e858483d0a78fbce4d0",
                    "3d9252612eefa71ad0bb824cdaf4edc344ca51dfe87e08a4451aad2d3f608bfd",
                    "be77138662ee7e961eb9ee44cef7f8aee5486efe00dd5223655a691081d6f5f6",
                    "d9c4f31407e7a04d01c36cc13938d5b3826b142d1a2abba1b94d75cf240c4dd2" };
                var documents = new Document[5];
                for (int i = 0; i < documents.Length; i++)
                    documents[i] = ObserveDocument(observed, root, work, export, prepared, map, proof,
                        names[i], guids[i], types[kinds[i]], bindings[kinds[i]], scripts[kinds[i]], rawHashes[i]);

                AssertCore(documents[0], true, false, documents[1].Loaded, "ca6aaeab30323114696bf3141d7f3f44", ProdOverrides, null, null);
                AssertCore(documents[1], false, true, null, null, DevOverrides, documents[2].Loaded, "7a4a450d53440c140a009c472a1f24d5");
                AssertLocalisation(documents[3]);
                Assert.That(typeof(HashedGroupConfiguration).BaseType, Is.EqualTo(typeof(ScriptableObject)));
                Assert.That(typeof(HashedGroupConfiguration).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), Is.Empty);
                foreach (string field in new[] { "m_guid", "m_baseAsset", "m_overriddenFields" })
                    Assert.That(documents[2].Serialized.FindProperty(field), Is.Null, "Hashed has plain fieldless storage: " + field);

                string[] rawSlots = Sequence(documents[4].Raw, "m_configurationAssets");
                SerializedProperty slots = Require(documents[4].Serialized, "m_configurationAssets", SerializedPropertyType.Generic);
                Assert.That(slots.isArray, Is.True); Assert.That(rawSlots.Length, Is.EqualTo(12)); Assert.That(slots.arraySize, Is.EqualTo(12));
                for (int i = 0; i < 12; i++)
                {
                    Assert.That(rawSlots[i], Is.EqualTo(Pointer(RuntimeGuids[i])), "Exact original Runtime slot " + i);
                    Assert.That(slots.GetArrayElementAtIndex(i).propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                }
                Assert.That(slots.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(documents[0].Loaded));
                Assert.That(slots.GetArrayElementAtIndex(1).objectReferenceValue, Is.EqualTo(documents[3].Loaded));
                // Slots 2..11 retain raw identities. Their loaded values and
                // provider payloads are separate gates and are not inspected.
                TestContext.Out.WriteLine("Observed four supplied menu assets and Runtime12 raw order; only its Prod/Localisation loaded slots were qualified. Initialization and provider behavior remain separate.");
            }
            catch (Exception failure) { primary = ExceptionDispatchInfo.Capture(failure); }
            List<Exception> secondary = observed.Finish();
            if (primary != null)
            {
                if (secondary.Count != 0)
                    try { primary.SourceException.Data["ReadonlyMenuImportSecondaryFailures"] = new AggregateException(secondary); }
                    catch { }
                primary.Throw();
            }
            if (secondary.Count != 0) throw new AggregateException(secondary);
        }

        private static void AssertBindingOwners(Bindings proof)
        {
            Assert.That(proof.bindings, Is.Not.Null); Assert.That(proof.bindings.Length, Is.EqualTo(33));
            var expected = new HashSet<string>(ExpectedOwners, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (Binding item in proof.bindings)
            {
                Assert.That(item, Is.Not.Null); Assert.That(item.assembly, Is.Not.Empty); Assert.That(item.full_name, Is.Not.Empty);
                string owner = item.assembly + "::" + item.full_name;
                Assert.That(expected.Contains(owner), Is.True, "Unexpected binding owner: " + owner);
                Assert.That(actual.Add(owner), Is.True, "Duplicate binding owner: " + owner);
            }
            CollectionAssert.AreEquivalent(expected, actual, "Every exact binding owner must be present.");
        }

        private static Binding SelectBinding(Bindings proof, string assembly, string fullName)
        {
            var selected = new List<Binding>();
            foreach (Binding binding in proof.bindings)
                if (binding.assembly == assembly && binding.full_name == fullName) selected.Add(binding);
            Assert.That(selected.Count, Is.EqualTo(1)); return selected[0];
        }

        private static Document ObserveDocument(Observation observed, string root, string work, ExportReceipt export,
            PreparationReceipt prepared, AssetMap map, Bindings proof, string name, string guid, Type type,
            Binding binding, MonoScript script, string expectedRawHash)
        {
            var originalRows = new List<ExportAsset>(); var boundRows = new List<BoundAsset>();
            foreach (ExportAsset row in map.assets) { Assert.That(row, Is.Not.Null); if (row.guid == guid) originalRows.Add(row); }
            foreach (BoundAsset row in proof.assets) { Assert.That(row, Is.Not.Null); if (row.guid == guid) boundRows.Add(row); }
            Assert.That(originalRows.Count, Is.EqualTo(1)); Assert.That(boundRows.Count, Is.EqualTo(1));
            ExportAsset original = originalRows[0]; BoundAsset bound = boundRows[0];
            string relative = "Assets/MonoBehaviour/" + name + ".asset";
            Assert.That(original.path, Is.EqualTo(relative)); Assert.That(bound.path, Is.EqualTo(relative));
            CollectionAssert.AreEqual(new[] { binding.exported_guid }, original.script_references);
            string rawPath = Path.Combine(export.project_path, relative);
            string importedPath = "Assets/Recovered/MonoBehaviour/" + name + ".asset";
            byte[] raw = observed.Read(work, rawPath);
            byte[] converted = observed.Read(prepared.recovered_path, Path.Combine(root, importedPath));
            Assert.That(Hash(raw), Is.EqualTo(expectedRawHash)); Assert.That(bound.exported_sha256, Is.EqualTo(expectedRawHash));
            Assert.That(Hash(converted), Is.EqualTo(bound.prepared_sha256)); Assert.That(IsHash(bound.prepared_sha256), Is.True);
            byte[] rawMeta = observed.Read(work, rawPath + ".meta");
            byte[] importedMeta = observed.Read(prepared.recovered_path, Path.Combine(root, importedPath + ".meta"));
            Assert.That(MetaGuid(rawMeta), Is.EqualTo(guid)); CollectionAssert.AreEqual(rawMeta, importedMeta, "Whole supplied metadata is byte-identical.");
            string originalText = new UTF8Encoding(false, true).GetString(raw);
            string from = "  m_Script: {fileID: " + binding.exported_file_id.ToString(CultureInfo.InvariantCulture) + ", guid: " + binding.exported_guid + ", type: 3}";
            string to = "  m_Script: {fileID: " + binding.target_file_id.ToString(CultureInfo.InvariantCulture) + ", guid: " + binding.target_guid + ", type: 3}";
            Assert.That(Regex.Matches(originalText, "^" + Regex.Escape(from) + "\\r?$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            Assert.That(bound.pointers_rewritten, Is.EqualTo(1));
            CollectionAssert.AreEqual(new UTF8Encoding(false, true).GetBytes(originalText.Replace(from, to)), converted, "Only the qualified m_Script pointer changes; every other byte survives.");
            Assert.That(Regex.Matches(originalText, "^--- !u!114 &11400000\\r?$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            Assert.That(Scalar(originalText, "m_Name"), Is.EqualTo(" " + name));
            Object loaded = AssetDatabase.LoadAssetAtPath(importedPath, type);
            Assert.That(loaded, Is.Not.Null); Assert.That(loaded.GetType(), Is.EqualTo(type));
            Assert.That(AssetDatabase.GetAssetPath(loaded), Is.EqualTo(importedPath));
            string actualGuid; long actualId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(loaded, out actualGuid, out actualId), Is.True);
            Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(actualId, Is.EqualTo(11400000));
            var serialized = new SerializedObject(loaded); observed.Serialized.Add(serialized); serialized.Update();
            Assert.That(Require(serialized, "m_Script", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.EqualTo(script));
            Assert.That(MonoScript.FromScriptableObject((ScriptableObject)loaded), Is.EqualTo(script));
            return new Document { Raw = originalText, Loaded = loaded, Serialized = serialized };
        }

        private static void AssertCore(Document document, bool exported, bool diagnostics, Object basis,
            string baseGuid, string[] overrides, Object hashed, string hashedGuid)
        {
            AssertReference(document, "m_baseAsset", basis, baseGuid); AssertStrings(document, "m_overriddenFields", overrides);
            AssertBoolean(document, "m_useExportedScenes", exported);
            AssertBoolean(document, "m_missionIntrosEnabled", false);
            AssertBoolean(document, "m_useFastValidation", true);
            AssertBoolean(document, "m_gameCenter", true);
            AssertEnum(document, "m_replayMode", 1122295862);
            AssertBoolean(document, "m_allowUnityShaderDebuggingWindow", false);
            AssertBoolean(document, "m_sendCrashReports", diagnostics);
            AssertReference(document, "m_runtimeIssueProfile", null, null);
            AssertBoolean(document, "m_deviceConsoleEnabled", diagnostics);
            AssertBoolean(document, "m_logHandlerEnabled", diagnostics);
            AssertBoolean(document, "m_calculateFPSCounter", diagnostics);
            AssertBoolean(document, "m_tapToSkipEnabled", false);
            AssertBoolean(document, "m_showGameCenterChallenges", false);
            AssertEnum(document, "m_challengeManagerIntegrationType", 0);
            AssertReference(document, "m_hashedComponentGroupDefaults", hashed, hashedGuid);
            Assert.That(document.Serialized.FindProperty("m_applicationVersionDefinition"), Is.Null, "The stale override string does not create a field.");
            Assert.That(document.Serialized.FindProperty("m_guid"), Is.Null);
        }

        private static void AssertLocalisation(Document document)
        {
            AssertReference(document, "m_baseAsset", null, null); AssertStrings(document, "m_overriddenFields", LocOverrides);
            AssertString(document, "m_localisedDefinitionsDirectory", "LanguageStrings");
            AssertString(document, "m_nodeDataExportPath", "../../submodules/HLLocalisation/DataGeneration/Src/Utils/data_export/full_data_export.js");
            AssertString(document, "m_charactersOutputDirectory", "/Data/Localisation/");
            AssertBoolean(document, "m_enableEditorDataGeneration", true);
            AssertBoolean(document, "m_enableLocalisedUIStringFunctionality", true);
            Assert.That(document.Serialized.FindProperty("m_guid"), Is.Null);
        }

        private static SerializedProperty Require(SerializedObject owner, string name, SerializedPropertyType type)
        {
            SerializedProperty value = owner.FindProperty(name);
            Assert.That(value, Is.Not.Null, "Required original field: " + name);
            Assert.That(value.propertyType, Is.EqualTo(type), name); return value;
        }
        private static void AssertBoolean(Document document, string name, bool expected)
        {
            Assert.That(Scalar(document.Raw, name), Is.EqualTo(expected ? " 1" : " 0"));
            Assert.That(Require(document.Serialized, name, SerializedPropertyType.Boolean).boolValue, Is.EqualTo(expected), name);
        }
        private static void AssertEnum(Document document, string name, int expected)
        {
            Assert.That(Scalar(document.Raw, name), Is.EqualTo(" " + expected.ToString(CultureInfo.InvariantCulture)));
            Assert.That(Require(document.Serialized, name, SerializedPropertyType.Enum).intValue, Is.EqualTo(expected), "Original raw enum integer: " + name);
        }
        private static void AssertString(Document document, string name, string expected)
        {
            Assert.That(Scalar(document.Raw, name), Is.EqualTo(" " + expected));
            Assert.That(Require(document.Serialized, name, SerializedPropertyType.String).stringValue, Is.EqualTo(expected), name);
        }
        private static void AssertReference(Document document, string name, Object expected, string guid)
        {
            Assert.That(Scalar(document.Raw, name), Is.EqualTo(" " + (guid == null ? "{fileID: 0}" : Pointer(guid))));
            Object actual = Require(document.Serialized, name, SerializedPropertyType.ObjectReference).objectReferenceValue;
            Assert.That(actual, Is.EqualTo(expected), name);
            if (guid != null)
            {
                string actualGuid; long id;
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual, out actualGuid, out id), Is.True);
                Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(id, Is.EqualTo(11400000));
            }
        }
        private static void AssertStrings(Document document, string name, string[] expected)
        {
            CollectionAssert.AreEqual(expected, Sequence(document.Raw, name));
            SerializedProperty array = Require(document.Serialized, name, SerializedPropertyType.Generic);
            Assert.That(array.isArray, Is.True); Assert.That(array.arraySize, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                Assert.That(entry.propertyType, Is.EqualTo(SerializedPropertyType.String)); Assert.That(entry.stringValue, Is.EqualTo(expected[i]));
            }
        }
        private static string Scalar(string text, string name)
        {
            MatchCollection matches = Regex.Matches(text, "^  " + Regex.Escape(name) + ":([^\\r\\n]*)\\r?$", RegexOptions.Multiline);
            Assert.That(matches.Count, Is.EqualTo(1), "One exact authored field: " + name); return matches[0].Groups[1].Value;
        }
        private static string[] Sequence(string text, string name)
        {
            string value = Scalar(text, name);
            if (value == " []") return new string[0];
            Assert.That(value, Is.EqualTo(""));
            string[] lines = text.Split('\n'); var result = new List<string>(); int found = -1;
            for (int i = 0; i < lines.Length; i++) if (lines[i].TrimEnd('\r') == "  " + name + ":") found = i;
            Assert.That(found, Is.GreaterThanOrEqualTo(0));
            for (int i = found + 1; i < lines.Length && lines[i].StartsWith("  - ", StringComparison.Ordinal); i++) result.Add(lines[i].TrimEnd('\r').Substring(4));
            Assert.That(result.Count, Is.GreaterThan(0)); return result.ToArray();
        }
        private static string Pointer(string guid) => "{fileID: 11400000, guid: " + guid + ", type: 2}";
        private static string MetaGuid(byte[] bytes)
        {
            MatchCollection matches = Regex.Matches(new UTF8Encoding(false, true).GetString(bytes), "^guid: ([0-9a-f]{32})\\r?$", RegexOptions.Multiline);
            Assert.That(matches.Count, Is.EqualTo(1)); return matches[0].Groups[1].Value;
        }
        private static bool IsHash(string value) => value != null && Regex.IsMatch(value, "^[0-9a-f]{64}$");
        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string CheckedPath(string ownedRoot, string candidate)
        {
            string root = Path.GetFullPath(ownedRoot); string path = Path.GetFullPath(candidate);
            Assert.That(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True, "Evidence stays beneath its declared root.");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
                if (File.Exists(part) || Directory.Exists(part))
                    Assert.That((int)(File.GetAttributes(part) & FileAttributes.ReparsePoint), Is.EqualTo(0), "Evidence cannot follow symlinks.");
            return path;
        }
    }
}
