using System;
using System.Collections.Generic;

namespace HLCloud.Plugin
{
    /// <summary>
    /// Reconstructed shipped plugin boundary. Genuine Cloud_MacOS source is preserved beside
    /// this factory; its original native import module is still unresolved.
    /// The offline port must select its separate adapter instead of this path.
    /// </summary>
    public abstract class Cloud
    {
        protected static Action<string> m_onConnect = message => { };
        protected ICloud cloudObject;

        public static Cloud NativePluginInstance(ICloud cloudObject, bool useFindGameObject)
        {
#if PROJECT_LUCID_ORIGINAL_APPLE_SERVICES
            // Original C5369EA842965B759669FB711DF450C136176D37: retained for research.
            // Its native import module remains unresolved; this opt-in path
            // cannot supply Apple's service in the portable game.
            Cloud_MacOS plugin = new Cloud_MacOS();
            Cloud_MacOS.UseFindGameObject = useFindGameObject;
            plugin.cloudObject = cloudObject;
            return plugin;
#else
            // Intentional offline port selection at the shipped factory boundary.
            return ProjectLucid.Offline.OfflineProviders.CreateCloud(cloudObject, useFindGameObject);
#endif
        }

        public abstract void InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID);
        public abstract bool Synchronize();
        public abstract HashSet<string> RetrieveAllCloudKeys(string allCloudKeysSeparator);
        public abstract string StringForKey(string key);
        public abstract void SetStringForKey(string value, string key);
        public abstract float FloatForKey(string key);
        public abstract void SetFloatForKey(float value, string key);
        public abstract int IntForKey(string key);
        public abstract void SetIntForKey(int value, string key);
        public abstract bool BoolForKey(string key);
        public abstract void SetBoolForKey(bool value, string key);
        public abstract void RemoveForKey(string key);
        public abstract void CloudDidChange(string message);

        public static void SubscribeOnConnect(Action<string> callback) => m_onConnect += callback;
        public static void UnsubscribeOnConnect(Action<string> callback) => m_onConnect -= callback;
        public virtual void SetSynchronisationCooldown(float seconds) { }
        protected Cloud() { }
    }
}
