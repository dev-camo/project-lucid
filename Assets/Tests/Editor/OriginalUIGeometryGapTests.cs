using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned Unity objects only. Scroll-selection components stay on an inactive
    // root and are never enabled; the measured RectTransforms live under a
    // separate root. The HLUIEffectManager singleton slot is restored by reference.
    public sealed class OriginalUIGeometryGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // HLNavigation.Equals 0x06000652 (x86-64 0x1b6fe90) compares Mode and
        // then each selectable through Object_op_Equality (0x26cdaf0), so a
        // destroyed HLSelectable equals null and another destroyed one in every
        // direction, while a live one does not. ReferenceEquals or
        // object.Equals on the fields would report the destroyed cases unequal.
        [Test]
        public void NavigationEqualityTreatsDestroyedSelectablesAsUnityNull()
        {
            var owned = new List<UObject>();
            try
            {
                HLSelectable live = Selectable(owned, "Lucid owned live selectable");
                HLSelectable destroyed = Selectable(owned, "Lucid owned destroyed selectable");
                HLSelectable otherDestroyed = Selectable(owned, "Lucid owned second destroyed selectable");
                UObject.DestroyImmediate(destroyed.gameObject);
                UObject.DestroyImmediate(otherDestroyed.gameObject);
                Assert.That(destroyed == null && !ReferenceEquals(destroyed, null), Is.True);

                foreach (string direction in new[] { "Up", "Down", "Left", "Right" })
                {
                    HLNavigation empty = HLNavigation.DefaultNavigation;
                    HLNavigation withDestroyed = With(direction, destroyed);
                    HLNavigation withOtherDestroyed = With(direction, otherDestroyed);
                    HLNavigation withLive = With(direction, live);
                    Assert.That(withDestroyed.Equals(empty), Is.True, direction + " destroyed equals null");
                    Assert.That(empty.Equals(withDestroyed), Is.True, direction + " null equals destroyed");
                    Assert.That(withDestroyed.Equals(withOtherDestroyed), Is.True, direction + " two destroyed objects");
                    Assert.That(withLive.Equals(empty), Is.False, direction + " live differs from null");
                    Assert.That(withLive.Equals(withDestroyed), Is.False, direction + " live differs from destroyed");
                }
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; --i)
                    if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
            }
        }

        private static HLNavigation With(string direction, HLSelectable selectable)
        {
            HLNavigation navigation = HLNavigation.DefaultNavigation;
            switch (direction)
            {
                case "Up": navigation.SelectOnUp = selectable; break;
                case "Down": navigation.SelectOnDown = selectable; break;
                case "Left": navigation.SelectOnLeft = selectable; break;
                default: navigation.SelectOnRight = selectable; break;
            }
            return navigation;
        }

        private static HLSelectable Selectable(List<UObject> owned, string name)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owned.Add(owner);
            owner.SetActive(false);
            return owner.AddComponent<HLSelectable>();
        }

        // FitTrimmedImageToBoundsHeight 0x060005d6 (x86-64 0x1b6bc10) reads the
        // Instance's reference-PPU field (+0x128) over sprite.pixelsPerUnit,
        // multiplies by textureRect.height, divides by rect.height, then divides
        // bounds.rect.height by that and writes a square sizeDelta. With
        // reference 64, PPU 32 and a 40x20 sprite the factor is 2, so a 50-high
        // bound gives (25, 25). Width-based factors, the bound's width, an
        // inverted PPU ratio or a non-square write all differ. Sprite.Create
        // gives textureRect == rect, so substituting rect for textureRect is not
        // observable here; that needs a packed atlas sprite.
        [Test]
        public void FitTrimmedImageWritesSquareSizeFromBoundsHeightAndReferencePixels()
        {
            PropertyInfo instance = typeof(MonoSingleton<HLUIEffectManager>).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo setInstance = instance.GetSetMethod(true);
            HLUIEffectManager previous = HLUIEffectManager.Instance;
            var owned = new List<UObject>();
            try
            {
                var managerOwner = new GameObject("Lucid owned inactive UI effect reference");
                owned.Add(managerOwner);
                managerOwner.SetActive(false);
                HLUIEffectManager manager = managerOwner.AddComponent<HLUIEffectManager>();
                // The public setter is an original no-op; the field stands in for authored data.
                typeof(HLUIEffectManager).GetField("m_referencePixelsPerUnit", AnyInstance).SetValue(manager, 64f);
                Assert.That(manager.ReferencePixelsPerUnit, Is.EqualTo(64f));
                // Owned singleton slot only; Awake and the registry are never entered.
                setInstance.Invoke(null, new object[] { manager });

                var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false);
                owned.Add(texture);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 40f, 20f), new Vector2(0.5f, 0.5f), 32f);
                owned.Add(sprite);
                Assert.That(sprite.textureRect, Is.EqualTo(sprite.rect));

                var imageOwner = new GameObject("Lucid owned trimmed image", typeof(RectTransform));
                owned.Add(imageOwner);
                imageOwner.SetActive(false);
                Image image = imageOwner.AddComponent<Image>();
                image.sprite = sprite;
                image.rectTransform.sizeDelta = new Vector2(3f, 5f);

                var boundsOwner = new GameObject("Lucid owned bounds", typeof(RectTransform));
                owned.Add(boundsOwner);
                var bounds = (RectTransform)boundsOwner.transform;
                bounds.sizeDelta = new Vector2(10f, 50f);

                UIUtils.FitTrimmedImageToBoundsHeight(image, bounds);
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(new Vector2(25f, 25f)));
            }
            finally
            {
                try
                {
                    for (int i = owned.Count - 1; i >= 0; --i)
                        if (owned[i] != null) UObject.DestroyImmediate(owned[i]);
                }
                finally { setInstance.Invoke(null, new object[] { previous }); }
            }
            Assert.That(ReferenceEquals(HLUIEffectManager.Instance, previous), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // CalculateTargetScrollOffset 0x06000599, Visible case (x86-64 0x1b68b6d):
        // horizontal tests beyondEnd as selectionEnd > viewportEnd and beyondStart
        // as viewportStart > selectionStart; vertical (0x1b68ced) inverts both.
        // beyondStart wins (start - selectionStart), else beyondEnd gives
        // start - selectionEnd - extent; the ratio over (list extent - viewport
        // extent) is clamped and complemented for vertical. Geometry: viewport
        // 200x100 and list 400x300 centred at the origin, 20x10 items. Swapping
        // < for > in the vertical beyondEnd test turns "below" into the -1
        // sentinel; testing beyondEnd first flips both spanning cases.
        [Test]
        public void VisibleAlignmentTargetsItemsBeyondEitherViewportEdge()
        {
            var cases = new[]
            {
                // horizontal, position, size, expected target
                (true, new Vector2(-150f, 0f), new Vector2(20f, 10f), 0.2f, "left of viewport"),
                (true, new Vector2(150f, 0f), new Vector2(20f, 10f), 0.8f, "right of viewport"),
                (true, Vector2.zero, new Vector2(300f, 10f), 0.25f, "spanning both horizontal edges"),
                (false, new Vector2(0f, 80f), new Vector2(20f, 10f), 0.675f, "above viewport"),
                (false, new Vector2(0f, -80f), new Vector2(20f, 10f), 0.325f, "below viewport"),
                (false, Vector2.zero, new Vector2(20f, 200f), 0.75f, "spanning both vertical edges"),
            };
            foreach (var (horizontal, position, size, expected, label) in cases)
            {
                using (var geometry = new ScrollGeometry(horizontal, new Vector2(400f, 300f)))
                {
                    RectTransform item = geometry.Item("Lucid owned measured item", size, position);
                    geometry.Recalculate();
                    Assert.That(geometry.Selection.GetDirection, Is.EqualTo(horizontal ? HLScrollRectSelection.Direction.Horizontal : HLScrollRectSelection.Direction.Vertical), label);
                    float target = geometry.Target(item, HLScrollRectSelection.Alignment.Visible, false);
                    Assert.That(target, Is.EqualTo(expected).Within(1e-5f), label);
                }
            }
        }

        // GetNearestChild 0x0600059e (x86-64 0x1b67050) computes both from-viewport
        // distances and then `ucomiss max, min; jbe` (0x1b67124) selects max when
        // max <= min or the pair is unordered, i.e. min only when min < max. Two
        // identical items give equal distances; a list as tall as the viewport
        // makes the denominator zero, so the min bookend's distance is NaN and
        // the max one's is -infinity. `<=` returns min for the equal pair and
        // `!(min >= max)` returns min for the NaN pair.
        [Test]
        public void NearestChildChoosesMaxBookendForEqualAndUnorderedDistances()
        {
            using (var geometry = new ScrollGeometry(false, new Vector2(400f, 300f)))
            {
                RectTransform first = geometry.Item("Lucid owned equal min", new Vector2(20f, 10f), Vector2.zero);
                RectTransform second = geometry.Item("Lucid owned equal max", new Vector2(20f, 10f), Vector2.zero);
                geometry.Recalculate();
                float firstDistance = geometry.Target(first, HLScrollRectSelection.Alignment.TopOrLeft, true);
                Assert.That(geometry.Target(second, HLScrollRectSelection.Alignment.TopOrLeft, true), Is.EqualTo(firstDistance));
                Assert.That(geometry.Selection.GetNearestChild(), Is.SameAs(second));
                Assert.That(geometry.Selection.CurrentSelection, Is.SameAs(second));
            }
            using (var geometry = new ScrollGeometry(false, new Vector2(200f, 100f)))
            {
                RectTransform first = geometry.Item("Lucid owned unordered min", new Vector2(20f, 10f), new Vector2(0f, 45f));
                RectTransform second = geometry.Item("Lucid owned unordered max", new Vector2(20f, 10f), Vector2.zero);
                geometry.Recalculate();
                Assert.That(float.IsNaN(geometry.Target(first, HLScrollRectSelection.Alignment.TopOrLeft, true)), Is.True);
                Assert.That(float.IsNegativeInfinity(geometry.Target(second, HLScrollRectSelection.Alignment.TopOrLeft, true)), Is.True);
                Assert.That(geometry.Selection.GetNearestChild(), Is.SameAs(second));
            }
        }

        private sealed class ScrollGeometry : IDisposable
        {
            private readonly GameObject componentRoot;
            private readonly GameObject geometryRoot;
            private readonly RectTransform list;
            private readonly HLScrollRect scroll;
            public readonly HLScrollRectSelection Selection;

            public ScrollGeometry(bool horizontal, Vector2 listSize)
            {
                componentRoot = new GameObject("Lucid owned inactive scroll selection", typeof(RectTransform));
                componentRoot.SetActive(false);
                geometryRoot = new GameObject("Lucid owned scroll geometry", typeof(RectTransform));
                try
                {
                    scroll = componentRoot.AddComponent<HLScrollRect>();
                    Selection = componentRoot.AddComponent<HLScrollRectSelection>();
                    RectTransform viewport = Rect("Lucid owned viewport", geometryRoot.transform, new Vector2(200f, 100f), Vector2.zero);
                    list = Rect("Lucid owned list", viewport, listSize, Vector2.zero);
                    scroll.viewport = viewport;
                    scroll.content = list;
                    scroll.horizontal = horizontal;
                    scroll.vertical = true;
                    // Stand in for the authored serialized references.
                    typeof(HLScrollRectSelection).GetField("m_listContents", AnyInstance).SetValue(Selection, list.gameObject);
                    typeof(HLScrollRectSelection).GetField("m_scrollRect", AnyInstance).SetValue(Selection, scroll);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public RectTransform Item(string name, Vector2 size, Vector2 position) => Rect(name, list, size, position);

            public void Recalculate() => Selection.RecalculateInterior();

            public float Target(RectTransform item, HLScrollRectSelection.Alignment alignment, bool fromViewport)
            {
                try
                {
                    return (float)typeof(HLScrollRectSelection).GetMethod("CalculateTargetScrollOffset", AnyInstance)
                        .Invoke(Selection, new object[] { item, alignment, fromViewport });
                }
                catch (TargetInvocationException failure) { throw failure.InnerException ?? failure; }
            }

            private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
            {
                var owner = new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)owner.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
                return rect;
            }

            public void Dispose()
            {
                try { if (componentRoot != null) UObject.DestroyImmediate(componentRoot); }
                finally { if (geometryRoot != null) UObject.DestroyImmediate(geometryRoot); }
            }
        }
    }
}
