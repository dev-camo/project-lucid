using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Verification
{
    // Engine-only witnesses use complete authentic providers and owned real
    // inactive objects. They do not schedule or enable selection components.
    public static class OriginalScrollSelectionEngineVerification
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo Pagination = typeof(HLScrollRectSelection).GetField("s_usePagination", Static);
        private static readonly FieldInfo Host = typeof(CoroutineUtils).GetField("s_instance", Static);
        private static FieldInfo Field(object value, string name)
        {
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException("Missing genuine field " + name);
        }
        private static void Set(object value, string name, object fieldValue) => Field(value, name).SetValue(value, fieldValue);
        private static object Get(object value, string name) => Field(value, name).GetValue(value);
        private static object Call(object value, string name, params object[] arguments)
        {
            try { return value.GetType().GetMethod(name, Fields).Invoke(value, arguments); }
            catch (TargetInvocationException failure) { throw failure.InnerException ?? failure; }
        }
        private static void Check(ref int count, bool result, string witness)
        { ++count; if (!result) throw new InvalidOperationException(witness); }
        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) <= 0.00001f;
        private static bool Near(Vector2 actual, Vector2 expected) => Near(actual.x, expected.x) && Near(actual.y, expected.y);
        private static bool Fault<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }

        private sealed class OwnedGeometry : IDisposable
        {
            private readonly object priorPagination = Pagination.GetValue(null);
            private readonly object priorHost = Host.GetValue(null);
            internal readonly GameObject Root;
            internal readonly HLScrollRect Scroll;
            internal readonly HLScrollRectSelection Selection;
            internal readonly RectTransform Viewport;
            internal readonly RectTransform List;
            internal readonly RectTransform First;
            internal readonly Transform Middle;
            internal readonly RectTransform Last;
            private bool closed;
            internal OwnedGeometry()
            {
                Root = new GameObject("ProjectLucid.ScrollSelection.OwnedGeometry", typeof(RectTransform));
                Root.SetActive(false);
                Scroll = Root.AddComponent<HLScrollRect>();
                Selection = Root.AddComponent<HLScrollRectSelection>();
                Viewport = Rect("Viewport", Root.transform, new Vector2(200f, 100f), Vector2.zero);
                List = Rect("Content", Viewport, new Vector2(400f, 300f), Vector2.zero);
                First = Rect("First", List, new Vector2(20f, 10f), new Vector2(50f, 20f));
                var middle = new GameObject("OrdinaryTransformSlot"); middle.transform.SetParent(List, false); Middle = middle.transform;
                Last = Rect("LastInactive", List, new Vector2(20f, 10f), new Vector2(-50f, -20f));
                Last.gameObject.SetActive(false);
            }
            private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
            {
                var gameObject = new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)gameObject.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size; rect.anchoredPosition = position;
                return rect;
            }
            internal void Configure()
            {
                Scroll.content = List; Scroll.viewport = Viewport;
                Scroll.horizontal = true; Scroll.vertical = true;
                Scroll.movementType = ScrollRect.MovementType.Clamped;
                Set(Selection, "m_listContents", List.gameObject);
                Set(Selection, "m_scrollRect", Scroll);
            }
            public void Dispose()
            {
                if (closed) return;
                closed = true;
                try { if (Root != null) UnityEngine.Object.DestroyImmediate(Root); }
                finally { Pagination.SetValue(null, priorPagination); }
            }
            internal void AssertRestored(ref int count)
            {
                Check(ref count, closed && Root == null, "Owned inactive engine objects destroyed after both success and faults");
                Check(ref count, Equals(Pagination.GetValue(null), priorPagination), "Prior pagination flag restored exactly");
                Check(ref count, ReferenceEquals(Host.GetValue(null), priorHost), "No original coroutine host or scheduler state was replaced");
            }
        }

        // Whole constructors/defaults are actual component construction here.
        public static int OriginalRealConstructorsPoliciesAndNullSelection()
        {
            int count = 0; var scope = new OwnedGeometry();
            using (scope)
            {
                object selection = scope.Selection;
                foreach (var policy in new[] { new KeyValuePair<string, object>("NormaliseWhenNegativeOne", true), new KeyValuePair<string, object>("ClampTargetScrollOffset", true), new KeyValuePair<string, object>("ShouldWrap", false), new KeyValuePair<string, object>("BookendOffset", 0f) })
                    Check(ref count, Equals(selection.GetType().GetProperty(policy.Key, Fields).GetValue(selection), policy.Value), "Original virtual policy " + policy.Key);
                Check(ref count, Equals(Get(selection, "m_tolleranceForInstantScrolling"), 0.05f), "Original misspelled tolerance default");
                Check(ref count, Equals(Get(selection, "m_scrollDurationInSeconds"), 1f), "Original duration default");
                Check(ref count, Equals(Get(selection, "m_scrollSpeedPerSecond"), 5f), "Original speed default");
                Check(ref count, Equals(Get(selection, "m_mouseScrollByXElements"), 1), "Original element-step default");
                Check(ref count, Equals(Get(selection, "m_snapDurationSeconds"), -1f), "Original snap duration sentinel");
                var curve = (AnimationCurve)Get(selection, "m_scrollCurve");
                Check(ref count, curve != null && curve.length == 0, "Actual original empty animation curve allocation");
                Check(ref count, Get(selection, "m_waitForFrameEnd") is WaitForEndOfFrame, "Actual original shared end-of-frame wait allocation");
                Check(ref count, ((List<RectTransform>)Get(selection, "m_contents")).Count == 0, "Actual original list allocation");
                Check(ref count, scope.Selection.GetDirection == HLScrollRectSelection.Direction.Vertical && Equals(Get(selection, "m_alignment"), HLScrollRectSelection.Alignment.TopOrLeft) && Equals(Get(selection, "m_activeChild"), 0), "Original zero enum/index defaults");
                Check(ref count, Equals(Get(selection, "m_scrollOffsetPixels"), 0f), "Original zero local pixel offset");
                foreach (string name in new[] { "m_scrollFromStartOfList", "m_inverseScrollDirection", "m_deferScrolling", "m_snapToItems", "m_applySnapOverrides" })
                    Check(ref count, Equals(Get(selection, name), false), "Original false policy " + name);
                foreach (string name in new[] { "m_listContents", "m_scrollRect", "m_viewport", "m_scrollCoroutine", "m_snappingCurve" })
                    Check(ref count, Get(selection, name) == null, "Original null field " + name);
                Check(ref count, Equals(Get(scope.Scroll, "m_ResetVerticalPositionOnEnable"), true), "Original scroll enable-reset default");
                Check(ref count, Equals(Get(scope.Scroll, "m_trackedPointerId"), -1), "Original signed pointer sentinel");
                Check(ref count, (Vector2)Get(scope.Scroll, "m_rubberDelta") == Vector2.zero && (Vector2)Get(scope.Scroll, "m_localCursorDragStartPosition") == Vector2.zero && (Vector2)Get(scope.Scroll, "m_cachedPointerDragStartPosition") == Vector2.zero, "Original retained zero drag vectors");
                Check(ref count, scope.Root.GetComponent<HLScrollableVisualInterface>() == null, "Default false cursor policy does not add a visual component");
                Check(ref count, scope.Selection.GetChild(-1) == null, "Original only special child index returns null");
                Check(ref count, scope.Selection.GetChild(0) == null, "Original missing-list child getter returns null");
                Check(ref count, scope.Selection.CurrentSelection == null, "Original empty cached bookends give no current selection");
            }
            scope.AssertRestored(ref count); return count;
        }

        // All numeric expectations are fixed from an identity-transform geometry:
        // viewport200x100, list400x300, selection20x10 centred at(50,20), offset10.
        public static int OriginalSignedGeometryAndAllAlignmentCases()
        {
            int count = 0; var scope = new OwnedGeometry();
            using (scope)
            {
                scope.Configure(); scope.Selection.RecalculateInterior();
                Check(ref count, scope.Selection.GetDirection == HLScrollRectSelection.Direction.Horizontal, "Original horizontal precedence when both axes are enabled");
                Check(ref count, ReferenceEquals(Get(scope.Selection, "m_viewport"), scope.Viewport), "Original real ScrollRect viewport fallback");
                foreach (var value in new[] { new KeyValuePair<string, float>("m_viewportTransformStart", -100f), new KeyValuePair<string, float>("m_viewportTransformEnd", 100f), new KeyValuePair<string, float>("m_viewportTransformExtent", -200f), new KeyValuePair<string, float>("m_viewportTransformHalfSize", -100f) })
                    Check(ref count, Near((float)Get(scope.Selection, value.Key), value.Value), "Original signed horizontal " + value.Key);
                List<RectTransform> children = (List<RectTransform>)Get(scope.Selection, "m_contents");
                Check(ref count, children.Count == 3 && ReferenceEquals(children[0], scope.First), "Immediate children retain first sibling");
                Check(ref count, children[1] == null, "Ordinary Transform retains its null RectTransform slot");
                Check(ref count, ReferenceEquals(children[2], scope.Last), "Inactive immediate child retains its final sibling slot");
                Check(ref count, ReferenceEquals(scope.Selection.GetChild(0), scope.First), "Original getter reads actual first sibling");
                Check(ref count, scope.Selection.GetChild(1) == null, "Original as-cast returns null for an ordinary Transform");
                Check(ref count, ReferenceEquals(scope.Selection.GetChild(2), scope.Last), "Original getter does not filter inactive child");
                Set(scope.Selection, "m_scrollOffsetPixels", 10f);
                object[] extents = { scope.First, 0f, 0f, 0f };
                Call(scope.Selection, "CalculateSelectionExtents", extents);
                Check(ref count, Near((float)extents[1], 30f), "Original horizontal pixel offset moves each local corner left");
                Check(ref count, Near((float)extents[2], 50f), "Original horizontal end corner");
                Check(ref count, Near((float)extents[3], 40f), "Original horizontal middle");
                object[] list = { 0f, 0f }; Call(scope.Selection, "CalculateListExtents", list);
                Check(ref count, Near((float)list[0], -200f), "Original horizontal list start");
                Check(ref count, Near((float)list[1], -400f), "Original negative horizontal list extent");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.TopOrLeft, false), 1f), "Original horizontal target clamps the signed ratio");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.TopOrLeft, true), 0.65f), "Original from-viewport horizontal distance uses absolute ratio");
                Check(ref count, Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.Visible, false) == -1f, "Original already-visible sentinel returns before list normalization");
                Check(ref count, Target(scope.Selection, scope.First, (HLScrollRectSelection.Alignment)123, false) == 0f, "Original invalid horizontal alignment begins at zero");
                scope.Scroll.horizontal = false; scope.Selection.RecalculateInterior();
                Check(ref count, scope.Selection.GetDirection == HLScrollRectSelection.Direction.Vertical, "Original vertical direction when horizontal is disabled");
                foreach (var value in new[] { new KeyValuePair<string, float>("m_viewportTransformStart", 50f), new KeyValuePair<string, float>("m_viewportTransformEnd", -50f), new KeyValuePair<string, float>("m_viewportTransformExtent", 100f), new KeyValuePair<string, float>("m_viewportTransformHalfSize", 50f) })
                    Check(ref count, Near((float)Get(scope.Selection, value.Key), value.Value), "Original signed vertical " + value.Key);
                extents = new object[] { scope.First, 0f, 0f, 0f }; Call(scope.Selection, "CalculateSelectionExtents", extents);
                Check(ref count, Near((float)extents[1], 35f), "Original vertical local offset moves corners up");
                Check(ref count, Near((float)extents[2], 25f), "Original vertical bottom corner");
                Check(ref count, Near((float)extents[3], 30f), "Original vertical middle");
                list = new object[] { 0f, 0f }; Call(scope.Selection, "CalculateListExtents", list);
                Check(ref count, Near((float)list[0], 150f), "Original vertical list start");
                Check(ref count, Near((float)list[1], 300f), "Original positive vertical list extent");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.TopOrLeft, false), 0.425f), "Original vertical top target");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.Middle, false), 0.65f), "Original vertical middle target");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.BottomOrRight, false), 0.875f), "Original vertical bottom target");
                Check(ref count, Near(Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.TopOrLeft, true), 0.925f), "Original vertical from-viewport distance is complemented after absolute value");
                Check(ref count, Target(scope.Selection, scope.First, HLScrollRectSelection.Alignment.Visible, false) == -1f, "Original vertical visibility retains sentinel");
                Check(ref count, Target(scope.Selection, scope.First, (HLScrollRectSelection.Alignment)123, false) == 1f, "Original invalid vertical alignment complements zero");
                Set(scope.Selection, "m_direction", (HLScrollRectSelection.Direction)123);
                Call(scope.Selection, "CalculateViewportExtents");
                Check(ref count, Equals(Get(scope.Selection, "m_viewportTransformStart"), 50f) && Equals(Get(scope.Selection, "m_viewportTransformEnd"), -50f) && Equals(Get(scope.Selection, "m_viewportTransformExtent"), 100f) && Equals(Get(scope.Selection, "m_viewportTransformHalfSize"), 50f), "Original invalid direction retains all previously published viewport fields");
            }
            scope.AssertRestored(ref count); return count;
        }
        private static float Target(HLScrollRectSelection selection, RectTransform rect, HLScrollRectSelection.Alignment alignment, bool fromViewport)
            => (float)Call(selection, "CalculateTargetScrollOffset", rect, alignment, fromViewport);

        public static int OriginalInstantCallbackAndPartialFailureOrder()
        {
            int count = 0; var scope = new OwnedGeometry();
            using (scope)
            {
                scope.Configure(); scope.Selection.RecalculateInterior();
                scope.Scroll.velocity = new Vector2(4f, 5f); int callbacks = 0;
                Action callback = () => { ++callbacks; throw new ApplicationException("Owned original completion fault"); };
                Check(ref count, Fault<ApplicationException>(() => Call(scope.Selection, "ScrollToOffset", new Vector2(0.25f, 0.75f), true, false, callback)), "Original completion exception propagates");
                Check(ref count, callbacks == 1, "Original instant callback executes once after setters");
                Check(ref count, scope.Scroll.velocity == Vector2.zero, "Original velocity reset precedes callback failure");
                Check(ref count, Near(scope.Scroll.normalizedPosition, new Vector2(0.25f, 0.75f)), "Original instant position publication survives callback failure");
                Check(ref count, Get(scope.Selection, "m_scrollCoroutine") == null, "Instant callback fault does not fabricate a retained iterator");
                Set(scope.Selection, "m_direction", (HLScrollRectSelection.Direction)123);
                scope.Selection.SetNormalisedPosition(0.9f);
                Check(ref count, Near(scope.Scroll.normalizedPosition, new Vector2(0.25f, 0.75f)), "Original invalid-direction setter leaves both axes");
                Check(ref count, Near(scope.Selection.GetNormalisedPosition(), 0.25f), "Original invalid-direction getter reads the horizontal axis");
                Set(scope.Selection, "m_direction", HLScrollRectSelection.Direction.Vertical);
                scope.Selection.SetNormalisedPosition(0.6f);
                Check(ref count, Near(scope.Scroll.verticalNormalizedPosition, 0.6f), "Original vertical setter");
                Check(ref count, Near(scope.Scroll.horizontalNormalizedPosition, 0.25f), "Original vertical setter retains horizontal axis");
                Set(scope.Selection, "m_direction", HLScrollRectSelection.Direction.Horizontal);
                scope.Selection.SetNormalisedPosition(0.4f);
                Check(ref count, Near(scope.Scroll.normalizedPosition, new Vector2(0.4f, 0.6f)), "Original horizontal setter retains vertical axis");
                Pagination.SetValue(null, true); Set(scope.Selection, "m_deferScrolling", true); Set(scope.Selection, "m_scrollRect", null);
                scope.Selection.OnScrollUp(null); scope.Selection.OnScrollDown(null);
                Check(ref count, Get(scope.Selection, "m_scrollRect") == null && callbacks == 1, "Original deferred guard exits before pagination/providers/event-data reads");
                Pagination.SetValue(null, false); Set(scope.Selection, "m_deferScrolling", false);
                ((List<RectTransform>)Get(scope.Selection, "m_contents")).Clear(); Set(scope.Selection, "m_activeChild", 7);
                Check(ref count, Fault<ArgumentOutOfRangeException>(() => scope.Selection.OnScrollDown(null)), "Original empty cached list faults rather than adding a boundary repair");
                Check(ref count, Equals(Get(scope.Selection, "m_activeChild"), 7), "Original invalid-list failure occurs before index publication");
                Check(ref count, Fault<NullReferenceException>(() => scope.Selection.SelectChildWhere(null)), "Original mandatory selector faults on actual first child");
                Set(scope.Selection, "m_scrollRect", scope.Scroll); Set(scope.Selection, "m_scrollFromStartOfList", true);
                Check(ref count, Fault<NullReferenceException>(() => Call(scope.Selection, "ScrollToObject", null, HLScrollRectSelection.Alignment.TopOrLeft, false, false, callback, false, false)), "Original missing selection faults after from-start setter");
                Check(ref count, Near(scope.Scroll.normalizedPosition, Vector2.zero), "Original from-start mutation survives selection failure");
                Check(ref count, callbacks == 1, "Original selection fault precedes the completion callback");
                Check(ref count, Get(scope.Selection, "m_scrollCoroutine") == null, "Original selection fault precedes iterator publication");
            }
            scope.AssertRestored(ref count); return count;
        }
    }
}
