using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerChallengeLeaderboard (Game.Runtime 0x02000883, x86-64 0x61CBF0..0x61CED0, parameters 0x02000884) as a real component on an
    // inactive owned host, with a real button widget over a real UI button, an owned string table and a registered ChallengeManager whose
    // leaderboard integration records its calls.
    public sealed class UIContainerChallengeLeaderboardTests
    {
        private const Strings Label = Strings.MENU_TAILS_CHALLENGES_LABEL_NEW_CHALLENGES;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string> { { Label, "Challenge score" } };

        private sealed class RecordingIntegration : ChallengeLeaderboardIntegration
        {
            public int Shown;
            protected override void DoInitialise(Action callback) { }
            protected override void DoDeinitialise() { }
            public override DateTime GetCurrentPeriodStart() => default;
            public override DateTime GetCurrentPeriodEnd() => default;
            public override DateTime GetTime() => default;
            protected override void DoSubmitScore(int score) { }
            public override void ShowLeaderboard() => Shown++;
            public override bool CanShowLeaderboard() => false;
            public override void AdvanceCycle() { }
            public override void RegressCycle() { }
        }

        private sealed class Rig
        {
            public UIContainerChallengeLeaderboard Container;
            public UIWidgetButton Widget;
            public Button Button;
        }

        private static void Run(Action<Owned, Rig> body, bool withButton = true) => OwnedStringTable.Run(Texts, () => Owned.Run(owned =>
        {
            var rig = new Rig();
            rig.Container = owned.Host("Lucid challenge leaderboard container", false).AddComponent<UIContainerChallengeLeaderboard>();
            Set(rig.Container, "m_scoreLabel", Label); // Reflection: serialized fields
            if (withButton)
            {
                GameObject host = owned.Host("Lucid challenge leaderboard button", false);
                rig.Button = host.AddComponent<Button>();
                rig.Widget = host.AddComponent<UIWidgetButton>();
                Set(rig.Widget, "m_uiButton", rig.Button); // Reflection: serialized m_uiButton the editor assigns
                Set(rig.Container, "m_button", rig.Widget); // Reflection: serialized m_button the editor assigns
            }
            body(owned, rig);
        }));

        // dump.cs TypeDefIndex 2179/2178 and the DummyDll rows (fields 0x04002142..0x04002148, methods 0x06003131..0x06003137).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerChallengeLeaderboard), 0x00100101,
                new (string, int)[] { ("Score", 0x0026), ("Available", 0x0026), ("ScoreLabel", 0x0026), ("m_button", 0x0001), ("m_scoreLabel", 0x0001) },
                new (string, int)[] { ("Awake", 0x0081), ("Action_OnButtonPressed", 0x0086), ("Setup", 0x00c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallengeLeaderboard), 1, 2);
            Assert.That(typeof(UIContainerChallengeLeaderboard).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters), 0x00100102,
                new (string, int)[] { ("<Score>k__BackingField", 0x0021), ("<Available>k__BackingField", 0x0021) },
                new (string, int)[] { ("get_Score", 0x0886), ("get_Available", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters), 1, 2);
        }

        // The parameters constructor 0x61CEE0 stores (score, available) at +0x10 and +0x14.
        [Test]
        public void TheParametersKeepTheirTwoValuesApart()
        {
            var parameters = new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(4200, true);

            Assert.That(parameters.Score, Is.EqualTo(4200));
            Assert.That(parameters.Available, Is.True);
            Assert.That(new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(1, false).Available, Is.False);
        }

        // The constructor 0x61CDB0 creates the three bindables (+0x38, +0x40, +0x48); Awake 0x61CBF0 localises the score label into the third.
        [Test]
        public void AwakeLocalisesTheScoreLabel()
        {
            Run((owned, rig) =>
            {
                var bindables = new object[] { rig.Container.Score, rig.Container.Available, rig.Container.ScoreLabel };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(3));
                Assert.That(rig.Container.ScoreLabel.Value, Is.Null);

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(rig.Container.ScoreLabel.Value, Is.EqualTo("Challenge score"));
            });
        }

        // Setup 0x61CCD0: score and availability are published, and the button follows the availability (SetInteractable, 0x61CD98).
        [Test]
        public void SetupPublishesTheScoreAndMakesTheButtonFollowTheAvailability()
        {
            Run((owned, rig) =>
            {
                rig.Button.interactable = false;
                rig.Container.Setup(new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(4200, true));

                Assert.That(rig.Container.Score.Value, Is.EqualTo(4200));
                Assert.That(rig.Container.Available.Value, Is.True);
                Assert.That(rig.Button.interactable, Is.True);

                rig.Container.Setup(new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(7, false));

                Assert.That(rig.Container.Score.Value, Is.EqualTo(7));
                Assert.That(rig.Container.Available.Value, Is.False);
                Assert.That(rig.Button.interactable, Is.False);
            });
        }

        // The button is tested with Unity's op_Inequality (0x61CD7B): without one the setup only publishes the two values.
        [Test]
        public void SetupWithoutAButtonOnlyPublishesTheValues()
        {
            Run((owned, rig) =>
            {
                Assert.DoesNotThrow(() => rig.Container.Setup(new UIContainerChallengeLeaderboard.UIContainerChallengeLeaderboardParameters(11, true)));

                Assert.That(rig.Container.Score.Value, Is.EqualTo(11));
                Assert.That(rig.Container.Available.Value, Is.True);
            }, withButton: false);
        }

        // The parameters are cast right after the base setup (0x61CD15..0x61CD27): a foreign object faults, a null object passes the cast and
        // faults on its first read; neither publishes anything.
        [Test]
        public void ForeignAndMissingParametersFaultBeforeAnythingIsPublished()
        {
            Run((owned, rig) =>
            {
                Assert.Throws<InvalidCastException>(() => rig.Container.Setup(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(1, 2, true)));
                Assert.Throws<NullReferenceException>(() => rig.Container.Setup(null));

                Assert.That(rig.Container.Score.Value, Is.Zero);
            });
        }

        // Action_OnButtonPressed 0x61CC60 takes the challenge manager from the registry and asks it to show the leaderboard, which forwards to
        // its integration.
        [Test]
        public void PressingTheButtonShowsTheLeaderboardThroughTheChallengeManager()
        {
            Run((owned, rig) =>
            {
                OwnedChallengeServices.Register(owned);
                ChallengeManager manager = ProcessManager.GetSystem<ChallengeManager>();
                var integration = new RecordingIntegration();
                Set(manager, "m_leaderboardIntegration", integration); // Reflection: chosen by the manager's Awake from the configuration

                rig.Container.Action_OnButtonPressed();
                rig.Container.Action_OnButtonPressed();

                Assert.That(integration.Shown, Is.EqualTo(2));
            });
        }
    }
}
