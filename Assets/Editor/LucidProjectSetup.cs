using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class LucidProjectSetup
    {
        public static void ApplyIdentity()
        {
            PlayerSettings.companyName = "Camo";
            PlayerSettings.productName = "Project Lucid";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "dev.camo.projectlucid");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("Project Lucid identity configured; local store root: " + Application.persistentDataPath);
        }
    }
}
