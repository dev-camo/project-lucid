using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

// Whole original HLEventSystems.Runtime02000003, one field and two methods.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public sealed class LastInputTypeUpdateEventData : BaseEventData
{
    private int m_newInputType;
    // Original0600000e calls the genuine base constructor before storing the supplied integer.
    public LastInputTypeUpdateEventData(EventSystem eventSystem, int newInputType) : base(eventSystem) { m_newInputType = newInputType; }
    // Original0600000f.
    public int NewInputType => m_newInputType;
}
