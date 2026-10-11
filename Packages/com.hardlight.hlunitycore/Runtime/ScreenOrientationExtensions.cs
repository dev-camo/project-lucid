// Preserved Sonic Dream Team 1.10.1 original 0x06000304 native evidence.
// ARM64 and x86_64 registered body addresses: 0x1ab6b0c, 0x1ab3990.
// Original HLUnityCore.Runtime.dll Hardlight.ScreenOrientationExtensions 0200007f.
// IsLandscape06000304: both CPUs use unsigned (value - 3) < 2; only values3/4 pass.
// Source spelling reconstructed; native optimizer/source fault/current Engine equivalence held.
using UnityEngine;

namespace Hardlight
{
    public static class ScreenOrientationExtensions
    {
        public static bool IsLandscape(this ScreenOrientation screenOrientation)
        {
            return screenOrientation == ScreenOrientation.LandscapeLeft ||
                   screenOrientation == ScreenOrientation.LandscapeRight;
        }
    }
}
