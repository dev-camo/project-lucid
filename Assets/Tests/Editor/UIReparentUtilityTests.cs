using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIReparentUtility (Game.Runtime 0x0200084a, x86-64 0x60B720..0x60B9B0) on a real RectTransform hierarchy of owned objects.
    // Awake and ApplyReparenting are called directly, as Edit Mode sends none. The serialized settings are authored data and are
    // set directly. Offsets are worked out by hand: a rect anchored to a point (the default 0.5, 0.5 anchors and pivot) with
    // sizeDelta s and anchoredPosition p has offsetMin = p - s / 2 and offsetMax = p + s / 2.
    public sealed class UIReparentUtilityTests
    {
        private static RectTransform NewRect(Owned owned, string name, Transform parent = null)
        {
            var rect = owned.Track(new GameObject(name, typeof(RectTransform))).GetComponent<RectTransform>();
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        // Root / Parent / Child, with the utility on Child.
        private static UIReparentUtility NewChain(Owned owned, out RectTransform root, out RectTransform parent)
        {
            root = NewRect(owned, "Lucid reparent root");
            parent = NewRect(owned, "Lucid reparent parent", root);
            RectTransform child = NewRect(owned, "Lucid reparent child", parent);
            child.gameObject.SetActive(false);
            var utility = child.gameObject.AddComponent<UIReparentUtility>();
            Call(utility, "Awake");
            return utility;
        }

        // dump.cs TypeDefIndex 2121 and the DummyDll rows (fields 0x04001fb7..0x04001fbc).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIReparentUtility), 0x00100001,
                new (string, int)[]
                {
                    ("m_levelsToMoveUp", 0x0001), ("m_setChildIndex", 0x0001), ("m_stretchWidth", 0x0001), ("m_stretchHeight", 0x0001),
                    ("m_rectTransform", 0x0001), ("m_initialParent", 0x0001),
                },
                new (string, int)[] { ("Awake", 0x0081), ("ApplyReparenting", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIReparentUtility), 1, 2);
            Assert.That(typeof(UIReparentUtility).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(typeof(UIReparentUtility).GetField("m_setChildIndex", Declared)),
                Is.EqualTo(new[] { typeof(MinAttribute), typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(typeof(UIReparentUtility).GetField("m_setChildIndex", Declared)),
                Is.EqualTo("Force child index of new parent, -1 leaves as last child"));
        }

        // The constructor 0x60B990 stores -1 (leave as last child) in the child index.
        [Test]
        public void TheChildIndexDefaultsToMinusOne()
        {
            Owned.Run(owned =>
            {
                var utility = owned.Host("Lucid reparent utility", false).AddComponent<UIReparentUtility>();

                Assert.That(Get<int>(utility, "m_setChildIndex"), Is.EqualTo(-1));
                Assert.That(Get<int>(utility, "m_levelsToMoveUp"), Is.EqualTo(0));
            });
        }

        // Awake 0x60B720 takes the transform as a RectTransform with a type test: a UI object gets it, a plain transform gets null.
        [Test]
        public void AwakeTakesTheRectTransformOnlyOfAUiObject()
        {
            Owned.Run(owned =>
            {
                RectTransform ui = NewRect(owned, "Lucid reparent ui");
                ui.gameObject.SetActive(false);
                var onUi = ui.gameObject.AddComponent<UIReparentUtility>();
                var onPlain = owned.Host("Lucid reparent plain", false).AddComponent<UIReparentUtility>();

                Call(onUi, "Awake");
                Call(onPlain, "Awake");

                Assert.That(Get<RectTransform>(onUi, "m_rectTransform"), Is.SameAs(ui));
                Assert.That(Get<RectTransform>(onPlain, "m_rectTransform"), Is.Null);
            });
        }

        // 0x60B7A0..0x60B8BD: the parent is remembered, the object is moved up the requested number of levels (re-parented without
        // keeping its world position) and an out-of-range count stops at the hierarchy root (0x60B89A: a null grandparent ends the walk).
        [TestCase(0, "parent", TestName = "ZeroLevelsLeaveTheObjectUnderItsParent")]
        [TestCase(1, "root", TestName = "OneLevelMovesTheObjectUnderItsGrandparent")]
        [TestCase(5, "root", TestName = "TooManyLevelsStopAtTheRoot")]
        public void LevelsMoveUpStopsAtTheRoot(int levels, string expectedParent)
        {
            Owned.Run(owned =>
            {
                UIReparentUtility utility = NewChain(owned, out RectTransform root, out RectTransform parent);
                Set(utility, "m_levelsToMoveUp", levels);

                utility.ApplyReparenting();

                Assert.That(utility.transform.parent, Is.SameAs(expectedParent == "root" ? root : parent));
                Assert.That(Get<Transform>(utility, "m_initialParent"), Is.SameAs(parent), "the parent before climbing");
            });
        }

        // 0x60B8C3..0x60B8DB: a child index other than -1 is applied to the final parent; -1 leaves the object last.
        [Test]
        public void TheChildIndexPlacesTheObjectAmongItsNewSiblings()
        {
            Owned.Run(owned =>
            {
                UIReparentUtility utility = NewChain(owned, out RectTransform root, out RectTransform parent);
                RectTransform firstSibling = NewRect(owned, "Lucid reparent first sibling", root);
                RectTransform secondSibling = NewRect(owned, "Lucid reparent second sibling", root);
                Set(utility, "m_levelsToMoveUp", 1);

                utility.ApplyReparenting();
                Assert.That(utility.transform.GetSiblingIndex(), Is.EqualTo(root.childCount - 1), "-1 leaves the object as the last child");

                Set(utility, "m_levelsToMoveUp", 0);
                Set(utility, "m_setChildIndex", 0);
                utility.ApplyReparenting();
                Assert.That(utility.transform.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(firstSibling.GetSiblingIndex(), Is.EqualTo(2));
                Assert.That(secondSibling.GetSiblingIndex(), Is.EqualTo(3));
                Assert.That(parent.GetSiblingIndex(), Is.EqualTo(1));
            });
        }

        // 0x60B829..0x60B849: the anchored position is reset to zero first. 0x60B8E0..0x60B98A: a stretched axis then has both its
        // offsets zeroed, an unstretched axis keeps what the reset left. Truth table over (stretch width, stretch height) for a rect with
        // sizeDelta (100, 50) and anchoredPosition (10, 20): after the reset offsetMin is (-50, -25) and offsetMax is (50, 25).
        [TestCase(false, false, -50f, -25f, 50f, 25f, TestName = "NoStretchKeepsTheResetOffsets")]
        [TestCase(true, false, 0f, -25f, 0f, 25f, TestName = "StretchWidthZeroesOnlyTheHorizontalOffsets")]
        [TestCase(false, true, -50f, 0f, 50f, 0f, TestName = "StretchHeightZeroesOnlyTheVerticalOffsets")]
        [TestCase(true, true, 0f, 0f, 0f, 0f, TestName = "StretchBothZeroesEveryOffset")]
        public void StretchingZeroesTheChosenOffsetAxes(bool stretchWidth, bool stretchHeight, float minX, float minY, float maxX, float maxY)
        {
            Owned.Run(owned =>
            {
                UIReparentUtility utility = NewChain(owned, out RectTransform root, out RectTransform parent);
                var rect = (RectTransform)utility.transform;
                rect.sizeDelta = new Vector2(100f, 50f);
                rect.anchoredPosition = new Vector2(10f, 20f);
                Set(utility, "m_stretchWidth", stretchWidth);
                Set(utility, "m_stretchHeight", stretchHeight);

                utility.ApplyReparenting();

                Assert.That(rect.offsetMin, Is.EqualTo(new Vector2(minX, minY)));
                Assert.That(rect.offsetMax, Is.EqualTo(new Vector2(maxX, maxY)));
            });
        }
    }
}
