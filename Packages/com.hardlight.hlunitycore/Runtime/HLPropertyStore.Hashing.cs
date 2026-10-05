using System;
using UnityEngine;

namespace Hardlight
{
    // Original type 0x020001fa; hashing and static defaults are independent of
    // the local disk adapter used by the recovered store lifecycle.
    public partial class HLPropertyStore : ISystem
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

        // 0x06000d16; arm64 0x1b0dcf8 passes Case.Lower (1).
        public static uint GetCRC(string value) { return HLCRC32.Generate(value, HLCRC32.Case.Lower); }
    }
}
