using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Checks automatic Unity lifecycle and original callback ordering. It uses original Language/store/singleton/process types and
    // callbacks, without loading author content or writing original/local saves.
    public sealed class LanguageLifecycleTests
    {
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Declared);

        [UnityTest]
        public IEnumerator OriginalLanguage_AutomaticSavedOverrideAndCallbackCleanup()
        {
            Assert.IsTrue(ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance, null));
            Type storeType = typeof(HLPropertyStore), languageType = typeof(Language);
            var storeInstance = Field(storeType, "s_internalInstance");
            var loads = Field(storeType, "LoadHandlers"); var saves = Field(storeType, "SaveHandlers");
            var languageEvent = Field(languageType, "OnLanguageChanged");
            object oldStore = storeInstance.GetValue(null), oldLoads = loads.GetValue(null), oldSaves = saves.GetValue(null), oldEvent = languageEvent.GetValue(null);
            GameObject owner = null; Language language = null; int notifications = 0; bool cleanupBeforeBaseClear = false;
            SystemRef<Language> reference = ProcessManager.GetSystemRef<Language>();
            Action<Language> shutdown = value =>
            {
                Assert.AreSame(language, value);
                Assert.IsFalse(HLPropertyStore.IsThereAnyLoadHandler);
                Assert.IsFalse(HLPropertyStore.IsThereAnySaveHandler);
                Assert.AreSame(language, MonoSingleton<Language>.Instance);
                cleanupBeforeBaseClear = true;
            };
            Language.LanguageChangedHandler changed = value =>
            {
                ++notifications;
                Assert.AreEqual(Languages.Korean, value);
                Assert.IsFalse(language.LanguageLoaded);
                Assert.IsTrue(Language.IsLanguageOverriden());
                Assert.IsFalse(HLPropertyStore.IsThereAnySaveHandler);
            };
            try
            {
                // Genuine original already-loaded store state; no constructor,
                // fake storage provider, file loading or transport replacement.
                var properties = new HLPropertyList();
                properties.AddProperty("language_override", true);
                properties.AddProperty("language_current", "Korean");
                var store = (HLPropertyStore)FormatterServices.GetUninitializedObject(storeType);
                Field(storeType, "m_isLoaded").SetValue(store, true);
                Field(storeType, "ActiveProperties").SetValue(store, properties);
                storeInstance.SetValue(null, store); loads.SetValue(null, null); saves.SetValue(null, null); languageEvent.SetValue(null, null);
                reference.OnSystemShutdown += shutdown; Language.OnLanguageChanged += changed;
                owner = new GameObject("Lucid automatic original language lifecycle");
                language = owner.AddComponent<Language>();
                Assert.AreSame(language, MonoSingleton<Language>.Instance, "Unity automatically dispatches original Awake.");
                Assert.AreSame(language, reference.GetSafe());
                Assert.AreEqual(Languages.EnglishUS, language.CurrentLanguage);
                Assert.IsFalse(language.LanguageLoaded);
                Assert.AreEqual(0, notifications);
                yield return null;
                Assert.AreEqual(1, notifications, "Unity automatically dispatches Start, whose AddLoadHandler immediately notifies.");
                Assert.IsTrue(language.LanguageLoaded);
                Assert.AreEqual(Languages.Korean, Language.GetLanguage());
                Assert.AreEqual((int)Languages.Korean, Language.GetLanguageAsInt());
                Assert.IsTrue(Language.IsLanguageOverriden());
                Assert.IsTrue(HLPropertyStore.IsThereAnyLoadHandler && HLPropertyStore.IsThereAnySaveHandler);
                var saved = new HLPropertyList(); ((HLPropertyStore.SaveHandler)saves.GetValue(null))(saved);
                Assert.IsTrue(saved.AsBool("language_override"));
                Assert.AreEqual("Korean", saved.AsString("language_current"));
                UnityEngine.Object.Destroy(owner); owner = null;
                yield return null;
                Assert.IsTrue(cleanupBeforeBaseClear, "Unity automatically dispatches original OnDestroy, removing property callbacks before base cleanup.");
                Assert.IsTrue(ProcessManager.IsSystemNull<Language>());
                Assert.IsTrue(ReferenceEquals(MonoSingleton<Language>.Instance, null));
                Assert.IsNotNull(languageEvent.GetValue(null), "Original OnDestroy retains the static language event.");
                Assert.AreEqual(Languages.EnglishUS, Language.GetLanguage());
            }
            finally
            {
                reference.OnSystemShutdown -= shutdown; Language.OnLanguageChanged -= changed;
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                storeInstance.SetValue(null, oldStore); loads.SetValue(null, oldLoads); saves.SetValue(null, oldSaves); languageEvent.SetValue(null, oldEvent);
            }
        }
    }
}
