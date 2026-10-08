using System;
using Hardlight.UI.Binding;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetButton : UIWidget, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] protected UnityEngine.UI.Button m_uiButton;
        public readonly Bindable<string> Label = new Bindable<string>();
        public readonly Bindable<bool> Selected = new Bindable<bool>();
        private Action m_callback;
        private UnityAction m_pointerDownListeners = () => { };
        private UnityAction m_pointerUpListeners = () => { };

        // Original 06000199 calls Selectable.IsInteractable(), including its virtual behavior.
        public bool IsInteractable => m_uiButton.IsInteractable();

        // Original 0600019a. The inherited original Setup has an empty body.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            if (parameters is UIWidgetButtonParameters data)
            {
                Label.Value = data.Label;
                m_callback = data.Callback;
            }
            else
                HLOutput.LogError("Could not set up UIWidgetButton with given data.");
        }

        // Original 0600019b uses Unity object equality, then rereads the field.
        public void SetInteractable(bool interactable)
        {
            if (m_uiButton == null) return;
            m_uiButton.interactable = interactable;
        }

        // Original 0600019c reads the callback after the bindable's virtual setter.
        public virtual void Action_OnButtonPress()
        {
            Selected.Value = true;
            m_callback?.Invoke();
        }

        // Original 0600019d duplicates the body rather than invoking the virtual action.
        public void OnButtonPress(bool invokeClickEvent)
        {
            Selected.Value = true;
            m_callback?.Invoke();
            if (invokeClickEvent) m_uiButton.onClick.Invoke();
        }

        // Original 0600019e. Exact original MethodRefs resolve both genuine generic APIs.
        // Fresh original metadata identifies the real message struct and enum literals.
        public void Action_PublishUIMessage(UIModernEvent eventId)
        {
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var message = new UIModernMessage(UIModernEventType.OnClick);
            exchange.PublishMessage(in message, in eventId, MessageBroadcastType.Both);
        }

        // Original 0600019f.
        public void SetSelected(bool selected) => Selected.Value = selected;

        // Original 060001a0.
        public void AddPointerDownListener(UnityAction onPointerDown) => m_pointerDownListeners += onPointerDown;

        // Original 060001a1 writes offset 0x48 in BOTH shipping architectures.
        // Preserve the original up-field replacement and its intermediate null publication.
        public void ClearPointerDownListeners()
        {
            m_pointerUpListeners = null;
            m_pointerUpListeners = () => { };
        }

        // Original 060001a2.
        public void AddPointerUpListener(UnityAction onPointerUp) => m_pointerUpListeners += onPointerUp;

        // Original 060001a3.
        public void ClearPointerUpListeners()
        {
            m_pointerUpListeners = null;
            m_pointerUpListeners = () => { };
        }

        // Original 060001a4/a5 ignore eventData and invoke their fields unconditionally.
        public void OnPointerDown(PointerEventData eventData) => m_pointerDownListeners();
        public void OnPointerUp(PointerEventData eventData) => m_pointerUpListeners();

        // Original 060001a6: field initializers precede the MonoBehaviour base call.
        // Six original cache methods 060001a7..060001ac are compiler generated;
        // exact emitted identities and native binding are held pending a real CIL comparison.
        public UIWidgetButton() { }
    }
}
