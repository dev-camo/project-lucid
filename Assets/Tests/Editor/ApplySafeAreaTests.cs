using System;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ApplySafeArea (Game.Runtime 0200082e, bodies 0x606BD0..0x606DB0). The Editor normally reports a safe
    // area covering the whole screen, so these cases only show that Apply and Awake overwrite the offsets
    // with the safe-area insets (zero there). They cannot separate a swapped sign or operand in the native
    // arithmetic (0x606D20..0x606D9A); that needs a device run with an inset safe area.
    public sealed class ApplySafeAreaTests
    {
        private const float Tolerance = 1e-3f;

        private static GameObject RectHost(Owned owned)
        {
            var host = owned.Track(new GameObject("Lucid safe area", typeof(RectTransform)));
            host.SetActive(false);
            return host;
        }

        private static void AssertSafeAreaApplied(RectTransform rectTransform)
        {
            Rect safeArea = Screen.safeArea;
            Assert.That(rectTransform.offsetMin.x, Is.EqualTo(safeArea.x).Within(Tolerance));
            Assert.That(rectTransform.offsetMin.y, Is.EqualTo(safeArea.y).Within(Tolerance));
            Assert.That(rectTransform.offsetMax.x, Is.EqualTo(safeArea.x + safeArea.width - Screen.width).Within(Tolerance));
            Assert.That(rectTransform.offsetMax.y, Is.EqualTo(safeArea.y + safeArea.height - Screen.height).Within(Tolerance));
        }

        // Constructor 0x606DB0 stores true in the flag.
        [Test]
        public void ItAppliesOnAwakeByDefault()
        {
            Owned.Run(owned =>
            {
                var safeArea = RectHost(owned).AddComponent<ApplySafeArea>();
                Assert.That(Get<bool>(safeArea, "m_applyOnAwake"), Is.True);
            });
        }

        [Test]
        public void ApplyOverwritesBothOffsetsFromTheSafeArea()
        {
            Owned.Run(owned =>
            {
                GameObject host = RectHost(owned);
                var rectTransform = host.GetComponent<RectTransform>();
                rectTransform.offsetMin = new Vector2(7f, 9f);
                rectTransform.offsetMax = new Vector2(5f, 3f);
                var safeArea = host.AddComponent<ApplySafeArea>();

                safeArea.Apply();
                AssertSafeAreaApplied(rectTransform);
            });
        }

        // Awake 0x606BD0 returns at once when the flag is clear (0x606BDB) and otherwise runs the Apply body.
        [Test]
        public void AwakeAppliesOnlyWhenTheFlagIsSet()
        {
            Owned.Run(owned =>
            {
                GameObject host = RectHost(owned);
                var rectTransform = host.GetComponent<RectTransform>();
                rectTransform.offsetMin = new Vector2(7f, 9f);
                rectTransform.offsetMax = new Vector2(5f, 3f);
                var safeArea = host.AddComponent<ApplySafeArea>();

                Set(safeArea, "m_applyOnAwake", false);
                Call(safeArea, "Awake");
                Assert.That(rectTransform.offsetMin.x, Is.EqualTo(7f).Within(Tolerance));
                Assert.That(rectTransform.offsetMin.y, Is.EqualTo(9f).Within(Tolerance));
                Assert.That(rectTransform.offsetMax.x, Is.EqualTo(5f).Within(Tolerance));
                Assert.That(rectTransform.offsetMax.y, Is.EqualTo(3f).Within(Tolerance));

                Set(safeArea, "m_applyOnAwake", true);
                Call(safeArea, "Awake");
                AssertSafeAreaApplied(rectTransform);
            });
        }

        // The transform is taken as a RectTransform with a type check that yields null (0x606D0B..0x606D1A),
        // and the first offset write then faults.
        [Test]
        public void AHostWithoutARectTransformFaults()
        {
            Owned.Run(owned =>
            {
                var safeArea = owned.Host("Lucid plain safe area", false).AddComponent<ApplySafeArea>();
                Assert.Throws<NullReferenceException>(() => safeArea.Apply());
            });
        }
    }
}
