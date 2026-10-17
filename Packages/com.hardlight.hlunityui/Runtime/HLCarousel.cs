// Original HLUnityUI.Runtime UnityEngine.UI.HLCarousel 02000015 (MethodDefs
// 0x06000089..91) and its nested ScrollDirection enum 02000016, translated from
// the x86-64 bodies. The carousel is an HLScrollRect whose content is flanked by
// two cap copies (the last child before the first, the first child after the
// last). Once the normalised position passes either end by more than the wrap
// offset (m_wrapOffset, half an item step) it jumps back or forward by 1 plus one
// item step.
using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace UnityEngine.UI
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLCarousel : HLScrollRect
    {
        private enum ScrollDirection
        {
            Horizontal = 0,
            Vertical = 1
        }

        [SerializeField]
        private ScrollDirection m_scrollDirection;

        [SerializeField]
        private RectTransform m_startCap;

        [SerializeField]
        private RectTransform m_endCap;

        private const float MinBound = 0f;
        private const float MaxBound = 1f;
        private const float HorizontalDirection = -1f;
        private const float VerticalDirection = 1f;
        private const float DoubleFloat = 2f;
        private const float HalfFloat = 0.5f;

        private Vector2 m_wrapOffset;

        // 0x06000089 @0x1B3C760: only the horizontal axis wraps when both are enabled.
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (horizontal)
                WrapAxis(horizontalNormalizedPosition, 0, m_wrapOffset.x);
            else if (vertical)
                WrapAxis(verticalNormalizedPosition, 1, m_wrapOffset.y);
        }

        // 0x0600008a @0x1B3C7D0. Moving the position also moves the drag origin by the
        // same content shift, and the velocity from before the jump is kept. An
        // approximately-zero wrap offset (Mathf.Approximately, inlined at
        // 0x1b3c888..8c7) pins an in-range position to the start.
        private void WrapAxis(float axisNormalisedPosition, int axis, float wrapOffset)
        {
            float previousContentPosition = content.anchoredPosition[axis];
            Vector2 previousVelocity = velocity;
            float wrappedPosition;
            if (axisNormalisedPosition > MaxBound + wrapOffset)
                wrappedPosition = axisNormalisedPosition - (wrapOffset * DoubleFloat + MaxBound);
            else if (axisNormalisedPosition < MinBound - wrapOffset)
                wrappedPosition = axisNormalisedPosition + (wrapOffset * DoubleFloat + MaxBound);
            else if (Mathf.Approximately(wrapOffset, 0f))
                wrappedPosition = MinBound;
            else
                return;

            SetNormalizedPosition(wrappedPosition, axis);
            m_ContentStartPosition[axis] += content.anchoredPosition[axis] - previousContentPosition;
            UpdatePrevData();
            velocity = previousVelocity;
        }

        // 0x0600008b @0x1B3C980.
        public void SetWrapOffset(Vector2 wrapOffset)
        {
            m_wrapOffset = wrapOffset;
        }

        // 0x0600008c @0x1B3C990: scrolling stays disabled until there are two items
        // besides the caps; the wrap offset is half an item step on the scroll axis.
        public void SetupCarousel()
        {
            horizontal = false;
            vertical = false;
            int itemCount = content.childCount;
            if (m_startCap != null)
                itemCount--;
            if (m_endCap != null)
                itemCount--;

            float itemWrapOffset = itemCount < 2 ? 0f : HalfFloat / (itemCount - 1);
            m_wrapOffset = m_scrollDirection == ScrollDirection.Horizontal
                ? new Vector2(itemWrapOffset, 0f)
                : new Vector2(0f, itemWrapOffset);
            if (itemCount >= 2)
            {
                horizontal = m_scrollDirection == ScrollDirection.Horizontal;
                vertical = m_scrollDirection == ScrollDirection.Vertical;
            }
        }

        // 0x0600008d @0x1B3CAA0: a cap with a LayoutElement is taken out of the layout
        // and placed over its source item, then shifted one anchor span along the
        // enabled axis (horizontal decides, otherwise vertical).
        private void SetupCap(RectTransform cap, RectTransform parentCap, float direction)
        {
            LayoutElement layoutElement = cap.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = true;
                cap.sizeDelta = parentCap.sizeDelta;
                cap.anchorMin = parentCap.anchorMin;
                cap.anchorMax = parentCap.anchorMax;
                cap.anchoredPosition = parentCap.anchoredPosition;
            }

            Vector2 anchorMin = cap.anchorMin;
            Vector2 anchorMax = cap.anchorMax;
            Vector2 offset = horizontal ? new Vector2(direction, 0f) : new Vector2(0f, direction);
            cap.anchorMin = anchorMin + offset;
            cap.anchorMax = anchorMax + offset;
        }

        // 0x0600008e @0x1B3CC00.
        public void RecreateCaps(Action<RectTransform> onCreateStartCap, Action<RectTransform> onCreateEndCap)
        {
            DestroyCaps();
            CreateCaps(onCreateStartCap, onCreateEndCap);
        }

        // 0x0600008f @0x1B3CC30.
        public void DestroyCaps()
        {
            if (m_startCap != null)
            {
                DestroyImmediate(m_startCap.gameObject);
                m_startCap = null;
            }
            if (m_endCap != null)
            {
                DestroyImmediate(m_endCap.gameObject);
                m_endCap = null;
            }
        }

        // 0x06000090 @0x1B3CD50: both sources are read before either cap exists. The
        // start cap copies the last child and moves to the front; the end cap copies
        // the first child and stays last. Each callback runs before its cap is placed.
        public void CreateCaps(Action<RectTransform> onCreateStartCap, Action<RectTransform> onCreateEndCap)
        {
            RectTransform lastChild = (RectTransform)content.GetChild(Mathf.Max(content.childCount - 1, 0));
            RectTransform firstChild = (RectTransform)content.GetChild(0);

            m_startCap = Instantiate(lastChild, lastChild.parent);
            onCreateStartCap?.Invoke(m_startCap);
            m_startCap.SetSiblingIndex(0);

            m_endCap = Instantiate(firstChild, firstChild.parent);
            onCreateEndCap?.Invoke(m_endCap);

            float direction = horizontal ? HorizontalDirection : VerticalDirection;
            SetupCap(m_startCap, lastChild, direction);
            SetupCap(m_endCap, firstChild, -direction);
        }

        // 0x06000091 @0x1B3CF50.
        public HLCarousel() { }
    }
}
