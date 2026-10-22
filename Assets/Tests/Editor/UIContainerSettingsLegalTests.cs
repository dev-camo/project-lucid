using System;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsLegal (Game.Runtime 0x0200093a, OnEnable 0x67DAD0, constructor 0x67DBD0) as a real component on an owned
    // inactive host (so OnEnable is called by each case). The services are real: an AnalyticsConsentManager that has not
    // loaded (so the consent screen button is not offered) and a UIManager whose only container manager is the scripted one
    // of UiPublishingFixtures, which holds the open set.
    // Not covered: the consent button being offered, which needs a ready GameCenter account that is not underage.
    public sealed class UIContainerSettingsLegalTests
    {
        private sealed class Rig
        {
            public UIContainerSettingsLegal Container;
            public ScriptedExclusivityContainerManager Containers;
            public UIContainerIdentifier LevelSelect;
            public UIContainerIdentifier MainMenu;
            public UIContainerIdentifier Other;
        }

        private static void Run(Action<Owned, Rig> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            Owned.Run(owned =>
            {
                var rig = new Rig();
                // The consent manager's constructor subscribes these actions before its row is admitted.
                owned.LeaseActions(SystemAction.AppInitialise, SystemAction.AppShutdown);
                var consent = new AnalyticsConsentManager();
                owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(consent)));
                owned.Register(consent);
                NewUiManager(owned, out rig.Containers);
                rig.LevelSelect = NewIdentifier(owned, "level-select");
                rig.MainMenu = NewIdentifier(owned, "main-menu");
                rig.Other = NewIdentifier(owned, "other");
                rig.Container = owned.Host("Lucid legal page", false).AddComponent<UIContainerSettingsLegal>();
                Set(rig.Container, "m_levelSelectIdentifier", rig.LevelSelect); // serialized in the original
                Set(rig.Container, "m_mainMenuIdentifier", rig.MainMenu); // serialized in the original
                body(owned, rig);
            });
        }

        // 0x67DB89: the credits are offered while the level select is open (the first IsOpen call) ...
        [Test]
        public void TheCreditsAreOfferedFromTheLevelSelect()
        {
            Run((owned, rig) =>
            {
                rig.Containers.Open.Add(rig.LevelSelect);

                Call(rig.Container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.True);
            });
        }

        // ... or the main menu (the second IsOpen call, 0x67DB9F, runs only when the first answers false).
        [Test]
        public void TheCreditsAreOfferedFromTheMainMenu()
        {
            Run((owned, rig) =>
            {
                rig.Containers.Open.Add(rig.MainMenu);

                Call(rig.Container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.True);
            });
        }

        [Test]
        public void TheCreditsAreOfferedWhenBothAreOpen()
        {
            Run((owned, rig) =>
            {
                rig.Containers.Open.Add(rig.LevelSelect);
                rig.Containers.Open.Add(rig.MainMenu);

                Call(rig.Container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.True);
            });
        }

        // From any other page the credits are hidden again: the binder is written whatever it held.
        [Test]
        public void TheCreditsAreHiddenFromAnyOtherPage()
        {
            Run((owned, rig) =>
            {
                rig.Containers.Open.Add(rig.Other);
                rig.Container.CanShowCreditsButton.Value = true;

                Call(rig.Container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.False);
            });
        }

        // 0x67DB48: the consent button binder takes the consent manager's answer (CanShowConsentScreenButtonOnSettings
        // 0x509090), which is false while the manager has not loaded; the credits binder is a separate one.
        [Test]
        public void TheConsentButtonFollowsTheConsentManagerAndIsIndependentOfTheCredits()
        {
            Run((owned, rig) =>
            {
                rig.Containers.Open.Add(rig.LevelSelect);
                rig.Container.CanShowAnalyticsConsentButton.Value = true;

                Call(rig.Container, "OnEnable"); // Reflection: Unity sends OnEnable; Edit Mode does not

                Assert.That(rig.Container.CanShowAnalyticsConsentButton.Value, Is.False);
                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.True);
            });
        }

        // The constructor 0x67DBD0 builds the two binders in declaration order, each its own object.
        [Test]
        public void TheConstructorBuildsTwoSeparateBinders()
        {
            Run((owned, rig) =>
            {
                Assert.That(rig.Container.CanShowAnalyticsConsentButton, Is.Not.Null.And.Not.SameAs(rig.Container.CanShowCreditsButton));
                Assert.That(rig.Container.CanShowCreditsButton, Is.Not.Null);
                Assert.That(rig.Container.CanShowAnalyticsConsentButton.Value, Is.False);
                Assert.That(rig.Container.CanShowCreditsButton.Value, Is.False);
            });
        }
    }
}
