using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class LucidProjectSetup
    {
        public static void ApplyIdentity()
        {
            PlayerSettings.companyName = "Project Lucid";
            PlayerSettings.productName = "Project Lucid";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.project.lucid");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("Project Lucid identity configured; local store root: " + Application.persistentDataPath);
        }
    }
}
