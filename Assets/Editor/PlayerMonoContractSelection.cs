#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Native query evidence only. The returned directories are observed values;
    // no runtime library is loaded and no contract/layout equivalence is assumed.
    internal static class PlayerMonoContractSelection
    {
        [Serializable]
        internal sealed class MethodReceipt
        {
            public string declaring_type;
            public string method;
            public string token;
            public int attributes;
            public int implementation_attributes;
            public string parameter_type;
            public string return_type;
        }
        [Serializable]
        internal sealed class Receipt
        {
            public string unity_version;
            public string target;
            public string module_path;
            public string module_sha256;
            public string module_mvid;
            public string module_assembly;
            public int api_compatibility_value;
            public int scripting_backend_value;
            public string scripting_backend_name;
            public bool runtime_selection_verified;
            public string api_compatibility_name;
            public string compatibility_profile_folder;
            public string mono_runtime_lib_directory;
            public string platform_profile_suffix;
            public MethodReceipt[] methods;
            public bool runtime_assemblies_loaded;
            public bool build_player_called;
            public bool layout_approved;
            public bool gameplay_verified;
        }

        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static string Hash(string path)
        {
            CheckPath(path);
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static void CheckPath(string path)
        {
            if (String.IsNullOrEmpty(path) || !Path.IsPathRooted(path) || Path.GetFullPath(path) != path)
                throw new InvalidDataException("Native contract query returned a noncanonical path");
            for (string current = path; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symbolic link in native contract selection evidence");
        }
        private static MethodInfo Require(Type owner, string name, Type parameter, int token, int attributes)
        {
            MethodInfo value = owner.GetMethod(name, Flags, null, new[] { parameter }, null);
            if (value == null || value.ReturnType != typeof(string) || value.MetadataToken != token ||
                (int)value.Attributes != attributes || (int)value.GetMethodImplementationFlags() != 4096 || value.GetMethodBody() != null)
                throw new InvalidDataException("Installed native contract query declaration differs");
            return value;
        }
        private static MethodReceipt Describe(MethodInfo value) => new MethodReceipt
        {
            declaring_type = value.DeclaringType.FullName, method = value.Name,
            token = "0x" + value.MetadataToken.ToString("x8"), attributes = (int)value.Attributes,
            implementation_attributes = (int)value.GetMethodImplementationFlags(),
            parameter_type = value.GetParameters()[0].ParameterType.FullName, return_type = value.ReturnType.FullName
        };

        // Caller supplies its already sealed installed Editor Core identity.
        // Include this result in the pre/post player compilation evidence.
        internal static Receipt Capture(BuildTarget target, string expectedPath, string expectedSha256, string expectedMvid)
        {
            if (Application.unityVersion != "2022.3.54f1" ||
                (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64))
                throw new InvalidDataException("Unsupported installed native contract selection context");
            Module module = typeof(BuildPipeline).Module;
            if (module.FullyQualifiedName != expectedPath || module.ModuleVersionId.ToString() != expectedMvid || Hash(expectedPath) != expectedSha256)
                throw new InvalidDataException("Loaded Editor Core differs from sealed native query module");
            Type discovery = module.Assembly.GetType("UnityEditor.BuildTargetDiscovery", true);
            MethodInfo runtime = Require(typeof(BuildPipeline), "GetMonoRuntimeLibDirectory", typeof(BuildTarget), 0x06002264, 147);
            MethodInfo compatibility = Require(typeof(BuildPipeline), "CompatibilityProfileToClassLibFolder", typeof(ApiCompatibilityLevel), 0x06002265, 147);
            MethodInfo suffix = Require(discovery, "GetPlatformProfileSuffix", typeof(BuildTarget), 0x0600237b, 150);
            foreach (MethodInfo value in new[] { runtime, compatibility, suffix })
                if (value.Module != module) throw new InvalidDataException("Native query belongs to a different loaded module");
            ApiCompatibilityLevel api = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone);
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            if (backend != ScriptingImplementation.Mono2x)
                throw new InvalidDataException("Native Mono queries require the selected Mono backend");
            string runtimeDirectory = runtime.Invoke(null, new object[] { target }) as string;
            string folder = compatibility.Invoke(null, new object[] { api }) as string;
            string platform = suffix.Invoke(null, new object[] { target }) as string;
            CheckPath(runtimeDirectory);
            string contents = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(expectedPath)));
            if (!Directory.Exists(runtimeDirectory) || !runtimeDirectory.StartsWith(contents + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                String.IsNullOrEmpty(folder) || folder.Length > 256 || String.IsNullOrEmpty(platform) || platform.Length > 64 ||
                folder.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0 || platform.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0)
                throw new InvalidDataException("Native contract query returned an unsupported directory/profile value");
            if (Hash(expectedPath) != expectedSha256 || module.ModuleVersionId.ToString() != expectedMvid)
                throw new InvalidDataException("Native query module changed during contract selection");
            return new Receipt
            {
                unity_version = Application.unityVersion, target = target.ToString(), module_path = expectedPath,
                module_sha256 = expectedSha256, module_mvid = expectedMvid, module_assembly = module.Assembly.FullName,
                api_compatibility_value = (int)api, api_compatibility_name = api.ToString(),
                scripting_backend_value = (int)backend, scripting_backend_name = backend.ToString(), runtime_selection_verified = false,
                compatibility_profile_folder = folder, mono_runtime_lib_directory = runtimeDirectory, platform_profile_suffix = platform,
                methods = new[] { Describe(runtime), Describe(compatibility), Describe(suffix) },
                runtime_assemblies_loaded = false, build_player_called = false, layout_approved = false, gameplay_verified = false
            };
        }
    }
}
#endif
