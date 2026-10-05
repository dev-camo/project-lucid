using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid
{
    public static class ScriptReferenceVerification
    {
        public static void Run()
        {
            // Audit helper is in Unity's default Editor assembly; this proof's
            // asmdef accesses the public entry point without a compile dependency.
            Type audit = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("ProjectLucid.Editor.LucidReferenceAudit"))
                .Single(type => type != null);
            MethodInfo method = audit.GetMethod("GetScriptBindingError", BindingFlags.Static | BindingFlags.Public);
            Func<string, string> error = line => (string)method.Invoke(null, new object[] { line });
            MonoScript script = MonoImporter.GetAllRuntimeMonoScripts()
                .Single(candidate => candidate.GetClass()?.FullName == "Cinemachine.CinemachinePipeline");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long id), "installed marker has an exact asset identity");
            string pointer = "  m_Script: {fileID: " + id + ", guid: " + guid + ", type: 3}";
            Require(error(pointer) == null, "actual installed component pointer resolves");
            Require(error(pointer.Replace("fileID: " + id + ",", "fileID: " + (id + 1) + ",")) != null, "correct GUID with wrong local ID is rejected");
            Require(error(pointer.Replace("fileID: " + id + ",", "fileID: 4294967297,")) != null, "64-bit IDs cannot truncate into another subasset");
            Require(error(pointer.Replace("type: 3", "type: 2")) != null, "wrong pointer type is rejected");
            Require(error("m_Script: {fileID: 0}") == null, "explicit null script pointer is accepted as null");
            Require(error("m_Script: {fileID: 11500000, guid: incomplete, type: 3}") != null, "malformed pointer is reported");
            MonoScript plain = AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/com.hardlight.hlunitycore/Runtime/HLPropertyStore.cs");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(plain, out guid, out id), "plain maintained class has script identity");
            Require(error("m_Script: {fileID: " + id + ", guid: " + guid + ", type: 3}") != null, "plain class cannot satisfy a serialized component pointer");
            Debug.Log("[Project Lucid] Exact MonoScript GUID/local-ID/class checks passed: 9");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
