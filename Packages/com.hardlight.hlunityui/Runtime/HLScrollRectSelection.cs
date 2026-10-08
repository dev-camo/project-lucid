using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 020000cd. Source preservation only: original
    // delegate/iterator ordinals, generated fields and runtime binding are held.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLScrollRectSelection : MonoBehaviour, IScrollUpHandler, IScrollDownHandler
    {
        public enum Direction { Vertical, Horizontal }
        public enum Alignment { TopOrLeft, Middle, BottomOrRight, Visible }
        public delegate bool ObjectSelector(RectTransform go);

        private const int NumberOfCorners = 4;
        private static bool s_usePagination;
        [SerializeField] private GameObject m_listContents;
        [SerializeField] private ScrollRect m_scrollRect;
        [SerializeField] private RectTransform m_viewport;
        [Header("Config"), SerializeField] private float m_scrollOffsetPixels;
        [SerializeField] private bool m_scrollFromStartOfList;
        [SerializeField, Range(0f, 1f)] private float m_tolleranceForInstantScrolling = 0.05f;
        [SerializeField, Range(0.1f, 5f)] private float m_scrollDurationInSeconds = 1f;
        [Range(1f, 50f), SerializeField] private float m_scrollSpeedPerSecond = 5f;
        [SerializeField] private Alignment m_alignment;
        [SerializeField, Tooltip("Scroll movement curve.")] private AnimationCurve m_scrollCurve = new AnimationCurve();
        [SerializeField] private int m_mouseScrollByXElements = 1;
        [SerializeField] private bool m_inverseScrollDirection;
        [FormerlySerializedAs("m_defferScrolling"), SerializeField] private bool m_deferScrolling;
        [HideInInspector, SerializeField] private bool m_snapToItems;
        [SerializeField, HideInInspector] private float m_snapMinSpeedThreshold;
        [SerializeField, Tooltip("If left as -1, will use Scroll Duration In Seconds defined above"), HideInInspector]
        private float m_snapDurationSeconds = -1f;
        [SerializeField, Tooltip("If left as null, will use Scroll Curve defined above"), HideInInspector]
        private AnimationCurve m_snappingCurve;
        private Direction m_direction;
        private float m_viewportTransformExtent;
        private float m_viewportTransformHalfSize;
        private float m_viewportTransformStart;
        private float m_viewportTransformEnd;
        private IEnumerator m_scrollCoroutine;
        private WaitForEndOfFrame m_waitForFrameEnd = new WaitForEndOfFrame();
        private List<RectTransform> m_contents = new List<RectTransform>();
        private int m_activeChild;
        private bool m_applySnapOverrides;

        // 0600057d..582: four original virtual policies followed by two getters.
        protected virtual bool NormaliseWhenNegativeOne => true;
        protected virtual bool ClampTargetScrollOffset => true;
        protected virtual bool ShouldWrap => false;
        protected virtual float BookendOffset => 0f;
        public RectTransform CurrentSelection => GetNearestChild();
        public Direction GetDirection => m_direction;

        // 06000583..587: both page methods and their actions ignore instant.
        public void Action_NextPage(bool instant) { NextPage(); }
        public void NextPage(bool instant = false)
        {
            GetBookends(out RectTransform min, out RectTransform max);
            if (min == null || max == null) return;
            ScrollByPage(max, Alignment.TopOrLeft,
                max.transform.GetSiblingIndex() == 0,
                max.transform.GetSiblingIndex() == m_contents.Count - 1);
        }
        public void Action_PreviousPage(bool instant) { PreviousPage(); }
        public void PreviousPage(bool instant = false)
        {
            GetBookends(out RectTransform min, out RectTransform max);
            if (min == null || max == null) return;
            ScrollByPage(min, Alignment.BottomOrRight,
                min.transform.GetSiblingIndex() == 0,
                min.transform.GetSiblingIndex() == m_contents.Count - 1);
        }
        private void ScrollByPage(RectTransform rectTransform, Alignment alignment, bool isFirst = false, bool isLast = false)
        {
            ScrollToObject(rectTransform, alignment, false, false, null, isFirst, isLast);
        }

        // 06000588: only -1 is special; original Transform.GetChild owns bounds.
        public RectTransform GetChild(int childIndex)
        {
            if (childIndex == -1) return null;
            if (m_listContents != null)
            {
                Transform child = m_listContents.transform.GetChild(childIndex);
                if (child != null) return child.gameObject.transform as RectTransform;
            }
            return null;
        }
        // 06000589: capture child count once; selector is mandatory.
        public void SelectChildWhere(ObjectSelector selector)
        {
            if (m_listContents == null) return;
            int childCount = m_listContents.transform.childCount;
            for (int i = 0; i < childCount; ++i)
            {
                RectTransform child = GetChild(i);
                if (selector(child))
                {
                    ScrollToObject(child, m_alignment);
                    return;
                }
            }
        }
        // 0600058a..590: authored overloads and optional parameters.
        public void Action_Select(RectTransform selection) { Select(selection); }
        public void Select(RectTransform selection, bool instant = false, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            ScrollToObject(selection, m_alignment, instant, useSpeed, whenFinishedCallback);
        }
        public void Select(RectTransform selection, Alignment alignmentOverride, bool instant = false, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            ScrollToObject(selection, alignmentOverride, instant, useSpeed, whenFinishedCallback);
        }
        public void Action_ScrollToStart(bool instant) { ScrollToStart(instant); }
        public void ScrollToStart(bool instant = false, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            Vector2 target = m_direction == Direction.Horizontal ? Vector2.zero : Vector2.one;
            Vector2 current = m_scrollRect.normalizedPosition;
            float distance = m_direction == Direction.Horizontal ? Mathf.Abs(target.x - current.x) : Mathf.Abs(target.y - current.y);
            ScrollToOffset(target, instant || distance < m_tolleranceForInstantScrolling, useSpeed, whenFinishedCallback);
        }
        public void Action_ScrollToEnd(bool instant) { ScrollToEnd(instant); }
        public void ScrollToEnd(bool instant = false, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            Vector2 target = m_direction == Direction.Horizontal ? Vector2.one : Vector2.zero;
            Vector2 current = m_scrollRect.normalizedPosition;
            float distance = m_direction == Direction.Horizontal ? Mathf.Abs(target.x - current.x) : Mathf.Abs(target.y - current.y);
            ScrollToOffset(target, instant || distance < m_tolleranceForInstantScrolling, useSpeed, whenFinishedCallback);
        }
        // 06000591: StopCoroutine faults precede clearing the retained iterator.
        private void ScrollToOffset(Vector2 offset, bool instant = false, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            m_scrollRect.velocity = Vector2.zero;
            if (m_scrollCoroutine != null)
            {
                StopCoroutine(m_scrollCoroutine);
                m_scrollCoroutine = null;
            }
            if (instant)
            {
                m_scrollRect.normalizedPosition = offset;
                whenFinishedCallback?.Invoke();
            }
            else
            {
                m_scrollCoroutine = MoveScrollRect(offset, useSpeed, whenFinishedCallback);
                StartCoroutine(m_scrollCoroutine);
            }
        }
        // 06000592..593: setter ignores invalid directions; getter uses horizontal.
        public void SetNormalisedPosition(float position)
        {
            switch (m_direction)
            {
                case Direction.Vertical: m_scrollRect.verticalNormalizedPosition = position; break;
                case Direction.Horizontal: m_scrollRect.horizontalNormalizedPosition = position; break;
            }
        }
        public float GetNormalisedPosition()
        {
            m_scrollRect.CalculateLayoutInputVertical();
            return m_direction == Direction.Vertical ? m_scrollRect.verticalNormalizedPosition : m_scrollRect.horizontalNormalizedPosition;
        }
        // 06000594..595: original snap provider is HLScrollRect.OnDragReleased.
        private void OnEnable()
        {
            CoroutineUtils.WaitForUI(RecalculateInterior);
            if (m_snapToItems) ((HLScrollRect)m_scrollRect).OnDragReleased += SnapToItem;
        }
        private void OnDisable()
        {
            if (m_snapToItems) ((HLScrollRect)m_scrollRect).OnDragReleased -= SnapToItem;
        }
        // 06000596: direction publication precedes fallback viewport and geometry.
        public void RecalculateInterior()
        {
            m_direction = m_scrollRect.horizontal ? Direction.Horizontal : Direction.Vertical;
            if (m_viewport == null) m_viewport = m_scrollRect.viewport;
            CalculateViewportExtents();
            if (m_listContents != null)
                (m_listContents.transform as RectTransform).GetImmediateChildren(m_contents);
        }
        // 06000597: from-start mutation precedes calculating the target.
        private void ScrollToObject(RectTransform selection, Alignment alignment = Alignment.TopOrLeft, bool instant = false,
            bool useSpeed = false, Action whenFinishedCallback = null, bool isFirst = false, bool isLast = false)
        {
            if (m_scrollFromStartOfList)
                m_scrollRect.normalizedPosition = m_direction == Direction.Horizontal ? Vector2.zero : Vector2.one;
            float offset = CalculateTargetScrollOffset(selection, alignment, false);
            Vector2 target = m_direction == Direction.Horizontal ? new Vector2(offset, 0f) : new Vector2(0f, offset);
            if (offset == -1f && NormaliseWhenNegativeOne) target = m_scrollRect.normalizedPosition;
            if (!ShouldWrap)
            {
                if (isFirst) target = Vector2.one;
                if (isLast) target = Vector2.zero;
            }
            ScrollToOffset(target, instant, useSpeed, whenFinishedCallback);
        }

        // 06000598 and original iterator 060005a9..ae. Both paths retain original
        // callback/fault order; no cleanup is introduced around exceptions.
        private IEnumerator MoveScrollRect(Vector2 target, bool useSpeed = false, Action whenFinishedCallback = null)
        {
            Vector2 startPosition = m_scrollRect.normalizedPosition;
            if (!useSpeed)
            {
                float currentTime = 0f;
                float durationSeconds = m_scrollDurationInSeconds;
                AnimationCurve scrollCurve = m_scrollCurve;
                if (m_applySnapOverrides)
                {
                    if (m_snapDurationSeconds >= 0f) durationSeconds = m_snapDurationSeconds;
                    if (m_snappingCurve != null) scrollCurve = m_snappingCurve;
                    m_applySnapOverrides = false;
                }
                while (currentTime <= durationSeconds)
                {
                    m_scrollRect.normalizedPosition = Vector2.Lerp(startPosition, target, scrollCurve.Evaluate(currentTime / durationSeconds));
                    currentTime += Time.deltaTime;
                    yield return null;
                }
                m_scrollRect.normalizedPosition = target;
                whenFinishedCallback?.Invoke();
                scrollCurve = null;
            }
            else
            {
                float differenceX = target.x - startPosition.x;
                float differenceY = target.y - startPosition.y;
                // Shipping ARM1b72338..374 and x861b69b8c..bd7 select these
                // unusual values for both signs. Ordered nonzero is intentional.
                float directionX = Mathf.Abs(differenceX) < m_tolleranceForInstantScrolling ? 0f :
                    (differenceX > 0f || differenceX < 0f ? 1f : -1f);
                float directionY = Mathf.Abs(differenceY) < m_tolleranceForInstantScrolling ? 0f :
                    (differenceY > 0f || differenceY < 0f ? -1f : 1f);
                // The original performs these two discarded getters before its loop.
                float unusedX = m_scrollRect.normalizedPosition.x;
                float unusedY = m_scrollRect.normalizedPosition.y;
                while (Mathf.Abs(m_scrollRect.normalizedPosition.x - target.x) > m_tolleranceForInstantScrolling ||
                       Mathf.Abs(m_scrollRect.normalizedPosition.y - target.y) > m_tolleranceForInstantScrolling)
                {
                    float delta = Time.deltaTime * m_scrollSpeedPerSecond;
                    float x = m_scrollRect.normalizedPosition.x + delta * directionX;
                    float y = m_scrollRect.normalizedPosition.y + delta * directionY;
                    if ((directionX < 0f && x < target.x) || (directionX > 0f && x > target.x)) x = target.x;
                    if ((directionY > 0f && y > target.y) || (directionY < 0f && y < target.y)) y = target.y;
                    m_scrollRect.normalizedPosition = new Vector2(x, y);
                    yield return m_waitForFrameEnd;
                }
                m_scrollRect.normalizedPosition = target;
                whenFinishedCallback?.Invoke();
            }
        }

        // 06000599: use signed original extents; invalid alignment starts at zero.
        private float CalculateTargetScrollOffset(RectTransform selection, Alignment alignment, bool fromViewport)
        {
            CalculateSelectionExtents(selection, out float selectionObjectStart, out float selectionObjectEnd, out float selectionObjectMiddle);
            float start;
            if (fromViewport) start = m_viewportTransformStart;
            else
            {
                Transform listTransform = m_listContents.transform;
                Vector3[] listCorners = new Vector3[NumberOfCorners];
                (listTransform as RectTransform).GetWorldCorners(listCorners);
                start = m_direction == Direction.Horizontal ? listCorners[0].x : listCorners[1].y;
            }
            float offset = 0f;
            switch (alignment)
            {
                case Alignment.TopOrLeft: offset = start - selectionObjectStart; break;
                case Alignment.Middle: offset = start - selectionObjectMiddle - m_viewportTransformHalfSize; break;
                case Alignment.BottomOrRight: offset = start - selectionObjectEnd - m_viewportTransformExtent; break;
                case Alignment.Visible:
                    bool beyondEnd = m_direction == Direction.Horizontal ? selectionObjectEnd > m_viewportTransformEnd : selectionObjectEnd < m_viewportTransformEnd;
                    bool beyondStart = m_direction == Direction.Horizontal ? selectionObjectStart < m_viewportTransformStart : selectionObjectStart > m_viewportTransformStart;
                    if (beyondStart) offset = start - selectionObjectStart;
                    else if (beyondEnd) offset = start - selectionObjectEnd - m_viewportTransformExtent;
                    else return -1f;
                    break;
            }
            CalculateListExtents(out float listObjectStart, out float listWorldExtent);
            offset /= listWorldExtent - m_viewportTransformExtent;
            if (fromViewport) offset = Mathf.Abs(offset);
            else if (ClampTargetScrollOffset) offset = Mathf.Clamp01(offset);
            return m_direction == Direction.Vertical ? 1f - offset : offset;
        }
        // 0600059a: invalid enum leaves all cached extents unchanged.
        private void CalculateViewportExtents()
        {
            if (m_viewport == null) return;
            Transform viewportTransform = m_viewport.transform;
            Vector3[] corners = new Vector3[NumberOfCorners];
            (viewportTransform as RectTransform).GetWorldCorners(corners);
            switch (m_direction)
            {
                case Direction.Vertical:
                    m_viewportTransformStart = corners[1].y;
                    m_viewportTransformEnd = corners[0].y;
                    break;
                case Direction.Horizontal:
                    m_viewportTransformStart = corners[0].x;
                    m_viewportTransformEnd = corners[3].x;
                    break;
                default: return;
            }
            m_viewportTransformExtent = m_viewportTransformStart - m_viewportTransformEnd;
            m_viewportTransformHalfSize = m_viewportTransformExtent * 0.5f;
        }
        // 0600059b: local pixel offsets are transformed separately at every corner.
        private void CalculateSelectionExtents(RectTransform selection, out float selectionObjectStart, out float selectionObjectEnd, out float selectionObjectMiddle)
        {
            Vector3[] corners = new Vector3[NumberOfCorners];
            selection.GetLocalCorners(corners);
            for (int i = 0; i < NumberOfCorners; ++i)
            {
                corners[i] += (m_direction == Direction.Horizontal ? Vector3.left : Vector3.up) * m_scrollOffsetPixels;
                corners[i] = selection.TransformPoint(corners[i]);
            }
            selectionObjectStart = m_direction == Direction.Horizontal ? corners[0].x : corners[1].y;
            selectionObjectEnd = m_direction == Direction.Horizontal ? corners[3].x : corners[0].y;
            selectionObjectMiddle = selectionObjectStart + (selectionObjectEnd - selectionObjectStart) * 0.5f;
        }
        // 0600059c: same signed list geometry used by the inlined target calculation.
        private void CalculateListExtents(out float listObjectStart, out float listWorldExtent)
        {
            Transform listTransform = m_listContents.transform;
            Vector3[] corners = new Vector3[NumberOfCorners];
            (listTransform as RectTransform).GetWorldCorners(corners);
            listObjectStart = m_direction == Direction.Horizontal ? corners[0].x : corners[1].y;
            float end = m_direction == Direction.Horizontal ? corners[3].x : corners[0].y;
            listWorldExtent = listObjectStart - end;
        }
        // 0600059d: return GetChild(index), not the cached RectTransform itself.
        private void GetBookends(out RectTransform min, out RectTransform max)
        {
            int minIndex = -1;
            int maxIndex = -1;
            for (int i = 0; i < m_contents.Count; ++i)
            {
                RectTransform child = m_contents[i];
                if (child == null || !child.gameObject.activeInHierarchy || !child.gameObject.activeSelf) continue;
                CalculateSelectionExtents(child, out float selectionStart, out float selectionEnd, out float selectionMiddle);
                if (m_direction == Direction.Vertical)
                {
                    if (minIndex == -1 && selectionEnd < m_viewportTransformStart + BookendOffset) minIndex = i;
                    if (selectionStart >= m_viewportTransformEnd - BookendOffset) maxIndex = i;
                }
                else
                {
                    if (minIndex == -1 && selectionEnd > m_viewportTransformStart - BookendOffset) minIndex = i;
                    if (selectionStart <= m_viewportTransformEnd + BookendOffset) maxIndex = i;
                }
            }
            min = GetChild(minIndex);
            max = GetChild(maxIndex);
        }
        // 0600059e: equal/unordered distances choose max.
        public RectTransform GetNearestChild()
        {
            GetBookends(out RectTransform min, out RectTransform max);
            if (min == null || max == null) return null;
            float minDistance = CalculateTargetScrollOffset(min, m_alignment, true);
            float maxDistance = CalculateTargetScrollOffset(max, m_alignment, true);
            return minDistance < maxDistance ? min : max;
        }
        // 0600059f..5a0 and iterator060005af..b4: no retained snap coroutine handle.
        private void SnapToItem() { StartCoroutine(SnapWhenReady()); }
        private IEnumerator SnapWhenReady()
        {
            while (m_scrollRect.velocity.magnitude > m_snapMinSpeedThreshold) yield return null;
            m_applySnapOverrides = true;
            Select(GetNearestChild());
        }
        // 060005a1: original boundary selection still scrolls inactive boundary items.
        private void OnScroll(bool positive)
        {
            bool reverse = positive ^ m_inverseScrollDirection;
            int step = reverse ? -1 : 1;
            int index = Math.Max(0, m_activeChild + (reverse ? -m_mouseScrollByXElements : m_mouseScrollByXElements));
            index = Math.Min(index, m_contents.Count - 1);
            while ((!m_contents[index].gameObject.activeInHierarchy || !m_contents[index].gameObject.activeSelf) &&
                   index != 0 && index != m_contents.Count - 1)
                index += step;
            m_activeChild = index;
            bool isFirst = index == 0;
            bool isLast = index == m_contents.Count - 1;
            ScrollToObject(m_contents[index], Alignment.TopOrLeft, false, false, null, isFirst, isLast);
        }
        // 060005a2..5a3: event data is ignored; deferred mode exits before pagination.
        public void OnScrollUp(ScrollEventData eventData)
        {
            if (m_deferScrolling) return;
            if (s_usePagination)
            {
                if (m_inverseScrollDirection) NextPage(); else PreviousPage();
            }
            else OnScroll(true);
        }
        public void OnScrollDown(ScrollEventData eventData)
        {
            if (m_deferScrolling) return;
            if (s_usePagination)
            {
                if (m_inverseScrollDirection) PreviousPage(); else NextPage();
            }
            else OnScroll(false);
        }
        // 060005a4: field initializer order is original; base constructor follows.
        public HLScrollRectSelection() { }
    }
}
