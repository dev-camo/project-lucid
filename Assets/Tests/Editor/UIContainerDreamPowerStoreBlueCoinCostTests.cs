using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerDreamPowerStoreBlueCoinCost (Game.Runtime 0x0200086f, Setup x86-64 0x6172C0) as a real component on an owned
    // inactive host, set up with real parameters and a dream power definition with an authored purchase cost.
    public sealed class UIContainerDreamPowerStoreBlueCoinCostTests
    {
        private static DreamPowerDefinition Power(Owned owned, int cost) =>
            Create<DreamPowerDefinition>(owned, "{\"m_purchaseCost\":" + cost + "}");

        // Setup 0x6172C0 compares the item state with 2 and 5 (0x61731D, 0x617323) and with 2 again (setae at 0x617354): the cost is
        // shown for Purchasable (3) and Insufficient (4) only. Bought (2) and Equipped (5) are excluded by the first two compares, the
        // two locked states (0, 1) by the below-2 compare. The compare is unsigned and open above: a state past Equipped also
        // shows the cost.
        [TestCase(UIDreamPowerStoreItemState.LockedByProgression, false, TestName = "AProgressionLockedPowerShowsNoCost")]
        [TestCase(UIDreamPowerStoreItemState.LockedByArchetype, false, TestName = "AnArchetypeLockedPowerShowsNoCost")]
        [TestCase(UIDreamPowerStoreItemState.Bought, false, TestName = "ABoughtPowerShowsNoCost")]
        [TestCase(UIDreamPowerStoreItemState.Purchasable, true, TestName = "APurchasablePowerShowsItsCost")]
        [TestCase(UIDreamPowerStoreItemState.Insufficient, true, TestName = "AnUnaffordablePowerShowsItsCost")]
        [TestCase(UIDreamPowerStoreItemState.Equipped, false, TestName = "AnEquippedPowerShowsNoCost")]
        [TestCase((UIDreamPowerStoreItemState)6, true, TestName = "AStatePastEquippedShowsTheCost")]
        [TestCase((UIDreamPowerStoreItemState)(-1), true, TestName = "ANegativeStateShowsTheCost")]
        public void TheCostIsShownExceptForTheLockedBoughtAndEquippedStates(UIDreamPowerStoreItemState state, bool shown)
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();
                container.ShowCost.Value = !shown;

                container.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(Power(owned, 35), state));

                Assert.That(container.ShowCost.Value, Is.EqualTo(shown));
            });
        }

        // The price text is the definition's purchase cost (+0x30), written for every state, formatted by Int32.ToString with the
        // invariant culture (0x617394..0x6173A2).
        [Test]
        public void TheCostTextIsThePurchaseCostOfTheDefinitionWhateverTheState()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();

                container.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(Power(owned, 35), UIDreamPowerStoreItemState.Purchasable));
                Assert.That(container.Cost.Value, Is.EqualTo("35"));

                container.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(Power(owned, 1200), UIDreamPowerStoreItemState.Equipped));
                Assert.That(container.Cost.Value, Is.EqualTo("1200"));
                Assert.That(container.ShowCost.Value, Is.False);
            });
        }

        // The price is formatted with Int32.ToString(IFormatProvider) and CultureInfo.InvariantCulture (0x617394..0x6173A2), not with
        // the current culture: a current culture whose negative sign is "~" still gives "-5".
        [Test]
        public void TheCostIsFormattedWithTheInvariantCultureNotTheCurrentOne()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid blue coin cost", false).AddComponent<UIContainerDreamPowerStoreBlueCoinCost>();

                TildeNegativeSignCulture.Run(() =>
                    container.Setup(new UIContainerDreamPowerStoreBlueCoinCostParameters(Power(owned, -5), UIDreamPowerStoreItemState.Purchasable)));

                Assert.That(container.Cost.Value, Is.EqualTo("-5"));
            });
        }
    }
}
