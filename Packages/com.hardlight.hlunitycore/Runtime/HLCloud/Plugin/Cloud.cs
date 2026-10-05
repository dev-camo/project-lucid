using System;
using System.Collections.Generic;

namespace HLCloud.Plugin
{
    public abstract class Cloud
    {
        protected static Action<string> m_onConnect;
        protected ICloud cloudObject;
        private static readonly object NotificationGate = new object();

        // Original C5369EA842965B759669FB711DF450C136176D37 constructs Cloud_MacOS.
        // Intentional platform replacement: every target uses the same local store.
        public static Cloud NativePluginInstance(ICloud cloudObject, bool useFindGameObject)
        {
            return new Cloud_Editor(Cloud_Editor.DefaultSavePath, cloudObject, useFindGameObject);
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

        // Recovered 735C2876B9E895F7C27510061FE1D0F4489D6A3B / 63EEF075986807E00B3298E3786EB738B8948843:
        // Delegate.Combine / Delegate.Remove, with no subscription-time replay.
        public static void SubscribeOnConnect(Action<string> callback)
        {
            lock (NotificationGate) m_onConnect += callback;
        }

        public static void UnsubscribeOnConnect(Action<string> callback)
        {
            lock (NotificationGate) m_onConnect -= callback;
        }

        protected static void DispatchNativeNotification(string message)
        {
            Action<string> callback;
            lock (NotificationGate) callback = m_onConnect;
            callback?.Invoke(message);
        }

        // Original 2FFDE96FF85FD4262B9531EC076AC1AA8F226B72 is RET.
        // Local Synchronize is synchronous, so there is no delayed cloud operation.
        public virtual void SetSynchronisationCooldown(float seconds) { }
    }
}
