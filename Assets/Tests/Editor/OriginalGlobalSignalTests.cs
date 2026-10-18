using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime GlobalSignalPublisher (0x02000442), GlobalSignalReceiver (0x02000443)
    // and MessageNameKeys (0x020006d9) against a real MessageManager in an owned registry row.
    // The manager and its five exchanges subscribe to Shutdown on construction, so that action
    // row is leased first; each case shuts the exchanges down and unsubscribes them before the
    // row is released. The row is registered before any component caches its typed reference.
    public sealed class OriginalGlobalSignalTests
    {
        private sealed class Messaging
        {
            public VerificationFixtureOwnership.RegistryRow Row;
            public readonly List<Object> Objects = new List<Object>();
            public readonly List<MessageManager> Managers = new List<MessageManager>();

            public MessageManager NewManager()
            {
                var manager = new MessageManager();
                Managers.Add(manager);
                return manager;
            }

            public GlobalSignal Signal(string guid)
            {
                var signal = ScriptableObject.CreateInstance<GlobalSignal>();
                Objects.Add(signal);
                Set(signal, "m_guid", guid);
                return signal;
            }

            public T Component<T>(string name) where T : Component
            {
                var host = new GameObject(name);
                Objects.Add(host);
                host.SetActive(false);
                return host.AddComponent<T>();
            }

            // Releases the message infos the subscriptions hold, then every Shutdown subscriber.
            public void ShutDownManagers()
            {
                foreach (MessageManager manager in Managers)
                {
                    manager.ProcessSystemAction(SystemAction.Shutdown);
                    ProcessManager.UnsubscribeFromAllActions(manager);
                    ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithoutCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithoutCompletion);
                    ProcessManager.UnsubscribeFromAllActions(manager.ScriptableObjectMessagesWithoutCompletion);
                }
            }
        }

        private static void WithMessaging(Action<Messaging> body)
        {
            var actions = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            var messaging = new Messaging();
            VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    messaging.Row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageManager>());
                    body(messaging);
                },
                () => { foreach (Object value in messaging.Objects) if (value != null) Object.DestroyImmediate(value); },
                messaging.ShutDownManagers,
                () => messaging.Row?.Dispose(),
                actions.Dispose);
        }

        private static GlobalSignalReceiver Receiver(Messaging messaging, GlobalSignal signal, List<string> log, string label)
        {
            var receiver = messaging.Component<GlobalSignalReceiver>("Lucid signal receiver " + label);
            Set(receiver, "m_signal", signal);
            var response = new UnityEvent();
            response.AddListener(() => log.Add(label));
            Set(receiver, "m_response", response);
            return receiver;
        }

        private static GlobalSignalPublisher Publisher(Messaging messaging, GlobalSignal signal)
        {
            var publisher = messaging.Component<GlobalSignalPublisher>("Lucid signal publisher");
            Set(publisher, "m_globalSignal", signal);
            return publisher;
        }

        // TypeDefs 0x02000442/0x02000443 and their metadata order; both constructors
        // (0x7A4D40, 0x7A4F30) store GetSystemRef<MessageManager>(null, true). The lambda
        // <OnEnable>b__3_0 is member ordinal 3 after the three fields. Raise 0x7A4CB0 publishes
        // only when TryGet finds a manager, so with an empty row it does nothing.
        [Test]
        public void SignalComponentsKeepTheirDeclarationsAndDropASignalWithoutAManager()
        {
            AssertMembers(typeof(GlobalSignalPublisher), 0x00100001,
                new[] { ("m_globalSignal", 0x0001), ("m_messageManagerRef", 0x0021) },
                new[] { ("Raise", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(GlobalSignalPublisher), 1, 2);
            AssertMembers(typeof(GlobalSignalReceiver), 0x00100001,
                new[] { ("m_signal", 0x0001), ("m_response", 0x0001), ("m_messageManagerRef", 0x0021) },
                new[] { ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnSignalReceived", 0x0081), (".ctor", 0x1886), ("<OnEnable>b__3_0", 0x0081) });
            AssertIl2CppOptions(typeof(GlobalSignalReceiver), 2, 1);

            WithMessaging(messaging =>
            {
                messaging.Row.Register(null);
                GlobalSignal signal = messaging.Signal("lucid-signal");
                GlobalSignalPublisher publisher = Publisher(messaging, signal);
                var log = new List<string>();
                GlobalSignalReceiver receiver = Receiver(messaging, signal, log, "receiver");
                SystemRef<MessageManager> reference = ProcessManager.GetSystemRef<MessageManager>();
                Assert.That(Get<SystemRef<MessageManager>>(publisher, "m_messageManagerRef"), Is.SameAs(reference));
                Assert.That(Get<SystemRef<MessageManager>>(receiver, "m_messageManagerRef"), Is.SameAs(reference));
                Assert.DoesNotThrow(publisher.Raise);
                Assert.DoesNotThrow(() => Call(receiver, "OnDisable"), "OnDisable 0x7A4E50 also uses TryGet");
                Call(receiver, "OnSignalReceived");
                Assert.That(log, Is.EqualTo(new[] { "receiver" }), "0x7A4F20 invokes the response directly");
            });
        }

        // OnEnable 0x7A4DC0 hands <OnEnable>b__3_0 to InvokeOnValid, which subscribes
        // OnSignalReceived to the receiver's signal on the ScriptableObject exchange (+0x30);
        // Raise publishes its signal there and OnDisable unsubscribes the same callback.
        [Test]
        public void ReceiverRespondsOnlyToItsSignalWhileEnabled()
        {
            WithMessaging(messaging =>
            {
                messaging.Row.Register(messaging.NewManager());
                GlobalSignal mine = messaging.Signal("lucid-signal-mine");
                GlobalSignal other = messaging.Signal("lucid-signal-other");
                var log = new List<string>();
                GlobalSignalReceiver receiver = Receiver(messaging, mine, log, "mine");
                GlobalSignalPublisher publishMine = Publisher(messaging, mine);
                GlobalSignalPublisher publishOther = Publisher(messaging, other);

                publishMine.Raise();
                Assert.That(log, Is.Empty, "nothing is subscribed before OnEnable");
                Call(receiver, "OnEnable");
                publishMine.Raise();
                publishOther.Raise();
                Assert.That(log, Is.EqualTo(new[] { "mine" }));
                publishMine.Raise();
                Assert.That(log, Is.EqualTo(new[] { "mine", "mine" }));

                Call(receiver, "OnDisable");
                publishMine.Raise();
                Assert.That(log, Is.EqualTo(new[] { "mine", "mine" }), "OnDisable withdrew the subscription");
            });
        }

        // Without a manager OnEnable's lambda waits on the typed reference, and OnDisable's
        // TryGet (0x7A4EBA) fails, so nothing withdraws it. Registering the manager later runs
        // the lambda, and the disabled receiver still responds.
        [Test]
        public void ADeferredSubscriptionSurvivesDisableAndStartsWithTheManager()
        {
            WithMessaging(messaging =>
            {
                messaging.Row.Register(null);
                GlobalSignal signal = messaging.Signal("lucid-signal-deferred");
                var log = new List<string>();
                GlobalSignalReceiver receiver = Receiver(messaging, signal, log, "deferred");
                Call(receiver, "OnEnable");
                SystemRef<MessageManager> reference = ProcessManager.GetSystemRef<MessageManager>();
                var pending = Get<FastAction<MessageManager>>(reference, "m_actionOnSystemValid");
                Assert.That(pending, Is.Not.Null);
                Assert.That(pending.GetInvocationList().Count, Is.EqualTo(1));
                Assert.That(pending.GetInvocationList()[0].Method.Name, Is.EqualTo("<OnEnable>b__3_0"));
                Assert.That(pending.GetInvocationList()[0].Target, Is.SameAs(receiver));
                Call(receiver, "OnDisable");

                messaging.Row.Register(messaging.NewManager());
                Publisher(messaging, signal).Raise();
                Assert.That(log, Is.EqualTo(new[] { "deferred" }));
            });
        }

        // TypeDef 0x020006d9 is static (0x00100181); the .cctor 0x59C8B0 stores each name's own
        // literal in field order.
        [Test]
        public void MessageNameKeysHoldTheirOwnNames()
        {
            AssertMembers(typeof(MessageNameKeys), 0x00100181,
                new[] { ("OnCharacterRespawn", 0x0036), ("OnCharacterTeleport", 0x0036), ("OnHubInteractionStateChanged", 0x0036), ("OnHubInteractionTextChanged", 0x0036) },
                new[] { (".cctor", 0x1891) });
            AssertIl2CppOptions(typeof(MessageNameKeys), 2, 1);
            Assert.That(new[] { MessageNameKeys.OnCharacterRespawn, MessageNameKeys.OnCharacterTeleport, MessageNameKeys.OnHubInteractionStateChanged, MessageNameKeys.OnHubInteractionTextChanged },
                Is.EqualTo(new[] { "OnCharacterRespawn", "OnCharacterTeleport", "OnHubInteractionStateChanged", "OnHubInteractionTextChanged" }));
        }
    }
}
