using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009c2; bodies translated from the 1.10.1 x86-64 binary.
    // A checkbox that queues the modern UI events authored for its checked or unchecked state.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(Toggle))]
    public sealed class UIWidgetSDTCheckbox : UIWidget
    {
        [SerializeField] private List<UIModernEvent> m_openEvents;
        [SerializeField] private List<UIModernEvent> m_closeEvents;

        // 0x0600382c (0x69FAD0): the events are added to the application's pending list without duplicates.
        public void Action_Toggle(bool value)
        {
            List<UIModernEvent> events = value ? m_openEvents : m_closeEvents;
            List<UIModernEvent> pendingEvents = ProcessManager.GetSystem<App>().GetUIModernEvents();
            foreach (UIModernEvent uiModernEvent in events)
                pendingEvents.AddUnique(uiModernEvent);
        }

        // 0x0600382d (0x69FC80)
        public UIWidgetSDTCheckbox() { }
    }
}
