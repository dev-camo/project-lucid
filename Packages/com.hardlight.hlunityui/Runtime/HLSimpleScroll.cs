// Original HLUnityUI.Runtime Hardlight.HLSimpleScroll 020000d3 and its iterator
// <OnScrollWithLerp>d__16 020000d4, reconstructed from the x86-64 bodies
// (0x1B69F90..0x1B6B070) and metadata. OnScrollWithLerp is member ordinal 16 (fields
// 0..8, then OnEnable 9 .. OnScroll 15), which names the iterator; its hoisted local
// names are original. No shipped .prefab/.unity/.asset in the 2026-10-05 reference
// export references the exported script GUID cffce5165732ecd3364802eec7a7d270, so the
// script stays unbound with a fresh GUID.
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class HLSimpleScroll : MonoBehaviour, IScrollUpHandler, IEventSystemHandler, IScrollDownHandler
    {
        [SerializeField]
        private int m_scrollPixels = 200;
        [SerializeField]
        private HLScrollRect m_scrollRect;
        [SerializeField]
        private bool m_lerpScroll;
        [SerializeField]
        private float m_lerpTime = 0.3f;
        // Never assigned in the original, so the lerp yields null each frame.
        private WaitForEndOfFrame m_wait;
        private Coroutine m_coroutine;
        private bool m_isActiveScroll;
        private UIRuntimeConfiguration m_configuration;
        private InputBridge m_inputBridge;

        private void OnEnable()
        {
            Setup();
        }

        private void OnDisable()
        {
            ClearUp();
        }

        // 0x060005b7: both inputs are registered on press with joystick index -1.
        private void Setup()
        {
            InputBridge.OnShutdown += ClearUp;
            m_configuration = ProcessManager.GetSystem<UIRuntimeConfiguration>();
            if (m_configuration == null)
                return;
            m_inputBridge = m_configuration.InputBridge;
            m_inputBridge.RegisterInputListenerOnDown(m_configuration.ScrollUpInput, OnScrollUp, -1);
            m_inputBridge.RegisterInputListenerOnDown(m_configuration.ScrollDownInput, OnScrollDown, -1);
        }

        private void ClearUp()
        {
            InputBridge.OnShutdown -= ClearUp;
            if (m_inputBridge == null)
                return;
            m_inputBridge.UnregisterInputListener(m_configuration.ScrollUpInput, OnScrollUp, -1);
            m_inputBridge.UnregisterInputListener(m_configuration.ScrollDownInput, OnScrollDown, -1);
        }

        // 0x060005b9: input only scrolls after a scroll event has armed m_isActiveScroll.
        // Up stops the running lerp unconditionally; Down (0x060005ba) checks it first.
        // SafeStopCoroutine itself ignores a null handle, so both behave the same.
        public void OnScrollUp(float value)
        {
            if (!m_isActiveScroll || !m_scrollRect.ContentNeedsScrolling())
                return;
            if (m_lerpScroll)
            {
                this.SafeStopCoroutine(ref m_coroutine);
                m_coroutine = StartCoroutine(OnScrollWithLerp(value));
            }
            else
            {
                OnScroll(value);
            }
        }

        public void OnScrollDown(float value)
        {
            if (!m_isActiveScroll || !m_scrollRect.ContentNeedsScrolling())
                return;
            if (m_lerpScroll)
            {
                if (m_coroutine != null)
                    this.SafeStopCoroutine(ref m_coroutine);
                m_coroutine = StartCoroutine(OnScrollWithLerp(value));
            }
            else
            {
                OnScroll(value);
            }
        }

        // 0x060005bb: the value drives the single enabled axis; with both or neither axis
        // enabled the step is zero. A clamped rect pulls the target back inside the
        // content bounds before the move, and the scroll is disarmed even when the
        // position does not change, but not when the rect is inactive.
        private void OnScroll(float value)
        {
            if (!m_scrollRect.IsActive())
                return;
            Vector2 direction = Vector2.zero;
            if (m_scrollRect.horizontal && !m_scrollRect.vertical)
                direction.x = value;
            else if (m_scrollRect.vertical && !m_scrollRect.horizontal)
                direction.y = value;
            Vector2 position = m_scrollRect.content.anchoredPosition + direction * m_scrollPixels;
            if (m_scrollRect.movementType == ScrollRect.MovementType.Clamped)
                position += m_scrollRect.ExposeCalculateOffset(position - m_scrollRect.content.anchoredPosition);
            m_scrollRect.SetContentAnchoredPosition(position);
            m_isActiveScroll = false;
        }

        // 0x060005c2: each step adds the growth of Lerp(0, target, timer / m_lerpTime)
        // since the last frame, so the first step is zero and the last one lands short of
        // the target. A positive value subtracts the step when either component is
        // negative; there is no clamping and an inactive rect leaves the scroll armed.
        private IEnumerator OnScrollWithLerp(float value)
        {
            if (!m_scrollRect.IsActive())
                yield break;
            float timer = 0f;
            Vector2 lerpTarget = Vector2.zero;
            if (m_scrollRect.horizontal && !m_scrollRect.vertical)
                lerpTarget.x = value;
            else if (m_scrollRect.vertical && !m_scrollRect.horizontal)
                lerpTarget.y = value;
            lerpTarget *= m_scrollPixels;
            Vector2 cumulativeLerpAmount = Vector2.zero;
            while (timer < m_lerpTime)
            {
                Vector2 lerpAmount = Vector2.Lerp(Vector2.zero, lerpTarget, timer / m_lerpTime) - cumulativeLerpAmount;
                cumulativeLerpAmount += lerpAmount;
                timer += Time.deltaTime;
                // The native test reads the Vector2.zero static (0x1B6AE79..0x1B6AE8C).
                Vector2 position = value > 0f && (lerpAmount.x < Vector2.zero.x || lerpAmount.y < Vector2.zero.y)
                    ? m_scrollRect.content.anchoredPosition - lerpAmount
                    : m_scrollRect.content.anchoredPosition + lerpAmount;
                m_scrollRect.SetContentAnchoredPosition(position);
                yield return m_wait;
            }
            m_isActiveScroll = false;
        }

        public void OnScrollUp(ScrollEventData eventData)
        {
            m_isActiveScroll = true;
        }

        public void OnScrollDown(ScrollEventData eventData)
        {
            m_isActiveScroll = true;
        }
    }
}
