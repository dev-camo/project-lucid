using System;
using System.Collections;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ScreenWidthHorizontalScrollGroup (Game.Runtime 0200083d, bodies 0x609240..0x609740) against the
    // 1.10.1 x86-64 bodies cited per case. The group is [ExecuteInEditMode], so activating its host runs
    // the real Awake and OnEnable. The canvas is a world-space canvas, whose rect size the fixture sets.
    //
    // Scrolling starts a coroutine, which in Edit Mode runs only its first step (to the first yield); the
    // fixture leaves the scroll speed at its default of 0, so that step sits at the starting position.
    public sealed class ScreenWidthHorizontalScrollGroupTests
    {
        private const float Tolerance = 1e-3f;
        private const float CanvasWidth = 640f;

        private sealed class Scene
        {
            public Canvas Canvas;
            public RectTransform CanvasRect;
            public RectTransform[] Children;
            public GameObject Host;
            public ScreenWidthHorizontalScrollGroup Group;
            public RectTransform GroupRect;
        }

        private static GameObject RectObject(Owned owned, string name, Transform parent = null)
        {
            var created = owned.Track(new GameObject(name, typeof(RectTransform)));
            if (parent != null) created.transform.SetParent(parent, false);
            return created;
        }

        // The group is added while inactive and after its children, as the editor Reset reads them, and
        // is activated by the case.
        private static Scene NewScene(Owned owned, params Vector2[] childPositions)
        {
            var scene = new Scene();
            GameObject canvasHost = RectObject(owned, "Lucid scroll canvas");
            scene.Canvas = canvasHost.AddComponent<Canvas>();
            scene.Canvas.renderMode = RenderMode.WorldSpace;
            scene.CanvasRect = canvasHost.GetComponent<RectTransform>();
            scene.CanvasRect.sizeDelta = new Vector2(CanvasWidth, 360f);
            canvasHost.SetActive(false);

            scene.Host = RectObject(owned, "Lucid scroll group");
            scene.Host.SetActive(false);
            scene.GroupRect = scene.Host.GetComponent<RectTransform>();
            scene.Children = new RectTransform[childPositions.Length];
            for (int i = 0; i < childPositions.Length; i++)
            {
                scene.Children[i] = RectObject(owned, "Lucid scroll child " + i, scene.Host.transform).GetComponent<RectTransform>();
                scene.Children[i].anchoredPosition = childPositions[i];
            }
            scene.Group = scene.Host.AddComponent<ScreenWidthHorizontalScrollGroup>();
            Set(scene.Group, "m_canvas", scene.Canvas);
            return scene;
        }

        private static void AssertPosition(RectTransform rectTransform, float x, float y, string label)
        {
            Assert.That(rectTransform.anchoredPosition.x, Is.EqualTo(x).Within(Tolerance), label + " x");
            Assert.That(rectTransform.anchoredPosition.y, Is.EqualTo(y).Within(Tolerance), label + " y");
        }

        // Reset 0x609390 sizes the array to the child count and fills it with the children in order.
        [Test]
        public void ResetListsEveryChildInOrder()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero, Vector2.zero, Vector2.zero);
                var listed = Get<RectTransform[]>(scene.Group, "m_childTransforms");
                Assert.That(listed, Is.EqualTo(scene.Children));
            });
        }

        // The child is cast to RectTransform (0x60946B..0x609470), which a plain transform fails.
        [Test]
        public void ResetFaultsOnAChildWithoutARectTransform()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero);
                var plain = owned.Track(new GameObject("Lucid plain scroll child"));
                plain.transform.SetParent(scene.Host.transform, false);
                Assert.That(plain.transform is RectTransform, Is.False, "fixture: the child keeps a plain transform");
                Assert.Throws<InvalidCastException>(() => Call(scene.Group, "Reset"));
            });
        }

        // Awake 0x609240 casts the transform to RectTransform, OnEnable (0x6092C0) calls Refresh and
        // Refresh 0x6092D0 puts child i at x = i * canvas width, keeping its own y.
        [Test]
        public void EnablingLaysTheChildrenOutOneCanvasWidthApart()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, new Vector2(3f, 11f), new Vector2(4f, 22f), new Vector2(5f, 33f));
                scene.Host.SetActive(true);

                Assert.That(Get<RectTransform>(scene.Group, "m_rectTransform"), Is.SameAs(scene.GroupRect));
                AssertPosition(scene.Children[0], 0f, 11f, "child 0");
                AssertPosition(scene.Children[1], CanvasWidth, 22f, "child 1");
                AssertPosition(scene.Children[2], 2f * CanvasWidth, 33f, "child 2");
            });
        }

        // The width is read again on every refresh (GetCanvasWidth 0x6094A0: the canvas rect's width).
        [Test]
        public void RefreshFollowsTheCurrentCanvasWidth()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, new Vector2(3f, 11f), new Vector2(4f, 22f));
                scene.Host.SetActive(true);
                scene.CanvasRect.sizeDelta = new Vector2(500f, 360f);

                Call(scene.Group, "Refresh");
                AssertPosition(scene.Children[0], 0f, 11f, "child 0");
                AssertPosition(scene.Children[1], 500f, 22f, "child 1");
            });
        }

        // SelectTargetFromOffset 0x609510 stores (offset + count + index) % count, so Next and Previous wrap,
        // and Action_ScrollToIndex (0x609500) passes the distance from the current index.
        [Test]
        public void ScrollingWrapsTheTargetIndexAroundTheChildren()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero, Vector2.zero, Vector2.zero);
                scene.Host.SetActive(true);

                var seen = new System.Collections.Generic.List<int>();
                scene.Group.Action_ScrollToNext();
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                scene.Group.Action_ScrollToNext();
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                scene.Group.Action_ScrollToNext();
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                scene.Group.Action_ScrollToPrevious();
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                scene.Group.Action_ScrollToIndex(1);
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                scene.Group.Action_ScrollToIndex(0);
                seen.Add(Get<int>(scene.Group, "m_targetIndex"));
                Assert.That(seen, Is.EqualTo(new[] { 1, 2, 0, 2, 1, 0 }));
                Assert.That(Get<Coroutine>(scene.Group, "m_scrollRoutine"), Is.Not.Null);
            });
        }

        // The scroll starts from the group's x for the old index, -oldIndex * width (0x609590..0x6095A1),
        // keeping the group's own y; with no speed the first step sits there.
        [Test]
        public void EachScrollStartsAtTheOldTargetsOffset()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero, Vector2.zero, Vector2.zero);
                scene.GroupRect.anchoredPosition = new Vector2(0f, 17f);
                scene.Host.SetActive(true);

                scene.Group.Action_ScrollToNext();
                AssertPosition(scene.GroupRect, 0f, 17f, "from index 0");
                scene.Group.Action_ScrollToNext();
                AssertPosition(scene.GroupRect, -CanvasWidth, 17f, "from index 1");
                scene.Group.Action_ScrollToNext();
                AssertPosition(scene.GroupRect, -2f * CanvasWidth, 17f, "from index 2");
            });
        }

        // A group without children returns before touching the canvas or starting a scroll (0x609528).
        [Test]
        public void ScrollingDoesNothingWithoutChildren()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned);
                scene.Host.SetActive(true);

                scene.Group.Action_ScrollToNext();
                scene.Group.Action_ScrollToIndex(5);
                Assert.That(Get<int>(scene.Group, "m_targetIndex"), Is.Zero);
                Assert.That(Get<Coroutine>(scene.Group, "m_scrollRoutine"), Is.Null);
            });
        }

        // MoveNext 0x609760: while the lerp value is not above 1 each step sets the group's position by
        // Vector2.Lerp and yields null. With no time passing the value stays 0, so the position is the start.
        [Test]
        public void TheScrollHoldsTheStartWhileNoTimePasses()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero);
                scene.Host.SetActive(true);
                var scroll = (IEnumerator)Call(scene.Group, "ScrollToTargetPosition", new Vector2(10f, 20f), new Vector2(110f, 220f));

                for (int step = 0; step < 3; step++)
                {
                    Assert.That(scroll.MoveNext(), Is.True);
                    Assert.That(scroll.Current, Is.Null);
                    AssertPosition(scene.GroupRect, 10f, 20f, "step " + step);
                }
            });
        }

        // Once the value passes 1 (0x6097AC) the position is set to the exact target (0x6097CD) and the
        // routine handle is cleared (0x6097D2). A huge speed makes any positive frame time do that.
        [Test]
        public void TheScrollEndsOnTheExactTargetAndClearsTheRoutine()
        {
            Assume.That(Time.deltaTime, Is.GreaterThan(0f), "Edit Mode reports no frame time");
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned, Vector2.zero, Vector2.zero);
                Set(scene.Group, "m_scrollSpeed", 1e30f);
                scene.Host.SetActive(true);
                scene.Group.Action_ScrollToNext();
                Assert.That(Get<Coroutine>(scene.Group, "m_scrollRoutine"), Is.Not.Null);

                var scroll = (IEnumerator)Call(scene.Group, "ScrollToTargetPosition", new Vector2(10f, 20f), new Vector2(110f, 220f));
                Assert.That(scroll.MoveNext(), Is.True);
                AssertPosition(scene.GroupRect, 110f, 220f, "clamped lerp");
                Assert.That(scroll.MoveNext(), Is.False);
                AssertPosition(scene.GroupRect, 110f, 220f, "exact target");
                Assert.That(Get<Coroutine>(scene.Group, "m_scrollRoutine"), Is.Null);
            });
        }
    }
}
