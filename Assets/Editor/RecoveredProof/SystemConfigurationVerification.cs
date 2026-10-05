using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class SystemConfigurationVerification
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action callback, string message) where T : Exception
        { checks++; try { callback(); } catch (T) { return; } throw new InvalidOperationException(message); }
        private sealed class ConfigProbe : SystemConfigurationAsset
        {
            public override void Validate() { }
            public static string Resolve(string path, string relativeTo) => GetFullPath(path, relativeTo);
            public static bool DirectoryValid(string path, string relativeTo = "") => ValidateDirectory(path, relativeTo);
            public static bool FileValid(string path, string relativeTo = "") => ValidateFile(path, relativeTo);
            public static bool FilePresent(string path, string relativeTo = "") => FileExists(path, relativeTo);
        }
        public static void Run()
        {
            Execute(true);
            Debug.Log("Original configuration dependency verified checks=" + checks);
        }
        public static int RunManaged()
        {
            Execute(false); return checks;
        }
        private static void Execute(bool unity)
        {
            checks = 0;
            SystemConfiguration previous = ProcessManager.GetSystemSafe<SystemConfiguration>(autoRegister: false);
            try { VerifyPaths(); VerifyManagedConfigShape(); if (unity) VerifyUnityResourceBodies(); }
            finally
            {
                ProcessManager.UnregisterSystem<SystemConfiguration>();
                if (previous != null) ProcessManager.RegisterSystem(previous);
            }
        }
        private static void VerifyPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "ProjectLucid-configuration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string file = Path.Combine(root, "entry.txt"); File.WriteAllText(file, "original validation wrappers");
                Check(ConfigProbe.Resolve("///entry.txt", root) == file, "relative root strips leading slashes");
                Check(ConfigProbe.Resolve("folder/../entry.txt", root) == file, "normalization occurs after path combine");
                Check(ConfigProbe.Resolve(file, "") == file, "empty root retains absolute path");
                Check(ConfigProbe.DirectoryValid(root) && !ConfigProbe.DirectoryValid(file), "directory validator delegates existence");
                Check(ConfigProbe.FilePresent("/entry.txt", root) && ConfigProbe.FileValid("/entry.txt", root), "both file wrappers share existence behavior");
                Check(!ConfigProbe.FileValid("absent.txt", root), "missing file returns false");
                Throws<NullReferenceException>(() => ConfigProbe.Resolve(null, root), "nonblank-root null path fails during TrimStart");
                Throws<NullReferenceException>(() => ConfigProbe.Resolve(null, ""), "blank-root null path fails during Replace");
                Throws<ArgumentNullException>(() => ConfigProbe.Resolve(file, null), "null relative root is still passed into Path.Combine");
            }
            finally { Directory.Delete(root, true); }
        }
        private static void VerifyManagedConfigShape()
        {
            var runtime = (RuntimeConfiguration)FormatterServices.GetUninitializedObject(typeof(RuntimeConfiguration));
            FieldInfo field = typeof(RuntimeConfiguration).GetField("m_configurationAssets", BindingFlags.NonPublic | BindingFlags.Instance);
            Throws<ArgumentNullException>(() => { var unused = runtime.Assets; }, "null array has original Array.AsReadOnly failure");
            var config = (ConfigProbe)FormatterServices.GetUninitializedObject(typeof(ConfigProbe));
            SystemConfigurationAsset[] entries = { config, null };
            field.SetValue(runtime, entries);
            var first = runtime.Assets; var second = runtime.Assets;
            Check(!ReferenceEquals(first, second) && first.Count == 2, "asset getter creates separate wrappers around original array");
            Check(ReferenceEquals(first[0], config) && ReferenceEquals(first[1], null), "asset getter preserves null entries");
            entries[0] = null;
            Check(ReferenceEquals(first[0], null), "read-only wrapper observes backing array changes");
            Throws<NotSupportedException>(() => ((IList<SystemConfigurationAsset>)first)[0] = config, "asset wrapper forbids collection writes");
            var active = (ActiveRuntimeConfiguration)FormatterServices.GetUninitializedObject(typeof(ActiveRuntimeConfiguration));
            active.ActiveConfig = runtime;
            Check(ReferenceEquals(active.Config, runtime), "active Config getter returns exact assigned runtime object");
            Check(active.AssetLocation() == "Assets/Configuration/Resources/" && active.GetFileName() == "ActiveRuntimeConfiguration", "active wrapper returns original constants");
            Check(field.IsDefined(typeof(SerializeField), false) && !field.IsPublic, "private runtime array retains serialize field");
            Check(typeof(ActiveRuntimeConfiguration).GetField("ActiveConfig").IsDefined(typeof(SerializeField), false), "public active config retains original serialized attribute");
            Check(typeof(SystemConfigurationAsset).GetField("m_baseAsset", BindingFlags.NonPublic | BindingFlags.Instance).IsFamily, "base asset retains protected field visibility");
            Check(typeof(SystemConfigurationAsset).GetMethod("Validate").IsAbstract, "configuration validation remains authored abstract contract");
            var system = new SystemConfiguration();
            ProcessManager.RegisterSystem(system, canReplace: true);
            try
            {
                var handle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(config);
                int key = HLCRC32.GenerateInt(typeof(ConfigProbe).Name);
                Check(ReferenceEquals(system.StackableData.Get<SystemConfigurationAsset>(key), config), "base registration uses runtime simple type name");
                Check(ReferenceEquals(SystemConfiguration.GetConfig<ConfigProbe>(), config), "GetConfig resolves registered base container then casts requested type");
                var directDerived = SystemConfiguration.AddConfig(config, 923);
                Check(ReferenceEquals(system.StackableData.Get<ConfigProbe>(923), config), "explicit caller generic type is retained in stack");
                Check(!system.StackableData.TryGet(923, out SystemConfigurationAsset incompatible), "derived generic container doesn't convert to base container");
                Throws<NullReferenceException>(() => SystemConfiguration.AddConfig<ConfigProbe>(null, 123), "explicit CRC doesn't bypass original GetType read");
                Check(system.StackableData.HasData(923), "failure leaves existing registered data");
            }
            finally { ProcessManager.UnregisterSystem<SystemConfiguration>(); }
        }
        private static void VerifyUnityResourceBodies()
        {
            var runtime = ScriptableObject.CreateInstance<RuntimeConfiguration>();
            var active = ScriptableObject.CreateInstance<ActiveRuntimeConfiguration>();
            var config = ScriptableObject.CreateInstance<ConfigProbe>();
            ConfigProbe created = null;
            try
            {
                Check(typeof(SystemConfigurationAsset).GetField("m_overriddenFields", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(config) is List<string> list && list.Count == 0, "native configuration ctor creates empty override list");
                Throws<ArgumentNullException>(() => { var unused = runtime.Assets; }, "Unity-allocated runtime config retains null array default");
                active.ActiveConfig = runtime;
                Check(ReferenceEquals(active.Config, runtime), "real Unity active resource getter retains reference");
                var system = new SystemConfiguration();
                ProcessManager.RegisterSystem(system, canReplace: true);
                try
                {
                    created = SystemConfiguration.GetConfig<ConfigProbe>();
                    Check(created != null && !ReferenceEquals(created, config), "missing config creates requested Unity asset");
                    Check(ReferenceEquals(system.StackableData.Get<SystemConfigurationAsset>(HLCRC32.GenerateInt(typeof(ConfigProbe).Name)), created), "fallback creation registers base-typed container");
                    Check(ReferenceEquals(SystemConfiguration.GetConfig<ConfigProbe>(), created), "subsequent lookup returns previously created config");
                }
                finally { ProcessManager.UnregisterSystem<SystemConfiguration>(); }
                // Real Resource loading itself still requires the exact original asset binding, owned by the integration audit.
            }
            finally { if (created != null) UnityEngine.Object.DestroyImmediate(created); UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(active); UnityEngine.Object.DestroyImmediate(runtime); }
        }
    }
}
