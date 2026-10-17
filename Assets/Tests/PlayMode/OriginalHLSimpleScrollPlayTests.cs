using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The lerp iterator of Hardlight.HLSimpleScroll (<OnScrollWithLerp>d__16.MoveNext
    // 0x060005c2 @0x1B6ACD0), stepped by hand inside one play-mode frame so Time.deltaTime
    // is a fixed positive dt; m_lerpTime is set to 3.5 dt, so the loop body runs at timer
    // 0, dt, 2 dt and 3 dt. HLSimpleScroll stays on an inactive root (its Setup never
    // runs); the HLScrollRect root is activated with reset-on-enable cleared, and both are
    // destroyed in TearDown.
    [TestFixture]
    public sealed class OriginalHLSimpleScrollPlayTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const float Tolerance = 1e-3f;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        static void Set(object target, System.Type type, string name, object value) => type.GetField(name, Instance).SetValue(target, value);
        static object Get(object target, System.Type type, string name) => type.GetField(name, Instance).GetValue(target);

        GameObject NewRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            return root;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        // A vertical-only, unrestricted 100 x 100 viewport over 100 x 300 content, an armed
        // HLSimpleScroll with the given pixel step, and the iterator for value.
        IEnumerator Lerp(int scrollPixels, float value, out HLSimpleScroll simple, out HLScrollRect scroll)
        {
            GameObject scrollRoot = NewRoot("ProjectLucid.HLSimpleScroll.Play.ScrollRect");
            scroll = scrollRoot.AddComponent<HLScrollRect>();
            Set(scroll, typeof(HLScrollRect), "m_ResetVerticalPositionOnEnable", false);
            RectTransform viewport = NewRect("Viewport", scrollRoot.transform, new Vector2(100f, 100f));
            scroll.viewport = viewport;
            scroll.content = NewRect("Content", viewport, new Vector2(100f, 300f));
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scrollRoot.SetActive(true);

            simple = NewRoot("ProjectLucid.HLSimpleScroll.Play.Owned").AddComponent<HLSimpleScroll>();
            Set(simple, typeof(HLSimpleScroll), "m_scrollRect", scroll);
            Set(simple, typeof(HLSimpleScroll), "m_scrollPixels", scrollPixels);
            Assert.That(Time.deltaTime, Is.GreaterThan(0f));
            Set(simple, typeof(HLSimpleScroll), "m_lerpTime", 3.5f * Time.deltaTime);
            simple.OnScrollUp((ScrollEventData)null);
            return (IEnumerator)typeof(HLSimpleScroll).GetMethod("OnScrollWithLerp", Instance).Invoke(simple, new object[] { value });
        }

        static float[] Run(IEnumerator lerp, HLScrollRect scroll, out bool finished)
        {
            var heights = new List<float>();
            finished = false;
            for (int step = 0; step < 6; step++)
            {
                if (!lerp.MoveNext())
                {
                    finished = true;
                    break;
                }
                Assert.That(lerp.Current, Is.Null, "m_wait is never assigned");
                heights.Add(scroll.content.anchoredPosition.y);
            }
            return heights.ToArray();
        }

        // 0x1B6AE0B..0x1B6AE30: each step adds Lerp(0, 100, timer / (3.5 dt)) minus what was
        // already added, so the steps are 0, 100/3.5, 200/3.5 and 300/3.5; the loop ends
        // short of the target and disarms the scroll.
        [Test]
        public void LerpAddsTheGrowthOfTheLerpEachFrameAndStopsShort()
        {
            IEnumerator lerp = Lerp(200, 0.5f, out HLSimpleScroll simple, out HLScrollRect scroll);
            Assert.That(lerp.GetType().Name, Is.EqualTo("<OnScrollWithLerp>d__16"));
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, 100f / 3.5f, 200f / 3.5f, 300f / 3.5f }).Within(Tolerance));
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(300f / 3.5f).Within(Tolerance));
            Assert.That((bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll"), Is.False);
            Assert.That((float)Get(lerp, lerp.GetType(), "<timer>5__2"), Is.EqualTo(4f * Time.deltaTime).Within(1e-6f));
            Assert.That((Vector2)Get(lerp, lerp.GetType(), "<lerpTarget>5__3"), Is.EqualTo(new Vector2(0f, 100f)));
        }

        // 0x1B6AE46..0x1B6AEB6: a positive value subtracts a step with a negative component,
        // so a negative pixel step still moves the content up.
        [Test]
        public void PositiveValueSubtractsNegativeSteps()
        {
            IEnumerator lerp = Lerp(-200, 0.5f, out _, out HLScrollRect scroll);
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, 100f / 3.5f, 200f / 3.5f, 300f / 3.5f }).Within(Tolerance));
        }

        // The subtraction needs a positive value: a negative value adds its negative steps.
        [Test]
        public void NegativeValueAddsItsSteps()
        {
            IEnumerator lerp = Lerp(200, -0.5f, out _, out HLScrollRect scroll);
            float[] heights = Run(lerp, scroll, out bool finished);
            Assert.That(finished, Is.True);
            Assert.That(heights, Is.EqualTo(new[] { 0f, -100f / 3.5f, -200f / 3.5f, -300f / 3.5f }).Within(Tolerance));
        }

        // 0x1B6AD1F: an inactive rect ends the iterator at once and leaves the scroll armed.
        [Test]
        public void InactiveRectEndsTheLerpAndLeavesTheScrollArmed()
        {
            IEnumerator lerp = Lerp(200, 0.5f, out HLSimpleScroll simple, out HLScrollRect scroll);
            scroll.enabled = false;
            Assert.That(lerp.MoveNext(), Is.False);
            Assert.That(scroll.content.anchoredPosition.y, Is.Zero);
            Assert.That((bool)Get(simple, typeof(HLSimpleScroll), "m_isActiveScroll"), Is.True);
        }
    }
}
