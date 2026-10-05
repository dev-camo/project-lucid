using System;
using UnityEngine;

namespace Hardlight
{
    // Partial diagnostic API. Static plugin initialization, runtime callbacks
    // and the registered HLUnityCore handler path have not been recovered.
    public static class HLOutput
    {
        // HLUnityCore.Runtime.dll:Hardlight.HLOutput:0x06000a77;
        // arm64 0x1afcb9c: no operation when HLUnityCore is not registered.
        // The generic default registry name is typeof(HLUnityCore).ToString().
        public static void LogError(object message, UnityEngine.Object context = null)
        {
            if (ProcessManager.IsSystemNull("Hardlight.HLUnityCore")) return;
            throw new NotSupportedException("Original registered HLUnityCore error-handler routing is not recovered.");
        }
    }
}
