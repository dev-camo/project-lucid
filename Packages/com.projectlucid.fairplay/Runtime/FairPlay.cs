// Original FairPlay.Runtime.dll:Apple.Arcade.FairPlay.
// Complete methods: 0x06000003, 0x06000004, 0x06000005.
// arm64 ranges: 0x06000003 0x4db07c..0x4db100, 0x06000004 0x4db100..0x4db238, 0x06000005 0x4db238..0x4db240.
// x86_64 ranges: 0x06000003 0x5053b0..0x505430, 0x06000004 0x505430..0x505570, 0x06000005 0x505570..0x505580.
// Original private PInvoke module/entry are FPBundleWrapper/DoRegisterAppWithFairPlay.
// Default calling convention, unspecified character set, zero parameters and mangling match native resolver arguments.
// The original public wrapper logs caught System.Exception then requests Application.Quit.
// This is preserved original code, not an offline adapter; the native plugin is not executed by verification.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Apple.Arcade
{
    public class FairPlay
    {
        [DllImport("FPBundleWrapper")]
        private static extern void DoRegisterAppWithFairPlay();

        public static void RegisterWithFairPlay()
        {
            try
            {
                DoRegisterAppWithFairPlay();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit();
            }
        }

        public FairPlay() { }
    }
}
