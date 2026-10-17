using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of UnityEngine.UI.HLCarousel (HLUnityUI.Runtime 0x06000089..91)
    // against the shipped x86-64 bodies. Each case owns an inactive HideAndDontSave
    // root holding the carousel, a 100x100 viewport and its content, and destroys
    // it in TearDown. ScrollRect.m_HasRebuiltLayout is set on the owned carousel so
    // SetNormalizedPosition never calls Canvas.ForceUpdateCanvases; no shared state
    // is touched. With a centred 300 unit content in the centred 100 unit viewport,
    // the engine maps normalised position v to anchored position 100 - 200v and back.
    [TestFixture]
    public sealed class OriginalHLCarouselTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        sealed class Rig
        {
            public HLCarousel Carousel;
            public RectTransform Viewport;
            public RectTransform Content;
        }

        Rig Build(Vector2 contentSize)
        {
            var root = new GameObject("ProjectLucid.HLCarousel.Owned", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            var rig = new Rig { Carousel = root.AddComponent<HLCarousel>() };
            rig.Viewport = NewRect("Viewport", root.transform, new Vector2(100f, 100f));
            rig.Content = NewRect("Content", rig.Viewport, contentSize);
            rig.Carousel.viewport = rig.Viewport;
            rig.Carousel.content = rig.Content;
            rig.Carousel.movementType = ScrollRect.MovementType.Unrestricted;
            typeof(ScrollRect).GetField("m_HasRebuiltLayout", Any).SetValue(rig.Carousel, true);
            return rig;
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

        static RectTransform[] Children(Rig rig, int count)
        {
            var children = new RectTransform[count];
            for (int i = 0; i < count; i++)
                children[i] = NewRect("Item" + i, rig.Content, new Vector2(10f, 10f));
            return children;
        }

        static object Get(object target, Type owner, string name) => owner.GetField(name, Any).GetValue(target);
        static void Set(object target, Type owner, string name, object value) => owner.GetField(name, Any).SetValue(target, value);
        static Vector2 WrapOffset(HLCarousel carousel) => (Vector2)Get(carousel, typeof(HLCarousel), "m_wrapOffset");
        static Vector2 ContentStart(HLCarousel carousel) => (Vector2)Get(carousel, typeof(ScrollRect), "m_ContentStartPosition");
        static Vector2 PrevPosition(HLCarousel carousel) => (Vector2)Get(carousel, typeof(ScrollRect), "m_PrevPosition");

        static void SetDirection(HLCarousel carousel, int direction)
        {
            FieldInfo field = typeof(HLCarousel).GetField("m_scrollDirection", Any);
            field.SetValue(carousel, Enum.ToObject(field.FieldType, direction));
        }

        static void Call(object target, string name, params object[] arguments)
        {
            try { typeof(HLCarousel).GetMethod(name, Declared).Invoke(target, arguments); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static void Near(Vector2 actual, Vector2 expected, string message = null)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-3f), message);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-3f), message);
        }

        // dump.cs TypeDefIndex 11845..11846: base type, private nested enum, field
        // order and types, the six float constants and the method set.
        [Test]
        public void DeclarationKeepsTheDumpedFieldsConstantsAndMembers()
        {
            Type type = typeof(HLCarousel);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("UnityEngine.UI.HLCarousel"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(Hardlight.HLScrollRect)));
            Assert.That(type.IsSealed || type.IsAbstract, Is.False);
            Assert.That(type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Count(), Is.EqualTo(2));

            Type direction = type.GetNestedTypes(Declared).Single();
            Assert.That(direction.Name, Is.EqualTo("ScrollDirection"));
            Assert.That(direction.IsNestedPrivate && direction.IsEnum, Is.True);
            Assert.That(Enum.GetNames(direction), Is.EqualTo(new[] { "Horizontal", "Vertical" }));
            Assert.That(Enum.GetValues(direction).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1 }));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_scrollDirection", "m_startCap", "m_endCap", "MinBound", "MaxBound", "HorizontalDirection",
                "VerticalDirection", "DoubleFloat", "HalfFloat", "m_wrapOffset",
            }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields.Where(f => f.IsDefined(typeof(SerializeField), false)).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "m_scrollDirection", "m_startCap", "m_endCap" }));
            Assert.That(fields.Where(f => !f.IsLiteral).Select(f => f.FieldType).ToArray(),
                Is.EqualTo(new[] { direction, typeof(RectTransform), typeof(RectTransform), typeof(Vector2) }));
            Assert.That(fields.Where(f => f.IsLiteral).Select(f => f.GetRawConstantValue()).ToArray(),
                Is.EqualTo(new object[] { 0f, 1f, -1f, 1f, 2f, 0.5f }));

            Assert.That(type.GetMethods(Declared).Select(m => m.Name).ToArray(), Is.EquivalentTo(new[]
            {
                "LateUpdate", "WrapAxis", "SetWrapOffset", "SetupCarousel", "SetupCap", "RecreateCaps", "DestroyCaps", "CreateCaps",
            }));
            MethodInfo lateUpdate = type.GetMethod("LateUpdate", Declared);
            Assert.That(lateUpdate.IsFamily && lateUpdate.IsVirtual && lateUpdate.GetBaseDefinition().DeclaringType == typeof(ScrollRect), Is.True);
            Assert.That(type.GetMethod("WrapAxis", Declared).GetParameters().Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "axisNormalisedPosition", "axis", "wrapOffset" }));
            Assert.That(type.GetMethod("SetupCap", Declared).GetParameters().Select(p => p.Name).ToArray(),
                Is.EqualTo(new[] { "cap", "parentCap", "direction" }));
        }

        // 0x1B3C980 stores both components.
        [Test]
        public void SetWrapOffsetStoresTheVector()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Carousel.SetWrapOffset(new Vector2(0.3f, -0.7f));
            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0.3f, -0.7f)));
        }

        // 0x1B3C990: childCount less one per non-null cap; at two or more items the
        // offset is 0.5 / (items - 1) (0x1b3ca5e) on the scroll axis and only that
        // axis is enabled.
        [Test]
        public void SetupCarouselExcludesBothCapsAndEnablesTheHorizontalAxis()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            RectTransform[] items = Children(rig, 5);
            Set(rig.Carousel, typeof(HLCarousel), "m_startCap", items[0]);
            Set(rig.Carousel, typeof(HLCarousel), "m_endCap", items[4]);
            SetDirection(rig.Carousel, 0);
            rig.Carousel.horizontal = false;
            rig.Carousel.vertical = true;
            rig.Carousel.SetWrapOffset(new Vector2(9f, 9f));

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0.25f, 0f)));
            Assert.That(rig.Carousel.horizontal, Is.True);
            Assert.That(rig.Carousel.vertical, Is.False);
        }

        [Test]
        public void SetupCarouselWithoutCapsCountsEveryChildOnTheVerticalAxis()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Children(rig, 5);
            SetDirection(rig.Carousel, 1);
            rig.Carousel.horizontal = true;
            rig.Carousel.vertical = false;

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0f, 0.125f)));
            Assert.That(rig.Carousel.horizontal, Is.False);
            Assert.That(rig.Carousel.vertical, Is.True);
        }

        // The two-item boundary of the `itemCount >= 2` test (cmp r14d, 2 / jge at
        // 0x1b3ca2b and 0x1b3ca81): 0.5 / (2 - 1) with the rodata 0.5 at 0x29a6c08.
        [Test]
        public void SetupCarouselWithExactlyTwoItemsEnablesTheAxis()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Children(rig, 2);
            SetDirection(rig.Carousel, 0);
            rig.Carousel.horizontal = false;
            rig.Carousel.vertical = true;

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(rig.Carousel.horizontal, Is.True);
            Assert.That(rig.Carousel.vertical, Is.False);
        }

        // Each cap is compared with null and subtracted on its own (0x1b3c9f5..9fd,
        // 0x1b3ca20..a28): four children less one cap leave three items, 0.5 / 2.
        [Test]
        public void SetupCarouselSubtractsOnlyTheEndCapWhenTheStartCapIsMissing()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            RectTransform[] items = Children(rig, 4);
            Set(rig.Carousel, typeof(HLCarousel), "m_endCap", items[3]);
            SetDirection(rig.Carousel, 0);

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0.25f, 0f)));
            Assert.That(rig.Carousel.horizontal, Is.True);
        }

        [Test]
        public void SetupCarouselSubtractsOnlyTheStartCapWhenTheEndCapIsMissing()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            RectTransform[] items = Children(rig, 4);
            Set(rig.Carousel, typeof(HLCarousel), "m_startCap", items[0]);
            SetDirection(rig.Carousel, 0);

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0.25f, 0f)));
            Assert.That(rig.Carousel.horizontal, Is.True);
        }

        // The caps are only compared with null (0x1b3c9f5, 0x1b3ca20), so caps outside
        // the content still reduce the count; below two items both axes stay off and
        // the offset is zero.
        [Test]
        public void SetupCarouselBelowTwoItemsDisablesScrollingEvenForCapsOutsideTheContent()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Children(rig, 3);
            Set(rig.Carousel, typeof(HLCarousel), "m_startCap", NewRect("OutsideStart", rig.Viewport, Vector2.one));
            Set(rig.Carousel, typeof(HLCarousel), "m_endCap", NewRect("OutsideEnd", rig.Viewport, Vector2.one));
            SetDirection(rig.Carousel, 0);
            rig.Carousel.horizontal = true;
            rig.Carousel.vertical = true;
            rig.Carousel.SetWrapOffset(new Vector2(9f, 9f));

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(Vector2.zero));
            Assert.That(rig.Carousel.horizontal, Is.False);
            Assert.That(rig.Carousel.vertical, Is.False);
        }

        // Any direction other than Horizontal builds the vertical offset (0x1b3ca72),
        // while each axis flag needs its exact literal (0x1b3ca87..90).
        [Test]
        public void SetupCarouselWithAnUndeclaredDirectionStoresAVerticalOffsetAndEnablesNoAxis()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Children(rig, 3);
            SetDirection(rig.Carousel, 2);
            rig.Carousel.horizontal = true;
            rig.Carousel.vertical = true;

            rig.Carousel.SetupCarousel();

            Assert.That(WrapOffset(rig.Carousel), Is.EqualTo(new Vector2(0f, 0.25f)));
            Assert.That(rig.Carousel.horizontal, Is.False);
            Assert.That(rig.Carousel.vertical, Is.False);
        }

        // 0x1B3C7D0: above 1 + w the position moves back by 2w + 1; the drag origin
        // moves with the content, UpdatePrevData records the new position and the
        // velocity SetNormalizedPosition cleared on the axis is put back.
        [Test]
        public void WrapAxisPastTheEndMovesBackOneSpanAndKeepsTheVelocity()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Content.anchoredPosition = new Vector2(0f, 20f);
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));
            rig.Carousel.velocity = new Vector2(12f, -5f);

            Call(rig.Carousel, "WrapAxis", 1.3f, 0, 0.25f);

            // 1.3 - 1.5 = -0.2, so the content goes to 100 - 200 * -0.2 = 140.
            Near(rig.Content.anchoredPosition, new Vector2(140f, 20f));
            Near(ContentStart(rig.Carousel), new Vector2(147f, 3f));
            Near(PrevPosition(rig.Carousel), new Vector2(140f, 20f));
            Assert.That(rig.Carousel.velocity, Is.EqualTo(new Vector2(12f, -5f)));
        }

        [Test]
        public void WrapAxisBeforeTheStartMovesForwardOneSpan()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));

            Call(rig.Carousel, "WrapAxis", -0.3f, 0, 0.25f);

            // -0.3 + 1.5 = 1.2, so the content goes to 100 - 240 = -140.
            Near(rig.Content.anchoredPosition, new Vector2(-140f, 0f));
            Near(ContentStart(rig.Carousel), new Vector2(-133f, 3f));
        }

        [Test]
        public void WrapAxisOnTheVerticalAxisMovesOnlyY()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Content.anchoredPosition = new Vector2(20f, 0f);
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));
            rig.Carousel.velocity = new Vector2(12f, -5f);

            Call(rig.Carousel, "WrapAxis", 1.3f, 1, 0.25f);

            Near(rig.Content.anchoredPosition, new Vector2(20f, 140f));
            Near(ContentStart(rig.Carousel), new Vector2(7f, 143f));
            Assert.That(rig.Carousel.velocity, Is.EqualTo(new Vector2(12f, -5f)));
        }

        // Both bounds are strict, and a nonzero offset leaves an in-range position
        // completely alone (no UpdatePrevData, no velocity write).
        [TestCase(1250)]
        [TestCase(-250)]
        [TestCase(500)]
        public void WrapAxisInsideTheWrapBoundsChangesNothing(int thousandths)
        {
            float position = thousandths / 1000f;
            Rig rig = Build(new Vector2(300f, 300f));
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));
            Set(rig.Carousel, typeof(ScrollRect), "m_PrevPosition", new Vector2(9f, 9f));
            rig.Carousel.velocity = new Vector2(12f, -5f);

            Call(rig.Carousel, "WrapAxis", position, 0, 0.25f);

            Assert.That(rig.Content.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(ContentStart(rig.Carousel), Is.EqualTo(new Vector2(7f, 3f)));
            Assert.That(PrevPosition(rig.Carousel), Is.EqualTo(new Vector2(9f, 9f)));
            Assert.That(rig.Carousel.velocity, Is.EqualTo(new Vector2(12f, -5f)));
        }

        // 0x1b3c888..8c7: Mathf.Approximately(wrapOffset, 0) sends an in-range
        // position to 0 (anchored 100).
        [Test]
        public void WrapAxisWithAZeroOffsetPinsAnInRangePositionToTheStart()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));

            Call(rig.Carousel, "WrapAxis", 0.6f, 0, 0f);

            Near(rig.Content.anchoredPosition, new Vector2(100f, 0f));
            Near(ContentStart(rig.Carousel), new Vector2(107f, 3f));
        }

        // The inlined test is max(|w| * 1e-6 (0x29a6e18), Mathf.Epsilon * 8 (0x29a6e1c))
        // > |0 - w|, so a denormal offset below 8 * Epsilon also counts as zero.
        [Test]
        public void WrapAxisWithADenormalOffsetPinsAnInRangePositionToTheStart()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            Set(rig.Carousel, typeof(ScrollRect), "m_ContentStartPosition", new Vector2(7f, 3f));

            Call(rig.Carousel, "WrapAxis", 0.6f, 0, 5e-45f);

            Near(rig.Content.anchoredPosition, new Vector2(100f, 0f));
            Near(ContentStart(rig.Carousel), new Vector2(107f, 3f));
        }

        // The inlined Vector2 indexer throws "Invalid Vector2 index!" (0x1b3c92e) on
        // the first read, before anything is written.
        [Test]
        public void WrapAxisWithAnUndeclaredAxisThrowsBeforeWriting()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Carousel.velocity = new Vector2(12f, -5f);

            var error = Assert.Throws<IndexOutOfRangeException>(() => Call(rig.Carousel, "WrapAxis", 1.3f, 2, 0.25f));
            Assert.That(error.Message, Is.EqualTo("Invalid Vector2 index!"));
            Assert.That(rig.Content.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(rig.Carousel.velocity, Is.EqualTo(new Vector2(12f, -5f)));
        }

        // 0x1B3C760: with both axes enabled only x wraps, using m_wrapOffset.x. The
        // y offset (0.1) would wrap y = -160 (1.3) to 80, and passed for x it would
        // give 80 instead of 140. Unrestricted movement and zero velocity
        // keep ScrollRect.LateUpdate from moving the content first.
        [Test]
        public void LateUpdateWrapsOnlyTheHorizontalAxisWhenBothAreEnabled()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Content.anchoredPosition = new Vector2(-160f, -160f);
            rig.Carousel.horizontal = true;
            rig.Carousel.vertical = true;
            rig.Carousel.SetWrapOffset(new Vector2(0.25f, 0.1f));

            Call(rig.Carousel, "LateUpdate");

            Near(rig.Content.anchoredPosition, new Vector2(140f, -160f));
        }

        [Test]
        public void LateUpdateWrapsTheVerticalAxisWithTheYOffset()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Content.anchoredPosition = new Vector2(-160f, -160f);
            rig.Carousel.horizontal = false;
            rig.Carousel.vertical = true;
            rig.Carousel.SetWrapOffset(new Vector2(0.5f, 0.25f));

            Call(rig.Carousel, "LateUpdate");

            Near(rig.Content.anchoredPosition, new Vector2(-160f, 140f));
        }

        [Test]
        public void LateUpdateWithBothAxesDisabledDoesNotWrap()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Content.anchoredPosition = new Vector2(-160f, -160f);
            rig.Carousel.horizontal = false;
            rig.Carousel.vertical = false;
            rig.Carousel.SetWrapOffset(new Vector2(0.25f, 0.25f));

            Call(rig.Carousel, "LateUpdate");

            Assert.That(rig.Content.anchoredPosition, Is.EqualTo(new Vector2(-160f, -160f)));
        }

        // HLScrollRect.LateUpdate (0x0600068e, called at 0x1b3c76b) runs first and
        // zeroes a velocity whose square magnitude is in (Epsilon, 100]: 25 here.
        // Unit deceleration keeps ScrollRect.LateUpdate from damping it, and the
        // content stays near normalised 0.5, inside the wrap bounds.
        [Test]
        public void LateUpdateRunsTheHLScrollRectSmallVelocityStopFirst()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            rig.Carousel.horizontal = true;
            rig.Carousel.vertical = false;
            rig.Carousel.inertia = true;
            rig.Carousel.decelerationRate = 1f;
            rig.Carousel.SetWrapOffset(new Vector2(0.25f, 0f));
            rig.Carousel.velocity = new Vector2(5f, 0f);

            Call(rig.Carousel, "LateUpdate");

            Assert.That(rig.Carousel.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(Mathf.Abs(rig.Content.anchoredPosition.x), Is.LessThan(50f), "No wrap happened.");
        }

        sealed class CapSources
        {
            public RectTransform First;
            public RectTransform Middle;
            public RectTransform Last;
        }

        // The first item has a LayoutElement and the last one does not, so the two
        // SetupCap branches (0x1b3cb1f) are both taken.
        static CapSources BuildCapSources(Rig rig)
        {
            RectTransform[] items = Children(rig, 3);
            items[0].gameObject.AddComponent<LayoutElement>();
            items[0].anchorMin = new Vector2(0f, 0f);
            items[0].anchorMax = new Vector2(0.2f, 1f);
            items[0].anchoredPosition = new Vector2(5f, 6f);
            items[0].sizeDelta = new Vector2(10f, 20f);
            items[2].anchorMin = new Vector2(0.6f, 0f);
            items[2].anchorMax = new Vector2(0.8f, 1f);
            items[2].anchoredPosition = new Vector2(1f, 2f);
            items[2].sizeDelta = new Vector2(3f, 4f);
            return new CapSources { First = items[0], Middle = items[1], Last = items[2] };
        }

        // 0x1B3CD50 / 0x1B3CAA0, horizontal: the start cap copies the last item and
        // moves to index 0 after its callback; the end cap copies the first item and
        // stays last. A LayoutElement cap is reset to its source's size, anchors and
        // position, undoing the callback; the other cap keeps the callback's values.
        // Both are then shifted by -1 (start) and +1 (end) anchor spans along x.
        [Test]
        public void CreateCapsCopiesTheOppositeEndsAndShiftsThemAlongX()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            CapSources sources = BuildCapSources(rig);
            rig.Carousel.horizontal = true;
            var calls = new List<string>();
            Action<RectTransform> onStart = cap =>
            {
                calls.Add("start " + cap.GetSiblingIndex());
                Assert.That(cap, Is.SameAs(Get(rig.Carousel, typeof(HLCarousel), "m_startCap")));
                cap.sizeDelta = new Vector2(55f, 55f);
                cap.anchorMin = new Vector2(0.1f, 0.1f);
                cap.anchorMax = new Vector2(0.3f, 0.3f);
            };
            Action<RectTransform> onEnd = cap =>
            {
                calls.Add("end " + cap.GetSiblingIndex());
                cap.sizeDelta = new Vector2(99f, 99f);
                cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 0.5f);
                cap.anchoredPosition = new Vector2(77f, 77f);
            };

            rig.Carousel.CreateCaps(onStart, onEnd);

            var startCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_startCap");
            var endCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_endCap");
            Assert.That(calls, Is.EqualTo(new[] { "start 3", "end 4" }));
            Assert.That(Enumerable.Range(0, rig.Content.childCount).Select(i => rig.Content.GetChild(i)).ToArray(),
                Is.EqualTo(new Transform[] { startCap, sources.First, sources.Middle, sources.Last, endCap }));
            Assert.That(startCap.name, Is.EqualTo("Item2(Clone)"));
            Assert.That(endCap.name, Is.EqualTo("Item0(Clone)"));

            Assert.That(startCap.GetComponent<LayoutElement>(), Is.Null);
            Near(startCap.sizeDelta, new Vector2(55f, 55f));
            Near(startCap.anchorMin, new Vector2(-0.9f, 0.1f));
            Near(startCap.anchorMax, new Vector2(-0.7f, 0.3f));

            Assert.That(endCap.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(sources.First.GetComponent<LayoutElement>().ignoreLayout, Is.False);
            Near(endCap.sizeDelta, new Vector2(10f, 20f));
            Near(endCap.anchoredPosition, new Vector2(5f, 6f));
            Near(endCap.anchorMin, new Vector2(1f, 0f));
            Near(endCap.anchorMax, new Vector2(1.2f, 1f));
        }

        // Only the horizontal flag is read (0x1b3cedf, 0x1b3cba5): with both axes off
        // the caps take the vertical direction, +1 for the start cap and -1 for the
        // end cap along y. Null callbacks are skipped.
        [Test]
        public void CreateCapsWithoutHorizontalShiftsAlongYAndAcceptsNullCallbacks()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            CapSources sources = BuildCapSources(rig);
            rig.Carousel.horizontal = false;
            rig.Carousel.vertical = false;

            rig.Carousel.CreateCaps(null, null);

            var startCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_startCap");
            var endCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_endCap");
            Near(startCap.anchorMin, new Vector2(0.6f, 1f));
            Near(startCap.anchorMax, new Vector2(0.8f, 2f));
            Near(endCap.anchorMin, new Vector2(0f, -1f));
            Near(endCap.anchorMax, new Vector2(0.2f, 0f));
            Assert.That(sources.Last.anchorMin, Is.EqualTo(new Vector2(0.6f, 0f)), "The source item is not moved.");
        }

        // 0x1B3CC30 destroys each cap's GameObject immediately and clears the field;
        // missing caps are skipped.
        [Test]
        public void DestroyCapsDestroysBothCapObjectsAndClearsTheFields()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            BuildCapSources(rig);
            rig.Carousel.CreateCaps(null, null);
            var startCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_startCap");
            var endCap = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_endCap");

            rig.Carousel.DestroyCaps();

            Assert.That(startCap == null && endCap == null, Is.True);
            Assert.That(ReferenceEquals(Get(rig.Carousel, typeof(HLCarousel), "m_startCap"), null), Is.True);
            Assert.That(ReferenceEquals(Get(rig.Carousel, typeof(HLCarousel), "m_endCap"), null), Is.True);
            Assert.That(rig.Content.childCount, Is.EqualTo(3));
            Assert.DoesNotThrow(() => rig.Carousel.DestroyCaps());
        }

        // 0x1B3CC00: the old caps are gone before the new ones are copied, so the new
        // start cap copies the real last item rather than the old end cap.
        [Test]
        public void RecreateCapsDestroysTheOldCapsBeforeCopyingTheItems()
        {
            Rig rig = Build(new Vector2(300f, 300f));
            BuildCapSources(rig);
            rig.Carousel.horizontal = true;
            rig.Carousel.CreateCaps(null, null);
            var oldStart = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_startCap");
            var oldEnd = (RectTransform)Get(rig.Carousel, typeof(HLCarousel), "m_endCap");
            var created = new List<string>();

            rig.Carousel.RecreateCaps(cap => created.Add(cap.name), cap => created.Add(cap.name));

            Assert.That(oldStart == null && oldEnd == null, Is.True);
            Assert.That(created, Is.EqualTo(new[] { "Item2(Clone)", "Item0(Clone)" }));
            Assert.That(rig.Content.childCount, Is.EqualTo(5));
        }
    }
}
