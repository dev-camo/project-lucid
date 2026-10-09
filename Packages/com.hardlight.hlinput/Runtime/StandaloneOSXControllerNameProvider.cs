using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class StandaloneOSXControllerNameProvider : BaseControllerProvider
    {
        // 06000063: preserved original macOS plugin import; portable routing must avoid it.
        // Two-CPU wrapper bytes back the exact library/entry and default descriptor.
        [DllImport("macOSControllerPlugin")]
        private static extern string GetControllerNamesNative();

        public StandaloneOSXControllerNameProvider(float controllerConnectionPollingRateInSeconds)
            : base(controllerConnectionPollingRateInSeconds) { } // 06000064

        public override IReadOnlyList<string> GetControllerNames() // 06000065
        {
            string controllerNames = GetControllerNamesNative();
            if (string.IsNullOrEmpty(controllerNames)) return Array.Empty<string>();
            return controllerNames.Split(',', StringSplitOptions.None);
        }
    }
}
