using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.Editor
{
    public sealed class OriginalMenuConfigurationAssetSerializationTests
    {
        private static readonly string[] DevOverrides = {
            "m_useFastValidation", "m_sendCrashReports", "m_gameCenter", "m_deviceConsoleEnabled",
            "m_logHandlerEnabled", "m_calculateFPSCounter", "m_applicationVersionDefinition",
            "m_showGameCenterChallenges", "m_hashedComponentGroupDefaults" };
        private static readonly string[] ProdOverrides = {
            "m_sendCrashReports", "m_gameCenter", "m_useExportedScenes", "m_deviceConsoleEnabled",
            "m_logHandlerEnabled", "m_calculateFPSCounter" };
        private static readonly string[] LocOverrides = {
            "m_nodeDataExportPath", "m_charactersOutputDirectory", "m_localisedDefinitionsDirectory" };

        [Test]
        public void GenuineOwnedCoreGraphRetainsSeparateBaseOverridesAndTwelveRuntimeSlots()
        {
            Owned.Run(owned => {
                var hashed = owned.New<HashedGroupConfiguration>("Hashed.asset");
                var dev = owned.New<CoreGameConfiguration>("Dev.asset");
                var prod = owned.New<CoreGameConfiguration>("Prod.asset");
                var loc = owned.New<HLLocalisationConfigurationAsset>("Localisation.asset");
                var runtime = owned.New<RuntimeConfiguration>("Runtime.asset");
                Assert.That(prod.UseExportedScenes, Is.False, "genuine constructor default precedes authored values");
                SetCore(dev, null, DevOverrides, false, true, hashed);
                SetCore(prod, dev, ProdOverrides, true, false, null);
                var data = new SerializedObject(runtime); data.Update();
                var slots = Require(data, "m_configurationAssets"); slots.arraySize = 12;
                for (int i = 0; i < 12; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = null;
                slots.GetArrayElementAtIndex(0).objectReferenceValue = prod;
                slots.GetArrayElementAtIndex(1).objectReferenceValue = loc;
                Assert.That(data.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                Assert.That(new SerializedObject(dev).FindProperty("m_applicationVersionDefinition"), Is.Null);
                owned.SaveAndReimport();
                var loadedDev = owned.Load<CoreGameConfiguration>("Dev.asset");
                var loadedProd = owned.Load<CoreGameConfiguration>("Prod.asset");
                var loadedHashed = owned.Load<HashedGroupConfiguration>("Hashed.asset");
                var loadedRuntime = owned.Load<RuntimeConfiguration>("Runtime.asset");
                AssertCore(loadedDev, null, DevOverrides, false, true, loadedHashed);
                AssertCore(loadedProd, loadedDev, ProdOverrides, true, false, null);
                Assert.That(loadedRuntime.Assets.Count, Is.EqualTo(12));
                Assert.That(loadedRuntime.Assets[0], Is.EqualTo(loadedProd));
                Assert.That(loadedRuntime.Assets[1], Is.EqualTo(owned.Load<HLLocalisationConfigurationAsset>("Localisation.asset")));
                for (int i = 2; i < 12; i++) Assert.That(loadedRuntime.Assets[i], Is.Null);
            });
        }

        [Test]
        public void GenuineOwnedLocalisationPreservesAuthoredPathsFlagsAndOverrideOrder()
        {
            Owned.Run(owned => {
                var loc = owned.New<HLLocalisationConfigurationAsset>("Localisation.asset");
                Assert.That(loc.LocalisedDefinitionsDirectory, Is.EqualTo("Language Strings"));
                var data = new SerializedObject(loc); data.Update();
                Require(data, "m_baseAsset").objectReferenceValue = null;
                SetStrings(Require(data, "m_overriddenFields"), LocOverrides);
                Require(data, "m_localisedDefinitionsDirectory").stringValue = "LanguageStrings";
                Require(data, "m_nodeDataExportPath").stringValue = "../../submodules/HLLocalisation/DataGeneration/Src/Utils/data_export/full_data_export.js";
                Require(data, "m_charactersOutputDirectory").stringValue = "/Data/Localisation/";
                Require(data, "m_enableEditorDataGeneration").boolValue = true;
                Require(data, "m_enableLocalisedUIStringFunctionality").boolValue = true;
                Assert.That(data.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                owned.SaveAndReimport();
                var loaded = owned.Load<HLLocalisationConfigurationAsset>("Localisation.asset");
                Assert.That(loaded.LocalisedDefinitionsDirectory, Is.EqualTo("LanguageStrings"));
                Assert.That(loaded.NodeDataExportPath, Is.EqualTo("../../submodules/HLLocalisation/DataGeneration/Src/Utils/data_export/full_data_export.js"));
                Assert.That(loaded.CharactersOutputDirectory, Is.EqualTo("/Data/Localisation/"));
                Assert.That(loaded.EnableEditorDataGeneration, Is.True);
                Assert.That(loaded.EnableLocalisedUIStringFunctionality, Is.True);
                var saved = new SerializedObject(loaded); saved.Update();
                Assert.That(Require(saved, "m_baseAsset").objectReferenceValue, Is.Null);
                AssertStrings(Require(saved, "m_overriddenFields"), LocOverrides);
                // The original Validate path invokes filesystem/global logging;
                // serialization qualification here does not invoke that boundary.
            });
        }

        // Original getters 0x060017ef..0x060017fc (x86-64 0x796cb0..0x796d98) each read
        // one distinct field byte in declaration order; ShowGameCenterChallenges
        // (x86-64 0x796d70) also requires m_gameCenter. One flag at a time catches
        // any getter swap that the shared values of the round-trip cases hide.
        [Test]
        public void GenuineCoreFlagsEachReachOnlyTheirOwnGetterAndChallengesNeedGameCenter()
        {
            string[] fields = {
                "m_useExportedScenes", "m_missionIntrosEnabled", "m_useFastValidation", "m_gameCenter",
                "m_allowUnityShaderDebuggingWindow", "m_sendCrashReports", "m_deviceConsoleEnabled",
                "m_logHandlerEnabled", "m_calculateFPSCounter", "m_tapToSkipEnabled", "m_showGameCenterChallenges" };
            Func<CoreGameConfiguration, bool>[] getters = {
                value => value.UseExportedScenes, value => value.MissionIntrosEnabled, value => value.FastValidation,
                value => value.GameCenter, value => value.AllowUnityShaderDebuggingWindow, value => value.SendCrashReports,
                value => value.DeviceConsoleEnabled, value => value.LogHandlerEnabled, value => value.CalculateFPSCounter,
                value => value.TapToSkipEnabled, value => value.ShowGameCenterChallenges };
            const int gameCenter = 3, challenges = 10;
            var config = ScriptableObject.CreateInstance<CoreGameConfiguration>();
            SerializedObject data = null;
            try
            {
                data = new SerializedObject(config);
                void Apply(params int[] enabled)
                {
                    data.Update();
                    for (int i = 0; i < fields.Length; i++)
                        Require(data, fields[i]).boolValue = Array.IndexOf(enabled, i) >= 0;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                for (int field = 0; field < fields.Length; field++)
                {
                    Apply(field);
                    for (int getter = 0; getter < getters.Length; getter++)
                        Assert.That(getters[getter](config), Is.EqualTo(getter == field && field != challenges),
                            fields[field] + " alone -> getter " + getter);
                }
                Apply(gameCenter, challenges);
                for (int getter = 0; getter < getters.Length; getter++)
                    Assert.That(getters[getter](config), Is.EqualTo(getter == gameCenter || getter == challenges), "getter " + getter);
            }
            finally
            {
                data?.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        // Original constructor 0x0600000f (x86-64 0x1a34110) stores "Language Strings",
        // "/../../Server/src/data/full_data_export.js", "/Game Assets/Fonts/Characters/"
        // and both flags true (one 0x0101 word). Getters 0x06000009..0x0600000d
        // (x86-64 0x1a33f00..0x1a33f40) read those fields in order. Opposite flag
        // values catch a swap that the all-true authored case cannot.
        [Test]
        public void GenuineLocalisationDefaultsAndOppositeFlagsReachTheirOwnGetters()
        {
            var loc = ScriptableObject.CreateInstance<HLLocalisationConfigurationAsset>();
            SerializedObject data = null;
            try
            {
                Assert.That(loc.LocalisedDefinitionsDirectory, Is.EqualTo("Language Strings"));
                Assert.That(loc.NodeDataExportPath, Is.EqualTo("/../../Server/src/data/full_data_export.js"));
                Assert.That(loc.CharactersOutputDirectory, Is.EqualTo("/Game Assets/Fonts/Characters/"));
                Assert.That(loc.EnableEditorDataGeneration, Is.True);
                Assert.That(loc.EnableLocalisedUIStringFunctionality, Is.True);
                data = new SerializedObject(loc);
                foreach (bool generation in new[] { false, true })
                {
                    data.Update();
                    Require(data, "m_enableEditorDataGeneration").boolValue = generation;
                    Require(data, "m_enableLocalisedUIStringFunctionality").boolValue = !generation;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(loc.EnableEditorDataGeneration, Is.EqualTo(generation));
                    Assert.That(loc.EnableLocalisedUIStringFunctionality, Is.EqualTo(!generation));
                }
            }
            finally
            {
                data?.Dispose();
                Object.DestroyImmediate(loc);
            }
        }

        [Test]
        public void GenuineOwnedHashedRemainsFieldlessPlainScriptableObjectAfterReimport()
        {
            Owned.Run(owned => {
                var hashed = owned.New<HashedGroupConfiguration>("Hashed.asset");
                Assert.That(typeof(HashedGroupConfiguration).BaseType, Is.EqualTo(typeof(ScriptableObject)));
                Assert.That(typeof(HashedGroupConfiguration).GetFields(System.Reflection.BindingFlags.DeclaredOnly |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic), Is.Empty);
                AssertFieldless(hashed); owned.SaveAndReimport();
                var loaded = owned.Load<HashedGroupConfiguration>("Hashed.asset"); AssertFieldless(loaded);
                Assert.That(loaded.GetType(), Is.EqualTo(typeof(HashedGroupConfiguration)));
            });
        }

        private static SerializedProperty Require(SerializedObject value, string name)
        {
            var result = value.FindProperty(name); Assert.That(result, Is.Not.Null, name); return result;
        }
        private static void SetStrings(SerializedProperty value, string[] items)
        {
            value.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) value.GetArrayElementAtIndex(i).stringValue = items[i];
        }
        private static void AssertStrings(SerializedProperty value, string[] expected)
        {
            Assert.That(value.arraySize, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(value.GetArrayElementAtIndex(i).stringValue, Is.EqualTo(expected[i]));
        }
        private static void SetCore(CoreGameConfiguration value, CoreGameConfiguration basis,
            string[] overrides, bool exported, bool diagnostics, HashedGroupConfiguration hashed)
        {
            var data = new SerializedObject(value); data.Update();
            Require(data, "m_baseAsset").objectReferenceValue = basis;
            SetStrings(Require(data, "m_overriddenFields"), overrides);
            Require(data, "m_useExportedScenes").boolValue = exported;
            Require(data, "m_missionIntrosEnabled").boolValue = false;
            Require(data, "m_useFastValidation").boolValue = true;
            Require(data, "m_gameCenter").boolValue = true;
            Require(data, "m_replayMode").intValue = (int)ReplayMode.Off;
            Require(data, "m_allowUnityShaderDebuggingWindow").boolValue = false;
            Require(data, "m_sendCrashReports").boolValue = diagnostics;
            Require(data, "m_runtimeIssueProfile").objectReferenceValue = null;
            Require(data, "m_deviceConsoleEnabled").boolValue = diagnostics;
            Require(data, "m_logHandlerEnabled").boolValue = diagnostics;
            Require(data, "m_calculateFPSCounter").boolValue = diagnostics;
            Require(data, "m_tapToSkipEnabled").boolValue = false;
            Require(data, "m_showGameCenterChallenges").boolValue = false;
            Require(data, "m_challengeManagerIntegrationType").intValue = (int)ChallengeManager.IntegrationType.Default;
            Require(data, "m_hashedComponentGroupDefaults").objectReferenceValue = hashed;
            Assert.That(data.ApplyModifiedPropertiesWithoutUndo(), Is.True);
        }
        private static void AssertCore(CoreGameConfiguration value, CoreGameConfiguration basis,
            string[] overrides, bool exported, bool diagnostics, HashedGroupConfiguration hashed)
        {
            var data = new SerializedObject(value); data.Update();
            Assert.That(Require(data, "m_baseAsset").objectReferenceValue, Is.EqualTo(basis));
            AssertStrings(Require(data, "m_overriddenFields"), overrides);
            Assert.That(value.UseExportedScenes, Is.EqualTo(exported)); Assert.That(value.MissionIntrosEnabled, Is.False);
            Assert.That(value.FastValidation, Is.True); Assert.That(value.GameCenter, Is.True);
            Assert.That(value.ReplayMode, Is.EqualTo(ReplayMode.Off)); Assert.That(value.AllowUnityShaderDebuggingWindow, Is.False);
            Assert.That(value.SendCrashReports, Is.EqualTo(diagnostics)); Assert.That(value.RuntimeIssueProfile, Is.Null);
            Assert.That(value.DeviceConsoleEnabled, Is.EqualTo(diagnostics)); Assert.That(value.LogHandlerEnabled, Is.EqualTo(diagnostics));
            Assert.That(value.CalculateFPSCounter, Is.EqualTo(diagnostics)); Assert.That(value.TapToSkipEnabled, Is.False);
            Assert.That(value.ShowGameCenterChallenges, Is.False);
            Assert.That(value.ChallengeManagerIntegrationType, Is.EqualTo(ChallengeManager.IntegrationType.Default));
            Assert.That(value.HashedComponentGroupDefaults, Is.EqualTo(hashed));
            Assert.That(data.FindProperty("m_applicationVersionDefinition"), Is.Null);
        }
        private static void AssertFieldless(HashedGroupConfiguration value)
        {
            var data = new SerializedObject(value); data.Update();
            Assert.That(data.FindProperty("m_baseAsset"), Is.Null); Assert.That(data.FindProperty("m_overriddenFields"), Is.Null);
            Assert.That(data.FindProperty("m_guid"), Is.Null);
        }

        private sealed class Owned
        {
            private readonly List<Object> objects = new List<Object>();
            private readonly List<string> paths = new List<string>();
            private string folder;
            private bool folderAttempted;
            private void Begin()
            {
                string name = "__LucidOwnedMenuConfiguration_" + Guid.NewGuid().ToString("N"); folder = "Assets/" + name;
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False); folderAttempted = true;
                Assert.That(AssetDatabase.CreateFolder("Assets", name), Is.Not.Empty);
            }
            public T New<T>(string name) where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>(); objects.Add(value); Assert.That(value, Is.Not.Null);
                string path = folder + "/" + name; paths.Add(path); AssetDatabase.CreateAsset(value, path); return value;
            }
            public void SaveAndReimport()
            {
                foreach (Object value in objects) if (value != null && AssetDatabase.Contains(value)) AssetDatabase.SaveAssetIfDirty(value);
                foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            public T Load<T>(string name) where T : ScriptableObject
            {
                T value = AssetDatabase.LoadAssetAtPath<T>(folder + "/" + name); objects.Add(value); Assert.That(value, Is.Not.Null); return value;
            }
            private List<Exception> Cleanup()
            {
                var errors = new List<Exception>();
                if (folderAttempted) try {
                    if (AssetDatabase.IsValidFolder(folder) && !AssetDatabase.DeleteAsset(folder)) throw new InvalidOperationException("Owned folder deletion failed: " + folder);
                } catch (Exception error) { errors.Add(error); }
                foreach (Object value in objects) try {
                    if (!ReferenceEquals(value, null) && value != null) Object.DestroyImmediate(value);
                } catch (Exception error) { errors.Add(error); }
                return errors;
            }
            public static void Run(Action<Owned> action)
            {
                var owned = new Owned(); ExceptionDispatchInfo primary = null;
                try { owned.Begin(); action(owned); } catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
                List<Exception> cleanup = owned.Cleanup();
                var reporting = new List<Exception>();
                foreach (Exception error in cleanup) try {
                    TestContext.Progress.WriteLine("Owned cleanup failure: " + error);
                } catch (Exception reportError) { reporting.Add(reportError); }
                if (primary != null) primary.Throw();
                cleanup.AddRange(reporting);
                if (cleanup.Count != 0) throw new AggregateException("Owned asset cleanup failed", cleanup);
            }
        }
    }
}
