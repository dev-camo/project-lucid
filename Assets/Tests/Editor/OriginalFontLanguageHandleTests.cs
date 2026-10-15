using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // FontAssetLanguageSetter language-change bookkeeping on owned inactive objects.
    // Start never runs, so the setter neither subscribes to Language/StringTable
    // nor loads through Addressables; the inactive AddressableManager is used only
    // for its preloaded-handle lookup and ReleaseHandle. Every handle comes from
    // an owned ResourceManager with callback hooks disabled.
    public sealed class OriginalFontLanguageHandleTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // LanguageChanged 0x060035ac, x86-64 0x6857a0: sets the responding flag
        // (+0x48, 0x685a08); per collection, Remove(Result) only when IsDone
        // (0x685ba3..0x685be0) and ReleaseHandle for every old handle (0x685c45);
        // clears the tracking list (0x685c97..0x685cb5) before the pairing loop;
        // skips pairings of another language (0x685d38) and loads each matching
        // font through the manager, whose preloaded route runs the fallback-table
        // AddUnique callback synchronously. ShouldInvokeStringHandlers 0x060035ab,
        // x86-64 0x685470: false while the flag is clear (0x68553a), false at the
        // first unfinished handle (0x685658..0x685668), otherwise clears the flag
        // (0x68570a) and returns true.
        // Catches: removing an unfinished handle's preset result; releasing only
        // finished handles; keeping old tracking; loading another language's fonts;
        // reporting readiness while idle or while a handle is pending; or leaving
        // the flag set after a completed pass (a repeated call would return true).
        // Only one pairing matches here: whether later matching pairings of the
        // same collection are processed is not asserted.
        [Test]
        public void LanguageChangeReleasesOldHandlesAndReadinessClearsAfterEveryHandleCompletes()
        {
            var owned = new List<Object>();
            var materials = new List<Material>();
            string appRow = ProcessManager.GetDefaultName<App>();
            IDictionary registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.IsFalse(registry.Contains(appRow), "Fixture requires an absent App row; its field initialiser creates one.");
            var resourceManager = new ResourceManager();
            typeof(ResourceManager).GetField("CallbackHooksEnabled", Instance | BindingFlags.Public).SetValue(resourceManager, false);
            var handles = new List<AsyncOperationHandle>();
            try
            {
                TMP_FontAsset baseFont = Font(owned, materials, "base");
                TMP_FontAsset oldDoneFont = Font(owned, materials, "old finished");
                TMP_FontAsset oldPendingFont = Font(owned, materials, "old pending");
                TMP_FontAsset keptFont = Font(owned, materials, "authored");
                TMP_FontAsset frenchFont = Font(owned, materials, "French");
                TMP_FontAsset germanFont = Font(owned, materials, "German");
                baseFont.fallbackFontAssetTable = new List<TMP_FontAsset> { oldDoneFont, oldPendingFont, keptFont };

                var collection = ScriptableObject.CreateInstance<FontAssetAndFallbacks>();
                owned.Add(collection);
                collection.m_fontAsset = baseFont;
                collection.m_fallbackCollection = new List<FontAssetAndFallbacks.FontAssetFallbackPairing>
                {
                    Pairing(Languages.German, "lucid-owned-font-german"),
                    Pairing(Languages.French, "lucid-owned-font-french"),
                };

                AsyncOperationHandle<TMP_FontAsset> oldDone = resourceManager.CreateCompletedOperation(oldDoneFont, (string)null);
                var oldPendingOperation = new PendingFontOperation(oldPendingFont);
                AsyncOperationHandle<TMP_FontAsset> oldPending = resourceManager.StartOperation(oldPendingOperation, default(AsyncOperationHandle));
                var frenchOperation = new PendingFontOperation(frenchFont);
                AsyncOperationHandle<TMP_FontAsset> french = resourceManager.StartOperation(frenchOperation, default(AsyncOperationHandle));
                AsyncOperationHandle<TMP_FontAsset> german = resourceManager.CreateCompletedOperation(germanFont, (string)null);
                handles.Add(oldDone); handles.Add(oldPending); handles.Add(french); handles.Add(german);
                Assert.IsTrue(oldDone.IsDone && !oldPending.IsDone && !french.IsDone && german.IsDone);

                GameObject managerObject = Inactive(owned, "Owned inactive font AddressableManager");
                AddressableManager manager = managerObject.AddComponent<AddressableManager>();
                var preloaded = (Dictionary<string, AsyncOperationHandle>)typeof(AddressableManager)
                    .GetField("m_preloadedAssetHandleLookup", Instance).GetValue(manager);
                preloaded.Add("lucid-owned-font-french", french);
                preloaded.Add("lucid-owned-font-german", german);

                GameObject setterObject = Inactive(owned, "Owned inactive FontAssetLanguageSetter");
                FontAssetLanguageSetter setter = setterObject.AddComponent<FontAssetLanguageSetter>();
                typeof(FontAssetLanguageSetter).GetField("m_assetManager", Instance).SetValue(setter, manager);
                var collections = (List<FontAssetAndFallbacks>)typeof(FontAssetLanguageSetter)
                    .GetField("m_fontAssetsAndFallBackCollection", Instance).GetValue(setter);
                var tracked = (Dictionary<FontAssetAndFallbacks, List<AsyncOperationHandle<TMP_FontAsset>>>)typeof(FontAssetLanguageSetter)
                    .GetField("m_asyncOperationHandles", Instance).GetValue(setter);
                collections.Add(collection);
                List<AsyncOperationHandle<TMP_FontAsset>> collectionHandles = new List<AsyncOperationHandle<TMP_FontAsset>> { oldDone, oldPending };
                tracked.Add(collection, collectionHandles);
                FieldInfo responding = typeof(FontAssetLanguageSetter).GetField("m_isRespondingToLanguageChange", Instance);
                MethodInfo shouldInvoke = typeof(FontAssetLanguageSetter).GetMethod("ShouldInvokeStringHandlers", Instance);
                Func<bool> ready = () => (bool)shouldInvoke.Invoke(setter, null);

                Assert.IsFalse(ready(), "Idle setter does not invoke string handlers.");

                typeof(FontAssetLanguageSetter).GetMethod("LanguageChanged", Instance).Invoke(setter, new object[] { Languages.French });

                Assert.IsTrue((bool)responding.GetValue(setter));
                CollectionAssert.AreEqual(new[] { oldPendingFont, keptFont, frenchFont }, baseFont.fallbackFontAssetTable,
                    "Only the finished old result is removed; the French font is appended; German is not loaded.");
                Assert.IsFalse(oldDone.IsValid(), "The finished old handle is released.");
                Assert.AreSame(collectionHandles, tracked[collection], "The tracking list is cleared in place.");
                CollectionAssert.AreEqual(new[] { french }, collectionHandles);
                Assert.IsTrue(german.IsValid(), "The other language's preloaded handle is untouched.");

                Assert.IsFalse(ready(), "A pending font keeps string handlers waiting.");
                Assert.IsTrue((bool)responding.GetValue(setter));

                // The unfinished old handle was released once by LanguageChanged: its
                // completion now drops the last reference.
                oldPendingOperation.Finish();
                Assert.IsFalse(oldPending.IsValid(), "The unfinished old handle was released too.");

                frenchOperation.Finish();
                Assert.IsTrue(french.IsDone && french.IsValid());
                Assert.IsTrue(ready(), "Every tracked handle has completed.");
                Assert.IsFalse((bool)responding.GetValue(setter), "The completed pass clears the flag.");
                Assert.IsFalse(ready(), "A later call is idle again.");
            }
            finally
            {
                try
                {
                    foreach (AsyncOperationHandle handle in handles)
                        if (handle.IsValid()) resourceManager.Release(handle);
                    resourceManager.Dispose();
                }
                finally
                {
                    try
                    {
                        for (int i = owned.Count - 1; i >= 0; --i)
                            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
                        foreach (Material material in materials)
                            if (material != null) Object.DestroyImmediate(material);
                    }
                    finally
                    {
                        // The setter's field initialiser created this App row; the
                        // queued App callback is discarded with it. No App existed.
                        if (registry.Contains(appRow))
                        {
                            Assert.IsTrue(ProcessManager.IsSystemNull(appRow), "The App row never held a system.");
                            registry.Remove(appRow);
                        }
                    }
                }
            }
        }

        private static FontAssetAndFallbacks.FontAssetFallbackPairing Pairing(Languages language, string guid) =>
            new FontAssetAndFallbacks.FontAssetFallbackPairing
            {
                m_language = language,
                m_fontsForLanguage = new List<AssetReferenceT<TMP_FontAsset>> { new AssetReferenceT<TMP_FontAsset>(guid) },
            };

        private static TMP_FontAsset Font(List<Object> owned, List<Material> materials, string name)
        {
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = "Owned " + name + " font";
            // TMP_FontAsset.OnDestroy destroys its material; give it an owned one.
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader, "Unity's built-in error shader is available.");
            var material = new Material(shader) { name = font.name + " material" };
            materials.Add(material);
            font.material = material;
            owned.Add(font);
            return font;
        }

        private static GameObject Inactive(List<Object> owned, string name)
        {
            var value = new GameObject(name);
            value.SetActive(false);
            owned.Add(value);
            return value;
        }

        // A package operation that stays unfinished until the test completes it;
        // its result is known in advance so a premature Remove(Result) is visible.
        private sealed class PendingFontOperation : AsyncOperationBase<TMP_FontAsset>
        {
            private readonly TMP_FontAsset font;
            public PendingFontOperation(TMP_FontAsset font) { this.font = font; Result = font; }
            protected override void Execute() { }
            public void Finish() { Complete(font, true, (string)null); }
        }
    }
}
