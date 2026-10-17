using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of the original axis subclasses Hardlight.HLHorizontalScrollRectContentFocus
    // (HLUnityUI.Runtime 0x06000568..56b) and Hardlight.HLVerticalScrollRectContentFocus
    // (0x060005c9..5cc) against the shipped x86-64 bodies. Each case owns an
    // inactive HideAndDontSave root with a plain ScrollRect, the component under
    // test and a content holder, and TearDown destroys it; no engine callback runs
    // and no shared state is touched. Protected base state is set by reflection.
    [TestFixture]
    public sealed class OriginalHLAxisScrollRectContentFocusTests
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
            public HLScrollRectContentFocus Focus;
            public ScrollRect Scroll;
            public RectTransform Holder;
            public RectTransform Child;
        }

        Rig Build(bool horizontal)
        {
            var root = new GameObject("ProjectLucid.HLAxisContentFocus.Owned", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            roots.Add(root);
            root.SetActive(false);
            var rig = new Rig { Scroll = root.AddComponent<ScrollRect>() };
            rig.Focus = horizontal
                ? (HLScrollRectContentFocus)root.AddComponent<HLHorizontalScrollRectContentFocus>()
                : root.AddComponent<HLVerticalScrollRectContentFocus>();
            rig.Holder = NewRect("Holder", root.transform, new Vector2(200f, 200f));
            rig.Holder.pivot = new Vector2(0.25f, 0.75f);
            rig.Child = NewRect("Child", rig.Holder, new Vector2(40f, 70f));
            Set(rig.Focus, "m_scrollRect", rig.Scroll);
            Set(rig.Focus, "m_scrollRectContentHolder", rig.Holder);
            Set(rig.Focus, "m_spacingBetweenContentItems", 10f);
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

        static object Get(object target, string name) => typeof(HLScrollRectContentFocus).GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => typeof(HLScrollRectContentFocus).GetField(name, Any).SetValue(target, value);

        static void Call(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, Declared);
            try { method.Invoke(target, arguments); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object Interaction(int value) =>
            Enum.ToObject(typeof(HLScrollRectContentFocus).GetNestedType("Interaction", BindingFlags.NonPublic), value);

        [TestCase(true)]
        [TestCase(false)]
        public void DeclarationOverridesBothSlotsAndHidesLateUpdate(bool horizontal)
        {
            Type type = horizontal ? typeof(HLHorizontalScrollRectContentFocus) : typeof(HLVerticalScrollRectContentFocus);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLScrollRectContentFocus)));
            Assert.That(type.IsSealed || type.IsAbstract, Is.False);
            Assert.That(type.GetFields(Declared), Is.Empty);
            Assert.That(type.GetMethods(Declared).Select(m => m.Name).ToArray(),
                Is.EquivalentTo(new[] { "Refocus", "CalculatePositionDelta", "LateUpdate" }));
            MethodInfo refocus = type.GetMethod("Refocus", Declared);
            Assert.That(refocus.IsPublic && refocus.IsVirtual && refocus.GetBaseDefinition().DeclaringType == typeof(HLScrollRectContentFocus), Is.True);
            MethodInfo delta = type.GetMethod("CalculatePositionDelta", Declared);
            Assert.That(delta.IsFamily && delta.IsVirtual, Is.True);
            Assert.That(delta.GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "child", "interactionType", "index" }));
            Assert.That(delta.GetParameters()[2].DefaultValue, Is.EqualTo(0));
            MethodInfo lateUpdate = type.GetMethod("LateUpdate", Declared);
            Assert.That(lateUpdate.IsPrivate && !lateUpdate.IsVirtual, Is.True);
        }

        // Horizontal 0x1B66140 passes rect.width (40) to the base calculation
        // 0x0600057a, which flips the x axis; vertical 0x1B6B0C0 inlines it with
        // rect.height (70) and no flip (jump table at 0x1b6b154). Holder pivot
        // (0.25, 0.75), spacing 10, starting from an accumulated 3. Index-based and
        // undeclared interactions add nothing. Expected additions per Interaction
        // 0..6: horizontal (50 * -(p - 0), -(p - 1), 0, (p - 0), (p - 1), 0, 0) with
        // p = 0.25; vertical (80 * (p - 0), (p - 1), 0, -(p - 0), -(p - 1), 0, 0)
        // with p = 0.75.
        static readonly float[] HorizontalDeltas = { -12.5f, 37.5f, 0f, 12.5f, -37.5f, 0f, 0f };
        static readonly float[] VerticalDeltas = { 60f, -20f, 0f, -60f, 20f, 0f, 0f };

        [Test]
        public void CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(
            [Values(true, false)] bool horizontal, [Values(0, 1, 2, 3, 4, 5, 6)] int interaction)
        {
            Rig rig = Build(horizontal);
            Set(rig.Focus, "m_accumulatedPositionDelta", 3f);

            Call(rig.Focus, "CalculatePositionDelta", rig.Child, Interaction(interaction), 1);

            float expected = 3f + (horizontal ? HorizontalDeltas : VerticalDeltas)[interaction];
            Assert.That((float)Get(rig.Focus, "m_accumulatedPositionDelta"), Is.EqualTo(expected).Within(1e-4f));
        }

        // 0x1B66120 / 0x1B6B0A0 write the accumulated delta onto their own axis of
        // m_contentRelativeOffset (zeroing the other) before the base Refocus moves
        // the holder by it and clears the accumulator.
        [TestCase(true)]
        [TestCase(false)]
        public void RefocusMovesTheHolderAlongItsAxis(bool horizontal)
        {
            float expectedX = horizontal ? 17f : 5f;
            float expectedY = horizontal ? 7f : 19f;
            Rig rig = Build(horizontal);
            rig.Holder.anchoredPosition = new Vector2(5f, 7f);
            Set(rig.Focus, "m_contentRelativeOffset", new Vector2(30f, 40f));
            Set(rig.Focus, "m_accumulatedPositionDelta", 12f);
            Set(rig.Focus, "m_requiresRefocusing", true);
            Set(rig.Focus, "m_focusEnabled", true);

            rig.Focus.Refocus();

            Assert.That(rig.Holder.anchoredPosition.x, Is.EqualTo(expectedX).Within(1e-4f));
            Assert.That(rig.Holder.anchoredPosition.y, Is.EqualTo(expectedY).Within(1e-4f));
            Assert.That(Get(rig.Focus, "m_contentRelativeOffset"), Is.EqualTo(horizontal ? new Vector2(12f, 0f) : new Vector2(0f, 12f)));
            Assert.That(Get(rig.Focus, "m_accumulatedPositionDelta"), Is.EqualTo(0f));
        }

        // The offset is written even when the base Refocus then does nothing.
        [TestCase(true)]
        [TestCase(false)]
        public void RefocusWritesTheOffsetEvenWhenFocusIsDisabled(bool horizontal)
        {
            Rig rig = Build(horizontal);
            rig.Holder.anchoredPosition = new Vector2(5f, 7f);
            Set(rig.Focus, "m_accumulatedPositionDelta", 12f);
            Set(rig.Focus, "m_requiresRefocusing", true);

            rig.Focus.Refocus();

            Assert.That(rig.Holder.anchoredPosition, Is.EqualTo(new Vector2(5f, 7f)));
            Assert.That(Get(rig.Focus, "m_contentRelativeOffset"), Is.EqualTo(horizontal ? new Vector2(12f, 0f) : new Vector2(0f, 12f)));
            Assert.That(Get(rig.Focus, "m_accumulatedPositionDelta"), Is.EqualTo(12f));
        }

        // 0x1B66180 / 0x1B6B170: with m_preserveVelocity set, the previous velocity is
        // restored (and the flag cleared) only when Mathf.Sign of the current axis
        // velocity is +1 (so 0 counts), the previous sign differs and the previous
        // axis velocity is nonzero; the other axis is ignored (its values here would
        // pass). The current velocity is then always sampled.
        [TestCase(true, 5, -3, true)]
        [TestCase(true, 0, -3, true)]
        [TestCase(true, -5, 3, false)]
        [TestCase(true, 5, 3, false)]
        [TestCase(true, -5, -3, false)]
        [TestCase(false, 5, -3, true)]
        [TestCase(false, 0, -3, true)]
        [TestCase(false, -5, 3, false)]
        [TestCase(false, 5, 3, false)]
        public void LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(bool horizontal, int current, int previous, bool restores)
        {
            Rig rig = Build(horizontal);
            Vector2 currentVelocity = horizontal ? new Vector2(current, -4f) : new Vector2(-4f, current);
            Vector2 previousVelocity = horizontal ? new Vector2(previous, 8f) : new Vector2(8f, previous);
            rig.Scroll.velocity = currentVelocity;
            Set(rig.Focus, "m_previousScrollRectVelocity", previousVelocity);
            Set(rig.Focus, "m_preserveVelocity", true);

            Call(rig.Focus, "LateUpdate");

            Vector2 expected = restores ? previousVelocity : currentVelocity;
            Assert.That(rig.Scroll.velocity, Is.EqualTo(expected));
            Assert.That(Get(rig.Focus, "m_preserveVelocity"), Is.EqualTo(!restores));
            Assert.That(Get(rig.Focus, "m_previousScrollRectVelocity"), Is.EqualTo(expected));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LateUpdateWithoutPreserveVelocityOnlySamplesTheVelocity(bool horizontal)
        {
            Rig rig = Build(horizontal);
            rig.Scroll.velocity = new Vector2(5f, 5f);
            Set(rig.Focus, "m_previousScrollRectVelocity", new Vector2(-3f, -3f));

            Call(rig.Focus, "LateUpdate");

            Assert.That(rig.Scroll.velocity, Is.EqualTo(new Vector2(5f, 5f)));
            Assert.That(Get(rig.Focus, "m_previousScrollRectVelocity"), Is.EqualTo(new Vector2(5f, 5f)));
        }

        // The base LateUpdate (0x0600057b, inlined in the vertical body) runs first:
        // a pending refocus moves the holder before the velocity is sampled.
        [TestCase(true)]
        [TestCase(false)]
        public void LateUpdateFlushesAPendingRefocusFirst(bool horizontal)
        {
            float expectedX = horizontal ? 17f : 5f;
            float expectedY = horizontal ? 7f : 19f;
            Rig rig = Build(horizontal);
            rig.Holder.anchoredPosition = new Vector2(5f, 7f);
            Set(rig.Focus, "m_accumulatedPositionDelta", 12f);
            Set(rig.Focus, "m_requiresRefocusing", true);
            Set(rig.Focus, "m_focusEnabled", true);
            rig.Scroll.velocity = new Vector2(1f, 2f);

            Call(rig.Focus, "LateUpdate");

            Assert.That(rig.Holder.anchoredPosition.x, Is.EqualTo(expectedX).Within(1e-4f));
            Assert.That(rig.Holder.anchoredPosition.y, Is.EqualTo(expectedY).Within(1e-4f));
            Assert.That(Get(rig.Focus, "m_requiresRefocusing"), Is.False);
            Assert.That(Get(rig.Focus, "m_previousScrollRectVelocity"), Is.EqualTo(new Vector2(1f, 2f)));
        }
    }
}
