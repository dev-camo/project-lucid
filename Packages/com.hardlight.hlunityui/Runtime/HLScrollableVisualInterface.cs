using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 020000c9: fieldless MonoBehaviour implementing the
    // real fieldless HLEventSystems marker and inherited event-system marker.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(HLScrollRect))]
    public sealed class HLScrollableVisualInterface : MonoBehaviour, IScrollFlexible, IEventSystemHandler
    {
        // Original 0600056c, base-only on both shipping CPUs.
        public HLScrollableVisualInterface() { }
    }
}
