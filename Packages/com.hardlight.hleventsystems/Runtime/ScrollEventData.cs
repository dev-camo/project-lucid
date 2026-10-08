using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

// Whole original HLEventSystems.Runtime02000004, one private field and one constructor; no accessor is invented.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public sealed class ScrollEventData : BaseEventData
{
    private float m_delta;
    // Original06000010 calls the genuine base constructor before storing the exact float bits.
    public ScrollEventData(EventSystem eventSystem, float scrollDelta) : base(eventSystem) { m_delta = scrollDelta; }
}
