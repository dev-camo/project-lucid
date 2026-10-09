using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CursorBase : MonoBehaviour
    {
        protected PointerEventData m_cursorData;
        protected HLCursorState m_cursorState;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_cursorButtonInput;
        private bool m_buttonDown;
        private bool m_buttonUp;

        // Original HLInput.Runtime 0x060000bb/0xbc: actual abstract contracts.
        public abstract RaycastResult GetRaycastResult();
        public abstract void Process();

        // Original 0x060000bd. Both transient flags are cleared together after processing.
        public void PostProcess() { m_buttonDown = false; m_buttonUp = false; }

        // Original 0x060000be/0xbf: actual abstract contracts.
        public abstract HLCursorState GetCursorEventData();
        public abstract bool CursorVisible();

        // Original 0x060000c0. EventSystem.current is evaluated before allocating event data.
        protected virtual void Awake()
        {
            m_cursorData = new PointerEventData(EventSystem.current);
            m_cursorState = new HLCursorState();
        }

        // Original 0x060000c1. Numeric-zero inputs are passed through rather than filtered.
        protected virtual void OnEnable()
        {
            ControlMapping.Subscribe(m_cursorButtonInput, OnCursorButtonDown, InputTrigger.Down, -1);
            ControlMapping.Subscribe(m_cursorButtonInput, OnCursorButtonUp, InputTrigger.Up, -1);
        }

        // Original 0x060000c2. Delegate removal order matches registration order.
        protected virtual void OnDisable()
        {
            ControlMapping.Unsubscribe(m_cursorButtonInput, OnCursorButtonDown, -1);
            ControlMapping.Unsubscribe(m_cursorButtonInput, OnCursorButtonUp, -1);
        }

        // Original 0x060000c3 really returns false; subclasses may override this.
        public virtual bool IsInputBlocked() { return false; }
        // Original 0x060000c4/0xc5 ignore the float value and retain both flags until PostProcess.
        private void OnCursorButtonUp(float value) { m_buttonUp = true; }
        private void OnCursorButtonDown(float value) { m_buttonDown = true; }

        // Original 0x060000c6: native return values 0/1/2/3 are the real Unity FramePressState.
        protected PointerEventData.FramePressState StateForMouseButton()
        {
            if (m_buttonDown)
                return m_buttonUp ? PointerEventData.FramePressState.PressedAndReleased : PointerEventData.FramePressState.Pressed;
            return m_buttonUp ? PointerEventData.FramePressState.Released : PointerEventData.FramePressState.NotChanged;
        }

        // Original 0x060000c7 implicit protected constructor only calls MonoBehaviour's ctor.
    }
}
