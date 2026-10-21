using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerChallengeBonus (Game.Runtime 0x02000880, x86-64 0x61C400..0x61C6E0, parameters 0x02000881) as a real component on an
    // inactive owned host over an owned string table. The serialized strings are authored data.
    public sealed class UIContainerChallengeBonusTests
    {
        private const Strings DescriptionText = Strings.MENU_TAILS_CHALLENGES_INFO_BODY1;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { DescriptionText, "Finish {0} challenges" },
            { Strings.MENU_TAILS_CHALLENGES_XP, "+{0} XP" },
        };

        private static void Run(Action<Owned, UIContainerChallengeBonus> body) => OwnedStringTable.Run(Texts, () => Owned.Run(owned =>
        {
            var container = owned.Host("Lucid challenge bonus container", false).AddComponent<UIContainerChallengeBonus>();
            Set(container, "m_descriptionText", DescriptionText); // Reflection: serialized string
            body(owned, container);
        }));

        // dump.cs TypeDefIndex 2176/2175 and the DummyDll rows (fields 0x0400212e..0x04002134, methods 0x06003127..0x0600312c).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerChallengeBonus), 0x00100101,
                new (string, int)[]
                {
                    ("BonusXP", 0x0026), ("ChallengeCount", 0x0026), ("Earned", 0x0026), ("Description", 0x0026), ("FormattedBonusXP", 0x0026),
                    ("m_descriptionText", 0x0001), ("m_bonusXPFormat", 0x0001),
                },
                new (string, int)[] { ("Setup", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallengeBonus), 1, 2);
            Assert.That(typeof(UIContainerChallengeBonus).BaseType, Is.EqualTo(typeof(UIContainer)));
            Assert.That(Tooltip(typeof(UIContainerChallengeBonus).GetField("m_bonusXPFormat", Declared)),
                Is.EqualTo("Format for XP display. Parameters: {0} XP reward amount."));
            AssertMembers(typeof(UIContainerChallengeBonus.UIContainerChallengeBonusParameters), 0x00100102,
                new (string, int)[] { ("<BonusXP>k__BackingField", 0x0021), ("<ChallengeCount>k__BackingField", 0x0021), ("<Earned>k__BackingField", 0x0021) },
                new (string, int)[] { ("get_BonusXP", 0x0886), ("get_ChallengeCount", 0x0886), ("get_Earned", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallengeBonus.UIContainerChallengeBonusParameters), 1, 2);
            Assert.That(typeof(UIContainerChallengeBonus.UIContainerChallengeBonusParameters).GetInterfaces(), Does.Contain(typeof(IUIContainerParameters)));
        }

        // The parameters constructor 0x61C710 stores (bonusXP, challengeCount, earned) at +0x10, +0x14 and +0x18; distinct values show a swap.
        [Test]
        public void TheParametersKeepTheirThreeValuesApart()
        {
            var parameters = new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(250, 3, true);

            Assert.That(parameters.BonusXP, Is.EqualTo(250));
            Assert.That(parameters.ChallengeCount, Is.EqualTo(3));
            Assert.That(parameters.Earned, Is.True);
            Assert.That(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(1, 2, false).Earned, Is.False);
        }

        // The constructor 0x61C570 creates the five bindables and stores the XP format default (0x30e0029c, MENU_TAILS_CHALLENGES_XP) at +0x64.
        [Test]
        public void ConstructionCreatesTheBindablesAndTheFormatDefault()
        {
            Run((owned, container) =>
            {
                Assert.That(Get<Strings>(container, "m_bonusXPFormat"), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_XP));
                var bindables = new object[] { container.BonusXP, container.ChallengeCount, container.Earned, container.Description, container.FormattedBonusXP };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(5));
            });
        }

        // Setup 0x61C400: the three values are published as they are; the description is the localised description formatted with the
        // challenge count and the bonus text the XP format with the bonus XP (250 and 3 are told apart by their positions).
        [Test]
        public void SetupPublishesTheValuesAndTheTwoFormattedTexts()
        {
            Run((owned, container) =>
            {
                container.Setup(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(250, 3, true));

                Assert.That(container.BonusXP.Value, Is.EqualTo(250));
                Assert.That(container.ChallengeCount.Value, Is.EqualTo(3));
                Assert.That(container.Earned.Value, Is.True);
                Assert.That(container.Description.Value, Is.EqualTo("Finish 3 challenges"));
                Assert.That(container.FormattedBonusXP.Value, Is.EqualTo("+250 XP"));

                container.Setup(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(0, 12, false));

                Assert.That(container.Earned.Value, Is.False);
                Assert.That(container.Description.Value, Is.EqualTo("Finish 12 challenges"));
                Assert.That(container.FormattedBonusXP.Value, Is.EqualTo("+0 XP"));
            });
        }

        // The parameters are cast right after the base setup (0x61C449..0x61C45B): a foreign object faults before any bindable is written,
        // and a null object passes the cast and faults on its first read.
        [Test]
        public void ForeignAndMissingParametersFaultBeforeAnythingIsPublished()
        {
            Run((owned, container) =>
            {
                Assert.Throws<InvalidCastException>(() => container.Setup(new UIContainerChallenge.UIContainerChallengeParameters(null)));
                Assert.Throws<NullReferenceException>(() => container.Setup(null));

                Assert.That(container.Description.Value, Is.Null);
                Assert.That(container.BonusXP.Value, Is.Zero);
            });
        }
    }
}
