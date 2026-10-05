using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class LucidBuild
    {
        [Serializable]
        private sealed class Readiness
        {
            public string status;
            public string stage;
            public string generated_by;
            public string source_fingerprint;
            public string prepared_asset_fingerprint;
        }

        // CLI and Editor entry points share the release guard. A compile-only
        // exported project must not accidentally ship as reconstructed gameplay.
        public static void Build()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            if (Application.unityVersion != "2022.3.54f1")
                throw new InvalidOperationException("Build with Unity 2022.3.54f1.");
            var arguments = Environment.GetCommandLineArgs();
            string receipt = Argument(arguments, "-lucidValidation");
            var readiness = File.Exists(receipt) ? JsonUtility.FromJson<Readiness>(File.ReadAllText(receipt)) : null;
            if (readiness == null || readiness.status != "complete" || readiness.stage != "release" ||
                readiness.generated_by != "ProjectLucid.validate_project" ||
                readiness.source_fingerprint != LucidArtifactIdentity.Fingerprint(root, false) ||
                readiness.prepared_asset_fingerprint != LucidArtifactIdentity.Fingerprint(root, true))
                throw new InvalidOperationException("Run tools/lucid.py validate after completing required test and asset checks; release build is blocked.");
            var targetName = Argument(arguments, "-lucidTarget");
            var output = Argument(arguments, "-lucidOutput");
            var target = targetName == "macos" ? BuildTarget.StandaloneOSX :
                targetName == "windows" ? BuildTarget.StandaloneWindows64 :
                targetName == "linux" ? BuildTarget.StandaloneLinux64 :
                throw new ArgumentException("Target must be macos, windows, or linux.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No recovered startup scene has been configured.");

            LucidProjectSetup.ApplyIdentity();
            if (target == BuildTarget.StandaloneOSX) PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 2);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + result.summary.result);
            Debug.Log("[Project Lucid] Build succeeded: " + output);
        }

        private static string Argument(string[] arguments, string key)
        {
            var index = Array.IndexOf(arguments, key);
            if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("Missing " + key);
            return arguments[index + 1];
        }
    }
}
