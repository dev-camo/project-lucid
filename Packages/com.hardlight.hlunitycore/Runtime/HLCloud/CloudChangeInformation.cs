using System.Collections.Generic;
using HLCloud.Plugin;

namespace HLCloud
{
    public readonly struct CloudChangeInformation
    {
        public readonly bool IsValid;
        public readonly ChangeReason ChangeReason;
        public readonly long LocalVersion;
        public readonly long CloudVersion;
        public readonly IReadOnlyDictionary<string, string> ChangedKeys;

        // Original0x0600006e: preserve dictionary aliasing; this is not a copy.
        public CloudChangeInformation(ChangeReason changeReason, long localVersion, long cloudVersion,
            IReadOnlyDictionary<string, string> changedKeys)
        {
            IsValid = true;
            ChangeReason = changeReason;
            LocalVersion = localVersion;
            CloudVersion = cloudVersion;
            ChangedKeys = changedKeys;
        }
    }
}
