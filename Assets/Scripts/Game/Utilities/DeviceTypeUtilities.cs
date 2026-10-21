using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a01; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class DeviceTypeUtilities
    {
        // Declared by the original but read by no shipped body.
        private const string UnsupportedPlatform = "Unsupported";

        // 0600396f (1.10.1 RVA 0x69BBF0): the shipped body is a tail call to SystemInfo.deviceModel.
        public static string GetAsString()
        {
            return SystemInfo.deviceModel;
        }
    }
}
