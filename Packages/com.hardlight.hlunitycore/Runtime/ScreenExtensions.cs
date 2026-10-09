using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLUnityCore.Runtime 0x0200007e: complete original static provider. The
    // shipped Mac architectures both tail-call Screen.dpi without a fallback.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ScreenExtensions
    {
        private const float SwitchDPI = 237f; // 0x040001d3, unused on shipped Mac
        public static float DPI() => Screen.dpi; // 0x06000303
    }
}
