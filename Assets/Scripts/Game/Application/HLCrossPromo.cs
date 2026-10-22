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
            // Offline hook H-057: the original builds the real download manager, which requests the
            // promotion metadata from the server. Offline registers the same system over a
            // resource manager that has no content, so CrossPromo takes its own "no metadata"
            // path: ready, disabled, no ads, and no request, folder or coroutine. The device
            // country and language waits only feed the metadata URL, so they are skipped too.
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
