// Original HLUnityUI.Runtime Hardlight.UIButton (TypeDef 0x0200006e), from the x86-64 bodies
// 0x1B510C0..0x1B521B0 (methods 0x0600032d..0x0600034c). Its UnityEvent properties are views onto the
// UISelectable event table; button down/up/exit are raised on top of the inherited pointer handling.
// No shipped prefab, scene or asset references the exported script GUID d2eaed523afd040961e01de9618bdad0.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [AddComponentMenu("Hardlight UI/UIButton", 30)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIButton : UIClickable
    {
        [Tooltip("When button is pressed but pointer exits, should we trigger the button to be up or wait for pointer up.")]
        [SerializeField]
        private bool m_pointerExitShouldTriggerButtonUp = true;
        private bool m_wasPointerDownAndInside;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_clickAccepted = false;
        }

        // A button disabled while highlighted reports OnDisabledSelected.
        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_currentSelectionState == SelectionState.Highlighted)
                InvokeEvent(UIEventType.OnDisabledSelected);
        }

        // Reading a missing event returns null without adding it; assigning adds or replaces the entry.
        public UnityEvent OnClick
        {
            get { return GetEventInfo(UIEventType.OnClick).m_onEvent; }
            set { SetEventHandler(UIEventType.OnClick, value); }
        }

        public UnityEvent OnDisabledClick
        {
            get { return GetEventInfo(UIEventType.OnDisabledClick).m_onEvent; }
            set { SetEventHandler(UIEventType.OnDisabledClick, value); }
        }

        public UnityEvent OnButtonDown
        {
            get { return GetEventInfo(UIEventType.OnButtonDown).m_onEvent; }
            set { SetEventHandler(UIEventType.OnButtonDown, value); }
        }

        public UnityEvent OnButtonUp
        {
            get { return GetEventInfo(UIEventType.OnButtonUp).m_onEvent; }
            set { SetEventHandler(UIEventType.OnButtonUp, value); }
        }

        public UnityEvent OnButtonExit
        {
            get { return GetEventInfo(UIEventType.OnButtonExit).m_onEvent; }
            set { SetEventHandler(UIEventType.OnButtonExit, value); }
        }

        public UnityEvent OnOfflineClick
        {
            get { return GetEventInfo(UIEventType.OnOfflineClick).m_onEvent; }
            set { SetEventHandler(UIEventType.OnOfflineClick, value); }
        }

        public void Action_OnClick()
        {
            if (IsActive())
                Press();
        }

        // 0x0600033c presses exactly like Action_OnClick (vtable slot 49 at 0x1B517E7);
        // the disabled-click event comes from Press itself.
        public void Action_OnDisabledClick()
        {
            if (IsActive())
                Press();
        }

        public void Action_OnButtonDown()
        {
            if (IsActive() && IsInteractable())
                InvokeEvent(UIEventType.OnButtonDown);
        }

        public void Action_OnButtonUp()
        {
            if (IsActive() && IsInteractable())
                InvokeEvent(UIEventType.OnButtonUp);
        }

        public void Action_OnButtonExit()
        {
            if (IsActive() && IsInteractable())
                InvokeEvent(UIEventType.OnButtonExit);
        }

        public override bool Press()
        {
            bool pressed = base.Press();
            if (pressed)
                InvokeEvent(UIEventType.OnClick);
            else if (!IsInteractable())
                InvokeEvent(UIEventType.OnDisabledClick);
            return pressed;
        }

        // Any button reports OnButtonDown while the left button is held, not only the left press itself.
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (IsPointerDown && InteractionOverride())
                InvokeEvent(UIEventType.OnButtonDown);
        }

        // The decision is taken before the base clears IsPointerDown, so a held left button also
        // reports OnButtonUp for another button's release.
        public override void OnPointerUp(PointerEventData eventData)
        {
            bool buttonUp = IsPointerDown && (IsPointerInside || (!m_pointerExitShouldTriggerButtonUp && m_wasPointerDownAndInside));
            m_wasPointerDownAndInside = false;
            base.OnPointerUp(eventData);
            if (buttonUp)
                InvokeEvent(UIEventType.OnButtonUp);
        }

        // Re-entering the selected button while it is still held reports OnButtonDown again.
        public override void OnPointerEnter(PointerEventData eventData)
        {
            bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;
            bool reenteringWhileDown = IsPointerDown && !IsPointerInside;
            base.OnPointerEnter(eventData);
            if (isSelected && reenteringWhileDown)
                InvokeEvent(UIEventType.OnButtonDown);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            bool leavingWhileDown = IsPointerDown && IsPointerInside;
            m_wasPointerDownAndInside = leavingWhileDown && !m_pointerExitShouldTriggerButtonUp;
            bool buttonUp = leavingWhileDown && m_pointerExitShouldTriggerButtonUp;
            base.OnPointerExit(eventData);
            if (leavingWhileDown)
                InvokeEvent(UIEventType.OnButtonExit);
            if (buttonUp)
                InvokeEvent(UIEventType.OnButtonUp);
        }

        public void PublishUIMessage(UIEventType type, UIEvent eventId)
        {
            ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIMessage>>()
                .PublishMessage(new UIMessage(type), eventId);
        }

        public void PublishUIMessageOnClick(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnClick, eventId);
        }

        public void PublishUIMessageOnDisabledClick(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnDisabledClick, eventId);
        }

        public void PublishUIMessageOnOfflineClick(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnOfflineClick, eventId);
        }

        public void PublishUIMessageOnButtonDown(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnButtonDown, eventId);
        }

        public void PublishUIMessageOnButtonUp(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnButtonUp, eventId);
        }

        public void PublishUIMessageOnButtonExit(UIEvent eventId)
        {
            PublishUIMessage(UIEventType.OnButtonExit, eventId);
        }
    }
}
