using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/ControllerProviders/Create UnityControllerNameProvider")]
    public class UnityControllerNameProviderScriptableObject : ScriptableControllerProvider
    {
        public override BaseControllerProvider GetControllerNameProvider(float controllerConnectionPollingRateInSeconds)
        { return new UnityControllerNameProvider(controllerConnectionPollingRateInSeconds); } // 0600027a
        public override bool IsActiveProvider() { return false; } // 0600027b: preserve original false.
        public UnityControllerNameProviderScriptableObject() { } // 0600027c
    }
}
