using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using OriginalRaycastMask = Hardlight.RaycastMask;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime Hardlight.RaycastMask (TypeDef
    // 0x020000d9, methods 0x060005d2..d4) from the x86-64 bodies. Each case owns an
    // inactive GameObject at the world origin (so a null-camera screen point is the
    // local point), a point-filtered Repeat texture and a FullRect sprite, and
    // destroys all three. Hit() finds which texel a point samples by making each
    // texel the only opaque one in turn. Every sprite here is unpacked: the packed
    // rejection (0x1B6BA81..0x1B6BACF, a half-open textureRect.Contains of the
    // truncated texel) is translated but untested, because EditMode cannot pack a
    // sprite into an atlas without writing assets.
    [TestFixture]
    public sealed class OriginalRaycastMaskTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 12041: Il2CppSetOption(ArrayBoundsChecks), (NullChecks),
        // RequireComponent(Image), RequireComponent(Mask); +0x20 m_image [SerializeField],
        // +0x28 m_enabled; ICanvasRaycastFilter at slot 4.
        [Test]
        public void DeclarationKeepsTheOriginalAttributesFieldsAndFilter()
        {
            Type type = typeof(OriginalRaycastMask);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(ICanvasRaycastFilter) }));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute), typeof(RequireComponent), typeof(RequireComponent) }));
            Assert.That(Convert.ToInt32(attributes[0].ConstructorArguments[0].Value), Is.EqualTo(2));
            Assert.That(Convert.ToInt32(attributes[1].ConstructorArguments[0].Value), Is.EqualTo(1));
            Assert.That(attributes[0].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[1].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[2].ConstructorArguments.Single().Value, Is.EqualTo(typeof(Image)));
            Assert.That(attributes[3].ConstructorArguments.Single().Value, Is.EqualTo(typeof(Mask)));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_image", "m_enabled" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(Image), typeof(bool) }));
            Assert.That(fields.All(f => f.Attributes == FieldAttributes.Private), Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(fields[1].GetCustomAttributesData(), Is.Empty);

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "Start", "IsRaycastLocationValid" }));
            Assert.That((int)methods[0].Attributes, Is.EqualTo(129));
            Assert.That(methods[0].GetParameters(), Is.Empty);
            Assert.That((int)methods[1].Attributes, Is.EqualTo(486));
            Assert.That(methods[1].ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(methods[1].GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(Vector2), typeof(Camera) }));
            Assert.That(methods[1].GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "screenPoint", "eventCamera" }));
            Assert.That((int)type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(6278));
        }

        // Start 0x060005d2 only sets +0x28; IsRaycastLocationValid starts with r14b = 1 and
        // returns it while m_enabled is clear (0x1B6B591..0x1B6B599), so a fully transparent
        // sprite still hits until Start has run.
        [Test]
        public void EveryPointHitsUntilStartHasRun()
        {
            using (var owned = new Owned(new Vector2(8f, 8f), Vector2.zero, Vector4.zero))
            {
                owned.Paint(-1);
                Assert.That(Enabled(owned.Mask), Is.False);
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.True);
                owned.Start();
                Assert.That(Enabled(owned.Mask), Is.True);
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.False);
                owned.Paint(Texel(1, 1));
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.True);
            }
        }

        // 0x1B6B5FB..0x1B6B671: the local point is moved to a bottom-left origin by
        // pivot * rect size; Simple then maps (local - 0) / rect.size * textureRect.size
        // + textureRect.position (0x1B6B807..0x1B6B81B) and truncates (cvttss2si, no floor).
        // An 8 x 16 rect pivoted at its centre: local (1, -2) -> (5, 6) -> (2.5, 1.5) ->
        // texel (2, 1). Swapped axes would give (1, 3); a missing pivot shift (0, 0).
        [Test]
        public void SimpleImagesMapThePivotShiftedPointOverTheRectSize()
        {
            using (var owned = new Owned(new Vector2(8f, 16f), new Vector2(0.5f, 0.5f), Vector4.zero))
            {
                owned.Start();
                Assert.That(owned.Hit(new Vector2(1f, -2f)), Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(owned.Hit(new Vector2(3f, -2f)), Is.EqualTo(new Vector2Int(3, 1)));
                Assert.That(owned.Hit(new Vector2(-4f, 7.9f)), Is.EqualTo(new Vector2Int(0, 3)));
            }
        }

        // Simple truncates toward zero: local -1 on an 8-wide rect gives -0.5 -> texel 0.
        // A FloorToInt mapping would sample -1, which the Repeat texture wraps to 3.
        [Test]
        public void SimpleImagesTruncateNegativeTexelCoordinatesTowardZero()
        {
            using (var owned = new Owned(new Vector2(8f, 8f), Vector2.zero, Vector4.zero))
            {
                owned.Start();
                Assert.That(owned.Hit(new Vector2(-1f, 1f)), Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(owned.Hit(new Vector2(1f, -1f)), Is.EqualTo(new Vector2Int(0, 0)));
            }
        }

        // 0x1B6B716..0x1B6B804: with preserveAspect (+0xEC) the sprite is a square of the
        // shorter side centred on the longer: h > w offsets y by (h - w) * 0.5 and uses
        // (w, w); otherwise x by (w - h) * 0.5 with (h, h). Tall 8 x 16 at (5, 10): (2, 3)
        // preserved, (2, 2) stretched. Wide 16 x 8 at (10, 5): (3, 2) preserved, (2, 2)
        // stretched.
        [Test]
        public void PreserveAspectSamplesTheCentredSquare()
        {
            using (var tall = new Owned(new Vector2(8f, 16f), Vector2.zero, Vector4.zero))
            {
                tall.Start();
                Assert.That(tall.Hit(new Vector2(5f, 10f)), Is.EqualTo(new Vector2Int(2, 2)));
                tall.Image.preserveAspect = true;
                Assert.That(tall.Hit(new Vector2(5f, 10f)), Is.EqualTo(new Vector2Int(2, 3)));
            }
            using (var wide = new Owned(new Vector2(16f, 8f), Vector2.zero, Vector4.zero))
            {
                wide.Start();
                Assert.That(wide.Hit(new Vector2(10f, 5f)), Is.EqualTo(new Vector2Int(2, 2)));
                wide.Image.preserveAspect = true;
                Assert.That(wide.Hit(new Vector2(10f, 5f)), Is.EqualTo(new Vector2Int(3, 2)));
            }
        }

        // Sliced (type 1, 0x1B6B69E..0x1B6BA65): per axis, inside the low border
        // floor(sprite.pos + local); beyond rect - high border floor(sprite.pos +
        // sprite.size - (rect - local)); between, the centre band is stretched:
        // floor(sprite.pos + border + (local - border) / (rect - borders) * (sprite.size -
        // borders)). A 40 x 40 rect over a 4 x 4 sprite with one-texel borders:
        // x 30 -> floor(1 + 29/38 * 2) = 2 (Simple would give 3); x -0.5 floors to -1 and
        // wraps to 3 (truncation would give 0).
        [Test]
        public void SlicedImagesFloorEachBorderBandSeparately()
        {
            using (var owned = new Owned(new Vector2(40f, 40f), Vector2.zero, new Vector4(1f, 1f, 1f, 1f)))
            {
                owned.Image.type = Image.Type.Sliced;
                owned.Start();
                Assert.That(owned.Hit(new Vector2(0.75f, 20f)), Is.EqualTo(new Vector2Int(0, 2)));
                Assert.That(owned.Hit(new Vector2(-0.5f, 20f)), Is.EqualTo(new Vector2Int(3, 2)));
                Assert.That(owned.Hit(new Vector2(30f, 0.5f)), Is.EqualTo(new Vector2Int(2, 0)));
                Assert.That(owned.Hit(new Vector2(10f, 39.5f)), Is.EqualTo(new Vector2Int(1, 3)));
                Assert.That(owned.Hit(new Vector2(39.25f, 30f)), Is.EqualTo(new Vector2Int(3, 2)));
            }
        }

        // Every mapping adds textureRect.position: Simple at 0x1B6B81B (addps), Sliced in
        // the low (0x1B6B6F6, 0x1B6B8FC), high (0x1B6B76E, 0x1B6B983) and centre
        // (0x1B6B82A, 0x1B6B9DC) bands. A 4 x 4 sprite at (4, 4) of an 8 x 8 texture:
        // Simple 8 x 8 local (2, 6) -> (4 + 2/8 * 4, 4 + 6/8 * 4) = texel (5, 7).
        // Sliced 40 x 40 with one-texel borders: local (0.5, 30) -> x floor(4 + 0.5) = 4,
        // y floor(4 + 1 + 29/38 * 2) = 6; local (39.5, 30) -> x floor(4 + 4 - 0.5) = 7.
        // Dropping the offset would give (1, 3), (0, 2) and (3, 2).
        [Test]
        public void EveryMappingStartsAtTheSpriteTextureRectOrigin()
        {
            var spriteRect = new Rect(4f, 4f, 4f, 4f);
            using (var simple = new Owned(new Vector2(8f, 8f), Vector2.zero, Vector4.zero, 8, spriteRect))
            {
                simple.Start();
                Assert.That(simple.Hit(new Vector2(2f, 6f)), Is.EqualTo(new Vector2Int(5, 7)));
            }
            using (var sliced = new Owned(new Vector2(40f, 40f), Vector2.zero, new Vector4(1f, 1f, 1f, 1f), 8, spriteRect))
            {
                sliced.Image.type = Image.Type.Sliced;
                sliced.Start();
                Assert.That(sliced.Hit(new Vector2(0.5f, 30f)), Is.EqualTo(new Vector2Int(4, 6)));
                Assert.That(sliced.Hit(new Vector2(39.5f, 30f)), Is.EqualTo(new Vector2Int(7, 6)));
            }
        }

        // Sliced x uses border.x/.z (0x1B6B6B6, 0x1B6B756, centre 0x1B6B835..0x1B6B84C) and
        // y uses border.y/.w (0x1B6B8F7, 0x1B6B96E, centre 0x1B6B9E0..0x1B6B9F4). Border
        // (1, 2, 2, 1) on a 40 x 40 rect, local (20, 20): x floor(1 + 19/37 * 1) = 1,
        // y floor(2 + 18/37 * 1) = 2. Swapping x/z gives (2, 2); swapping y/w, or the x
        // borders on y, gives (1, 1).
        [Test]
        public void SlicedImagesTakeEachAxisFromItsOwnBorderPair()
        {
            using (var owned = new Owned(new Vector2(40f, 40f), Vector2.zero, new Vector4(1f, 2f, 2f, 1f)))
            {
                owned.Image.type = Image.Type.Sliced;
                owned.Start();
                Assert.That(owned.Hit(new Vector2(20f, 20f)), Is.EqualTo(new Vector2Int(1, 2)));
            }
        }

        // 0x1B6B695..0x1B6B698: any type other than Simple (0) or Sliced (1) returns the
        // initial true without sampling, even over a fully transparent sprite.
        [Test]
        public void TiledAndFilledImagesAlwaysHit()
        {
            using (var owned = new Owned(new Vector2(8f, 8f), Vector2.zero, Vector4.zero))
            {
                owned.Start();
                owned.Paint(-1);
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.False);
                owned.Image.type = Image.Type.Tiled;
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.True);
                owned.Image.type = Image.Type.Filled;
                Assert.That(owned.Mask.IsRaycastLocationValid(new Vector2(2f, 2f), null), Is.True);
            }
        }

        // Texel index in the default 4 x 4 fixture texture.
        static int Texel(int x, int y) { return y * 4 + x; }

        static bool Enabled(OriginalRaycastMask mask)
        {
            return (bool)typeof(OriginalRaycastMask).GetField("m_enabled", Declared).GetValue(mask);
        }

        // An inactive RectTransform at the origin with the RaycastMask (which brings its
        // required Image and Mask), m_image wired to that Image, and an unpacked FullRect
        // sprite with the given border, by default over the whole 4 x 4 texture.
        sealed class Owned : IDisposable
        {
            readonly GameObject owner;
            readonly Texture2D texture;
            readonly Sprite sprite;
            readonly int textureSize;
            public readonly Image Image;
            public readonly OriginalRaycastMask Mask;

            public Owned(Vector2 size, Vector2 pivot, Vector4 border)
                : this(size, pivot, border, 4, new Rect(0f, 0f, 4f, 4f))
            {
            }

            public Owned(Vector2 size, Vector2 pivot, Vector4 border, int textureSize, Rect spriteRect)
            {
                this.textureSize = textureSize;
                owner = new GameObject("OriginalRaycastMask fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    owner.SetActive(false);
                    var rect = (RectTransform)owner.transform;
                    rect.pivot = pivot;
                    rect.sizeDelta = size;
                    Mask = owner.AddComponent<OriginalRaycastMask>();
                    Image = owner.GetComponent<Image>();
                    Assert.That(Image, Is.Not.Null);
                    Assert.That(owner.GetComponent<Mask>(), Is.Not.Null);
                    typeof(OriginalRaycastMask).GetField("m_image", Declared).SetValue(Mask, Image);
                    texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Repeat,
                    };
                    sprite = Sprite.Create(texture, spriteRect, Vector2.zero, 100f, 0, SpriteMeshType.FullRect, border);
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    Assert.That(sprite.packed, Is.False);
                    Assert.That(sprite.textureRect, Is.EqualTo(spriteRect));
                    Image.sprite = sprite;
                    Image.type = Image.Type.Simple;
                }
                catch { Dispose(); throw; }
            }

            public void Start()
            {
                typeof(OriginalRaycastMask).GetMethod("Start", Declared).Invoke(Mask, null);
            }

            // Makes texel `opaque` the only one with alpha above zero (-1 for none).
            public void Paint(int opaque)
            {
                var pixels = new Color[textureSize * textureSize];
                for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(1f, 1f, 1f, i == opaque ? 1f : 0f);
                texture.SetPixels(pixels);
                texture.Apply();
            }

            // The single texel whose opacity decides the hit at this point.
            public Vector2Int Hit(Vector2 screenPoint)
            {
                var hits = new List<Vector2Int>();
                for (int i = 0; i < textureSize * textureSize; ++i)
                {
                    Paint(i);
                    if (Mask.IsRaycastLocationValid(screenPoint, null)) hits.Add(new Vector2Int(i % textureSize, i / textureSize));
                }
                Assert.That(hits.Count, Is.EqualTo(1), "texels hit at " + screenPoint);
                return hits[0];
            }

            public void Dispose()
            {
                if (owner != null) Object.DestroyImmediate(owner);
                if (sprite != null) Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }
    }
}
