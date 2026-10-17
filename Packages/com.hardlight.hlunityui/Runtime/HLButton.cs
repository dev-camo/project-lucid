// Original HLUnityUI.Runtime.dll Hardlight.HLButton, TypeDef020000db; one complete owner.
// Source spelling is inferred from shipping metadata and both native slices; C# bytes unavailable.
// All22 own methods/14 fields/5 properties are preserved. No current compiler/Engine/runtime credit.
// Ordered parent and field annotations retain literal metadata order. Static FastAction fields remain null.
// Five UnityEvents are allocated in field order before the genuine base constructor; OnDisabledSelected remains null.
// OnEnable calls the real base then clears m_clickAccepted. OnDisable calls base first, then invokes the public
// event only for literal Highlighted=1; no null guard, listener allocation or shutdown exception suppression.
// Both click Actions call virtual Press only if IsActive. Down/Up Actions additionally test IsInteractable,
// invoke their UnityEvent first, and read the mutable static sound action plus audio ID afterwards.
// Press captures the base result, invokes success event/sound or non-interactable failure event/sound,
// then returns the captured result. A false but interactable press invokes neither event. Event reentry may
// change later static action/ID reads. No new offline/network dispatch is inferred from the stored property.
// PointerDown preserves the base input route before down&&InteractionOverride event dispatch; it does not play sound.
// PointerUp and Exit capture down&&inside before base callback/state mutation, then dispatch Up from that capture.
// PointerEnter evaluates the two genuine EventSystem.current reads and selected object before down&&!inside,
// calls the genuine base, then dispatches Down using both prior booleans. No getter-purity or fault equivalence claim.
// Inlined base OnDisable/PointerDown/PointerUp/constructor work is retained as base calls rather than duplicated
// into HLButton. Exact original C# spelling and optimizer/inlining/native null-fault equivalence remain held.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;
using Hardlight.Utils;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [AddComponentMenu("Hardlight UI/HLButton", 30)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLButton : HLCurveAnimatedGraphic
    {
        public static FastAction<HLAudioTypes> OnClickSfx;
        public static FastAction<HLAudioTypes> OnDisabledClickSfx;
        public static FastAction<HLAudioTypes> OnDownSfx;
        public static FastAction<HLAudioTypes> OnUpSfx;
        [SerializeField]
        [HashEnum(typeof(HLAudioTypes))]
        private HLAudioTypes m_onClickSfxId;
        [HashEnum(typeof(HLAudioTypes))]
        [SerializeField]
        private HLAudioTypes m_onDisabledClickSfxId;
        [SerializeField]
        [HashEnum(typeof(HLAudioTypes))]
        private HLAudioTypes m_onDownSfxId;
        [HashEnum(typeof(HLAudioTypes))]
        [SerializeField]
        private HLAudioTypes m_onUpSfxId;
        [SerializeField] protected UnityEvent m_onClick = new UnityEvent();
        [SerializeField] protected UnityEvent m_onDisabledClick = new UnityEvent();
        [SerializeField] protected UnityEvent m_onOfflineClick = new UnityEvent();
        [SerializeField] protected UnityEvent m_onButtonDown = new UnityEvent();
        [SerializeField] protected UnityEvent m_onButtonUp = new UnityEvent();
        public UnityEvent OnDisabledSelected;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_clickAccepted = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (CurrentSelectionState == SelectionState.Highlighted)
                OnDisabledSelected.Invoke();
        }

        public UnityEvent OnClick { get { return m_onClick; } set { m_onClick = value; } }
        public UnityEvent OnDisabledClick { get { return m_onDisabledClick; } set { m_onDisabledClick = value; } }
        public UnityEvent OnButtonDown { get { return m_onButtonDown; } set { m_onButtonDown = value; } }
        public UnityEvent OnButtonUp { get { return m_onButtonUp; } set { m_onButtonUp = value; } }
        public UnityEvent OnOfflineClick { get { return m_onOfflineClick; } set { m_onOfflineClick = value; } }

        public void Action_OnClick()
        {
            if (IsActive()) Press();
        }

        public void Action_OnDisabledClick()
        {
            if (IsActive()) Press();
        }

        public void Action_OnButtonDown()
        {
            if (IsActive() && IsInteractable())
            {
                m_onButtonDown.Invoke();
                OnDownSfx.Invoke(m_onDownSfxId);
            }
        }

        public void Action_OnButtonUp()
        {
            if (IsActive() && IsInteractable())
            {
                m_onButtonUp.Invoke();
                OnUpSfx.Invoke(m_onUpSfxId);
            }
        }

        public override bool Press()
        {
            bool accepted = base.Press();
            if (accepted)
            {
                m_onClick.Invoke();
                OnClickSfx.Invoke(m_onClickSfxId);
            }
            else if (!IsInteractable())
            {
                m_onDisabledClick.Invoke();
                OnDisabledClickSfx.Invoke(m_onDisabledClickSfxId);
            }
            return accepted;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (IsPointerDown && InteractionOverride()) m_onButtonDown.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            bool wasPressedInside = IsPointerDown && IsPointerInside;
            base.OnPointerUp(eventData);
            if (wasPressedInside) m_onButtonUp.Invoke();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            bool wasSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            bool wasPressedOutside = IsPointerDown && !IsPointerInside;
            base.OnPointerEnter(eventData);
            if (wasPressedOutside && wasSelected) m_onButtonDown.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            bool wasPressedInside = IsPointerDown && IsPointerInside;
            base.OnPointerExit(eventData);
            if (wasPressedInside) m_onButtonUp.Invoke();
        }

        public HLButton() { }
    }
}
