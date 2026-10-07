using Hardlight;
using HLCloud.Plugin;

namespace ProjectLucid.Offline
{
    // Intentional port factories. These share the original contract assembly:
    // the original factories depend on the adapters, and an adapter assembly
    // depending back on HLUnityCore.Runtime would create a dependency cycle.
    public static class OfflineProviders
    {
        public static IHLSaveMethod CreatePropertyStorage(string key, string fileName)
        {
            return new LocalPropertySave(key, fileName);
        }

        public static Cloud CreateCloud(ICloud receiver, bool useFindGameObject)
        {
            return new LocalCloud(LocalCloud.DefaultSavePath, receiver, useFindGameObject);
        }
    }
}
