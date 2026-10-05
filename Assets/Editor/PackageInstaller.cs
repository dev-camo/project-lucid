using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class PackageInstaller
    {
        private static readonly string[] Packages =
        {
            "com.unity.addressables@1.22.3",
            "com.unity.render-pipelines.universal@14.0.11",
            "com.unity.render-pipelines.core@14.0.11",
            "com.unity.shadergraph@14.0.11",
            "com.unity.textmeshpro@3.2.0-pre.4",
            "com.unity.ugui@1.0.0",
            // Native Confiner padding, exact transform equality and
            // GetPositionAndRotation calls match the 2.10.3 runtime source.
            "com.unity.cinemachine@2.10.3",
            // The original contains Unity.Timeline. Patch provenance remains
            // under comparison; this compatible candidate enables its shipped
            // Cinemachine Timeline branch for field and behavior verification.
            "com.unity.timeline@1.7.6",
            "com.unity.test-framework@1.1.33"
        };
        private static AddAndRemoveRequest request;
        private static double deadline;

        // Invoke the Editor directly without -quit: package requests complete on
        // later Editor update ticks. This method owns shutdown after resolution.
        public static void Install()
        {
            Debug.Log("[Project Lucid] Resolving " + string.Join(", ", Packages));
            request = Client.AddAndRemove(Packages);
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("[Project Lucid] Package resolution timed out.");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                Debug.LogError("[Project Lucid] Package resolution failed: " + request.Error.message);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[Project Lucid] Packages resolved: " + string.Join(", ", request.Result.Select(p => p.name + "@" + p.version)));
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
