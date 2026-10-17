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
                Func<string, string> document = (header, script) => header + "\nMonoBehaviour:\n" + optionalNulls + script + "\n  m_Name: Fixture\n  m_EditorClassIdentifier: \n";
                Func<string, string> yaml = body => "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + body;
                string bound = InstalledComponentPointer(out _, out _);

                Require(Scan(scan, directory, "bound.asset", yaml(document("--- !u!114 &11400000", bound)), out string[] bad, out int unresolved) == "complete" &&
                    bad.Length == 0 && unresolved == 0, "exact installed binding with optional null PPtrs is complete");
                Require(Scan(scan, directory, "optional.prefab", yaml("--- !u!1 &100\nGameObject:\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"),
                    out bad, out unresolved) == "complete" && bad.Length == 0 && unresolved == 0, "a document with only optional null PPtrs is complete");
                Require(Scan(scan, directory, "missing.asset", yaml(document("--- !u!114 &11400000", "  m_Script: {fileID: 0}")), out bad, out unresolved) == "incomplete" &&
                    unresolved == 1 && bad.SequenceEqual(new[] { "Assets/Recovered/missing.asset !u!114 &11400000: MonoScript pointer is null: m_Script: {fileID: 0}" }),
                    "data asset with a null script is incomplete and names its object");
                Require(Scan(scan, directory, "scene.unity", yaml(document("--- !u!114 &7", "  m_Script: {fileID: 0}") + document("--- !u!114 &-9", "  m_Script: {fileID: 0}") +
                    document("--- !u!114 &8", bound)), out bad, out unresolved) == "incomplete" && unresolved == 2 &&
                    bad.SequenceEqual(new[] { "Assets/Recovered/scene.unity !u!114 &-9: MonoScript pointer is null: m_Script: {fileID: 0}",
                        "Assets/Recovered/scene.unity !u!114 &7: MonoScript pointer is null: m_Script: {fileID: 0}" }),
                    "each null-script component is reported once beside a bound one");
            }
            finally { Directory.Delete(directory, true); }
            Debug.Log("[Project Lucid] Recovered YAML reference status checks passed: 4");
        }

        private static string Scan(MethodInfo scan, string directory, string name, string text, out string[] invalidScriptBindings, out int unresolvedReferences)
        {
            string file = Path.Combine(directory, name);
            File.WriteAllText(file, text);
            object[] arguments = { file, "Assets/Recovered/" + name, null, null };
            string status = (string)scan.Invoke(null, arguments);
            invalidScriptBindings = (string[])arguments[2];
            unresolvedReferences = (int)arguments[3];
            return status;
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
