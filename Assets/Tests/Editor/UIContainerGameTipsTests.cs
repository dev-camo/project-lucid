using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerGameTips (Game.Runtime 020008c3, x86-64 0x636070..0x6363D0) as a real component on an owned
    // inactive host, so its Unity messages are called directly. The shuffle draws with UnityEngine.Random, so a
    // case asserts what any draw order must satisfy (every tip exactly once, the first shown) and never a drawn order
    // exactly; TheShuffleDrawsFromTheRandomState checks only that the draws are used and repeat under a seed.
    // Not covered: ShuffleTips and Refresh with an empty list, where Refresh's modulo by the zero count is a
    // divide fault in the original (idiv at 0x636248) that this edit-mode host cannot reproduce faithfully.
    public sealed class UIContainerGameTipsTests : RandomStateFixture
    {
        private static readonly Strings[] ThreeTips = { (Strings)0x101, (Strings)0x102, (Strings)0x103 };

        // Literal string-table content, so a shown tip is checked against text and not against another lookup.
        private static readonly Dictionary<Strings, string> TipTexts = new Dictionary<Strings, string>
        {
            { (Strings)0x101, "tip A" },
            { (Strings)0x102, "tip B" },
            { (Strings)0x103, "tip C" },
            { (Strings)0x201, "override A" },
            { (Strings)0x202, "override B" },
            { (Strings)0x301, "single tip" },
        };

        private static void WithTips(Strings[] tips, System.Action<UIContainerGameTips> body) =>
            OwnedStringTable.Run(TipTexts, () => Owned.Run(owned =>
            {
                var container = owned.Host("Lucid game tips", false).AddComponent<UIContainerGameTips>();
                Set(container, "m_tipIds", new List<Strings>(tips)); // serialized authored data
                body(container);
            }));

        // The text the tip at the given shuffled position must show.
        private static string TextAt(UIContainerGameTips container, int index) => TipTexts[Shuffled(container)[index]];

        private static List<Strings> Shuffled(UIContainerGameTips container) => Get<List<Strings>>(container, "m_shuffledTipIds");

        // dump.cs TypeDefIndex 2242 and the DummyDll rows (fields 0x040022ef..0x040022f4).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerGameTips), 0x00100001,
                new (string, int)[]
                {
                    ("m_tipIds", 0x0001),
                    ("m_showDurationSeconds", 0x0001),
                    ("CurrentTip", 0x0026),
                    ("m_currentIndex", 0x0001),
                    ("m_showTimerSeconds", 0x0001),
                    ("m_shuffledTipIds", 0x0021),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    ("ShuffleTips", 0x0081),
                    ("Update", 0x0081),
                    ("Refresh", 0x0081),
                    ("SetTipOverrides", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerGameTips), 2, 1);
            Assert.That(typeof(UIContainerGameTips).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(AttributeTypes(Field(typeof(UIContainerGameTips), "m_tipIds")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(HashEnumAttribute) }));
        }

        // The constructor 0x636310 creates the tip bindable (+0x48) and the shuffled list (+0x58) only; the
        // authored tip list and the duration are left to serialization.
        [Test]
        public void ConstructionCreatesTheBindableAndAnEmptyShuffledList()
        {
            WithTips(ThreeTips, container =>
            {
                Assert.That(container.CurrentTip, Is.Not.Null);
                Assert.That(container.CurrentTip.Value, Is.Null);
                Assert.That(Shuffled(container), Is.Empty);
                Assert.That(Get<float>(container, "m_showDurationSeconds"), Is.Zero);
                Assert.That(Get<int>(container, "m_currentIndex"), Is.Zero);
            });
        }

        // OnEnable 0x636070 shuffles the authored list (jmp 0x636080). ShuffleTips clears the shuffled list, copies
        // the argument, draws Random.Range(0, remaining) until it is empty (0x6361a2) and ends with the index at -1
        // and a Refresh (0x63614e), which steps to 0 and writes the string of the first shuffled tip.
        [Test]
        public void OnEnableShufflesEveryTipOnceAndShowsTheFirst()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");

                Assert.That(Shuffled(container), Is.EquivalentTo(ThreeTips));
                Assert.That(Get<int>(container, "m_currentIndex"), Is.Zero);
                Assert.That(Get<float>(container, "m_showTimerSeconds"), Is.Zero);
                Assert.That(container.CurrentTip.Value, Is.EqualTo(TextAt(container, 0)));
                Assert.That(container.CurrentTip.Value, Is.EqualTo("tip A").Or.EqualTo("tip B").Or.EqualTo("tip C"));
                Assert.That(Get<List<Strings>>(container, "m_tipIds"), Is.EqualTo(ThreeTips), "the authored list is only read");
            });
        }

        // The shuffled list is cleared first (0x6360f8: count field zeroed), so a second shuffle replaces it.
        [Test]
        public void ShufflingAgainReplacesThePreviousOrder()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");
                Call(container, "OnEnable");

                Assert.That(Shuffled(container), Has.Count.EqualTo(3));
                Assert.That(Shuffled(container), Is.EquivalentTo(ThreeTips));
            });
        }

        // The order comes from Random.Range(0, remaining) draws (0x6361a2), each taking the tip at the drawn index and
        // removing it (get_Item 0x6361be, RemoveAt 0x636189). A body that copied the list in order, or always took the
        // first, would give the authored order for every seed; a drawing one does not, and the same seed repeats its order.
        [Test]
        public void TheShuffleDrawsFromTheRandomState()
        {
            WithTips(ThreeTips, container =>
            {
                UnityEngine.Random.State prior = UnityEngine.Random.state;
                try
                {
                    bool anyReordered = false;
                    for (int seed = 1; seed <= 16; seed++)
                    {
                        UnityEngine.Random.InitState(seed);
                        Call(container, "OnEnable");
                        var first = new List<Strings>(Shuffled(container));
                        UnityEngine.Random.InitState(seed);
                        Call(container, "OnEnable");

                        Assert.That(Shuffled(container), Is.EqualTo(first), "seed " + seed + " repeats its order");
                        Assert.That(first, Is.EquivalentTo(ThreeTips));
                        anyReordered |= !first.SequenceEqual(ThreeTips);
                    }
                    Assert.That(anyReordered, Is.True, "sixteen seeds all gave the authored order");
                }
                finally
                {
                    UnityEngine.Random.state = prior;
                }
            });
        }

        // SetTipOverrides 0x636300 jumps straight to ShuffleTips with the new list; the authored list stays.
        [Test]
        public void TipOverridesReplaceTheShuffledTips()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");
                var overrides = new List<Strings> { (Strings)0x201, (Strings)0x202 };

                container.SetTipOverrides(overrides);

                Assert.That(Shuffled(container), Is.EquivalentTo(overrides));
                Assert.That(Get<int>(container, "m_currentIndex"), Is.Zero);
                Assert.That(Get<List<Strings>>(container, "m_tipIds"), Is.EqualTo(ThreeTips));
                Assert.That(container.CurrentTip.Value, Is.EqualTo(TextAt(container, 0)));
                Assert.That(container.CurrentTip.Value, Is.EqualTo("override A").Or.EqualTo("override B"));
                Assert.That(overrides, Has.Count.EqualTo(2), "the caller's list is copied, not consumed");
            });
        }

        [Test]
        public void ASingleTipIsShownAgainOnEveryRefresh()
        {
            WithTips(ThreeTips, container =>
            {
                container.SetTipOverrides(new[] { (Strings)0x301 });
                Assert.That(Shuffled(container), Is.EqualTo(new[] { (Strings)0x301 }));

                Set(container, "m_showDurationSeconds", 0f);
                Call(container, "Update");

                Assert.That(Get<int>(container, "m_currentIndex"), Is.Zero, "(0 + 1) % 1");
                Assert.That(container.CurrentTip.Value, Is.EqualTo("single tip"));
            });
        }

        // Update 0x6362b0 adds the unscaled frame time and refreshes when the duration is no longer greater than the
        // timer (ucomiss/jbe at 0x6362d9..0x6362dc). A zero duration is passed by any timer, so each Update steps
        // the index and wraps at the count (idiv at 0x636248); a refresh also zeroes the timer (0x63624e).
        [Test]
        public void UpdateRefreshesOnceTheShowDurationIsReached()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");
                Set(container, "m_showDurationSeconds", 0f);

                var seen = new List<int>();
                for (int frame = 0; frame < 4; frame++)
                {
                    Call(container, "Update");
                    seen.Add(Get<int>(container, "m_currentIndex"));
                    Assert.That(container.CurrentTip.Value, Is.EqualTo(TextAt(container, seen[frame])), "the tip of the new index is shown");
                    Assert.That(Get<float>(container, "m_showTimerSeconds"), Is.Zero, "a refresh restarts the timer");
                }

                Assert.That(seen, Is.EqualTo(new[] { 1, 2, 0, 1 }));
            });
        }

        [Test]
        public void UpdateKeepsTheTipWhileTheDurationIsLong()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");
                Set(container, "m_showDurationSeconds", 1e9f);

                Call(container, "Update");
                Call(container, "Update");

                Assert.That(Get<int>(container, "m_currentIndex"), Is.Zero);
                Assert.That(container.CurrentTip.Value, Is.EqualTo(TextAt(container, 0)));
                Assert.That(Get<float>(container, "m_showTimerSeconds"), Is.GreaterThanOrEqualTo(0f).And.LessThan(1e9f));
            });
        }

        // jbe is also taken for an unordered compare, so a NaN duration refreshes on every Update. This separates
        // "timer >= duration" (false for NaN) from the original "not (duration > timer)".
        [Test]
        public void ANaNDurationRefreshesEveryUpdate()
        {
            WithTips(ThreeTips, container =>
            {
                Call(container, "OnEnable");
                Set(container, "m_showDurationSeconds", float.NaN);

                Call(container, "Update");

                Assert.That(Get<int>(container, "m_currentIndex"), Is.EqualTo(1));
                Assert.That(container.CurrentTip.Value, Is.EqualTo(TextAt(container, 1)));
            });
        }
    }
}
