using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hardlight;
using Source = HLButtonHighlightCustomisations.ButtonHighlightChangeSource;

namespace ProjectLucid.Tests
{
    // Behaviour specification for the five original HLUnityUI.Runtime ModifiedOnUISelection
    // layers (TypeDefs 0x02000094..98), from their x86-64 bodies in the pinned 6c89 dylib.
    // ButtonHighlightChangeSource layout (dump.cs 11829): Enabled +0x10, Top +0x14, Right
    // +0x18, Bottom +0x1C, Left +0x20, Sprite +0x28, Material +0x30, Color +0x38, SkewX
    // +0x48, SkewY +0x4C. Each lerp clamps t to [0, 1] once (minss with 1.0f at 0x29a6be0,
    // then the `< 0` mask), then computes a + (b - a) * t (Mathf.Lerp or Vector2.Lerp
    // semantics). Owners stay inactive unless a case needs otherwise.
    public sealed class OriginalSelectionModifierTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string SourceParameters = "previousSource,currentSource";
        private const string UpdatedParameters = "previousSource,currentSource,lerpValue";

        private readonly List<UnityEngine.Object> m_owned = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = m_owned.Count - 1; i >= 0; --i)
                if (m_owned[i] != null) UnityEngine.Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }

        // ---------------------------------------------------------------- declarations

        // dump.cs 11972: no fields, no attributes; only OnHighlightUpdated (slot 5) and
        // OnDeactivate (slot 8) are overridden, so OnActivate stays the base no-op.
        [Test]
        public void ActivateRetainsTheOriginalDeclaration()
        {
            CheckType(typeof(OnUISelectionActivate), null, new string[0], new Type[0]);
            CheckMethods(typeof(OnUISelectionActivate),
                new[] { "OnHighlightUpdated", "OnDeactivate" },
                new[] { 198, 198 },
                new[] { UpdatedParameters, "source" });
        }

        // dump.cs 11973: no RequireComponent; [SerializeField] RectTransform +0x20, plain
        // Vector2 +0x28.
        [Test]
        public void ResizeRetainsTheOriginalDeclaration()
        {
            CheckType(typeof(OnUISelectionResize), null, new[] { "m_rectTransform", "m_reusableVector2" }, new[] { typeof(RectTransform), typeof(Vector2) });
            CheckMethods(typeof(OnUISelectionResize), new[] { "Reset", "OnHighlightUpdated" }, new[] { 129, 198 }, new[] { "", UpdatedParameters });
        }

        [Test]
        public void SkewRetainsTheOriginalDeclaration()
        {
            CheckType(typeof(OnUISelectionSkew), typeof(Hardlight.Skew), new[] { "m_skew", "m_skewAmount" }, new[] { typeof(Hardlight.Skew), typeof(Vector2) });
            CheckMethods(typeof(OnUISelectionSkew), new[] { "Reset", "OnHighlightUpdated" }, new[] { 129, 198 }, new[] { "", UpdatedParameters });
        }

        [Test]
        public void SwapColourRetainsTheOriginalDeclaration()
        {
            CheckType(typeof(OnUISelectionSwapColour), typeof(Image), new[] { "m_image", "m_currentColourBlend" }, new[] { typeof(Image), typeof(Color) });
            CheckMethods(typeof(OnUISelectionSwapColour), new[] { "Reset", "OnHighlightUpdated" }, new[] { 129, 198 }, new[] { "", UpdatedParameters });
        }

        // dump.cs 11976: methods in token order 0x06000493..96.
        [Test]
        public void SwapSpriteRetainsTheOriginalDeclaration()
        {
            CheckType(typeof(OnUISelectionSwapSprite), typeof(Image), new[] { "m_image", "m_spritesSwapped" }, new[] { typeof(Image), typeof(bool) });
            CheckMethods(typeof(OnUISelectionSwapSprite),
                new[] { "Reset", "OnActivate", "OnHighlightUpdated", "OnHighlightBegin" },
                new[] { 129, 198, 198, 198 },
                new[] { "", "source", UpdatedParameters, SourceParameters });
        }

        // ---------------------------------------------------------------- OnUISelectionActivate

        // 0x06000487 @0x1B5F680: get_gameObject, then SetActive(currentSource.Enabled +0x10).
        // previousSource (rsi) and lerpValue (xmm0) are never read, so a null previous source
        // and lerp values on either side of any threshold change nothing.
        [Test]
        public void ActivateUpdateFollowsTheCurrentEnabledFlagAtAnyLerp()
        {
            OnUISelectionActivate layer = Make<OnUISelectionActivate>("OriginalActivate-update", true);
            layer.OnHighlightUpdated(null, NewSource(enabled: true), 0f);
            Assert.That(layer.gameObject.activeSelf, Is.True);
            layer.OnHighlightUpdated(null, NewSource(enabled: false), 1f);
            Assert.That(layer.gameObject.activeSelf, Is.False);
            layer.OnHighlightUpdated(NewSource(enabled: false), NewSource(enabled: true), -5f);
            Assert.That(layer.gameObject.activeSelf, Is.True);
        }

        // 0x1B5F690: the current source is null-checked only after get_gameObject; nothing
        // else happens before the fault.
        [Test]
        public void ActivateUpdateWithoutACurrentSourceThrowsAndLeavesTheObject()
        {
            OnUISelectionActivate layer = Make<OnUISelectionActivate>("OriginalActivate-null", true);
            layer.gameObject.SetActive(true);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(NewSource(enabled: false), null, 1f));
            Assert.That(layer.gameObject.activeSelf, Is.True);
        }

        // 0x06000488 @0x1B5F6C0: SetActive(false) without reading the source. OnActivate is
        // not overridden, so the base RET-only body leaves the object as it was.
        [Test]
        public void ActivateDeactivatesWithoutReadingTheSourceAndInheritsANoOpActivate()
        {
            OnUISelectionActivate layer = Make<OnUISelectionActivate>("OriginalActivate-deactivate", true);
            layer.OnActivate(NewSource(enabled: true));
            Assert.That(layer.gameObject.activeSelf, Is.False);

            layer.gameObject.SetActive(true);
            layer.OnActivate(NewSource(enabled: false));
            Assert.That(layer.gameObject.activeSelf, Is.True);
            layer.OnHighlightBegin(NewSource(enabled: false), NewSource(enabled: false));
            layer.OnHighlightEnd(NewSource(enabled: false), NewSource(enabled: false));
            Assert.That(layer.gameObject.activeSelf, Is.True);

            layer.OnDeactivate(null);
            Assert.That(layer.gameObject.activeSelf, Is.False);
        }

        // ---------------------------------------------------------------- OnUISelectionResize

        // Reset 0x0600048a @0x1B5F700: get_transform, then an exact-class RectTransform test
        // (`as`), overwriting whatever was stored.
        [Test]
        public void ResizeResetTakesTheOwnRectTransformOrNull()
        {
            OnUISelectionResize rect = Make<OnUISelectionResize>("OriginalResize-reset-rect", true);
            var otherOwner = new GameObject("OriginalResize-reset-other", typeof(RectTransform));
            m_owned.Add(otherOwner);
            var other = (RectTransform)otherOwner.transform;
            Set(rect, "m_rectTransform", other);
            Invoke(rect, "Reset");
            Assert.That(Get(rect, "m_rectTransform"), Is.SameAs(rect.transform));

            OnUISelectionResize plain = Make<OnUISelectionResize>("OriginalResize-reset-plain", false);
            Assert.That(plain.transform is RectTransform, Is.False);
            Set(plain, "m_rectTransform", other);
            Invoke(plain, "Reset");
            Assert.That((object)Get(plain, "m_rectTransform"), Is.Null);
        }

        // 0x0600048b @0x1B5F780: pair (+0x1C Bottom, +0x20 Left) lerped and swapped to
        // (Left, Bottom) -> offsetMin; pair (+0x14 Top, +0x18 Right) lerped and swapped to
        // (Right, Top), stored, then negated by the 0x80000000 mask at 0x29a6cc0 -> offsetMax.
        // prev (T1 R2 B3 L4), cur (T9 R10 B11 L12), t = 0.25: Left 6, Bottom 5, Right 4, Top 3.
        [Test]
        public void ResizeUpdateMapsLeftBottomToOffsetMinAndNegatedRightTopToOffsetMax()
        {
            OnUISelectionResize layer = MakeResize("OriginalResize-update");
            layer.OnHighlightUpdated(Insets(1f, 2f, 3f, 4f), Insets(9f, 10f, 11f, 12f), 0.25f);
            RectTransform rect = (RectTransform)layer.transform;
            AssertVector(rect.offsetMin, new Vector2(6f, 5f));
            AssertVector(rect.offsetMax, new Vector2(-4f, -3f));
            Assert.That(Get(layer, "m_reusableVector2"), Is.EqualTo(new Vector2(4f, 3f)), "The field keeps the un-negated Right/Top pair.");
        }

        // The lerp clamps t to [0, 1]: t = 4 lands on the current insets, t = -3 on the
        // previous ones. Fails for an unclamped lerp.
        [Test]
        public void ResizeUpdateClampsTheLerpValue()
        {
            OnUISelectionResize layer = MakeResize("OriginalResize-clamp");
            RectTransform rect = (RectTransform)layer.transform;
            layer.OnHighlightUpdated(Insets(1f, 2f, 3f, 4f), Insets(9f, 10f, 11f, 12f), 4f);
            AssertVector(rect.offsetMin, new Vector2(12f, 11f));
            AssertVector(rect.offsetMax, new Vector2(-10f, -9f));
            layer.OnHighlightUpdated(Insets(1f, 2f, 3f, 4f), Insets(9f, 10f, 11f, 12f), -3f);
            AssertVector(rect.offsetMin, new Vector2(4f, 3f));
            AssertVector(rect.offsetMax, new Vector2(-2f, -1f));
        }

        // 0x1B5F78D/0x1B5F799: both sources are null-checked before anything is stored.
        [Test]
        public void ResizeUpdateWithoutASourceThrowsBeforeStoringAnything()
        {
            OnUISelectionResize layer = MakeResize("OriginalResize-null-source");
            RectTransform rect = (RectTransform)layer.transform;
            rect.offsetMin = new Vector2(-7f, -8f);
            rect.offsetMax = new Vector2(7f, 8f);
            Set(layer, "m_reusableVector2", new Vector2(100f, 200f));
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(null, Insets(9f, 10f, 11f, 12f), 0.5f));
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(Insets(1f, 2f, 3f, 4f), null, 0.5f));
            Assert.That(Get(layer, "m_reusableVector2"), Is.EqualTo(new Vector2(100f, 200f)));
            AssertVector(rect.offsetMin, new Vector2(-7f, -8f));
            AssertVector(rect.offsetMax, new Vector2(7f, 8f));
        }

        // 0x1B5F7DF stores the Left/Bottom pair before 0x1B5F7E7 null-checks m_rectTransform.
        [Test]
        public void ResizeUpdateWithoutARectStoresTheFirstPairThenThrows()
        {
            OnUISelectionResize layer = MakeResize("OriginalResize-null-rect");
            Set(layer, "m_rectTransform", null);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(Insets(1f, 2f, 3f, 4f), Insets(9f, 10f, 11f, 12f), 0.25f));
            Assert.That(Get(layer, "m_reusableVector2"), Is.EqualTo(new Vector2(6f, 5f)));
        }

        // ---------------------------------------------------------------- OnUISelectionSkew

        // Reset 0x0600048d @0x1B5F850: GetComponent<Skew>() into +0x20.
        [Test]
        public void SkewResetTakesTheSiblingSkew()
        {
            OnUISelectionSkew layer = Make<OnUISelectionSkew>("OriginalSelectionSkew-reset", true);
            Hardlight.Skew skew = layer.GetComponent<Hardlight.Skew>();
            Assert.That(skew, Is.Not.Null, "RequireComponent(typeof(Skew)) adds the sibling.");
            Set(layer, "m_skew", null);
            Invoke(layer, "Reset");
            Assert.That(Get(layer, "m_skew"), Is.SameAs(skew));
        }

        // 0x0600048e @0x1B5F8A0: pair (+0x48 SkewX, +0x4C SkewY) lerped into +0x28
        // m_skewAmount, then a tail call ChangeSkewAmount(x, y, instant: esi = 1).
        // prev (1, -2), cur (5, 6), t = 0.25 -> (2, 0). On the inactive owner a non-instant
        // change would log a refused coroutine, so the silent log pins instant = true.
        [Test]
        public void SkewUpdateLerpsBothAxesAndAppliesThemInstantly()
        {
            OnUISelectionSkew layer = MakeSkew("OriginalSelectionSkew-update");
            Hardlight.Skew skew = layer.GetComponent<Hardlight.Skew>();
            layer.OnHighlightUpdated(SkewSource(1f, -2f), SkewSource(5f, 6f), 0.25f);
            Assert.That(Get(layer, "m_skewAmount"), Is.EqualTo(new Vector2(2f, 0f)));
            Assert.That(Get(skew, "m_skewX"), Is.EqualTo(2f));
            Assert.That(Get(skew, "m_skewY"), Is.EqualTo(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SkewUpdateClampsTheLerpValue()
        {
            OnUISelectionSkew layer = MakeSkew("OriginalSelectionSkew-clamp");
            Hardlight.Skew skew = layer.GetComponent<Hardlight.Skew>();
            layer.OnHighlightUpdated(SkewSource(1f, -2f), SkewSource(5f, 6f), 2f);
            Assert.That(Get(skew, "m_skewX"), Is.EqualTo(5f));
            Assert.That(Get(skew, "m_skewY"), Is.EqualTo(6f));
            layer.OnHighlightUpdated(SkewSource(1f, -2f), SkewSource(5f, 6f), -1f);
            Assert.That(Get(skew, "m_skewX"), Is.EqualTo(1f));
            Assert.That(Get(skew, "m_skewY"), Is.EqualTo(-2f));
            LogAssert.NoUnexpectedReceived();
        }

        // 0x1B5F8A4/0x1B5F8A9 null-check both sources first; 0x1B5F8DC stores m_skewAmount
        // before 0x1B5F8E4 null-checks m_skew.
        [Test]
        public void SkewUpdateFaultsOnSourcesBeforeStoringAndOnTheSkewAfterStoring()
        {
            OnUISelectionSkew layer = MakeSkew("OriginalSelectionSkew-null");
            Hardlight.Skew skew = layer.GetComponent<Hardlight.Skew>();
            Set(layer, "m_skewAmount", new Vector2(30f, 40f));
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(null, SkewSource(5f, 6f), 0.25f));
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(SkewSource(1f, -2f), null, 0.25f));
            Assert.That(Get(layer, "m_skewAmount"), Is.EqualTo(new Vector2(30f, 40f)));
            Assert.That(Get(skew, "m_skewX"), Is.EqualTo(0f));

            Set(layer, "m_skew", null);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(SkewSource(1f, -2f), SkewSource(5f, 6f), 0.25f));
            Assert.That(Get(layer, "m_skewAmount"), Is.EqualTo(new Vector2(2f, 0f)));
        }

        // ---------------------------------------------------------------- OnUISelectionSwapColour

        // Reset 0x06000490 @0x1B5F910: GetComponent<Image>() into +0x20.
        [Test]
        public void SwapColourResetTakesTheSiblingImage()
        {
            OnUISelectionSwapColour layer = Make<OnUISelectionSwapColour>("OriginalSwapColour-reset", true);
            Image image = layer.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, "RequireComponent(typeof(Image)) adds the sibling.");
            Set(layer, "m_image", null);
            Invoke(layer, "Reset");
            Assert.That(Get(layer, "m_image"), Is.SameAs(image));
        }

        // 0x06000491 @0x1B5F960: color (vslot 23) = LABColor.Lerp(prev.Color, cur.Color, t)
        // (0x06000015 @0x1B5F9AD); +0x28 m_currentColourBlend = color (vslot 22); blend.a
        // (+0x34) = Mathf.Lerp(prev.Color.a +0x44, cur.Color.a +0x44, t); color = blend.
        // Alphas 0.25 -> 0.75 at t = 0.5 give exactly 0.5. The RGB expectation calls
        // LABColor.Lerp itself, so it checks delegation only; LABColor's own tests (graphics
        // cluster) are the oracle for the RGB values.
        [Test]
        public void SwapColourBlendsRgbThroughLabAndLerpsAlphaSeparately()
        {
            OnUISelectionSwapColour layer = MakeSwapColour("OriginalSwapColour-update");
            Image image = layer.GetComponent<Image>();
            Color from = new Color(1f, 0f, 0f, 0.25f);
            Color to = new Color(0f, 0f, 1f, 0.75f);
            layer.OnHighlightUpdated(ColourSource(from), ColourSource(to), 0.5f);

            Color lab = LABColor.Lerp(from, to, 0.5f);
            Color expected = new Color(lab.r, lab.g, lab.b, 0.5f);
            Assert.That(image.color, Is.EqualTo(expected));
            Assert.That(Get(layer, "m_currentColourBlend"), Is.EqualTo(expected));
            Assert.That(image.color, Is.Not.EqualTo(Color.Lerp(from, to, 0.5f)), "RGB is not an RGB-space lerp.");
        }

        // The alpha lerp clamps like the LAB one (LABColor.Lerp 0x1B397F7 uses the same
        // minss 1.0 / cmpltss 0 clamp): t = 3 gives the current alpha, t = -2 the previous
        // alpha. The RGB checks are delegation-only, as above.
        [Test]
        public void SwapColourClampsTheAlphaLerp()
        {
            OnUISelectionSwapColour layer = MakeSwapColour("OriginalSwapColour-clamp");
            Image image = layer.GetComponent<Image>();
            Color from = new Color(0.2f, 0.4f, 0.6f, 0.25f);
            Color to = new Color(0.9f, 0.1f, 0.3f, 0.75f);
            layer.OnHighlightUpdated(ColourSource(from), ColourSource(to), 3f);
            Assert.That(image.color.a, Is.EqualTo(0.75f));
            Assert.That(image.color.r, Is.EqualTo(LABColor.Lerp(from, to, 3f).r));
            layer.OnHighlightUpdated(ColourSource(from), ColourSource(to), -2f);
            Assert.That(image.color.a, Is.EqualTo(0.25f));
            Assert.That(image.color.b, Is.EqualTo(LABColor.Lerp(from, to, -2f).b));
        }

        // 0x1B5F96F/0x1B5F97B null-check both sources before anything; with a null image the
        // LAB blend is computed but 0x1B5F9B2 faults before any store.
        [Test]
        public void SwapColourFaultsBeforeStoringAnything()
        {
            OnUISelectionSwapColour layer = MakeSwapColour("OriginalSwapColour-null");
            Image image = layer.GetComponent<Image>();
            image.color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
            Color blend = new Color(0.5f, 0.6f, 0.7f, 0.8f);
            Set(layer, "m_currentColourBlend", blend);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(null, ColourSource(Color.white), 0.5f));
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(ColourSource(Color.white), null, 0.5f));
            Assert.That(image.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));

            Set(layer, "m_image", null);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(ColourSource(Color.red), ColourSource(Color.blue), 0.5f));
            Assert.That(Get(layer, "m_currentColourBlend"), Is.EqualTo(blend));
            Assert.That(image.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
        }

        // ---------------------------------------------------------------- OnUISelectionSwapSprite

        // Reset 0x06000493 @0x1B5FA70: GetComponent<Image>() into +0x20.
        [Test]
        public void SwapSpriteResetTakesTheSiblingImage()
        {
            OnUISelectionSwapSprite layer = Make<OnUISelectionSwapSprite>("OriginalSwapSprite-reset", true);
            Image image = layer.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, "RequireComponent(typeof(Image)) adds the sibling.");
            Set(layer, "m_image", null);
            Invoke(layer, "Reset");
            Assert.That(Get(layer, "m_image"), Is.SameAs(image));
        }

        // 0x06000496 @0x1B5FB80: `cmp rsi, rdx; sete [+0x28]`, reference equality of the two
        // sources, written every time. Equal-content distinct sources do not count.
        [Test]
        public void SwapSpriteBeginMarksSwappedOnlyForTheSameSourceInstance()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-begin");
            Source source = NewSource(enabled: true);
            layer.OnHighlightBegin(source, source);
            Assert.That(Get(layer, "m_spritesSwapped"), Is.True);
            layer.OnHighlightBegin(NewSource(enabled: true), NewSource(enabled: true));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.False);
            layer.OnHighlightBegin(null, null);
            Assert.That(Get(layer, "m_spritesSwapped"), Is.True);
            layer.OnHighlightBegin(source, null);
            Assert.That(Get(layer, "m_spritesSwapped"), Is.False);
        }

        // 0x06000495 @0x1B5FB10: `ucomiss 0.5f (0x29a6c08), lerp; ja return` comes first, so
        // below half nothing is read, not even a null current source.
        [Test]
        public void SwapSpriteUpdateBelowHalfChangesNothing()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-below");
            Image image = layer.GetComponent<Image>();
            Sprite original = NewSprite("OriginalSwapSprite-below-original");
            image.sprite = original;
            layer.OnHighlightUpdated(null, SpriteSource(NewSprite("OriginalSwapSprite-below-next"), NewMaterial("OriginalSwapSprite-below-material")), 0.49f);
            Assert.That(image.sprite, Is.SameAs(original));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.False);
            Assert.DoesNotThrow(() => layer.OnHighlightUpdated(null, null, -1f));
        }

        // At exactly 0.5 the guard falls through: sprite (+0x28) then material (+0x30, vslot 33)
        // from the current source, then +0x28 m_spritesSwapped = 1. Later updates are skipped.
        [Test]
        public void SwapSpriteUpdateFromHalfSwapsOnceToTheCurrentSource()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-half");
            Image image = layer.GetComponent<Image>();
            Sprite sprite = NewSprite("OriginalSwapSprite-half-sprite");
            Material material = NewMaterial("OriginalSwapSprite-half-material");
            layer.OnHighlightUpdated(SpriteSource(NewSprite("OriginalSwapSprite-half-previous"), NewMaterial("OriginalSwapSprite-half-previous-material")), SpriteSource(sprite, material), 0.5f);
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.material, Is.SameAs(material));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.True);

            layer.OnHighlightUpdated(null, SpriteSource(NewSprite("OriginalSwapSprite-half-later"), NewMaterial("OriginalSwapSprite-half-later-material")), 1f);
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.material, Is.SameAs(material));
            Assert.DoesNotThrow(() => layer.OnHighlightUpdated(null, null, 1f), "A swapped layer reads no source.");
        }

        // `ja` is not taken on an unordered compare, so a NaN lerp value swaps. Fails for a
        // `lerpValue >= 0.5f` guard.
        [Test]
        public void SwapSpriteUpdateWithANaNLerpSwaps()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-nan");
            Image image = layer.GetComponent<Image>();
            Sprite sprite = NewSprite("OriginalSwapSprite-nan-sprite");
            layer.OnHighlightUpdated(null, SpriteSource(sprite, NewMaterial("OriginalSwapSprite-nan-material")), float.NaN);
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.True);
        }

        // 0x1B5FB3E: a null image faults after the guard and before the flag store at 0x1B5FB6B.
        [Test]
        public void SwapSpriteUpdateWithoutAnImageThrowsAndStaysUnswapped()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-null-image");
            Set(layer, "m_image", null);
            Assert.Throws<NullReferenceException>(() => layer.OnHighlightUpdated(null, SpriteSource(NewSprite("OriginalSwapSprite-null-image-sprite"), null), 0.75f));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.False);
        }

        // 0x06000494 @0x1B5FAC0: sprite and material from the source, with no guard and no
        // write to m_spritesSwapped either way.
        [Test]
        public void SwapSpriteActivateAppliesTheSourceAndKeepsTheSwappedFlag()
        {
            OnUISelectionSwapSprite layer = MakeSwapSprite("OriginalSwapSprite-activate");
            Image image = layer.GetComponent<Image>();
            Sprite sprite = NewSprite("OriginalSwapSprite-activate-sprite");
            Material material = NewMaterial("OriginalSwapSprite-activate-material");
            layer.OnActivate(SpriteSource(sprite, material));
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.material, Is.SameAs(material));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.False);

            Set(layer, "m_spritesSwapped", true);
            Sprite next = NewSprite("OriginalSwapSprite-activate-next");
            layer.OnActivate(SpriteSource(next, material));
            Assert.That(image.sprite, Is.SameAs(next));
            Assert.That(Get(layer, "m_spritesSwapped"), Is.True);
            Assert.Throws<NullReferenceException>(() => layer.OnActivate(null));
        }

        // ---------------------------------------------------------------- helpers

        private T Make<T>(string name, bool rectTransform) where T : Component
        {
            GameObject owner = rectTransform ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            m_owned.Add(owner);
            owner.SetActive(false);
            return owner.AddComponent<T>();
        }

        private OnUISelectionResize MakeResize(string name)
        {
            OnUISelectionResize layer = Make<OnUISelectionResize>(name, true);
            Set(layer, "m_rectTransform", layer.transform);
            return layer;
        }

        private OnUISelectionSkew MakeSkew(string name)
        {
            OnUISelectionSkew layer = Make<OnUISelectionSkew>(name, true);
            Set(layer, "m_skew", layer.GetComponent<Hardlight.Skew>());
            return layer;
        }

        private OnUISelectionSwapColour MakeSwapColour(string name)
        {
            OnUISelectionSwapColour layer = Make<OnUISelectionSwapColour>(name, true);
            Set(layer, "m_image", layer.GetComponent<Image>());
            return layer;
        }

        private OnUISelectionSwapSprite MakeSwapSprite(string name)
        {
            OnUISelectionSwapSprite layer = Make<OnUISelectionSwapSprite>(name, true);
            Set(layer, "m_image", layer.GetComponent<Image>());
            Set(layer, "m_spritesSwapped", false);
            return layer;
        }

        private Sprite NewSprite(string name)
        {
            var texture = new Texture2D(2, 2) { name = name };
            m_owned.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero);
            sprite.name = name;
            m_owned.Add(sprite);
            return sprite;
        }

        private Material NewMaterial(string name)
        {
            var material = new Material(Graphic.defaultGraphicMaterial) { name = name };
            m_owned.Add(material);
            return material;
        }

        private static Source NewSource(bool enabled) => new Source { Enabled = enabled };
        private static Source Insets(float top, float right, float bottom, float left) => new Source { Top = top, Right = right, Bottom = bottom, Left = left };
        private static Source SkewSource(float x, float y) => new Source { SkewX = x, SkewY = y };
        private static Source ColourSource(Color color) => new Source { Color = color };
        private static Source SpriteSource(Sprite sprite, Material material) => new Source { Sprite = sprite, Material = material };

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            // RectTransform offsets are derived from anchoredPosition and sizeDelta on read.
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Own).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Own).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Own).Invoke(target, null);

        private static void CheckType(Type type, Type required, string[] fieldNames, Type[] fieldTypes)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(ModifiedOnUISelection)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            if (required == null)
            {
                Assert.That(attributes, Is.Empty);
            }
            else
            {
                Assert.That(attributes.Count, Is.EqualTo(1));
                Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(RequireComponent)));
                Assert.That(attributes[0].ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { required }));
                Assert.That(attributes[0].NamedArguments, Is.Empty);
            }

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(fieldNames));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(fieldTypes));
            for (int i = 0; i < fields.Length; ++i)
            {
                Assert.That((int)fields[i].Attributes, Is.EqualTo(1), fields[i].Name);
                Type[] expected = i == 0 ? new[] { typeof(SerializeField) } : new Type[0];
                Assert.That(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), fields[i].Name);
            }

            ConstructorInfo constructor = type.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
            Assert.That(constructor.GetParameters(), Is.Empty);
        }

        private static void CheckMethods(Type type, string[] names, int[] flags, string[] parameters)
        {
            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(names));
            for (int i = 0; i < methods.Length; ++i)
            {
                MethodInfo method = methods[i];
                Assert.That((int)method.Attributes, Is.EqualTo(flags[i]), method.Name);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), method.Name);
                Assert.That(string.Join(",", method.GetParameters().Select(p => p.Name)), Is.EqualTo(parameters[i]), method.Name);
                Assert.That(method.GetCustomAttributesData(), Is.Empty, method.Name);
                if (method.IsVirtual)
                {
                    Assert.That(method.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(ModifiedOnUISelection)), method.Name);
                    Type[] expected = typeof(ModifiedOnUISelection).GetMethod(method.Name, Declared).GetParameters().Select(p => p.ParameterType).ToArray();
                    Assert.That(method.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(expected), method.Name);
                }
                else
                {
                    Assert.That(method.GetParameters(), Is.Empty, method.Name);
                }
            }
        }
    }
}
