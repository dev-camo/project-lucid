using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UserEditableUIBase (02000994) against its 1.10.1 x86-64 bodies (0x697070..0x697A20), through a concrete subclass
    // that only counts the two hooks. The App, SaveManager and a real message exchange are registered services
    // (OnScreenControlTestServices); the edit events are real UIModernEvent assets published on that exchange.
    public sealed class UserEditableUIBaseTests
    {
        private static readonly FieldInfo GuidField = typeof(ScriptableObjectWithGuid).GetField("m_guid", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class EventSet
        {
            internal UIModernEvent Start;
            internal UIModernEvent Cancel;
            internal UIModernEvent Confirm;
            internal UIModernEvent Flip;
            internal UIModernEvent Reset;
        }

        private static UIModernEvent NewEvent(Owned owned, string guid)
        {
            // ScriptableObjectWithGuid equality and hash use m_guid, and a bare CreateInstance leaves it empty,
            // which would make the five events one key.
            var value = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            GuidField.SetValue(value, guid);
            return value;
        }

        private static MessageExchangeBoundCallbackArg<UIModernMessage> RegisterExchange(Owned owned)
        {
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>());
            owned.Lease(row);
            var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
            owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(exchange)));
            row.Register(exchange);
            return exchange;
        }

        private static void Publish(MessageExchangeBoundCallbackArg<UIModernMessage> exchange, UIModernEvent value) =>
            exchange.PublishMessage(new UIModernMessage(UIModernEventType.OnClick), value);

        private static UserEditableUIBaseProbe NewProbe(Owned owned, out EventSet events)
        {
            var probe = owned.Host("Lucid owned editable base", false).AddComponent<UserEditableUIBaseProbe>();
            events = new EventSet
            {
                Start = NewEvent(owned, "lucid-edit-start"), Cancel = NewEvent(owned, "lucid-edit-cancel"), Confirm = NewEvent(owned, "lucid-edit-confirm"),
                Flip = NewEvent(owned, "lucid-edit-flip"), Reset = NewEvent(owned, "lucid-edit-reset"),
            };
            Set(probe, "m_editStartEvent", events.Start); // serialized in the original
            Set(probe, "m_editCancelEvent", events.Cancel);
            Set(probe, "m_editConfirmEvent", events.Confirm);
            Set(probe, "m_editFlipEvent", events.Flip);
            Set(probe, "m_editResetEvent", events.Reset);
            return probe;
        }

        // Awake 0x697100 clears the state, takes the auto-created UIModernMessage exchange, binds each of the five
        // events under OnClick to its own handler (start 0x697810, cancel 0x697840, confirm 0x697860, flip 0x6979D0,
        // reset 0x6979E0) and defers into the SaveManager reference, which delivers the settings at once.
        [Test]
        public void EachEditEventDrivesItsOwnHandler()
        {
            With(services =>
            {
                MessageExchangeBoundCallbackArg<UIModernMessage> exchange = RegisterExchange(services.Owned);
                var settings = new SaveDataSettings();
                Set(services.Save, "m_saveSettings", settings); // loaded by the save flow in the original
                UserEditableUIBaseProbe probe = NewProbe(services.Owned, out EventSet events);

                Call(probe, "Awake");
                Assert.That(probe.Settings, Is.SameAs(settings));
                Assert.That((probe.Initialised, probe.EditMode, probe.Flipped, probe.EditModeActive.Value), Is.EqualTo((false, false, false, false)));

                Publish(exchange, events.Start);
                Assert.That((probe.EditMode, probe.EditModeActive.Value, probe.Flipped), Is.EqualTo((true, true, false)), "start");
                Publish(exchange, events.Flip);
                Assert.That((probe.EditMode, probe.Flipped), Is.EqualTo((true, true)), "flip");
                Publish(exchange, events.Flip);
                Assert.That(probe.Flipped, Is.False, "flip again");
                Publish(exchange, events.Flip);
                Publish(exchange, events.Reset);
                Assert.That((probe.EditMode, probe.Flipped), Is.EqualTo((true, false)), "reset clears the flip and keeps editing");
                Publish(exchange, events.Cancel);
                Assert.That((probe.EditMode, probe.EditModeActive.Value), Is.EqualTo((false, false)), "cancel");

                Publish(exchange, events.Start);
                Assert.That(ProcessManager.GetSystem<App>().Storage.TryGetValue(AppFSMKeys.GameSettings, out SaveDataSettings _), Is.False,
                    "nothing is published to the app storage before a confirm");
                Publish(exchange, events.Confirm);
                Assert.That(probe.EditMode, Is.False, "confirm leaves edit mode");
                Assert.That(ProcessManager.GetSystem<App>().Storage.TryGetValue(AppFSMKeys.GameSettings, out SaveDataSettings stored), Is.True);
                Assert.That(stored, Is.SameAs(settings), "the settings object itself is published");
            });
        }

        // OnDestroy 0x6973D0 clears the initialised flag and unsubscribes the five handlers, so later events are ignored.
        [Test]
        public void DestroyingStopsListeningToTheEditEvents()
        {
            With(services =>
            {
                MessageExchangeBoundCallbackArg<UIModernMessage> exchange = RegisterExchange(services.Owned);
                Set(services.Save, "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
                UserEditableUIBaseProbe probe = NewProbe(services.Owned, out EventSet events);
                Call(probe, "Awake");
                Publish(exchange, events.Start);
                Assert.That(probe.EditMode, Is.True);
                Call(probe, "OnDestroy");
                Publish(exchange, events.Cancel);
                Assert.That(probe.EditMode, Is.True, "cancel is no longer heard");
                Publish(exchange, events.Flip);
                Assert.That(probe.Flipped, Is.False, "flip is no longer heard");
            });
        }

        // OnDestroy leaves at once when Awake never took the exchange (the null test at 0x697411).
        [Test]
        public void DestroyingWithoutAnExchangeOnlyClearsTheInitialisedFlag()
        {
            With(services =>
            {
                UserEditableUIBaseProbe probe = NewProbe(services.Owned, out EventSet _);
                Assert.DoesNotThrow(() => Call(probe, "OnDestroy"));
                Assert.That(probe.Initialised, Is.False);
            });
        }

        // Update 0x6975C0 waits for the settings; the first update then initialises (0x697610) and, while editing, runs
        // DoUpdate. The stored ControlsEditMode flag is read as GetValue(false, storeDefault true).
        [Test]
        public void UpdateWaitsForTheSettingsThenInitialises()
        {
            With(services =>
            {
                var settings = new SaveDataSettings { OnScreenControlsFlipped = true };
                Set(services.Save, "m_saveSettings", settings); // loaded by the save flow in the original
                IGraphStorage storage = ProcessManager.GetSystem<App>().Storage;
                UserEditableUIBaseProbe probe = NewProbe(services.Owned, out EventSet _);

                Call(probe, "Update");
                Assert.That((probe.Initialised, probe.InitialiseCount, probe.UpdateCount), Is.EqualTo((false, 0, 0)), "no settings yet");
                Assert.That(storage.TryGetValue(AppFSMKeys.ControlsEditMode, out bool _), Is.False, "the flag is not read yet");

                probe.RunOnSaveManagerValid(services.Save);
                Assert.That(probe.Settings, Is.SameAs(settings));
                storage.SetValue(AppFSMKeys.ControlsEditMode, true);
                Call(probe, "Update");

                Assert.That((probe.Initialised, probe.InitialiseCount, probe.EditMode, probe.EditModeActive.Value, probe.Flipped),
                    Is.EqualTo((true, 1, true, true, true)));
                Assert.That(probe.UpdateCount, Is.EqualTo(1), "the first update also runs DoUpdate while editing");
                Call(probe, "Update");
                Assert.That((probe.InitialiseCount, probe.UpdateCount), Is.EqualTo((1, 2)));
            });
        }

        // With the flag off the edit mode ends up off (the stored flag is stored inverted and Initialise then exits edit
        // mode), the stored default false is written back, the flip comes from the settings and DoUpdate never runs.
        [Test]
        public void UpdateStaysOutOfEditModeWithoutTheStoredFlag()
        {
            With(services =>
            {
                Set(services.Save, "m_saveSettings", new SaveDataSettings { OnScreenControlsFlipped = true }); // loaded by the save flow in the original
                IGraphStorage storage = ProcessManager.GetSystem<App>().Storage;
                UserEditableUIBaseProbe probe = NewProbe(services.Owned, out EventSet _);
                probe.RunOnSaveManagerValid(services.Save);

                Call(probe, "Update");

                Assert.That((probe.Initialised, probe.InitialiseCount, probe.EditMode, probe.EditModeActive.Value, probe.Flipped),
                    Is.EqualTo((true, 1, false, false, true)));
                Assert.That(storage.TryGetValue(AppFSMKeys.ControlsEditMode, out bool stored), Is.True, "the default was stored");
                Assert.That(stored, Is.False);
                Call(probe, "Update");
                Assert.That(probe.UpdateCount, Is.EqualTo(0));
            });
        }
    }
}
