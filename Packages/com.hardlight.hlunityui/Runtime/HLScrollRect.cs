using System;
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hardlight
{
    // Original HLUnityUI.Runtime owner 020000eb: 26 direct APIs, four two-method
    // display classes and the six-method ResetToTop iterator. All 42 physical
    // contexts (including the fully shared generic variant) retain dual-CPU evidence.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLScrollRect : ScrollRect
    {
        private const float minSqrVelocityMagnitude = 100f;
        [SerializeField] protected RectTransform m_rectTransform;
        [SerializeField] protected bool m_shouldRouteDragToParents;
        [SerializeField] protected bool m_ResetVerticalPositionOnEnable = true;
        [SerializeField] protected bool m_doNotScrollIfContentIsTooSmall;
        [SerializeField] protected bool m_changeCursorIfContentIsTooSmall;
        [FormerlySerializedAs("m_scrollingCursorIndicator")]
        [SerializeField] protected HLScrollableVisualInterface m_scrollingIndicator;
        protected bool m_routeToParentsInternal;
        protected int m_trackedPointerId = -1;
        protected bool m_trackingPointer;
        protected bool m_beginDragIgnoredBecauseContentIsTooSmall;
        protected Vector2 m_rubberDelta = Vector2.zero;
        protected Vector2 m_localCursorDragStartPosition = Vector2.zero;
        protected Vector2 m_cachedPointerDragStartPosition = Vector2.zero;
        public FastAction OnDragReleased;
        public FastAction<float> OnRubberDrag;

        // Original 06000684..688, property and accessor order retained.
        public Vector2 RubberDelta => m_rubberDelta;
        public bool ContentWidthIsTooSmall { get; protected set; }
        public bool ContentHeightIsTooSmall { get; protected set; }

        // Original 06000689: base callback precedes both caches.
        protected override void Awake()
        {
            base.Awake();
            CacheTransformAsRectTransform();
            GetScrollIndicator();
        }

        // Original 0600068a intentionally repeats GetComponent on its success path.
        private void GetScrollIndicator()
        {
            if (m_changeCursorIfContentIsTooSmall && m_scrollingIndicator == null)
            {
                m_scrollingIndicator = gameObject.GetComponent<HLScrollableVisualInterface>() == null
                    ? gameObject.AddComponent<HLScrollableVisualInterface>()
                    : gameObject.GetComponent<HLScrollableVisualInterface>();
            }
        }

        // Original 0600068b: no stored coroutine handle.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_ResetVerticalPositionOnEnable)
                StartCoroutine(ResetToTop());
        }

        // Original 0600068c uses Unity equality and an as-cast.
        private void CacheTransformAsRectTransform()
        {
            if (m_rectTransform == null)
                m_rectTransform = transform as RectTransform;
        }

        // Original 0600068d leaves a disabled axis's previous small-content flag intact.
        private void Update()
        {
            if (!m_doNotScrollIfContentIsTooSmall)
                return;
            if (horizontal)
                ContentWidthIsTooSmall = content.rect.width <= m_rectTransform.rect.width;
            if (vertical)
                ContentHeightIsTooSmall = content.rect.height <= m_rectTransform.rect.height;
            if (m_changeCursorIfContentIsTooSmall)
                m_scrollingIndicator.enabled = !(horizontal ? ContentWidthIsTooSmall : ContentHeightIsTooSmall);
        }

        // Original 0600068e uses System.Math.Abs and the smallest positive float.
        // The original virtual slot 41 is ScrollRect.StopMovement, not OnScroll.
        protected override void LateUpdate()
        {
            base.LateUpdate();
            float magnitude = Math.Abs(velocity.sqrMagnitude);
            if (magnitude > float.Epsilon && magnitude <= minSqrVelocityMagnitude)
            {
                velocity = Vector2.zero;
                StopMovement();
            }
        }

        // Original 0600068f / natural 060006a6..6ab yields once before the setter.
        private IEnumerator ResetToTop()
        {
            yield return null;
            verticalNormalizedPosition = 1f;
        }

        // Original 06000690 adjusts the inherited start position only.
        public void ForceOffsetContentStartPosition(Vector2 offset)
        {
            m_ContentStartPosition += offset;
        }

        // Original 06000691 / display class34 always notifies the first parent first.
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            DoForFirstFoundParent<IInitializePotentialDragHandler>(parent => parent.OnInitializePotentialDrag(eventData));
            base.OnInitializePotentialDrag(eventData);
        }

        // Original 06000692 / display class35 captures its parameter before guards.
        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (m_trackingPointer)
            {
                if (m_trackedPointerId != eventData.pointerId)
                    return;
            }
            else
            {
                m_trackedPointerId = eventData.pointerId;
                m_trackingPointer = true;
            }
            float absoluteDeltaX = Math.Abs(eventData.delta.x);
            float absoluteDeltaY = Math.Abs(eventData.delta.y);
            m_cachedPointerDragStartPosition = eventData.position;
            m_routeToParentsInternal = (!horizontal && absoluteDeltaX >= absoluteDeltaY)
                || (!vertical && Math.Abs(eventData.delta.x) <= absoluteDeltaY)
                || m_beginDragIgnoredBecauseContentIsTooSmall;
            if (m_doNotScrollIfContentIsTooSmall)
                ClampEventData(ref eventData);
            if (m_routeToParentsInternal && m_shouldRouteDragToParents)
            {
                DoForFirstFoundParent<IBeginDragHandler>(parent => parent.OnBeginDrag(eventData));
                return;
            }
            base.OnBeginDrag(eventData);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position,
                eventData.pressEventCamera, out m_localCursorDragStartPosition);
        }

        // Original 06000693 / display class36 resets flags after dispatch succeeds.
        // It retains the pointer id and invokes the authentic null-tolerant extension.
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (m_trackedPointerId != eventData.pointerId)
                return;
            if (m_shouldRouteDragToParents && m_routeToParentsInternal)
                DoForFirstFoundParent<IEndDragHandler>(parent => parent.OnEndDrag(eventData));
            else if (!m_beginDragIgnoredBecauseContentIsTooSmall)
                base.OnEndDrag(eventData);
            m_routeToParentsInternal = false;
            m_trackingPointer = false;
            m_beginDragIgnoredBecauseContentIsTooSmall = false;
            OnDragReleased.Invoke();
        }

        // Original 06000694 / display class37 calculates rubber displacement after
        // base drag. Bounds performs the original half-size then size multiplication.
        public override void OnDrag(PointerEventData eventData)
        {
            if (m_trackedPointerId != eventData.pointerId)
                return;
            if (m_doNotScrollIfContentIsTooSmall)
                ClampEventData(ref eventData);
            if (m_shouldRouteDragToParents && m_routeToParentsInternal)
            {
                DoForFirstFoundParent<IDragHandler>(parent => parent.OnDrag(eventData));
                return;
            }
            base.OnDrag(eventData);
            Vector2 localCursor;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position,
                eventData.pressEventCamera, out localCursor))
                return;
            Vector2 position = m_ContentStartPosition + (localCursor - m_localCursorDragStartPosition);
            Vector2 offset = CalculateOffset(position - content.anchoredPosition);
            Rect rect = viewRect.rect;
            Bounds bounds = new Bounds(rect.center, rect.size);
            m_rubberDelta.x = CalculateRubberDelta(offset.x, bounds.size.x);
            m_rubberDelta.y = CalculateRubberDelta(offset.y, bounds.size.y);
            if (m_rubberDelta.y > 0f)
                OnRubberDrag.Invoke(m_rubberDelta.y / bounds.size.y);
        }

        // Original 06000695 / inlined 06000699: no UpdateBounds or tolerance check.
        // Horizontal tests min first; vertical tests max first.
        private Vector2 CalculateOffset(Vector2 delta)
        {
            Vector2 offset = Vector2.zero;
            if (movementType == MovementType.Unrestricted)
                return offset;
            Vector2 min = m_ContentBounds.min;
            Vector2 max = m_ContentBounds.max;
            Rect rect = viewRect.rect;
            Bounds bounds = new Bounds(rect.center, rect.size);
            if (horizontal)
            {
                min.x += delta.x;
                max.x += delta.x;
                if (min.x > bounds.min.x)
                    offset.x = bounds.min.x - min.x;
                else if (max.x < bounds.max.x)
                    offset.x = bounds.max.x - max.x;
            }
            if (vertical)
            {
                min.y += delta.y;
                max.y += delta.y;
                if (max.y < bounds.max.y)
                    offset.y = bounds.max.y - max.y;
                else if (min.y > bounds.min.y)
                    offset.y = bounds.min.y - min.y;
            }
            return offset;
        }

        // Original 06000696 retains float product, double reciprocal, then float
        // rounding before the final products. Zero/negative/nonfinite sizes are unguarded.
        private static float CalculateRubberDelta(float overStretching, float viewSize)
        {
            return (float)(1.0 - 1.0 / (Mathf.Abs(overStretching) * 0.55f / (double)viewSize + 1.0))
                * viewSize * Mathf.Sign(overStretching);
        }

        // Original 06000697 has only the IEventSystemHandler constraint. Both physical
        // variants traverse all Component entries, dispatch the first match only,
        // then read that transform's parent even after successful dispatch.
        private void DoForFirstFoundParent<T>(Action<T> action) where T : IEventSystemHandler
        {
            Transform parent = transform.parent;
            bool found = false;
            while (!found && parent != null)
            {
                foreach (Component component in parent.GetComponents<Component>())
                {
                    if (component is T)
                    {
                        action((T)(IEventSystemHandler)component);
                        found = true;
                        break;
                    }
                }
                parent = parent.parent;
            }
        }

        // Original 06000698 reads the two vectors before axis tests, writes position
        // before delta, and does not replace the ref parameter.
        private void ClampEventData(ref PointerEventData eventData)
        {
            if (!m_doNotScrollIfContentIsTooSmall)
                return;
            Vector2 position = eventData.position;
            Vector2 delta = eventData.delta;
            if (horizontal && ContentWidthIsTooSmall)
            {
                position.x = m_cachedPointerDragStartPosition.x;
                delta.x = 0f;
            }
            if (vertical && ContentHeightIsTooSmall)
            {
                position.y = m_cachedPointerDragStartPosition.y;
                delta.y = 0f;
            }
            eventData.position = position;
            eventData.delta = delta;
        }

        // Original 06000699.
        public Vector2 ExposeCalculateOffset(Vector2 delta) => CalculateOffset(delta);

        // Original 0600069a deliberately hides the protected virtual base method.
        public new void SetContentAnchoredPosition(Vector2 position)
        {
            if (!horizontal)
                position.x = content.anchoredPosition.x;
            if (!vertical)
                position.y = content.anchoredPosition.y;
            if (position != content.anchoredPosition)
            {
                content.anchoredPosition = position;
                UpdateBounds();
            }
        }

        // Original 0600069b checks the published small flags before base callback.
        public override void OnScroll(PointerEventData data)
        {
            if ((ContentWidthIsTooSmall || ContentHeightIsTooSmall) && m_doNotScrollIfContentIsTooSmall
                && ((horizontal && ContentWidthIsTooSmall) || (vertical && ContentHeightIsTooSmall)))
                return;
            base.OnScroll(data);
        }

        // Original 0600069c has the same ordered predicate, with no engine call.
        public bool ContentNeedsScrolling()
        {
            return !((ContentWidthIsTooSmall || ContentHeightIsTooSmall) && m_doNotScrollIfContentIsTooSmall
                && ((horizontal && ContentWidthIsTooSmall) || (vertical && ContentHeightIsTooSmall)));
        }

        // Original 0600069d field initializers precede the real ScrollRect constructor.
        public HLScrollRect() { }
    }
}
