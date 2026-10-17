using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class ScriptReferenceVerification
    {
        // Audit helpers are in Unity's default Editor assembly; this proof's
        // asmdef reaches their public entry points without a compile dependency.
        private static MethodInfo AuditMethod(string name)
        {
            Type audit = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("ProjectLucid.Editor.LucidReferenceAudit"))
                .Single(type => type != null);
            return audit.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        }

        private static string InstalledComponentPointer(out string guid, out long id)
        {
            MonoScript script = MonoImporter.GetAllRuntimeMonoScripts()
                .Single(candidate => candidate.GetClass()?.FullName == "Cinemachine.CinemachinePipeline");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out id), "installed marker has an exact asset identity");
            return "  m_Script: {fileID: " + id + ", guid: " + guid + ", type: 3}";
        }

        public static void Run()
        {
            MethodInfo method = AuditMethod("GetScriptBindingError");
            Func<string, string> error = line => (string)method.Invoke(null, new object[] { line });
            string pointer = InstalledComponentPointer(out string guid, out long id);
            Require(error(pointer) == null, "actual installed component pointer resolves");
            Require(error(pointer.Replace("fileID: " + id + ",", "fileID: " + (id + 1) + ",")) != null, "correct GUID with wrong local ID is rejected");
            Require(error(pointer.Replace("fileID: " + id + ",", "fileID: 4294967297,")) != null, "64-bit IDs cannot truncate into another subasset");
            Require(error(pointer.Replace("type: 3", "type: 2")) != null, "wrong pointer type is rejected");
            Require(error("m_Script: {fileID: 0}") != null, "null MonoScript pointer is rejected");
            Require(error("m_Script: {fileID: 11500000, guid: incomplete, type: 3}") != null, "malformed pointer is reported");
            MonoScript plain = AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/com.hardlight.hlunitycore/Runtime/HLPropertyStore.cs");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(plain, out guid, out id), "plain maintained class has script identity");
            Require(error("m_Script: {fileID: " + id + ", guid: " + guid + ", type: 3}") != null, "plain class cannot satisfy a serialized component pointer");
            Debug.Log("[Project Lucid] Exact MonoScript GUID/local-ID/class checks passed: 9");
        }

        // Whole-file audit path: a fixture YAML document outside Assets/Recovered
        // runs through the same line scan and status rule as LucidReferenceAudit.Run.
        public static void RunRecoveredYaml()
        {
            MethodInfo scan = AuditMethod("ScanSerializedFile");
            string directory = Path.Combine(Path.GetTempPath(), "lucid-reference-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string optionalNulls =
                    "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                    "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n";
                Func<string, string, string> document = (header, script) => header + "\nMonoBehaviour:\n" + optionalNulls + script + "\n  m_Name: Fixture\n  m_EditorClassIdentifier: \n";
                Func<string, string> yaml = body => "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + body;
                string bound = InstalledComponentPointer(out _, out _);

                Require(Scan(scan, directory, "bound.asset", yaml(document("--- !u!114 &11400000", bound)), out string[] bad, out string[] objects, out int unresolved) == "complete" &&
                    bad.Length == 0 && objects.Length == 0 && unresolved == 0, "exact installed binding with optional null PPtrs is complete");
                Require(Scan(scan, directory, "optional.prefab", yaml("--- !u!1 &100\nGameObject:\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"),
                    out bad, out objects, out unresolved) == "complete" && bad.Length == 0 && objects.Length == 0 && unresolved == 0, "a document with only optional null PPtrs is complete");
                Require(Scan(scan, directory, "missing.asset", yaml(document("--- !u!114 &11400000", "  m_Script: {fileID: 0}")), out bad, out objects, out unresolved) == "incomplete" &&
                    unresolved == 1 && objects.Length == 0 && bad.SequenceEqual(new[] { "Assets/Recovered/missing.asset !u!114 &11400000: MonoScript pointer is null: m_Script: {fileID: 0}" }),
                    "data asset with a null script is incomplete and names its object");
                Require(Scan(scan, directory, "scene.unity", yaml(document("--- !u!114 &7", "  m_Script: {fileID: 0}") + document("--- !u!114 &-9", "  m_Script: {fileID: 0}") +
                    document("--- !u!114 &8", bound)), out bad, out objects, out unresolved) == "incomplete" && unresolved == 2 && objects.Length == 0 &&
                    bad.SequenceEqual(new[] { "Assets/Recovered/scene.unity !u!114 &-9: MonoScript pointer is null: m_Script: {fileID: 0}",
                        "Assets/Recovered/scene.unity !u!114 &7: MonoScript pointer is null: m_Script: {fileID: 0}" }),
                    "each null-script component is reported once beside a bound one");
            }
            finally { Directory.Delete(directory, true); }
            Debug.Log("[Project Lucid] Recovered YAML reference status checks passed: 4");
        }

        private static string Scan(MethodInfo scan, string directory, string name, string text, out string[] invalidScriptBindings,
            out string[] unresolvedObjectReferences, out int unresolvedReferences)
        {
            string file = Path.Combine(directory, name);
            File.WriteAllText(file, text);
            object[] arguments = { file, "Assets/Recovered/" + name, null, null, null };
            string status = (string)scan.Invoke(null, arguments);
            invalidScriptBindings = (string[])arguments[2];
            unresolvedObjectReferences = (string[])arguments[3];
            unresolvedReferences = (int)arguments[4];
            return status;
        }

        // Non-script PPtrs: an owned material, its subasset and its folder supply
        // identities that Unity itself assigns; each YAML fixture names one of them.
        // LucidReferenceAudit.Run scans the whole prepared Assets/Recovered tree and
        // writes the release report, so it is exercised by the native audit run of a
        // clean prepare, not by this owned fixture.
        public static void RunObjectPointers()
        {
            MethodInfo scan = AuditMethod("ScanSerializedFile");
            string directory = Path.Combine(Path.GetTempPath(), "lucid-reference-audit-" + Guid.NewGuid().ToString("N"));
            string folder = "Assets/ProjectLucidOwnedReferenceAudit_" + Guid.NewGuid().ToString("N");
            bool created = false;
            Material main = null, sub = null;
            Exception primary = null;
            try
            {
                Directory.CreateDirectory(directory);
                Require(!AssetDatabase.IsValidFolder(folder), "owned fixture folder is new");
                string folderGuid = AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
                created = !string.IsNullOrEmpty(folderGuid);
                Require(created && AssetDatabase.GUIDToAssetPath(folderGuid) == folder, "owned fixture folder is imported");
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                Require(shader != null, "builtin fixture shader is available");
                string path = folder + "/OwnedMaterial.mat";
                main = new Material(shader);
                AssetDatabase.CreateAsset(main, path);
                sub = new Material(shader) { name = "OwnedSubMaterial" };
                AssetDatabase.AddObjectToAsset(sub, main);
                AssetDatabase.SaveAssetIfDirty(main);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Material loadedMain = AssetDatabase.LoadMainAssetAtPath(path) as Material;
                Material loadedSub = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Single(material => material != loadedMain);
                Require(loadedMain != null, "owned material is the main asset");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(loadedMain, out string guid, out long mainID), "owned material has an exact asset identity");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(loadedSub, out string subGuid, out long subID) && subGuid == guid && subID != mainID,
                    "owned subasset shares the material GUID under its own local identifier");
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetDatabase.LoadMainAssetAtPath(folder), out string mainFolderGuid, out long folderID) &&
                    mainFolderGuid == folderGuid, "owned folder has an exact main-object identity");
                const long absentID = 999999999;
                Require(mainID != absentID && subID != absentID, "fixture identifiers differ from the absent local identifier");

                Func<string, string> yaml = body => "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + body;
                Func<long, string, string> renderer = (id, pointerGuid) => yaml("--- !u!23 &5\nMeshRenderer:\n  m_GameObject: {fileID: 0}\n  m_Materials:\n  - {fileID: " +
                    id + ", guid: " + pointerGuid + ", type: 2}\n");
                Require(Scan(scan, directory, "material.prefab", renderer(mainID, guid), out string[] bad, out string[] objects, out int unresolved) == "complete" &&
                    bad.Length == 0 && objects.Length == 0 && unresolved == 0, "exact material pointer is complete");
                Require(Scan(scan, directory, "wrong-id.prefab", renderer(absentID, guid), out bad, out objects, out unresolved) == "incomplete" &&
                    bad.Length == 0 && unresolved == 1 && objects.SequenceEqual(new[] {
                        "{fileID: 999999999, guid: " + guid + ", type: 2}: Local identifier does not resolve at " + path }),
                    "resolving GUID with an absent local identifier is incomplete and names the pointer");
                Require(Scan(scan, directory, "subasset.prefab", renderer(subID, guid), out bad, out objects, out unresolved) == "complete" &&
                    objects.Length == 0 && unresolved == 0, "exact subasset pointer is complete");
                Require(Scan(scan, directory, "folder.spriteatlas", yaml("--- !u!687078895 &4343727234628468602\nSpriteAtlas:\n  m_EditorData:\n    packables:\n    - {fileID: " +
                    folderID + ", guid: " + folderGuid + ", type: 3}\n"), out bad, out objects, out unresolved) == "complete" && objects.Length == 0 && unresolved == 0,
                    "folder main-object pointer is complete");
                Require(Scan(scan, directory, "local.prefab", yaml("--- !u!1 &1\nGameObject:\n  m_Component:\n  - component: {fileID: 2}\n  - component: {fileID: -3}\n" +
                    "  m_PrefabInstance: {fileID: 0}\n--- !u!4 &2\nTransform:\n  m_GameObject: {fileID: 1}\n--- !u!4 &-3 stripped\nTransform:\n  m_PrefabInstance: {fileID: 0}\n"),
                    out bad, out objects, out unresolved) == "complete" && objects.Length == 0 && unresolved == 0,
                    "earlier, later, stripped and null local pointers are complete");
                Require(Scan(scan, directory, "absent.prefab", yaml("--- !u!1 &1\nGameObject:\n  m_Component:\n  - component: {fileID: 2}\n  - component: {fileID: 77}\n" +
                    "--- !u!4 &2\nTransform:\n  m_GameObject: {fileID: 1}\n"), out bad, out objects, out unresolved) == "incomplete" && unresolved == 1 &&
                    objects.SequenceEqual(new[] { "Assets/Recovered/absent.prefab {fileID: 77}: Local identifier names no document in this file" }),
                    "local pointer without its document is incomplete");
                Require(Scan(scan, directory, "malformed.prefab", yaml("--- !u!23 &5\nMeshRenderer:\n  m_Materials:\n  - {fileID: " + mainID + ", guid: " + guid + "}\n"),
                    out bad, out objects, out unresolved) == "incomplete" && unresolved == 1 &&
                    objects.SequenceEqual(new[] { "Assets/Recovered/malformed.prefab {fileID: " + mainID + ", guid: " + guid + "}: Unsupported or malformed object pointer" }),
                    "pointer without its type is reported instead of passing");

                // Unity's emitter wraps a long flow mapping when it reserializes a file.
                Func<long, string> wrapped = id => yaml("--- !u!23 &5\nMeshRenderer:\n  m_Materials:\n  - {fileID: " + id + ", guid: " + guid + ",\n    type: 2}\n");
                Require(Scan(scan, directory, "wrapped.prefab", wrapped(mainID), out bad, out objects, out unresolved) == "complete" &&
                    objects.Length == 0 && unresolved == 0, "wrapped exact material pointer is complete");
                Require(Scan(scan, directory, "wrapped-wrong-id.prefab", wrapped(absentID), out bad, out objects, out unresolved) == "incomplete" &&
                    unresolved == 1 && objects.SequenceEqual(new[] { "{fileID: 999999999, guid: " + guid + ", type: 2}: Local identifier does not resolve at " + path }),
                    "wrapped pointer with an absent local identifier is checked whole");
                Require(Scan(scan, directory, "open.prefab", yaml("--- !u!23 &5\nMeshRenderer:\n  m_Materials:\n  - {fileID: " + mainID + ", guid: " + guid + ",\n" +
                    "--- !u!4 &6\nTransform:\n  m_GameObject: {fileID: 0}\n"), out bad, out objects, out unresolved) == "incomplete" && unresolved == 1 &&
                    objects.SequenceEqual(new[] { "Assets/Recovered/open.prefab {fileID: " + mainID + ", guid: " + guid + ",: Unsupported or malformed object pointer" }),
                    "a pointer left open before the next document is reported instead of joined");

                // Outside the project check: builtin resources and {fileID: 0} nulls.
                Require(Scan(scan, directory, "builtin.prefab", renderer(10303, "0000000000000000f000000000000000"), out bad, out objects, out unresolved) == "complete" &&
                    objects.Length == 0 && unresolved == 0, "builtin resource pointer is outside the project check");
                Require(Scan(scan, directory, "null-guid.prefab", renderer(0, guid), out bad, out objects, out unresolved) == "complete" &&
                    objects.Length == 0 && unresolved == 0, "null local identifier with a GUID is a null pointer");
                Require(Scan(scan, directory, "overflow.prefab", renderer(0, guid).Replace("{fileID: 0,", "{fileID: 9223372036854775808,"), out bad, out objects, out unresolved) == "incomplete" &&
                    unresolved == 1 && objects.SequenceEqual(new[] {
                        "Assets/Recovered/overflow.prefab {fileID: 9223372036854775808, guid: " + guid + ", type: 2}: Local identifier is not a signed 64-bit value" }),
                    "a local identifier above Int64.MaxValue is reported instead of wrapping");
                string missingGuid = Guid.NewGuid().ToString("N");
                Require(string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(missingGuid)), "fixture GUID is unassigned");
                Require(Scan(scan, directory, "missing-guid.prefab", renderer(mainID, missingGuid), out bad, out objects, out unresolved) == "incomplete" &&
                    unresolved == 1 && objects.Length == 0 && bad.Length == 0, "a missing GUID counts once, in missing_guids only");
            }
            catch (Exception exception)
            {
                primary = exception;
                throw;
            }
            finally
            {
                // Cleanup runs on every path; a cleanup failure never hides the first failure.
                var cleanup = new System.Collections.Generic.List<Exception>();
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch (Exception exception) { cleanup.Add(exception); }
                try { if (created && !AssetDatabase.DeleteAsset(folder)) cleanup.Add(new InvalidOperationException("owned fixture folder is removed")); }
                catch (Exception exception) { cleanup.Add(exception); }
                try
                {
                    if (sub != null && !AssetDatabase.Contains(sub)) UnityEngine.Object.DestroyImmediate(sub);
                    if (main != null && !AssetDatabase.Contains(main)) UnityEngine.Object.DestroyImmediate(main);
                }
                catch (Exception exception) { cleanup.Add(exception); }
                if (cleanup.Count > 0)
                    throw new AggregateException("Owned reference audit fixture cleanup failed",
                        primary == null ? cleanup : new[] { primary }.Concat(cleanup));
            }
            Debug.Log("[Project Lucid] Recovered YAML object pointer checks passed: 14");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
