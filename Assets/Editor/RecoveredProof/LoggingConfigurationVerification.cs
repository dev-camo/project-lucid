using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class LoggingConfigurationVerification
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException(message);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Set(object instance, string name, object value) => Field(instance.GetType(), name).SetValue(instance, value);
        private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        // The source-only portion deliberately avoids invoking engine object
        // construction or Unity object-null. Actual Unity Run adds those paths.
        public static int RunManaged()
        {
            checks = 0;
            foreach (var pair in new[] { Tuple.Create(typeof(AndroidConfigurationAsset), 5), Tuple.Create(typeof(HLUnityCoreConfigurationAsset), 6), Tuple.Create(typeof(HLUnityCoreConfigurationAsset.LowMemoryConfiguration), 5), Tuple.Create(typeof(iOSConfigurationAsset), 6), Tuple.Create(typeof(MacOSConfigurationAsset), 3) })
            {
                Check(pair.Item1.Assembly.GetName().Name == "HLUnityCore.Runtime", "original config assembly identity");
                Check(pair.Item1.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == pair.Item2, "all original config fields without additions");
                Check(pair.Item1.IsPublic || pair.Item1.IsNestedPublic, "original public config API");
            }
            Check(typeof(AndroidConfigurationAsset).BaseType == typeof(SystemConfigurationAsset), "original Android configuration base");
            Check(typeof(HLUnityCoreConfigurationAsset).BaseType == typeof(SystemConfigurationAsset), "original Core configuration base");
            Check(typeof(iOSConfigurationAsset).BaseType == typeof(SystemConfigurationAsset), "original iOS configuration base");
            Check(typeof(MacOSConfigurationAsset).BaseType == typeof(SystemConfigurationAsset), "original macOS configuration base");
            Check(typeof(IGradleFileConfig).IsAssignableFrom(typeof(AndroidConfigurationAsset)), "original Gradle contract");
            Check(typeof(IModuleConfigurationWithFileList).IsAssignableFrom(typeof(iOSConfigurationAsset)) && typeof(IModuleConfigurationWithFileList).IsAssignableFrom(typeof(MacOSConfigurationAsset)), "original native-file contracts");
            Check(typeof(HLUnityCoreConfigurationAsset).GetProperty("macOSConfig") != null && typeof(HLUnityCoreConfigurationAsset).GetProperty("MacOSConfig") == null, "original lower-case macOS property identity");

            var low = new HLUnityCoreConfigurationAsset.LowMemoryConfiguration();
            Check(low.Enabled, "original low-memory default enabled");
            Check(low.BreadcrumbMessage == "Low Memory Warning", "original low-memory message");
            Check(low.MinimumLogIntervalSeconds == 5f, "original low-memory interval seconds");
            Check(low.LogBreadcrumbMessageAsWarning && low.TriggerResourcesUnloadAssets, "retained warning/unload defaults");
            Set(low, "m_enabled", false); Set(low, "m_breadcrumbMessage", null); Set(low, "m_minimumLogIntervalSeconds", -2.5f);
            Set(low, "m_logBreadcrumbMessageAsWarning", false); Set(low, "m_triggerResourcesUnloadAssets", false);
            Check(!low.Enabled && low.BreadcrumbMessage == null && low.MinimumLogIntervalSeconds == -2.5f && !low.LogBreadcrumbMessageAsWarning && !low.TriggerResourcesUnloadAssets, "low-memory getters expose original fields unchanged");
            var core = Uninitialized<HLUnityCoreConfigurationAsset>();
            Check(Field(typeof(HLUnityCoreConfigurationAsset), "m_lowMemoryConfiguration").GetValue(core) == null, "source fixture begins with genuine CLR-null low-memory field");
            var created = core.LowMemoryConfig;
            Check(created != null && created.Enabled && created.BreadcrumbMessage == "Low Memory Warning", "low-memory getter lazily creates exact original defaults");
            Check(ReferenceEquals(created, core.LowMemoryConfig), "low-memory getter preserves existing reference");
            Set(core, "m_lowMemoryConfiguration", low);
            Check(ReferenceEquals(low, core.LowMemoryConfig), "low-memory getter returns authored object unchanged");
            Set(core, "m_logInPlaceOfException", true); Set(core, "m_allowErrorLoggingInDistribution", false);
            Check(core.LogInPlaceOfException && !core.AllowErrorLoggingInDistribution, "Core flag getters expose actual fields");
            core.Validate(); Check(true, "Core retail validation RET");

            var android = Uninitialized<AndroidConfigurationAsset>();
            Set(android, "m_gradleTemplateFileOverride", "template"); Set(android, "m_gradleFileOverride", "generated"); Set(android, "m_pathToUnityPlayerActivitySourceFile", null); Set(android, "m_unityPlayerActivityFileOverride", "activity"); Set(android, "m_gradlePluginDependencyImplementation", "dependency");
            Check(android.PathToGradleTemplateFile == "template" && android.PathToGradleFile == "generated", "Gradle override getter transport");
            Check(android.PathToUnityPlayerActivitySourceFile == null && android.UnityPlayerActivityFileOverride == "activity" && android.GradlePluginDependencyImplementation == "dependency", "Android remaining getters transport unchanged");
            Check(((IGradleFileConfig)android).DefaultGradlePath == "/NativeAndroid~/build.gradle", "original default Gradle path body");
            Check(((IGradleFileConfig)android).DefaultGradleTemplatePath == "/NativeAndroid~/build.2020gradle", "original default Gradle template body");

            var ios = Uninitialized<iOSConfigurationAsset>();
            Set(ios, "m_requiredNativeFiles", new[] { "required", "overlap", null }); Set(ios, "m_includedNativeFiles", new[] { "overlap", "extra", null });
            Set(ios, "m_requiredFrameworks", new[] { "base", "both" }); Set(ios, "m_includedFrameworks", new[] { "both", "more" });
            string[] ld = { "-objc", "-force_load" }; Set(ios, "m_mainTargetOtherLdflags", ld); Set(ios, "m_nativeCodePath", null);
            var files = ios.IncludedNativeFiles;
            Check(files is HashSet<string> && new HashSet<string>(files).SetEquals(new[] { "required", "overlap", "extra", null }), "original iOS HashSet merge/default comparer/null elements");
            Check(!ReferenceEquals(files, ios.IncludedNativeFiles), "iOS native list getter allocates every call");
            Check(new HashSet<string>(ios.GetIncludedNativeFiles()).SetEquals(files), "iOS interface route delegates property");
            Check(ios.IncludedFrameworks is HashSet<string> && new HashSet<string>(ios.IncludedFrameworks).SetEquals(new[] { "base", "both", "more" }), "original framework merge");
            Check(ReferenceEquals(ld, ios.MainTargetOtherLdflags) && ios.GetNativeCodePath() == null, "original linker array alias/path transport");
            ios.Validate(); Check(true, "iOS retail validation RET");
            var mac = Uninitialized<MacOSConfigurationAsset>();
            Set(mac, "m_requiredNativeFiles", new[] { "A", "B" }); Set(mac, "m_includedNativeFiles", new[] { "B", "b" }); Set(mac, "m_nativeCodePath", "exact");
            Check(mac.GetIncludedNativeFiles() is HashSet<string> && new HashSet<string>(mac.GetIncludedNativeFiles()).SetEquals(new[] { "A", "B", "b" }), "macOS original merge/default case sensitivity");
            Check(!ReferenceEquals(mac.GetIncludedNativeFiles(), mac.GetIncludedNativeFiles()), "macOS merge allocates every call");
            Check(mac.GetNativeCodePath() == "exact", "macOS original path transport");
            mac.Validate(); Check(true, "macOS retail validation RET");
            return checks;
        }

        public static void Run()
        {
            RunManaged();
            var core = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            AndroidConfigurationAsset android = null; iOSConfigurationAsset ios = null; MacOSConfigurationAsset mac = null;
            try
            {
                Check(core.AllowErrorLoggingInDistribution && !core.LogInPlaceOfException, "actual Core constructor flag defaults");
                Check(Field(typeof(HLUnityCoreConfigurationAsset), "m_lowMemoryConfiguration").GetValue(core) == null, "actual constructor leaves low memory lazy");
                Check(Field(typeof(HLUnityCoreConfigurationAsset), "m_gradleConfigurationAsset").GetValue(core) == null && Field(typeof(HLUnityCoreConfigurationAsset), "m_iOSConfigurationAsset").GetValue(core) == null && Field(typeof(HLUnityCoreConfigurationAsset), "m_macOSConfigurationAsset").GetValue(core) == null, "actual constructor leaves three platform assets lazy");
                android = core.AndroidConfig; ios = core.iOSConfig; mac = core.macOSConfig;
                Check(android != null && ios != null && mac != null, "actual getters create genuine platform configuration assets");
                Check(ReferenceEquals(android, core.AndroidConfig) && ReferenceEquals(ios, core.iOSConfig) && ReferenceEquals(mac, core.macOSConfig), "actual getters retain live assets");
                Check(android.PathToGradleTemplateFile == null && android.PathToGradleFile == null && android.UnityPlayerActivityFileOverride == null, "Android original null override defaults");
                Check(android.PathToUnityPlayerActivitySourceFile == "/unityLibrary/src/main/java/com/unity3d/player/UnityPlayerActivity.java" && android.GradlePluginDependencyImplementation == "implementation project(path: ':Plugin')", "Android exact original constructor strings");
                android.Validate(); Check(true, "actual Android empty overrides skip engine file checks");
                Check(ios.GetNativeCodePath() == @"/NativeiOS\~/" && mac.GetNativeCodePath() == @"/NativeMacOS\~/HLMacCore/", "native platform paths retain original backslash/tilde characters");
                string[] requiredIOS = { "HLUnityCore.h", "HLUnityCore.mm", "HLOutput.h", "HLOutput.m", "CoreUtilities.h", "CoreUtilities.mm", "IOSAntipiracy.h", "IOSAntipiracy_stubbed.cpp", "ApplicationCore.h", "ApplicationCore.mm", "ApplicationModuleBase.h", "ApplicationModuleBase.mm", "HLHealthMonitor.h", "HLHealthMonitor.mm" };
                Check(((string[])Field(typeof(iOSConfigurationAsset), "m_requiredNativeFiles").GetValue(ios)).SequenceEqual(requiredIOS), "exact original14 native files and constructor order");
                Check(((string[])Field(typeof(iOSConfigurationAsset), "m_requiredFrameworks").GetValue(ios)).SequenceEqual(new[] { "GameKit.framework", "StoreKit.framework" }), "exact original framework order");
                Check(ios.IncludedNativeFiles.Count() == 14 && ios.IncludedFrameworks.Count() == 2 && !ios.MainTargetOtherLdflags.Any(), "actual default original iOS collections");
                Check(ReferenceEquals(Field(typeof(iOSConfigurationAsset), "m_includedNativeFiles").GetValue(ios), Array.Empty<string>()) && ReferenceEquals(Field(typeof(iOSConfigurationAsset), "m_includedFrameworks").GetValue(ios), ios.MainTargetOtherLdflags), "original arrays share Array.Empty<string> instance");
                Check(((string[])Field(typeof(MacOSConfigurationAsset), "m_requiredNativeFiles").GetValue(mac)).SequenceEqual(new[] { "CoreUtilities.h", "CoreUtilities.mm", "HLMacCore.h", "HLMacCore.mm", "HLMacCore/HLOutput.h", "HLMacCore/HLOutput.m" }), "exact original six macOS native files/order");
                Check(mac.GetIncludedNativeFiles().Count() == 6, "actual original macOS default collections");
                var originalAndroid = android; UnityEngine.Object.DestroyImmediate(android); android = core.AndroidConfig;
                Check(android != null && !ReferenceEquals(android, originalAndroid), "Android getter replaces destroyed Unity object");
                var originalIOS = ios; UnityEngine.Object.DestroyImmediate(ios); ios = core.iOSConfig;
                Check(ios != null && !ReferenceEquals(ios, originalIOS), "iOS getter replaces destroyed Unity object");
                var originalMac = mac; UnityEngine.Object.DestroyImmediate(mac); mac = core.macOSConfig;
                Check(mac != null && !ReferenceEquals(mac, originalMac), "macOS getter replaces destroyed Unity object");
            }
            finally
            {
                if (android != null) UnityEngine.Object.DestroyImmediate(android);
                if (ios != null) UnityEngine.Object.DestroyImmediate(ios);
                if (mac != null) UnityEngine.Object.DestroyImmediate(mac);
                UnityEngine.Object.DestroyImmediate(core);
            }
            Debug.Log("PASS original logging configuration checks=" + checks);
        }
    }
}
