using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Regression cases for the native edge behaviour of two recovered HLUnityCore utilities.
    [TestFixture]
    public sealed class OriginalHLUnityCoreUtilityTests
    {
        // RectExtensions.Absolute (060002f9, 1.10.1 x64 RVA 0x1AB3360) compares the endpoints, not the size sign.
        // At 1e8 the endpoints round to the same float, so the negative size is retained.
        [Test]
        public void AbsoluteKeepsSizeWhenEndpointsRoundEqual()
        {
            Rect horizontal = new Rect(100000000f, 0f, -1f, 1f).Absolute();
            Assert.AreEqual(100000000f, horizontal.x);
            Assert.AreEqual(-1f, horizontal.width);

            Rect vertical = new Rect(0f, 100000000f, 1f, -1f).Absolute();
            Assert.AreEqual(100000000f, vertical.y);
            Assert.AreEqual(-1f, vertical.height);
        }

        [Test]
        public void AbsoluteSwapsReversedEndpoints()
        {
            Rect rect = new Rect(10f, 20f, -4f, -6f).Absolute();
            Assert.AreEqual(new Rect(6f, 14f, 4f, 6f), rect);
        }

        // CollectionUtils.BestMatch (060000d7, 1.10.1 x64 RVA 0x8EDA70): the magnitude of int.MinValue does not
        // overflow and compares unsigned, so it never beats the int.MaxValue start and enumeration continues.
        [Test]
        public void BestMatchSkipsIntMinValueScore()
        {
            string match = CollectionUtils.BestMatch(new[] { "min", "one" }, x => x == "min" ? int.MinValue : 1);
            Assert.AreEqual("one", match);
        }
    }
}
