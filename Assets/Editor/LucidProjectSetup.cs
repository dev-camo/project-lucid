using System;
using UnityEditor;

namespace ProjectLucid.Editor
{
    public static class LucidProjectSetup
    {
        private const string Name = "Project Lucid";
        private const string Identifier = "com.project.lucid";
        private const ScriptingImplementation Backend = ScriptingImplementation.Mono2x;

        // The tracked ProjectSettings.asset carries the identity and is covered by the
        // release fingerprint, so the build checks it rather than re-saving it.
        public static void RequireIdentity()
        {
            if (PlayerSettings.companyName != Name || PlayerSettings.productName != Name ||
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone) != Identifier ||
                PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != Backend)
                throw new InvalidOperationException("The tracked ProjectSettings.asset lost the Project Lucid identity; release build is blocked.");
        }
    }
}
