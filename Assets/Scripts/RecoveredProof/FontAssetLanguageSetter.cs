// Original Game.Runtime.dll HardlightProject.FontAssetLanguageSetter; complete shipping declaration and dual native evidence.
// The C# reconstruction preserves the original algorithm; source spelling, compiler-generated layout and native fault equivalence remain underdetermined.
// Original method evidence below uses shipping MethodDef tokens and half-open native address ranges (ARM64; x86_64).
// 0x060035aa HardlightProject.FontAssetLanguageSetter.Start: ARM64 0x65fc00..0x660118; x86_64 0x684f40..0x685460.
// 0x060035ab HardlightProject.FontAssetLanguageSetter.ShouldInvokeStringHandlers: ARM64 0x660120..0x660408; x86_64 0x685470..0x6857a0.
// 0x060035ac HardlightProject.FontAssetLanguageSetter.LanguageChanged: ARM64 0x660408..0x660d4c; x86_64 0x6857a0..0x6861c0.
// 0x060035ad HardlightProject.FontAssetLanguageSetter.OnDestroy: ARM64 0x660d5c..0x660e64; x86_64 0x6861e0..0x6862c0.
// 0x060035ae HardlightProject.FontAssetLanguageSetter.OnAppValid: ARM64 0x660e64..0x661030; x86_64 0x6862c0..0x686490.
// 0x060035af HardlightProject.FontAssetLanguageSetter.WaitForHandlesMarkReady: ARM64 0x661030..0x6610d0; x86_64 0x686490..0x686530.
// 0x060035b0 HardlightProject.FontAssetLanguageSetter..ctor: ARM64 0x6610fc..0x66123c; x86_64 0x686550..0x686670.
// 0x060035b1 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass6_0..ctor: ARM64 0x660118..0x660120; x86_64 0x685460..0x685470.
// 0x060035b2 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass6_0.<Start>b__1: ARM64 0x66123c..0x661498; x86_64 0x686670..0x6868d0.
// 0x060035b3 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass6_0.<Start>b__0: ARM64 0x661498..0x661534; x86_64 0x6868d0..0x686960.
// 0x060035b4 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass8_0..ctor: ARM64 0x660d4c..0x660d54; x86_64 0x6861c0..0x6861d0.
// 0x060035b5 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass8_0.<LanguageChanged>b__0: ARM64 0x661534..0x6615d0; x86_64 0x686960..0x6869f0.
// 0x060035b6 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass8_1..ctor: ARM64 0x660d54..0x660d5c; x86_64 0x6861d0..0x6861e0.
// 0x060035b7 HardlightProject.FontAssetLanguageSetter+<>c__DisplayClass8_1.<LanguageChanged>b__1: ARM64 0x6615d0..0x661664; x86_64 0x6869f0..0x686a90.
// 0x060035b8 HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11..ctor: ARM64 0x6610d0..0x6610fc; x86_64 0x686530..0x686550.
// 0x060035b9 HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.System.IDisposable.Dispose: ARM64 0x661664..0x6616e0; x86_64 0x686a90..0x686af0.
// 0x060035ba HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.MoveNext: ARM64 0x6616e0..0x661a6c; x86_64 0x686af0..0x686e90.
// 0x060035bb HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.<>m__Finally1: ARM64 0x661a6c..0x661ac8; x86_64 0x686e90..0x686ee0.
// 0x060035bc HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.System.Collections.Generic.IEnumerator<System.Object>.get_Current: ARM64 0x661ac8..0x661ad0; x86_64 0x686ee0..0x686ef0.
// 0x060035bd HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.System.Collections.IEnumerator.Reset: ARM64 0x661ad0..0x661b10; x86_64 0x686ef0..0x686f30.
// 0x060035be HardlightProject.FontAssetLanguageSetter+<WaitForHandlesMarkReady>d__11.System.Collections.IEnumerator.get_Current: ARM64 0x661b10..0x661b18; x86_64 0x686f30..0x686f40.
// Start subscribes Language then StringTable, loads all referenced collections, and registers its App callback after enumeration disposal.
// App callbacks select FontsFallbackPreloadReady or FontsChangeLanguageReady, write false before starting the static coroutine, then yield null and each handle before writing true.
// Readiness observes handle completion order, not validity/status/success. String handlers return false while idle or any handle is unfinished; the completed pass clears the responding flag.
// For each old handle, language changes remove its result only if complete, then release the handle regardless; tracking is cleared, then only the first pairing of the language is loaded (x86-64 0x685f82 leaves the pairing loop after its font loop) before the next collection; no rollback.
// OnDestroy only unsubscribes Language then StringTable; it does not release handles, revoke queued App callbacks or stop the coroutine.
// Addressable assets, TMP fonts/fallback lists, App lifecycle and actual Engine readiness remain separate qualification requirements.
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class FontAssetLanguageSetter : MonoBehaviour
    {
        [SerializeField]
        private List<AssetReferenceT<FontAssetAndFallbacks>> m_fontAssetsAndFallBackCollectionRefs;
        private readonly List<FontAssetAndFallbacks> m_fontAssetsAndFallBackCollection = new List<FontAssetAndFallbacks>();
        private readonly Dictionary<FontAssetAndFallbacks, List<AsyncOperationHandle<TMP_FontAsset>>> m_asyncOperationHandles = new Dictionary<FontAssetAndFallbacks, List<AsyncOperationHandle<TMP_FontAsset>>>();
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>();
        private AddressableManager m_assetManager;
        private bool m_isRespondingToLanguageChange;

        private void Start()
        {
            m_assetManager = ProcessManager.GetSystem<AddressableManager>();
            Language.OnLanguageChanged += LanguageChanged;
            StringTable.ShouldInvokeStringHandlers += ShouldInvokeStringHandlers;
            var loadHandles = new List<AsyncOperationHandle>();
            foreach (var assetReference in m_fontAssetsAndFallBackCollectionRefs)
            {
                var handle = m_assetManager.LoadAsset<FontAssetAndFallbacks>(assetReference, fallbackConfig =>
                {
                    m_fontAssetsAndFallBackCollection.AddUnique(fallbackConfig);
                    foreach (var collection in m_fontAssetsAndFallBackCollection)
                    {
                        if (!m_asyncOperationHandles.ContainsKey(collection))
                            m_asyncOperationHandles.Add(collection, new List<AsyncOperationHandle<TMP_FontAsset>>());
                    }
                });
                loadHandles.Add(handle);
            }
            m_appRef.InvokeOnValid(app => OnAppValid(app, loadHandles, AppFSMKeys.FontsFallbackPreloadReady));
        }

        private bool ShouldInvokeStringHandlers()
        {
            if (!m_isRespondingToLanguageChange)
                return false;
            foreach (var collection in m_fontAssetsAndFallBackCollection)
            {
                foreach (var handle in m_asyncOperationHandles[collection])
                {
                    if (!handle.IsDone)
                        return false;
                }
            }
            m_isRespondingToLanguageChange = false;
            return true;
        }

        private void LanguageChanged(Languages language)
        {
            m_isRespondingToLanguageChange = true;
            var loadHandles = new List<AsyncOperationHandle>();
            foreach (var fontAssetAndLanguageFallbacks in m_fontAssetsAndFallBackCollection)
            {
                foreach (var handle in m_asyncOperationHandles[fontAssetAndLanguageFallbacks])
                {
                    if (handle.IsDone)
                        fontAssetAndLanguageFallbacks.m_fontAsset.fallbackFontAssetTable.Remove(handle.Result);
                    m_assetManager.ReleaseHandle(handle);
                }
                m_asyncOperationHandles[fontAssetAndLanguageFallbacks].Clear();
                foreach (var pairing in fontAssetAndLanguageFallbacks.m_fallbackCollection)
                {
                    if (pairing.m_language != language)
                        continue;
                    foreach (var assetReference in pairing.m_fontsForLanguage)
                    {
                        var handle = m_assetManager.LoadAsset<TMP_FontAsset>(assetReference, asset =>
                        {
                            var fallbackFontAssetTable = fontAssetAndLanguageFallbacks.m_fontAsset.fallbackFontAssetTable;
                            fallbackFontAssetTable.AddUnique(asset);
                            fontAssetAndLanguageFallbacks.m_fontAsset.fallbackFontAssetTable = fallbackFontAssetTable;
                        });
                        m_asyncOperationHandles[fontAssetAndLanguageFallbacks].AddUnique(handle);
                        loadHandles.Add(handle);
                    }
                    break;
                }
            }
            m_appRef.InvokeOnValid(app => OnAppValid(app, loadHandles, AppFSMKeys.FontsChangeLanguageReady));
        }

        private void OnDestroy()
        {
            Language.OnLanguageChanged -= LanguageChanged;
            StringTable.ShouldInvokeStringHandlers -= ShouldInvokeStringHandlers;
        }

        private void OnAppValid(IGraphUser app, List<AsyncOperationHandle> handles, GraphStorageKey varName)
        {
            app.Storage.SetValue<bool>(varName, false);
            StartCoroutine(WaitForHandlesMarkReady(app, handles, varName));
        }

        private static IEnumerator WaitForHandlesMarkReady(IGraphUser app, List<AsyncOperationHandle> handles, GraphStorageKey varName)
        {
            yield return null;
            foreach (var handle in handles)
                yield return handle;
            app.Storage.SetValue<bool>(varName, true);
        }
    }
}
