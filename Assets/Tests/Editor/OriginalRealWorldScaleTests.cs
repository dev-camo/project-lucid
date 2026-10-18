using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.RealWorldScale (TypeDef 0x02000a1e, x86-64 0x6B3820..0x6B39B6). The Editor's
    // Screen width, height and dpi are read as they are, so expected values are built from them
    // with the native expression order.
    public sealed class OriginalRealWorldScaleTests
    {
        private static void AssertSame(float expected, float actual, string label)
        {
            if (float.IsNaN(expected)) Assert.That(float.IsNaN(actual), Is.True, label + ": expected NaN but was " + actual);
            else Assert.That(actual, Is.EqualTo(expected), label);
        }

        [Test]
        public void DeclarationsAndConstructorDefaults()
        {
            AssertMembers(typeof(RealWorldScale), 0x00100001,
                new[] { ("m_canvasScaler", 0x0001), ("m_scaleTransform", 0x0001), ("m_dimensionsInInches", 0x0001),
                        ("m_multiplyOnLowerAspects", 0x0001), ("m_widescreenAspectRatio", 0x0001), ("m_disableInGameWindow", 0x0001),
                        ("m_totalScreenPixels", 0x0001), ("m_activeProportionOfScreen", 0x0001), ("m_screenDPI", 0x0001) },
                new[] { ("get_TotalScreenPixels", 0x0886), ("get_ActiveProportionOfScreen", 0x0886), ("Awake", 0x0081),
                        ("ScaleToRealWorldInches", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(RealWorldScale), 1, 2);
            Assert.That(Tooltip(typeof(RealWorldScale).GetField("m_scaleTransform", Declared)),
                Is.EqualTo("The transform that will have its size changed to match the \"real\" size."));

            // .ctor 0x6B3960: Vector2.one, 0x3f11eb85 (0.57f) and true.
            Owned.Run(owned =>
            {
                var scale = owned.Host("Lucid real world scale").AddComponent<RealWorldScale>();
                Assert.That(Get<Vector2>(scale, "m_multiplyOnLowerAspects"), Is.EqualTo(Vector2.one));
                Assert.That(Get<float>(scale, "m_widescreenAspectRatio"), Is.EqualTo(0.57f));
                Assert.That(Get<bool>(scale, "m_disableInGameWindow"), Is.True);
                Assert.That(scale.TotalScreenPixels, Is.EqualTo(Vector2.zero));
            });
        }

        // ScaleToRealWorldInches 0x6B3850: the factor is 1 / m_canvasScaler.localScale.x, or 1 without
        // a scaler (op_Inequality). It stores Screen.width, then Screen.height, then Screen.dpi, and
        // keeps the dimensions only when m_widescreenAspectRatio > height / width (ucomiss/ja at
        // 0x6B3910); otherwise they are multiplied component-wise by m_multiplyOnLowerAspects.
        // The rows fix the branch as literals: +inf keeps, -inf multiplies, a ratio equal to the
        // aspect multiplies (separating ratio >= aspect) and a NaN ratio multiplies (separating
        // !(aspect >= ratio)). The active proportion is dimensions / (pixels / dpi) and the
        // sizeDelta is (factor * dpi) * dimensions. Awake 0x6B3840 tail-calls it.
        [Test]
        public void ScalesTheTransformToTheDimensionsAtTheScreenDpi()
        {
            float screenAspect = (float)Screen.height / Screen.width;
            Assume.That(float.IsFinite(screenAspect), Is.True, "the equality row needs a finite Editor screen aspect");
            // A zero or NaN dpi makes both branches give the same proportion and size.
            Assume.That(float.IsFinite(Screen.dpi) && Screen.dpi > 0f, Is.True, "the branch rows need a positive finite Editor dpi");
            Owned.Run(owned =>
            {
                var scale = owned.Host("Lucid real world scale").AddComponent<RealWorldScale>();
                var target = owned.Track(new GameObject("Lucid scaled rect", typeof(RectTransform))).GetComponent<RectTransform>();
                var scaler = owned.Track(new GameObject("Lucid canvas scaler", typeof(RectTransform))).GetComponent<RectTransform>();
                scaler.localScale = new Vector3(4f, 9f, 9f);
                Set(scale, "m_scaleTransform", target);
                Set(scale, "m_dimensionsInInches", new Vector2(2f, 3f));
                Set(scale, "m_multiplyOnLowerAspects", new Vector2(10f, 100f));

                var pixels = new Vector2(Screen.width, Screen.height);
                float dpi = Screen.dpi;
                float aspect = pixels.y / pixels.x;
                var rows = new[]
                {
                    (false, float.PositiveInfinity, "Awake", true),
                    (true, float.NegativeInfinity, "ScaleToRealWorldInches", false),
                    (false, aspect, "ScaleToRealWorldInches", false),
                    (true, float.NaN, "Awake", false),
                };
                foreach (var (useScaler, widescreen, method, keep) in rows)
                {
                    Set(scale, "m_canvasScaler", useScaler ? scaler : null);
                    Set(scale, "m_widescreenAspectRatio", widescreen);
                    Call(scale, method);
                    float factor = useScaler ? 1f / 4f : 1f;
                    Vector2 dimensions = keep ? new Vector2(2f, 3f) : new Vector2(20f, 300f);
                    string label = method + " at ratio " + widescreen + (keep ? " keeps" : " multiplies");
                    Assert.That(scale.TotalScreenPixels, Is.EqualTo(pixels), label);
                    AssertSame(dpi, Get<float>(scale, "m_screenDPI"), label + " dpi");
                    AssertSame(dimensions.x / (pixels.x / dpi), scale.ActiveProportionOfScreen.x, label + " proportion x");
                    AssertSame(dimensions.y / (pixels.y / dpi), scale.ActiveProportionOfScreen.y, label + " proportion y");
                    AssertSame(factor * dpi * dimensions.x, target.sizeDelta.x, label + " width");
                    AssertSame(factor * dpi * dimensions.y, target.sizeDelta.y, label + " height");
                }
            });
        }
    }
}
