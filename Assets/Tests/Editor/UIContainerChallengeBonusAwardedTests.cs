using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerChallengeBonusAwarded (Game.Runtime 0x02000882, x86-64 0x61C750..0x61CBF0) as a real component on an inactive owned host
    // over the genuine ChallengeManager (registered, with challenge save data of 100 earned XP and a period bonus of 40) and an owned
    // string table. Edit Mode runs no OnEnable or OnDestroy for it, so the cases call them. Not covered: enabling before the manager
    // exists (the registry then keeps the pending callback).
    public sealed class UIContainerChallengeBonusAwardedTests
    {
        private const Strings Format = Strings.MENU_TAILS_CHALLENGES_XP;
        private const Strings Title = Strings.MENU_TAILS_CHALLENGES_RESULTS_TITLE;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string> { { Format, "+{0} bonus XP" } };

        private sealed class Rig
        {
            public UIContainerChallengeBonusAwarded Container;
            public ChallengeManager Manager;
            public SaveDataChallenges Save;
            public Sprite Icon;
        }

        private static void Run(Action<Owned, Rig> body) => OwnedStringTable.Run(Texts, () => Owned.Run(owned =>
        {
            var rig = new Rig();
            OwnedChallengeServices.Register(owned);
            rig.Manager = ProcessManager.GetSystem<ChallengeManager>();
            rig.Save = new SaveDataChallenges { TotalXPEarned = 100, BonusXPUnacknowledged = 9 };
            rig.Save.DisableSaving(); // the acknowledgement requests a save, which needs the save manager
            Set(rig.Manager, "m_saveDataChallenges", rig.Save); // Reflection: taken from the open save by OnSaveGameOpen, which needs the App
            AwardPeriodBonus(owned, rig.Manager);
            rig.Icon = NewSprite(owned);
            rig.Container = owned.Host("Lucid challenge bonus awarded container", false).AddComponent<UIContainerChallengeBonusAwarded>();
            Set(rig.Container, "m_titleChallengesComplete", Title); // Reflection: serialized fields
            Set(rig.Container, "m_iconChallenges", rig.Icon);
            body(owned, rig);
        }));

        // The period bonus is awarded by the manager's own routine for a finished cycle: every current challenge has been played, so the
        // cycle definition's bonus (40) becomes the period bonus and is added to the save's unacknowledged bonus (9 to 49).
        private static void AwardPeriodBonus(Owned owned, ChallengeManager manager)
        {
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
            var mission = Create<MissionDefinition>(owned);
            var definition = Create<ChallengeDefinition>(owned);
            Set(definition, "m_mission", mission); // Reflection: serialized references and lists
            Set(definition, "m_characterSelection", new CharacterId[0]);
            Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]);
            var state = new ChallengeState(0, definition, 0, null, 0);
            state.OnAwardXP = (challenge, first, ringXp) => { };
            state.OnUpdateScore = challenge => { };
            state.UpdateScoreInfo(5, 1f, 0, true);
            Set(manager, "m_challengeCycleDefinition", Create<ChallengeCycleDefinition>(owned, "{\"m_periodBonusXP\":40}"));
            Get<List<ChallengeState>>(manager, "m_currentChallenges").Add(state);
            Assert.That(Call(manager, "CalculateBonusChallengeXP", state), Is.EqualTo(40), "set-up: the cycle's bonus is awarded");
        }

        // dump.cs TypeDefIndex 2177 and the DummyDll rows (fields 0x04002138..0x04002141, methods 0x0600312d..0x06003130).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerChallengeBonusAwarded), 0x00100101,
                new (string, int)[]
                {
                    ("XPRewardTitle", 0x0026), ("BonusXPAwarded", 0x0026), ("TotalXPBefore", 0x0026), ("TotalXPAfter", 0x0026),
                    ("BonusXPString", 0x0026), ("RewardIcon", 0x0026), ("m_xpStringFormat", 0x0001), ("m_titleChallengesComplete", 0x0001),
                    ("m_iconChallenges", 0x0001), ("m_challengeManager", 0x0001),
                },
                new (string, int)[] { ("OnEnable", 0x0081), ("OnChallengeManagerValid", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallengeBonusAwarded), 1, 2);
            Assert.That(typeof(UIContainerChallengeBonusAwarded).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // The constructor 0x61CA30 creates the six bindables and stores the XP format default (0x30e0029c) at +0x68.
        [Test]
        public void ConstructionCreatesTheBindablesAndTheFormatDefault()
        {
            Run((owned, rig) =>
            {
                var container = rig.Container;
                Assert.That(Get<Strings>(container, "m_xpStringFormat"), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_XP));
                var bindables = new object[] { container.XPRewardTitle, container.BonusXPAwarded, container.TotalXPBefore, container.TotalXPAfter,
                    container.BonusXPString, container.RewardIcon };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(6));
                Assert.That(Get<ChallengeManager>(container, "m_challengeManager"), Is.Null);
            });
        }

        // OnChallengeManagerValid 0x61C830: the manager is kept, the bonus is its period bonus (+0xD0), the total after is its current XP and
        // the total before is that less the bonus (through the bindables' implicit conversion), the text is the bonus formatted into the
        // string format and the title and icon are the authored ones.
        [Test]
        public void TheValidManagerSuppliesTheBonusAndTheTotalsBeforeAndAfter()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "OnChallengeManagerValid", rig.Manager);

                Assert.That(Get<ChallengeManager>(rig.Container, "m_challengeManager"), Is.SameAs(rig.Manager));
                Assert.That(rig.Container.BonusXPAwarded.Value, Is.EqualTo(40));
                Assert.That(rig.Container.TotalXPAfter.Value, Is.EqualTo(100));
                Assert.That(rig.Container.TotalXPBefore.Value, Is.EqualTo(60));
                Assert.That(rig.Container.BonusXPString.Value, Is.EqualTo("+40 bonus XP"));
                Assert.That(rig.Container.XPRewardTitle.Value, Is.EqualTo(Title));
                Assert.That(rig.Container.RewardIcon.Value, Is.SameAs(rig.Icon));
            });
        }

        // OnEnable 0x61C750 asks the registry's reference for the manager and runs the same callback; with the manager already registered it
        // runs at once.
        [Test]
        public void EnablingTakesTheRegisteredManager()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "OnEnable");

                Assert.That(Get<ChallengeManager>(rig.Container, "m_challengeManager"), Is.SameAs(rig.Manager));
                Assert.That(rig.Container.BonusXPAwarded.Value, Is.EqualTo(40));
                Assert.That(rig.Container.TotalXPBefore.Value, Is.EqualTo(60));
            });
        }

        // OnDestroy 0x61C9B0 acknowledges the bonus on the kept manager (its period bonus and the unacknowledged bonus of the save both reset)
        // and forgets the manager; without one it only clears the field.
        [Test]
        public void DestroyingAcknowledgesTheBonusAndForgetsTheManager()
        {
            Run((owned, rig) =>
            {
                Call(rig.Container, "OnDestroy");
                Assert.That(rig.Manager.PeriodBonusXP, Is.EqualTo(40), "without a manager nothing is acknowledged");
                Assert.That(rig.Save.BonusXPUnacknowledged, Is.EqualTo(49));

                Call(rig.Container, "OnChallengeManagerValid", rig.Manager);
                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Manager.PeriodBonusXP, Is.Zero);
                Assert.That(rig.Save.BonusXPUnacknowledged, Is.Zero);
                Assert.That(Get<ChallengeManager>(rig.Container, "m_challengeManager"), Is.Null);
            });
        }
    }
}
