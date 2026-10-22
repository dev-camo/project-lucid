using System.Collections;
using Hardlight;
using Hardlight.Localisation;
using ProjectLucid.Offline;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000426 and its <Start>d__0 iterator 0x02000427. Creates and
    // registers the cross promotion system once core and localisation have started.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLCrossPromo : MonoBehaviour, ICoroutineHandler
    {
        // 0x0600186b with 0x0600186e..1873: waits for the core, then the language, before
        // building the configuration from the device country and language.
        private IEnumerator Start()
        {
            // Offline hook H-057: the original builds the real download manager (a metadata request to the
            // server). Offline registers the same system over a resource manager with no content, so
            // CrossPromo takes its own no-metadata path: ready, disabled, no ads, no request or coroutine.
            // The original ignores the download result and reads any existing metadata cache
            // (CrossPromo.OnMetaDataDownloadCompleted); the adapter ignores that cache. The country and
            // language waits feed the metadata URL and CrossPromoDataParser.FindPressToPlayString (unused
            // offline, nothing is parsed), so they are skipped.
            if (OfflineAdapters.Active)
            {
                OfflineCrossPromo.Register();
                yield break;
            }
            CrossPromoConfigurationAsset configurationAsset = SystemConfiguration.GetConfig<CrossPromoConfigurationAsset>();
            yield return MonoSingleton<HLUnityCore>.WaitOnInstance();
            string country = HLUnityCore.GetDeviceISO2CountryCode();
            yield return MonoSingleton<Language>.WaitOnInstance();
            string language = Language.GetLanguage().ToString();
            CrossPromoConfig config = new CrossPromoConfig(configurationAsset, Application.version, country,
                language, Application.persistentDataPath);
            CrossPromo crossPromo;
            if (config.CanUseLocalStorage)
            {
                crossPromo = new CrossPromo(config, this);
            }
            else
            {
                // Without local storage the system works from the cache alone.
                CrossPromoResourceManagerCacheOnly resourceManager = new CrossPromoResourceManagerCacheOnly(config, this);
                CrossPromoDataParser dataParser = new CrossPromoDataParser(config);
                crossPromo = new CrossPromo(config, resourceManager, dataParser);
            }
            ProcessManager.RegisterSystem(crossPromo, null, false, false);
        }

        // 0x0600186c: MonoBehaviour constructor only.
        public HLCrossPromo() { }

        // 0x0600186d: explicit interface implementation forwarding to MonoBehaviour.
        Coroutine ICoroutineHandler.StartCoroutine(IEnumerator downloadFile)
        {
            return StartCoroutine(downloadFile);
        }
    }
}
