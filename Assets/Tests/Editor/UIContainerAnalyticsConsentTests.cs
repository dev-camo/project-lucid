using System;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerAnalyticsConsent (Game.Runtime 0x02000878; Start 0x6198B0, Action_OnToggleConsent 0x619950, Action_Close
    // 0x619980) as a real component on an owned inactive host over the App of OwnedCharacterManagers and a genuine
    // AnalyticsConsentManager in its row (built by its own constructor, so its Initialise-time references are absent and
    // the case supplies the GameCenter reference).
    //
    // Not covered: Action_Close for a player who is not underage (Accepted 0x6199D1, Declined 0x6199E2). The offline
    // GameCenter never reports a logged-in player, so every player counts as underage and the page cannot reach them.
    public sealed class UIContainerAnalyticsConsentTests
    {
        private static void Run(Action<Owned, App, AnalyticsConsentManager, UIContainerAnalyticsConsent> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                owned.LeaseActions(SystemAction.AppInitialise, SystemAction.AppShutdown);
                owned.LeaseRow<GameCenter>(); // the reference the case hands the manager
                AnalyticsConsentManager manager = null;
                // The constructor subscribes now, before the row is admitted; released after the unregistration.
                owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(manager)));
                manager = owned.Register(new AnalyticsConsentManager());
                var page = owned.Host("Lucid analytics consent", false).AddComponent<UIContainerAnalyticsConsent>();
                body(owned, ProcessManager.GetSystem<App>(), manager, page);
            });
        }

        // Start 0x6198B0 runs the gameplay control base first (it remembers the control state and applies the serialized
        // choice), then looks the manager up and publishes its consent state through the binder (0x619931 reads +0x30).
        [Test]
        public void StartAppliesTheBaseGameplayControlThenPublishesTheManagersConsentState()
        {
            Run((owned, app, manager, page) =>
            {
                // Reflection: Accepted and Declined also save through the SaveManager, which this fixture does not initialise.
                Call(manager, "SetConsentOnSelection", false);
                app.SetHasGameplayControl(true, true);
                Set(page, "m_enableGameplayControl", false); // serialized in the original

                Call(page, "Start");

                Assert.That(page.ConsentState.Value, Is.EqualTo(AnalyticsConsentState.Rejected));
                Assert.That(Get<bool>(page, "m_previousGameplayControl"), Is.True);
                Assert.That(app.HasGameplayControl(), Is.False);
                Assert.That(Get<AnalyticsConsentManager>(page, "m_analyticsConsentManager"), Is.SameAs(manager));
            });
        }

        // The toggle 0x619950 maps true to the Consented hash (0xeca8303d) and false to the Rejected hash (0xbb963fb6).
        [Test]
        public void TheToggleSetsConsentedForOnAndRejectedForOff()
        {
            Run((owned, app, manager, page) =>
            {
                page.Action_OnToggleConsent(true);
                Assert.That(page.ConsentState.Value, Is.EqualTo(AnalyticsConsentState.Consented));

                page.Action_OnToggleConsent(false);
                Assert.That(page.ConsentState.Value, Is.EqualTo(AnalyticsConsentState.Rejected));
            });
        }

        // Action_Close 0x619980 asks the manager whether the player is underage first (0x61998F) and does nothing for one
        // (0x619998). Accepting or declining would save through the manager's SaveManager reference, which this
        // fixture leaves empty, so a page that ignored the answer would fault here instead of leaving the state alone.
        [Test]
        public void ClosingForAnUnderagePlayerLeavesTheManagerUntouched()
        {
            Run((owned, app, manager, page) =>
            {
                // Reflection: AppInitialiseProcess also wires the SaveManager; only the GameCenter reference is needed.
                Set(manager, "m_gameCenterRef", ProcessManager.GetSystemRef<GameCenter>());
                Set(page, "m_analyticsConsentManager", manager); // Reflection: the field Start assigns, which this case does not run
                Assert.That(manager.IsPlayerUnderage(), Is.True);
                page.ConsentState.Value = AnalyticsConsentState.Consented;

                Assert.DoesNotThrow(page.Action_Close);

                Assert.That(manager.ConsentState, Is.EqualTo(AnalyticsConsentState.None));
            });
        }
    }
}
