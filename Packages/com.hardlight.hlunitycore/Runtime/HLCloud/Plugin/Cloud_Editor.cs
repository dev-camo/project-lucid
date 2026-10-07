using System.Collections.Generic;
using HLCloud.Plugin.iOS;
using UnityEngine;

namespace HLCloud.Plugin
{
    /// <summary>
    /// Reconstructed original in-memory editor cloud implementation from the
    /// supplied release. Its numeric and Boolean methods intentionally retain
    /// the shipped constant/RET behavior; this class is not the local disk port.
    /// </summary>
    public class Cloud_Editor : Cloud
    {
        private readonly Dictionary<string, string> m_cloudProperties = new Dictionary<string, string>();
        private readonly HashSet<string> m_allCloudKeys = new HashSet<string>();

        // Original 0x06000093 is RET and ignores both arguments.
        public override void InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID) { }

        public override bool Synchronize() => true;

        public override HashSet<string> RetrieveAllCloudKeys(string allCloudKeysSeparator)
        {
            m_allCloudKeys.Clear();
            foreach (KeyValuePair<string, string> property in m_cloudProperties)
                m_allCloudKeys.Add(property.Key);
            // The same mutable set is returned. A later retrieval clears it.
            return m_allCloudKeys;
        }

        public override string StringForKey(string key)
        {
            string value;
            return m_cloudProperties.TryGetValue(key, out value) ? value : string.Empty;
        }

        public override void SetStringForKey(string value, string key) => m_cloudProperties[key] = value;

        public override float FloatForKey(string key) => 0f;
        public override void SetFloatForKey(float value, string key) { }
        public override int IntForKey(string key) => 0;
        public override void SetIntForKey(int value, string key) { }
        public override bool BoolForKey(string key) => true;
        public override void SetBoolForKey(bool value, string key) { }

        public override void RemoveForKey(string key) => m_cloudProperties.Remove(key);

        public override void CloudDidChange(string message)
        {
            UserInfo info = new UserInfo();
            JsonUtility.FromJsonOverwrite(message, info);
            cloudObject.OnCloudChange(info.NSUbiquitousKeyValueStoreChangedKeysKey,
                (ChangeReason)info.NSUbiquitousKeyValueStoreChangeReasonKey);
        }

        // Both collections are allocated in their original field order before
        // the original base constructor. No comparer, loading or storage work.
        public Cloud_Editor() { }
    }
}
