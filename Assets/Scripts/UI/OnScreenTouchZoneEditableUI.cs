using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200097d; bodies translated from the 1.10.1 x86-64 binary.
    // A touch zone that follows an editable UI component: it exposes the touch for the layout editor and
    // supplies no game input of its own.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OnScreenTouchZoneEditableUI : OnScreenTouchZone, IUserEditableUIComponent
    {
        [SerializeField] private RectTransform m_editableComponentTransform;
        [SerializeField] private bool m_initiallyActive;
        private Vector2 m_touchPosition;
        private bool m_active;

        // 060036d6 (1.10.1 RVA 0x692C70)
        private void Awake()
        {
            m_active = m_initiallyActive;
        }

        // 060036d7 (RVA 0x692C90)
        protected override void SupplyHeldGameInput(Vector2 resultantMovement)
        {
        }

        // 060036d8 (RVA 0x692CA0)
        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta)
        {
            m_touchPosition = touchPosition;
            return Vector2.zero;
        }

        // 060036d9 (RVA 0x692CE0)
        public void TransformUpdated()
        {
        }

        // 060036da (RVA 0x692CF0)
        public RectTransform GetRectTransform()
        {
            return m_editableComponentTransform;
        }

        // 060036db (RVA 0x692D00)
        public void SetActive(bool active)
        {
            m_active = active;
        }

        // 060036dc (RVA 0x692D10)
        public bool GetTouchPosition(out Vector2 touch)
        {
            touch = m_touchPosition;
            return PointerIsDown;
        }

        // 060036dd (RVA 0x692D60): the pinch distances read 0 unless a pinch is in progress.
        public bool GetPinchInfo(out float pinchDistance, out float initialPinchDistance)
        {
            pinchDistance = PinchDistance;
            initialPinchDistance = InitialPinchDistance;
            return IsPinching;
        }

        // 060036de (RVA 0x692DA0): editing also turns multi-touch on, without the touch reset of SetMultiTouchEnabled.
        public void SetEditing(bool editing)
        {
            m_active = editing;
            MultiTouchEnabled = true;
        }

        // 060036df (RVA 0x692DC0)
        public override bool IsActive()
        {
            return gameObject.activeInHierarchy && m_active;
        }

        // 060036e0 (RVA 0x692E00): a touch on the component, or any touch already tracked, scores 0; otherwise the
        // score is the squared distance from the touch to the component's centre.
        protected override float GetScoreForTouchPosition(Vector2 position)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(m_editableComponentTransform, position, null)) return 0f;
            if (PointerIsDown) return 0f;
            Vector2 local = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_editableComponentTransform, position, null, out local);
            return (m_editableComponentTransform.rect.center - local).sqrMagnitude;
        }

        // 060036e1 (RVA 0x692F00)
        public OnScreenTouchZoneEditableUI()
        {
        }
    }
}
