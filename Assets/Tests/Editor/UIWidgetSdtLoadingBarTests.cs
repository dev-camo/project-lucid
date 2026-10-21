using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTLoadingBar (Game.Runtime 0x0200084e, x86-64 0x60D390..0x60D3D0) as a real component on an inactive owned host with a real
    // RectTransform. The serialized rect and curve are authored data. The native body writes anchorMax = (curve.Evaluate(progress),
    // 1.0f); 1.0f is the constant at 0x29A6C64 (the blendps at 0x60D3A4 takes lane 1 from it).
    public sealed class UIWidgetSdtLoadingBarTests
    {
        private const float Tolerance = 1e-5f;

        // dump.cs TypeDefIndex 2125 and the DummyDll rows (fields 0x04001fe1, 0x04001fe2).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTLoadingBar), 0x00100001,
                new (string, int)[] { ("m_loadingBarUI", 0x0001), ("m_curve", 0x0001) },
                new (string, int)[] { ("UpdateLoadingProgress", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetSDTLoadingBar), 2, 1);
            Assert.That(typeof(UIWidgetSDTLoadingBar).BaseType, Is.EqualTo(typeof(Hardlight.UIWidget)));
            foreach (string field in new[] { "m_loadingBarUI", "m_curve" })
                Assert.That(AttributeTypes(typeof(UIWidgetSDTLoadingBar).GetField(field, Declared)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
        }

        // The curve maps the progress to the horizontal anchor; the vertical anchor is written as 1 whatever it was; the minimum anchor
        // is not touched. The curve here runs linearly from 0.2 at progress 0 to 0.8 at progress 1, and clamps outside that range.
        [TestCase(0f, 0.2f, TestName = "NoProgressAnchorsAtTheCurveStart")]
        [TestCase(0.5f, 0.5f, TestName = "HalfProgressAnchorsAtTheCurveMiddle")]
        [TestCase(1f, 0.8f, TestName = "FullProgressAnchorsAtTheCurveEnd")]
        [TestCase(2f, 0.8f, TestName = "ProgressBeyondTheCurveClampsToItsEnd")]
        public void ProgressMovesTheRightAnchorAlongTheCurve(float progress, float expectedX)
        {
            Owned.Run(owned =>
            {
                var bar = owned.Host("Lucid loading bar", false).AddComponent<UIWidgetSDTLoadingBar>();
                RectTransform fill = owned.Track(new GameObject("Lucid loading bar fill", typeof(RectTransform))).GetComponent<RectTransform>();
                fill.anchorMin = new Vector2(0.1f, 0.2f);
                fill.anchorMax = new Vector2(0.3f, 0.4f);
                Set(bar, "m_loadingBarUI", fill);
                Set(bar, "m_curve", AnimationCurve.Linear(0f, 0.2f, 1f, 0.8f));

                bar.UpdateLoadingProgress(progress);

                Assert.That(fill.anchorMax.x, Is.EqualTo(expectedX).Within(Tolerance));
                Assert.That(fill.anchorMax.y, Is.EqualTo(1f));
                Assert.That(fill.anchorMin, Is.EqualTo(new Vector2(0.1f, 0.2f)));
            });
        }
    }
}
