// Original HLUnityUI.Runtime HLParentScrollRectBlocker 0200000c (global namespace) and
// its display class <>c__DisplayClass2_0 0200000d, reconstructed from the x86-64 bodies
// (0x1B3B8B0..0x1B3BAC0, DoForFirstFoundParent<object> 0x92FBC0) and metadata. The
// display class name comes from member ordinal 2 (the two fields are 0 and 1). No
// shipped .prefab/.unity/.asset in the 2026-10-05 reference export references the
// exported script GUID abf8b6457fbb9c46091e46a86f63264f, so the script stays unbound
// with a fresh GUID.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
public class HLParentScrollRectBlocker : UIBehaviour, IInitializePotentialDragHandler, IEventSystemHandler,
    IBeginDragHandler, IEndDragHandler, IDragHandler
{
    private int m_trackedPointerId = -1;
    private bool m_trackingPointer;

    // 0x0600006e: only a left-button press is forwarded to the first parent handler.
    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        DoForFirstFoundParent<IInitializePotentialDragHandler>(parent => parent.OnInitializePotentialDrag(eventData));
    }

    // 0x0600006f: the first pointer to begin a drag is tracked until its drag ends.
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (m_trackingPointer)
            return;
        m_trackedPointerId = eventData.pointerId;
        m_trackingPointer = true;
    }

    // 0x06000070: the pointer id is kept; only the tracking flag is released.
    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (m_trackedPointerId == eventData.pointerId)
            m_trackingPointer = false;
    }

    // 0x06000071: handling the drag here keeps it from reaching a parent scroll rect.
    public virtual void OnDrag(PointerEventData eventData)
    {
    }

    // 0x06000072 (shared 0x92FBC0) matches HLScrollRect's helper: each ancestor's
    // components are tested in order, only the first match is invoked, and the parent is
    // still read after the successful dispatch.
    private void DoForFirstFoundParent<T>(Action<T> action) where T : IEventSystemHandler
    {
        Transform parent = transform.parent;
        bool found = false;
        while (!found && parent != null)
        {
            foreach (Component component in parent.GetComponents<Component>())
            {
                if (component is T)
                {
                    action((T)(IEventSystemHandler)component);
                    found = true;
                    break;
                }
            }
            parent = parent.parent;
        }
    }
}
