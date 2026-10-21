using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UITargetingListener (Game.Runtime 0x02000952, x86-64 0x683DF0..0x684760) as a real component on an inactive owned host over a
    // real CharacterManager (built by its own constructor), a real MessageManager and real Characters on inactive hosts. The
    // messages are published on the manager's component exchange under the character, as the abilities publish them.
    public sealed class UITargetingListenerTests
    {
        // The first lease owns the action rows, the App and the other rows the CharacterManager constructor reads, and is released
        // last. The MessageManager and its five exchanges subscribe to Shutdown on construction, so each is shut down and
        // unsubscribed before its row is released.
        private static void Run(Action<Owned, CharacterManager, MessageManager> body) => Owned.Run(owned =>
        {
            OwnedCharacterManagers characters = owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<LevelManager>();
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageManager>());
            owned.Lease(row);
            var messages = new MessageManager();
            owned.Lease(new ReleaseAction(() =>
            {
                messages.ProcessSystemAction(SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(messages);
                ProcessManager.UnsubscribeFromAllActions(messages.ComponentMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(messages.StringMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(messages.ComponentMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(messages.StringMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(messages.ScriptableObjectMessagesWithoutCompletion);
            }));
            row.Register(messages);
            CharacterManager manager = owned.Register(characters.NewManager());
            body(owned, manager, messages);
        });

        private static Character NewCharacter(Owned owned) => TriggerConditionTestSupport.NewCharacter(owned, new FSMStorage(0));

        private static UITargetingListener NewListener(Owned owned) =>
            owned.Host("Lucid targeting listener", false).AddComponent<UITargetingListener>();

        // Publishes under the sender; the number of times the finished callback ran comes back (it runs once every subscriber
        // has called its completion callback).
        private static int Publish<T>(MessageManager messages, Character sender, T message)
        {
            int finished = 0;
            messages.ComponentMessagesWithCompletion.PublishMessage(sender, in message, () => finished++);
            return finished;
        }

        private static readonly HomingTargetUpdateMessage Targeting = new HomingTargetUpdateMessage { IsValid = true, IsActive = true };
        private static readonly SurfaceDetectorUpdateMessage Detector = new SurfaceDetectorUpdateMessage { Type = HomingTargetType.LightspeedDash };
        private static readonly BurstAttackUpdateMessage Burst = new BurstAttackUpdateMessage { CanTrigger = true, IsActive = true };

        // dump.cs TypeDefIndex 2385 and the DummyDll rows (fields 0x04002672..0x04002676).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UITargetingListener), 0x00100001,
                new (string, int)[]
                {
                    ("TargetingActiveData", 0x0026), ("DetectorActiveData", 0x0026), ("BurstAttackActiveData", 0x0026),
                    ("m_messageManagerRef", 0x0021), ("m_characterManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("OnTargetingActive", 0x0081), ("OnDetectorActive", 0x0081),
                    ("OnBurstAttackActive", 0x0081), ("OnCharacterChange", 0x0081), ("UnsubscribeCharacterMessages", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UITargetingListener), 1, 2);
            Assert.That(typeof(UITargetingListener).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        // The constructor 0x6845C0 builds the three bindables in field order and then takes the MessageManager (+0x38) and
        // CharacterManager (+0x40) references.
        [Test]
        public void ConstructorBuildsTheBindablesAndTakesBothRegistryReferences()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);

                Assert.That(listener.TargetingActiveData, Is.Not.Null);
                Assert.That(listener.DetectorActiveData, Is.Not.Null);
                Assert.That(listener.BurstAttackActiveData, Is.Not.Null);
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.False);
                Assert.That(listener.DetectorActiveData.Value.Type, Is.EqualTo(default(HomingTargetType)));
                Assert.That(listener.BurstAttackActiveData.Value.CanTrigger, Is.False);
                Assert.That(Get<SystemRef<MessageManager>>(listener, "m_messageManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<MessageManager>()));
                Assert.That(Get<SystemRef<CharacterManager>>(listener, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));
            });
        }

        // 0x6841A0, 0x684210, 0x6842B0: each handler stores its message in its own bindable and then runs the completion callback
        // (an absent one is skipped, 0x6841EE test/je).
        [Test]
        public void EachMessageHandlerStoresItsMessageThenCompletes()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                int completed = 0;
                Action completion = () => completed++;

                Call(listener, "OnTargetingActive", Targeting, completion);
                Call(listener, "OnDetectorActive", Detector, completion);
                Call(listener, "OnBurstAttackActive", Burst, completion);

                Assert.That(completed, Is.EqualTo(3));
                Assert.That((listener.TargetingActiveData.Value.IsValid, listener.TargetingActiveData.Value.IsActive), Is.EqualTo((true, true)));
                Assert.That(listener.DetectorActiveData.Value.Type, Is.EqualTo(HomingTargetType.LightspeedDash));
                Assert.That((listener.BurstAttackActiveData.Value.CanTrigger, listener.BurstAttackActiveData.Value.IsChaosControl,
                    listener.BurstAttackActiveData.Value.IsActive), Is.EqualTo((true, false, true)));

                Assert.DoesNotThrow(() => Call(listener, "OnTargetingActive", default(HomingTargetUpdateMessage), null));
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.False, "the value is still stored without a completion callback");
            });
        }

        // OnCharacterChange 0x684300 subscribes all three messages under the new character: published messages reach the bindables,
        // and each publish finishes (the handlers complete).
        [Test]
        public void ACharacterChangeSubscribesTheThreeMessagesUnderTheNewCharacter()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                Character character = NewCharacter(owned);

                Call(listener, "OnCharacterChange", character);

                Assert.That(Publish(messages, character, Targeting), Is.EqualTo(1));
                Assert.That(Publish(messages, character, Detector), Is.EqualTo(1));
                Assert.That(Publish(messages, character, Burst), Is.EqualTo(1));
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True);
                Assert.That(listener.DetectorActiveData.Value.Type, Is.EqualTo(HomingTargetType.LightspeedDash));
                Assert.That(listener.BurstAttackActiveData.Value.CanTrigger, Is.True);

                Character other = NewCharacter(owned);
                Publish(messages, other, new HomingTargetUpdateMessage { IsValid = false, IsActive = false });
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True, "only the subscribed character reaches the listener");
            });
        }

        // 0x6843A2..0x684499: a character change first writes the default targeting and detector messages, but not the burst
        // attack one, and only then subscribes.
        [Test]
        public void ACharacterChangeClearsTheTargetingAndDetectorDataButNotTheBurstData()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                Call(listener, "OnTargetingActive", Targeting, null);
                Call(listener, "OnDetectorActive", Detector, null);
                Call(listener, "OnBurstAttackActive", Burst, null);

                Call(listener, "OnCharacterChange", NewCharacter(owned));

                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.False);
                Assert.That(listener.TargetingActiveData.Value.IsActive, Is.False);
                Assert.That(listener.DetectorActiveData.Value.Type, Is.EqualTo(default(HomingTargetType)));
                Assert.That(listener.BurstAttackActiveData.Value.CanTrigger, Is.True, "the burst data is left as it was");
            });
        }

        // UnsubscribeCharacterMessages 0x683F60 withdraws the three subscriptions from the manager's CURRENT character: with that
        // character the one being replaced, a change moves the listener to the new character; with no current character it
        // withdraws nothing and the old subscriptions stay.
        [Test]
        public void ACharacterChangeWithdrawsTheSubscriptionsOfTheManagersCurrentCharacter()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                Character first = NewCharacter(owned), second = NewCharacter(owned);
                Call(listener, "OnCharacterChange", first);
                SetCurrent(manager, first, true);

                Call(listener, "OnCharacterChange", second);

                Publish(messages, first, Targeting);
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.False, "no subscriber is left under the first character");
                Assert.That(Publish(messages, second, Targeting), Is.EqualTo(1));
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True);
            });
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                Character first = NewCharacter(owned), second = NewCharacter(owned);
                Call(listener, "OnCharacterChange", first);

                Call(listener, "OnCharacterChange", second);

                Assert.That(Publish(messages, first, Targeting), Is.EqualTo(1));
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True, "nothing was withdrawn without a current character");
                listener.TargetingActiveData.Value = default;
                Assert.That(Publish(messages, second, Targeting), Is.EqualTo(1));
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True);
            });
        }

        // Start 0x683DF0 registers OnCharacterChange with the CharacterManager (invoking it now for a valid character) and OnDestroy
        // 0x683E90 first withdraws the current character's subscriptions and then releases that registration.
        [Test]
        public void StartFollowsTheCharacterManagerAndDestroyLetsGo()
        {
            Run((owned, manager, messages) =>
            {
                UITargetingListener listener = NewListener(owned);
                Character character = NewCharacter(owned);
                SetCurrent(manager, character, true);

                Call(listener, "Start");

                Assert.That(HandlerNames(Get<Delegate>(manager, "m_onCharacterChange")), Has.Exactly(1).EqualTo("OnCharacterChange"));
                Assert.That(Publish(messages, character, Targeting), Is.EqualTo(1), "the current character was subscribed at once");
                Assert.That(listener.TargetingActiveData.Value.IsValid, Is.True);

                Call(listener, "OnDestroy");

                Assert.That(HandlerNames(Get<Delegate>(manager, "m_onCharacterChange")), Has.None.EqualTo("OnCharacterChange"));
                Publish(messages, character, new HomingTargetUpdateMessage { IsValid = false, IsActive = false });
                Assert.That(listener.TargetingActiveData.Value.IsActive, Is.True, "the listener no longer receives the character's messages");
            });
        }
    }
}
