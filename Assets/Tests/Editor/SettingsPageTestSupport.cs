using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Fixtures for the settings pages (UIContainerSettings*): a real SaveManager registered into its absent row, holding
    // live settings, and the setting-changed analytics event the pages report. The pages are real components on owned
    // inactive hosts, so Unity runs none of their messages and each case calls the original methods itself.
    internal static class SettingsPageTestSupport
    {
        internal sealed class Page
        {
            internal Owned Owned;
            internal SaveManager Save;
            internal SaveDataSettings Settings;
            internal SettingChangedWatch Reported;
            // Only when the case was started with WithTime.
            internal TimeManager Time;
        }

        // The settings are installed as the save flow of the original does (SaveManager.m_saveSettings has no
        // setter) and are marked saved, so that a case sees which edits dirty them and which save them.
        internal static void With(Action<Page> body)
        {
            InitialiseAnalytics();
            Owned.Run(owned => body(NewPage(owned)));
        }

        // The same with a TimeManager registered into its absent row, for the pages that take a typed reference to it.
        internal static void WithTime(Action<Page> body)
        {
            InitialiseAnalytics();
            TimeScaledTestHarness.WithRegisteredManager(scene =>
            {
                Page page = NewPage(scene.Owned);
                page.Time = scene.Manager;
                body(page);
            });
        }

        // AnalyticsEventCollector's type initialiser (0x0600006d) auto-creates the UIModernMessage exchange row, subscribes the
        // button-press callback and takes a typed DataManager reference, which would leave those rows (and a Shutdown subscriber)
        // behind in whichever case reports a setting change first. It runs here against an owned exchange and an idle
        // DataManager row that are released at once, the way the other fixtures run theirs; the reference to the released
        // DataManager row is not read by the setting-changed event.
        private static void InitialiseAnalytics() => Owned.Run(owned =>
        {
            owned.LeaseActions(SystemAction.Shutdown);
            owned.LeaseRow<DataManager>();
            var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>()));
            var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
            owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(exchange)));
            row.Register(exchange);
            RuntimeHelpers.RunClassConstructor(typeof(AnalyticsEventCollector).TypeHandle);
        });

        private static Page NewPage(Owned owned)
        {
            // Number formatting follows the current culture; the pages format with the culture of the machine.
            CultureInfo priorCulture = CultureInfo.CurrentCulture;
            owned.Lease(new ReleaseAction(() => CultureInfo.CurrentCulture = priorCulture));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var page = new Page { Owned = owned, Reported = owned.Lease(new SettingChangedWatch()) };
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            page.Save = owned.Register(new SaveManager());
            page.Settings = new SaveDataSettings();
            Set(page.Save, "m_saveSettings", page.Settings); // loaded by the save flow in the original
            page.Settings.MarkSaved();
            return page;
        }

        internal static T Add<T>(Page page) where T : Component => page.Owned.Host("Lucid settings page", false).AddComponent<T>();

        // The settings text RequestSaveSettings (0x5F0250) serialises; null until the page has saved. The save itself
        // stops at the unloaded save (SaveSynchronous returns before it writes anything).
        internal static string SavedJson(Page page) => Get<string>(page.Save, "m_rawSaveSettings");

        // The setting-changed analytics event. The offline analytics provider releases every pushed event, and an
        // unsupported event type hands out one shared stub, so the stub holds the last event the pages reported.
        // The stub's fields are put back afterwards.
        internal sealed class SettingChangedWatch : IDisposable
        {
            private const string Unsent = "<unsent>";
            private readonly SettingChanged m_stub = SettingChanged.Create();
            private readonly string m_name, m_old, m_new;

            internal SettingChangedWatch()
            {
                m_name = m_stub.SettingName;
                m_old = m_stub.OldValue;
                m_new = m_stub.NewValue;
                Clear();
            }

            internal void Clear()
            {
                m_stub.SettingName = Unsent;
                m_stub.OldValue = Unsent;
                m_stub.NewValue = Unsent;
            }

            internal bool Sent => m_stub.SettingName != Unsent;

            internal (string name, string oldValue, string newValue) Last => (m_stub.SettingName, m_stub.OldValue, m_stub.NewValue);

            public void Dispose()
            {
                m_stub.SettingName = m_name;
                m_stub.OldValue = m_old;
                m_stub.NewValue = m_new;
            }
        }
    }
}
