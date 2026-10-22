using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SoftMasking;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // com.olegknyazev.softmask (Soft Mask 1.7.0 as shipped). Expected values are read from the x86-64
    // bodies of the shipped assembly (addresses in each comment) and from the constants those bodies load,
    // not from the restored source. Components are created on inactive objects so no Awake/OnEnable runs,
    // and every Unity object a case creates is destroyed afterwards.
    public sealed class OriginalSoftMaskTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private Material OwnedMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "built-in Sprites/Default shader");
            var material = new Material(shader);
            owned.Add(material);
            return material;
        }

        private SoftMask OwnedSoftMask()
        {
            var gameObject = new GameObject("Lucid SoftMask", typeof(RectTransform));
            gameObject.SetActive(false);
            owned.Add(gameObject);
            return gameObject.AddComponent<SoftMask>();
        }

        private static Type Nested(string name) => typeof(SoftMask).GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public);

        private sealed class StubReplacer : IMaterialReplacer
        {
            private readonly List<string> log;
            private readonly string name;
            private readonly Func<Material, Material> replace;

            public StubReplacer(List<string> log, string name, int order, Func<Material, Material> replace)
            {
                this.log = log;
                this.name = name;
                this.order = order;
                this.replace = replace;
            }

            public int order { get; }

            public Material Replace(Material material)
            {
                log.Add(name);
                return replace(material);
            }
        }

        // MaskChannel..cctor 0x494f40 stores alpha = (0,0,0,1) from a 16-byte constant, red = (1,0,0,0),
        // green = (0,1,0,0), blue = (0,0,1,0) and gray = (1/3, 1/3, 1/3, 0) from float constants 0x3eaaaaab.
        // A gray of (1,1,1,0) un-divided, or a gray with a third alpha, fails.
        [Test]
        public void MaskChannelPresetsAreTheNativeConstants()
        {
            Assert.That(MaskChannel.alpha, Is.EqualTo(new Color(0f, 0f, 0f, 1f)));
            Assert.That(MaskChannel.red, Is.EqualTo(new Color(1f, 0f, 0f, 0f)));
            Assert.That(MaskChannel.green, Is.EqualTo(new Color(0f, 1f, 0f, 0f)));
            Assert.That(MaskChannel.blue, Is.EqualTo(new Color(0f, 0f, 1f, 0f)));
            float third = BitConverter.ToSingle(BitConverter.GetBytes(0x3eaaaaab), 0);
            Assert.That(MaskChannel.gray.r, Is.EqualTo(third));
            Assert.That(MaskChannel.gray.g, Is.EqualTo(third));
            Assert.That(MaskChannel.gray.b, Is.EqualTo(third));
            Assert.That(MaskChannel.gray.a, Is.EqualTo(0f));
        }

        // ImageTypeToBorderMode 0x497e60 answers 1 for 1, 2 for 2 and 0 otherwise (Filled falls to Simple);
        // IsImageTypeSupported 0x497e80 is an unsigned `type < 3`, so Filled (3), 4 and -1 are unsupported.
        [Test]
        public void ImageTypesMapToBorderModesAndOnlyThreeAreSupported()
        {
            Assert.That(SoftMask.ImageTypeToBorderMode(Image.Type.Simple), Is.EqualTo(SoftMask.BorderMode.Simple));
            Assert.That(SoftMask.ImageTypeToBorderMode(Image.Type.Sliced), Is.EqualTo(SoftMask.BorderMode.Sliced));
            Assert.That(SoftMask.ImageTypeToBorderMode(Image.Type.Tiled), Is.EqualTo(SoftMask.BorderMode.Tiled));
            Assert.That(SoftMask.ImageTypeToBorderMode(Image.Type.Filled), Is.EqualTo(SoftMask.BorderMode.Simple));
            Assert.That(SoftMask.IsImageTypeSupported(Image.Type.Simple), Is.True);
            Assert.That(SoftMask.IsImageTypeSupported(Image.Type.Sliced), Is.True);
            Assert.That(SoftMask.IsImageTypeSupported(Image.Type.Tiled), Is.True);
            Assert.That(SoftMask.IsImageTypeSupported(Image.Type.Filled), Is.False);
            Assert.That(SoftMask.IsImageTypeSupported((Image.Type)4), Is.False);
            Assert.That(SoftMask.IsImageTypeSupported((Image.Type)(-1)), Is.False);
        }

        // SoftMask.Mathr (private nested). Remap 0x4998e0 is (c - from.xy) / (from.zw - from.xy) * (to.zw - to.xy)
        // + to.xy per component; Inside 0x4961d0 is inclusive on all four edges and false for NaN;
        // ApplyBorder 0x498b90 adds the border to xy and subtracts it from zw. Min 0x4998c0 and Max 0x4998d0 are
        // private in the shipped build (the 1.6.3 package has them public).
        [Test]
        public void MathrRemapsBetweenRectsInsideIsInclusiveAndBordersShrinkTheRect()
        {
            Type mathr = Nested("Mathr");
            Assert.That(mathr, Is.Not.Null);
            MethodInfo remap = mathr.GetMethod("Remap", Static);
            MethodInfo inside = mathr.GetMethod("Inside", Static);
            MethodInfo applyBorder = mathr.GetMethod("ApplyBorder", Static);

            var mapped = (Vector2)remap.Invoke(null, new object[] { new Vector2(5f, 5f), new Vector4(0f, 0f, 10f, 20f), new Vector4(100f, 200f, 300f, 600f) });
            Assert.That(mapped, Is.EqualTo(new Vector2(200f, 300f)));
            var flipped = (Vector2)remap.Invoke(null, new object[] { new Vector2(10f, 0f), new Vector4(0f, 0f, 10f, 20f), new Vector4(1f, 1f, 0f, 0f) });
            Assert.That(flipped, Is.EqualTo(new Vector2(0f, 1f)));

            var rect = new Vector4(0f, 0f, 10f, 20f);
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(10f, 20f), new Vector2(0f, 20f), new Vector2(10f, 0f), new Vector2(5f, 5f) })
                Assert.That((bool)inside.Invoke(null, new object[] { corner, rect }), Is.True, "inclusive " + corner);
            foreach (var outside in new[] { new Vector2(-0.001f, 5f), new Vector2(5f, -0.001f), new Vector2(10.001f, 5f), new Vector2(5f, 20.001f),
                                            new Vector2(float.NaN, 5f), new Vector2(5f, float.NaN) })
                Assert.That((bool)inside.Invoke(null, new object[] { outside, rect }), Is.False, "outside " + outside);

            var bordered = (Vector4)applyBorder.Invoke(null, new object[] { new Vector4(1f, 2f, 11f, 12f), new Vector4(0.5f, 1f, 2f, 3f) });
            Assert.That(bordered, Is.EqualTo(new Vector4(1.5f, 3f, 9f, 9f)));

            Assert.That(mathr.GetMethod("Min", BindingFlags.Static | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(mathr.GetMethod("Max", BindingFlags.Static | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(mathr.GetMethod("Min", BindingFlags.Static | BindingFlags.Public), Is.Null);
            Assert.That(mathr.GetMethod("Max", BindingFlags.Static | BindingFlags.Public), Is.Null);
        }

        // SoftMask..ctor 0x494fc0 stores 1.0 in _spritePixelsPerUnitMultiplier, DefaultUVRect (0,0,1,1) from the
        // cctor 0x498fe0 in _textureUVRect and MaskChannel.alpha in _channelWeights; every other serialized
        // field keeps its zero default (source Graphic, border mode Simple, no sprite, texture or separate
        // mask, no inversion, raycast threshold 0). Masking is off while the object is inactive.
        [Test]
        public void ANewSoftMaskStartsWithTheNativeFieldDefaults()
        {
            SoftMask mask = OwnedSoftMask();
            Assert.That(mask.source, Is.EqualTo(SoftMask.MaskSource.Graphic));
            Assert.That(mask.separateMask, Is.Null);
            Assert.That(mask.sprite, Is.Null);
            Assert.That(mask.spriteBorderMode, Is.EqualTo(SoftMask.BorderMode.Simple));
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(1f));
            Assert.That(mask.texture, Is.Null);
            Assert.That(mask.renderTexture, Is.Null);
            Assert.That(mask.textureUVRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(mask.channelWeights, Is.EqualTo(MaskChannel.alpha));
            Assert.That(mask.raycastThreshold, Is.EqualTo(0f));
            Assert.That(mask.invertMask, Is.False);
            Assert.That(mask.invertOutsides, Is.False);
            Assert.That(mask.isUsingRaycastFiltering, Is.False);
            Assert.That(mask.isMaskingEnabled, Is.False);
        }

        // set_spritePixelsPerUnitMultiplier 0x495570 returns when the stored value equals the unclamped input,
        // otherwise stores max(input, 0.01f) (maxss with 0.01f as the memory operand, so NaN gives 0.01f too).
        [Test]
        public void SpritePixelsPerUnitMultiplierIsClampedAtOneHundredth()
        {
            SoftMask mask = OwnedSoftMask();
            float floor = BitConverter.ToSingle(BitConverter.GetBytes(0x3c23d70a), 0);
            mask.spritePixelsPerUnitMultiplier = 2.5f;
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(2.5f));
            mask.spritePixelsPerUnitMultiplier = 0f;
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(floor));
            mask.spritePixelsPerUnitMultiplier = 2.5f;
            mask.spritePixelsPerUnitMultiplier = -3f;
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(floor));
            mask.spritePixelsPerUnitMultiplier = 2.5f;
            mask.spritePixelsPerUnitMultiplier = float.NaN;
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(floor));
            mask.spritePixelsPerUnitMultiplier = 0.02f;
            Assert.That(mask.spritePixelsPerUnitMultiplier, Is.EqualTo(0.02f));
        }

        // MaterialReplacerChain.Initialize 0x494a40 sorts the replacers (the extra one included) ascending by order
        // and takes order from element 0; Replace 0x494c40 walks that order and returns the first result for
        // which UnityEngine.Object.op_Inequality(result, null) holds, so a destroyed Material counts as null.
        [Test]
        public void ReplacerChainRunsInAscendingOrderAndSkipsNullAndDestroyedResults()
        {
            var log = new List<string>();
            Material fromHigh = OwnedMaterial(), fromMiddle = OwnedMaterial(), destroyed = OwnedMaterial();
            Object.DestroyImmediate(destroyed);
            var high = new StubReplacer(log, "high", 5, m => fromHigh);
            var skipped = new StubReplacer(log, "low", 1, m => destroyed);
            var extra = new StubReplacer(log, "extra", 3, m => fromMiddle);

            var chain = new MaterialReplacerChain(new IMaterialReplacer[] { high, skipped }, extra);
            Assert.That(chain.order, Is.EqualTo(1));
            Assert.That(chain.Replace(null), Is.SameAs(fromMiddle));
            Assert.That(log, Is.EqualTo(new[] { "low", "extra" }));

            log.Clear();
            var none = new MaterialReplacerChain(new IMaterialReplacer[] { new StubReplacer(log, "a", 2, m => null) }, new StubReplacer(log, "b", -2, m => null));
            Assert.That(none.order, Is.EqualTo(-2));
            Assert.That(none.Replace(null) == null, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "b", "a" }));
        }

        // MaterialReplacements.Get 0x493990 / Release 0x493ca0. A first Get asks the replacer, hides the result
        // with HideFlags 0x3d (HideAndDontSave) and applies the parameters; a Get for the same original object
        // returns the cached replacement, copies the original's properties onto it and applies the parameters
        // again; Release destroys the replacement only when the use count (1 per Get) reaches zero; a null
        // replacement is cached and returned without asking the replacer again.
        [Test]
        public void MaterialReplacementsShareOneReplacementPerOriginalUntilTheLastRelease()
        {
            Type type = typeof(SoftMask).Assembly.GetType("SoftMasking.MaterialReplacements", true);
            var asked = new List<Material>();
            var applied = new List<Material>();
            var log = new List<string>();
            Material original = OwnedMaterial(), unreplaced = OwnedMaterial();
            var replacer = new StubReplacer(log, "stub", 0, m =>
            {
                asked.Add(m);
                return m == unreplaced ? null : OwnedMaterial();
            });
            object replacements = Activator.CreateInstance(type, replacer, (Action<Material>)(m => applied.Add(m)));
            MethodInfo get = type.GetMethod("Get");
            MethodInfo release = type.GetMethod("Release");

            var first = (Material)get.Invoke(replacements, new object[] { original });
            Assert.That(first, Is.Not.SameAs(original));
            Assert.That(first.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(asked, Is.EqualTo(new[] { original }));
            Assert.That(applied, Is.EqualTo(new[] { first }));

            original.color = new Color(1f, 0f, 0f, 1f);
            var second = (Material)get.Invoke(replacements, new object[] { original });
            Assert.That(second, Is.SameAs(first));
            Assert.That(asked.Count, Is.EqualTo(1));
            Assert.That(applied.Count, Is.EqualTo(2));
            Assert.That(first.color, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));

            release.Invoke(replacements, new object[] { first });
            Assert.That(first != null, Is.True, "one use is still outstanding");
            release.Invoke(replacements, new object[] { first });
            Assert.That(first == null, Is.True, "the last release destroys the replacement");

            var third = (Material)get.Invoke(replacements, new object[] { original });
            Assert.That(asked.Count, Is.EqualTo(2), "the entry was removed, so the replacer is asked again");
            Assert.That(third != null, Is.True);

            Assert.That((Material)get.Invoke(replacements, new object[] { unreplaced }) == null, Is.True);
            Assert.That((Material)get.Invoke(replacements, new object[] { unreplaced }) == null, Is.True);
            Assert.That(asked.Count(m => m == unreplaced), Is.EqualTo(1), "a null replacement is cached");
        }

        // The TextMeshPro replacer (get_order 0x49bf00 = 10, Replace 0x49bf30) matches shader names that start
        // with "TextMeshPro/" and answers null for a null material or any other shader. It carries
        // [GlobalMaterialReplacer], so MaterialReplacer.globalReplacers (0x494060, a reflection scan that creates
        // one instance per attributed type) includes it.
        [Test]
        public void TheTextMeshProReplacerIsGlobalOrderTenAndIgnoresOtherShaders()
        {
            IMaterialReplacer tmp = MaterialReplacer.globalReplacers.FirstOrDefault(r => r.GetType().FullName == "SoftMasking.TextMeshPro.MaterialReplacer");
            Assert.That(tmp, Is.Not.Null, "collected through GlobalMaterialReplacerAttribute");
            Assert.That(tmp.order, Is.EqualTo(10));
            Assert.That(tmp.Replace(null) == null, Is.True);
            Assert.That(tmp.Replace(OwnedMaterial()) == null, Is.True, "Sprites/Default is not a TextMeshPro shader");
            Assert.That(MaterialReplacer.globalReplacers, Is.SameAs(MaterialReplacer.globalReplacers), "collected once and cached");
        }
    }
}
