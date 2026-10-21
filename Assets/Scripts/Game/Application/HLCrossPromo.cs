using System.Collections;
using Hardlight;
using Hardlight.Localisation;
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
