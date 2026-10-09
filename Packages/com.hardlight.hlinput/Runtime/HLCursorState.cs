using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.EventSystems;

[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public class HLCursorState
{
    private List<HLButtonState> m_TrackedButtons = new List<HLButtonState>();

    // Original HLInput.Runtime 0x06000009. Repeated indexers and live Count reads are
    // retained; a null tracked entry faults while inspecting its button value.
    public HLButtonState GetButtonState(PointerEventData.InputButton button)
    {
        HLButtonState result = null;
        for (int i = 0; i < m_TrackedButtons.Count; ++i)
        {
            if (m_TrackedButtons[i].button == button)
            {
                result = m_TrackedButtons[i];
                break;
            }
        }
        if (result == null)
        {
            result = new HLButtonState();
            result.button = button;
            result.eventData = new PointerInputModule.MouseButtonEventData();
            m_TrackedButtons.Add(result);
        }
        return result;
    }

    // Original 0x0600000a implicit constructor initializes the list before Object's ctor.
}

[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public class HLButtonState
{
    private PointerEventData.InputButton m_Button;
    private PointerInputModule.MouseButtonEventData m_EventData;

    // Original HLInput.Runtime 0x06000004/05, then 0x06000006/07.
    public PointerInputModule.MouseButtonEventData eventData
    {
        get { return m_EventData; }
        set { m_EventData = value; }
    }
    public PointerEventData.InputButton button
    {
        get { return m_Button; }
        set { m_Button = value; }
    }

    // Original 0x06000008 implicit public constructor only calls Object's constructor.
}
