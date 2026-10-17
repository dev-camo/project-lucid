using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
    // Original HLUnityUI.Runtime owner 020000ca (methods 0600056d..57c) with its
    // nested Interaction 020000cb and Extent 020000cc. Subclasses supply the axis
    // and child size through CalculatePositionDelta; this base keeps the ordered
    // children, the pending-refocus flags and the per-direction change counters.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(ScrollRect))]
    public abstract class HLScrollRectContentFocus : MonoBehaviour
    {
        protected enum Interaction
        {
            AddedToStart = 0,
            AddedToEnd = 1,
            AddedAtIndex = 2,
            RemovedFromStart = 3,
            RemovedFromEnd = 4,
            RemovedAtIndex = 5,
        }

        public enum Extent
        {
            Start = 0,
            End = 1,
        }

        public FastAction<int> OnChildrenAddedToStart;
        public FastAction<int> OnChildrenAddedToEnd;
        public FastAction<int> OnChildrenRemovedFromStart;
        public FastAction<int> OnChildrenRemovedFromEnd;
        protected const float StartInsertConstant = 0f;
        protected const float EndInsertConstant = 1f;
        protected const float RemovalConstant = -1f;
        protected const float AxisFlipConstant = -1f;
        [SerializeField] protected ScrollRect m_scrollRect;
        [SerializeField] protected RectTransform m_scrollRectContentHolder;
        [SerializeField] protected RectTransform m_removalArea;
        [SerializeField] protected float m_spacingBetweenContentItems;
        [SerializeField] protected int m_childLimit;
        protected HLScrollRect m_hlScrollRect;
        protected bool m_requiresRefocusing;
        protected bool m_requiresLayoutRebuilding;
        protected bool m_preserveVelocity;
        protected bool m_focusEnabled;
        protected float m_accumulatedPositionDelta;
        protected Vector2 m_contentRelativeOffset = Vector2.zero;
        protected Vector2 m_previousScrollRectVelocity = Vector2.zero;
        protected int m_childrenAddedToStart;
        protected int m_childrenAddedToEnd;
        protected int m_childrenRemovedFromStart;
        protected int m_childrenRemovedFromEnd;
        protected LinkedList<RectTransform> m_orderedChildren = new LinkedList<RectTransform>();

        // Original 0600056d: the content holder comes from the (possibly just
        // fetched) scroll rect, and the HLScrollRect is an as-cast, not a lookup.
        // ScrollRect.content is inlined in retail (field read at 0x1b663a1).
        protected void Awake()
        {
            if (m_scrollRect == null)
                m_scrollRect = GetComponent<ScrollRect>();
            if (m_scrollRectContentHolder == null)
                m_scrollRectContentHolder = m_scrollRect.content;
            m_hlScrollRect = m_scrollRect as HLScrollRect;
            // Retail keeps only this discarded Unity null comparison (tail call at
            // 0x1b6644c). Inferred: it guarded a [Conditional] diagnostic that was
            // compiled out; no string literal survives to confirm it.
            if (m_hlScrollRect == null) { }
        }

        // Original 0600056e: a pending refocus is flushed (even while focus is
        // disabled, when Refocus itself does nothing) before both flags clear.
        private void OnDisable()
        {
            if (m_requiresRefocusing)
            {
                Refocus();
                m_requiresRefocusing = false;
                m_accumulatedPositionDelta = 0f;
            }
            if (m_requiresLayoutRebuilding)
                m_requiresLayoutRebuilding = false;
        }

        // Original 0600056f.
        public void EnableFocus(bool enabled)
        {
            m_focusEnabled = enabled;
        }

        // Original 06000570: at the limit the end child is evicted first and returned.
        public RectTransform AddContentChildToStart(RectTransform newChild)
        {
            RectTransform removedChild = null;
            if (m_childLimit > 0 && m_orderedChildren.Count >= m_childLimit)
                removedChild = RemoveContentChildFromEnd();
            m_orderedChildren.AddFirst(newChild);
            newChild.SetParent(m_scrollRectContentHolder, false);
            newChild.SetSiblingIndex(0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(newChild);
            if (m_focusEnabled)
            {
                CalculatePositionDelta(newChild, Interaction.AddedToStart);
                m_requiresRefocusing = true;
                m_requiresLayoutRebuilding = true;
                m_preserveVelocity = true;
                m_childrenAddedToStart++;
            }
            return removedChild;
        }

        // Original 06000571: at the limit the start child is evicted first and returned.
        public RectTransform AddContentChildToEnd(RectTransform newChild)
        {
            RectTransform removedChild = null;
            if (m_childLimit > 0 && m_orderedChildren.Count >= m_childLimit)
                removedChild = RemoveContentChildFromStart();
            m_orderedChildren.AddLast(newChild);
            newChild.SetParent(m_scrollRectContentHolder, false);
            newChild.SetSiblingIndex(m_scrollRectContentHolder.childCount - 1);
            LayoutRebuilder.ForceRebuildLayoutImmediate(newChild);
            if (m_focusEnabled)
            {
                CalculatePositionDelta(newChild, Interaction.AddedToEnd);
                m_requiresRefocusing = true;
                m_requiresLayoutRebuilding = true;
                m_preserveVelocity = true;
                m_childrenAddedToEnd++;
            }
            return removedChild;
        }

        // Original 06000572: the search stops before the last node, so the last
        // child (or a missing one) reports index 0. No counter is incremented.
        // An empty list throws from the first node.Next, as get_Next is called on
        // the null head at 0x1b668c8.
        public bool RemoveContentChild(RectTransform childToRemove)
        {
            int childIndex = 0;
            int index = 0;
            for (LinkedListNode<RectTransform> node = m_orderedChildren.First; node.Next != null; node = node.Next)
            {
                if (childToRemove == node.Value)
                {
                    childIndex = index;
                    break;
                }
                index++;
            }
            bool removed = m_orderedChildren.Remove(childToRemove);
            if (removed)
            {
                childToRemove.gameObject.SetActive(false);
                childToRemove.SetParent(m_removalArea);
                if (m_focusEnabled)
                {
                    CalculatePositionDelta(childToRemove, Interaction.RemovedAtIndex, childIndex);
                    m_requiresRefocusing = true;
                    m_requiresLayoutRebuilding = true;
                    m_preserveVelocity = true;
                }
            }
            return removed;
        }

        // Original 06000573: the node's value is read after RemoveFirst. The child
        // keeps its world position under the removal area.
        public RectTransform RemoveContentChildFromStart()
        {
            LinkedListNode<RectTransform> firstChild = m_orderedChildren.First;
            m_orderedChildren.RemoveFirst();
            firstChild.Value.gameObject.SetActive(false);
            firstChild.Value.SetParent(m_removalArea);
            if (m_focusEnabled)
            {
                CalculatePositionDelta(firstChild.Value, Interaction.RemovedFromStart);
                m_requiresRefocusing = true;
                m_requiresLayoutRebuilding = true;
                m_preserveVelocity = true;
                m_childrenRemovedFromStart++;
            }
            return firstChild.Value;
        }

        // Original 06000574.
        public RectTransform RemoveContentChildFromEnd()
        {
            LinkedListNode<RectTransform> lastChild = m_orderedChildren.Last;
            m_orderedChildren.RemoveLast();
            lastChild.Value.gameObject.SetActive(false);
            lastChild.Value.SetParent(m_removalArea);
            if (m_focusEnabled)
            {
                CalculatePositionDelta(lastChild.Value, Interaction.RemovedFromEnd);
                m_requiresRefocusing = true;
                m_requiresLayoutRebuilding = true;
                m_preserveVelocity = true;
                m_childrenRemovedFromEnd++;
            }
            return lastChild.Value;
        }

        // Original 06000575.
        public void RebuildContentLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_scrollRectContentHolder);
            m_requiresLayoutRebuilding = false;
        }

        // Original 06000576 (slot 4): m_preserveVelocity is left set, and each
        // counter is published in addedToStart, addedToEnd, removedFromStart,
        // removedFromEnd order, and reset only after its Invoke returns.
        // Retail inlines RebuildContentLayout (0x1b66a98..0x1b66abe) and the
        // non-virtual ForceOffsetContentStartPosition (0x1b66b19..0x1b66b2a, an add
        // to m_ContentStartPosition); both calls are recovered from those bodies.
        public virtual void Refocus()
        {
            if (!m_requiresRefocusing || !m_focusEnabled)
                return;
            if (m_requiresLayoutRebuilding)
                RebuildContentLayout();
            m_scrollRectContentHolder.anchoredPosition += m_contentRelativeOffset;
            m_accumulatedPositionDelta = 0f;
            m_requiresRefocusing = false;
            if (m_hlScrollRect != null)
                m_hlScrollRect.ForceOffsetContentStartPosition(m_contentRelativeOffset);
            if (m_childrenAddedToStart > 0)
            {
                OnChildrenAddedToStart.Invoke(m_childrenAddedToStart);
                m_childrenAddedToStart = 0;
            }
            if (m_childrenAddedToEnd > 0)
            {
                OnChildrenAddedToEnd.Invoke(m_childrenAddedToEnd);
                m_childrenAddedToEnd = 0;
            }
            if (m_childrenRemovedFromStart > 0)
            {
                OnChildrenRemovedFromStart.Invoke(m_childrenRemovedFromStart);
                m_childrenRemovedFromStart = 0;
            }
            if (m_childrenRemovedFromEnd > 0)
            {
                OnChildrenRemovedFromEnd.Invoke(m_childrenRemovedFromEnd);
                m_childrenRemovedFromEnd = 0;
            }
        }

        // Original 06000577: only an HLScrollRect is moved.
        public void SetScrollRectNormalisedPosition(Vector2 normalisedPosition)
        {
            if (m_hlScrollRect != null)
                m_hlScrollRect.normalizedPosition = normalisedPosition;
        }

        // Original 06000578: steps past the far end stay on the last reachable
        // child. Only strict overlap with the viewport's world rect counts.
        // ScrollRect.viewport is inlined in retail (field read at 0x1b66dd3).
        public bool IsChildVisibleToViewport(Extent extent, int numberOfStepsInward = 0)
        {
            LinkedListNode<RectTransform> child = extent == Extent.Start ? m_orderedChildren.First : m_orderedChildren.Last;
            if (child == null)
                return false;
            for (int step = 0; step < numberOfStepsInward; step++)
            {
                if (extent == Extent.Start)
                {
                    if (child.Next != null)
                        child = child.Next;
                }
                else if (child.Previous != null)
                {
                    child = child.Previous;
                }
            }
            Vector3[] childCorners = new Vector3[4];
            Vector3[] viewportCorners = new Vector3[4];
            child.Value.GetWorldCorners(childCorners);
            m_scrollRect.viewport.GetWorldCorners(viewportCorners);
            Rect childRect = new Rect(childCorners[0].x, childCorners[0].y,
                childCorners[3].x - childCorners[0].x, childCorners[1].y - childCorners[0].y);
            Rect viewportRect = new Rect(viewportCorners[0].x, viewportCorners[0].y,
                viewportCorners[3].x - viewportCorners[0].x, viewportCorners[1].y - viewportCorners[0].y);
            return childRect.Overlaps(viewportRect);
        }

        // Original 06000579 (slot 5).
        protected abstract void CalculatePositionDelta(RectTransform child, Interaction interactionType, int index = 0);

        // Original 0600057a: the holder pivot on the chosen axis sets the share of
        // the spaced size; index-based interactions contribute nothing and the
        // horizontal axis is flipped. The index is unused.
        protected void InternalCalculatePositionDelta(RectTransform.Axis axis, float size, Interaction interactionType, int index = 0)
        {
            Vector2 contentPivot = m_scrollRectContentHolder.pivot;
            float pivot = axis == RectTransform.Axis.Horizontal ? contentPivot.x : contentPivot.y;
            float direction = 0f;
            switch (interactionType)
            {
                case Interaction.AddedToStart:
                    direction = pivot - StartInsertConstant;
                    break;
                case Interaction.AddedToEnd:
                    direction = pivot - EndInsertConstant;
                    break;
                case Interaction.RemovedFromStart:
                    direction = (pivot - StartInsertConstant) * RemovalConstant;
                    break;
                case Interaction.RemovedFromEnd:
                    direction = (pivot - EndInsertConstant) * RemovalConstant;
                    break;
            }
            if (axis == RectTransform.Axis.Horizontal)
                direction *= AxisFlipConstant;
            m_accumulatedPositionDelta += (size + m_spacingBetweenContentItems) * direction;
        }

        // Original 0600057b dispatches through slot 4.
        protected void LateUpdate()
        {
            if (m_requiresRefocusing)
                Refocus();
        }

        // Original 0600057c: field initializers precede the MonoBehaviour constructor.
        protected HLScrollRectContentFocus() { }
    }
}
