using System;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Offline
{
    // Offline cross promotion (hook H-057, HLCrossPromo.Start). The original builds the real
    // download manager, whose first act is a metadata request to the promotion server. Offline
    // registers the same CrossPromo system over a resource manager whose metadata download ends
    // at once as a failed download and which holds no metadata file. CrossPromo then takes its
    // own "metadata is null or empty" branch: the sync ends without raising Initialised, the
    // system is ready, and its metadata stays default (disabled, no ads). No request is made,
    // no folder is created and no coroutine is started.
    internal static class OfflineCrossPromo
    {
        internal static CrossPromo Register()
        {
            CrossPromoConfigurationAsset asset = SystemConfiguration.GetConfig<CrossPromoConfigurationAsset>();
            // The country and language feed the metadata URL (never requested) and the press-to-play
            // string lookup (never reached, nothing is parsed).
            CrossPromoConfig config = new CrossPromoConfig(asset, Application.version, string.Empty,
                string.Empty, Application.persistentDataPath);
            CrossPromo crossPromo = new CrossPromo(config, new OfflineCrossPromoResourceManager(),
                new CrossPromoDataParser(config));
            ProcessManager.RegisterSystem(crossPromo, null, false, false);
            return crossPromo;
        }
    }

    // The failed-download outcome as a resource manager: the metadata download reports failure,
    // no metadata file exists, nothing is pending and no texture is ever available.
    internal sealed class OfflineCrossPromoResourceManager : ICrossPromoResourceManager
    {
        public event Action<bool> DownloadCompleted
        {
            add { }
            remove { }
        }

        public bool HasPendingDownloads => false;

        public void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted)
        {
            onMetaDataDownloadCompleted?.Invoke(false);
        }

        public void VerifyAdResources(CrossPromoAd[] metaDataAds) { }

        public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta) => null;

        public void ReleaseFullScreenTexture(Sprite texture) { }

        public Sprite RequestIconTexture(CrossPromoTexture texMeta) => null;

        public void ReleaseIconTexture(Sprite texture) { }

        public string RequestMetaData() => null;
    }
}
