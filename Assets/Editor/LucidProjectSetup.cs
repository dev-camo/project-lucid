using System;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class LucidProjectSetup
    {
        private const string Name = "Project Lucid";
        private const string Identifier = "com.project.lucid";
        private const ScriptingImplementation Backend = ScriptingImplementation.Mono2x;

        public static void ApplyIdentity()
        {
            PlayerSettings.companyName = Name;
            PlayerSettings.productName = Name;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, Identifier);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, Backend);
            AssetDatabase.SaveAssets();
            Debug.Log("Project Lucid identity configured; local store root: " + Application.persistentDataPath);
        }

        // Read-only counterpart for the release build, which must not rewrite
        // ProjectSettings after its fingerprint has been validated.
        public static void RequireIdentity()
        {
            if (PlayerSettings.companyName != Name || PlayerSettings.productName != Name ||
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone) != Identifier ||
                PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != Backend)
                throw new InvalidOperationException("ProjectSettings do not carry the Project Lucid identity; run ApplyIdentity before validation.");
        }
    }
}
