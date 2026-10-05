using System;
using UnityEngine;

namespace Hardlight
{
    // Original type 0x020001fa. This group restores hashing/static defaults;
    // loading, saving and instance initialization are still being recovered.
    public partial class HLPropertyStore
    {
        public static readonly string DefaultSaveIdentifier;
        public static readonly string DefaultPropertyValue;
        private static readonly WaitForEndOfFrame s_waitForEndOfFrame;
        private static HLPropertyStore s_internalInstance;

        // 0x06000d2d; arm64 0x1b11748. Both defaults are String.Empty.
        static HLPropertyStore()
        {
            DefaultSaveIdentifier = string.Empty;
            DefaultPropertyValue = string.Empty;
            s_waitForEndOfFrame = new WaitForEndOfFrame();
            s_internalInstance = null;
        }

        // 0x06000d03 remains unresolved; do not expose an implicit empty ctor
        // that would appear to initialize a usable property store.
        public HLPropertyStore(string key, int clientVersion, string outputFileName)
        {
            throw new NotSupportedException("Original property-store instance initialization is not recovered.");
        }

        // 0x06000d16; arm64 0x1b0dcf8 passes Case.Lower (1).
        public static uint GetCRC(string value) { return HLCRC32.Generate(value, HLCRC32.Case.Lower); }
    }
}
