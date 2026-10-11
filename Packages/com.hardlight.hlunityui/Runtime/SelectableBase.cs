// Original HLUnityUI.Runtime research reconstruction.
// Source/native preservation only: provider algorithms, current generated layout,
// Unity/menu/bootstrap/native faults and full gameplay remain unqualified.
// 0x020000fc Hardlight.SelectableBase
// 0x06000708 System.Void OnMove(UnityEngine.EventSystems.AxisEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x06000709 System.Void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070a System.Void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070b System.Void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070c System.Void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070d System.Void OnSelect(UnityEngine.EventSystems.BaseEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070e System.Void OnDeselect(UnityEngine.EventSystems.BaseEventData eventData) | abstract declaration; arm64=0 / x86_64=0
// 0x0600070f System.Boolean IsInteractable() | abstract declaration; arm64=0 / x86_64=0
// 0x06000710 System.Void .ctor() | arm64 0x1b79bcc..0x1b79bd4; x86_64 0x1b71550..0x1b71560
using UnityEngine.EventSystems;

namespace Hardlight
{
    public abstract class SelectableBase : UIBehaviour, IMoveHandler, IEventSystemHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        public abstract void OnMove(AxisEventData eventData);
        public abstract void OnPointerDown(PointerEventData eventData);
        public abstract void OnPointerUp(PointerEventData eventData);
        public abstract void OnPointerEnter(PointerEventData eventData);
        public abstract void OnPointerExit(PointerEventData eventData);
        public abstract void OnSelect(BaseEventData eventData);
        public abstract void OnDeselect(BaseEventData eventData);
        public abstract bool IsInteractable();
        protected SelectableBase() { }
    }
}
