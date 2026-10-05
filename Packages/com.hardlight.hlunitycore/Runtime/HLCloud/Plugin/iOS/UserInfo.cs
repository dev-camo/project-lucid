using System;

namespace HLCloud.Plugin.iOS
{
    [Serializable]
    public class UserInfo
    {
        public enum NSUbiquitousKeyValueStoreChangeReason
        {
            NSUbiquitousKeyValueStoreServerChange = 0,
            NSUbiquitousKeyValueStoreInitialSyncChange = 1,
            NSUbiquitousKeyValueStoreQuotaViolationChange = 2,
            NSUbiquitousKeyValueStoreAccountChange = 3
        }

        public NSUbiquitousKeyValueStoreChangeReason NSUbiquitousKeyValueStoreChangeReasonKey;
        public string[] NSUbiquitousKeyValueStoreChangedKeysKey;
    }
}
