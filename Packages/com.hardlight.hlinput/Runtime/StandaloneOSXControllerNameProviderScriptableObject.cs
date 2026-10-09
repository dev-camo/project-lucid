using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/ControllerProviders/Create StandaloneOSXControllerNameProvider")]
    public class StandaloneOSXControllerNameProviderScriptableObject : ScriptableControllerProvider
    {
        public override BaseControllerProvider GetControllerNameProvider(float controllerConnectionPollingRateInSeconds)
        { return new StandaloneOSXControllerNameProvider(controllerConnectionPollingRateInSeconds); } // 06000273
        public override bool IsActiveProvider() { return false; } // 06000274: actual shipped two-CPU result.
        public StandaloneOSXControllerNameProviderScriptableObject() { } // 06000275
    }
}
