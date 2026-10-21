using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ButtonBinaryStateMessageListener (Game.Runtime 02000834, bodies 0x607950..0x607E00) against a real
    // MessageManager in an owned registry row. The manager and its five exchanges subscribe to Shutdown on
    // construction, so that action row is leased first; the exchanges are shut down and unsubscribed before
    // the row is released.
    //
    // Not covered: the removal of the OnSystemStartup subscription in OnDestroy (0x607C19), which has no
    // observable effect while the manager stays registered.
    public sealed class ButtonBinaryStateMessageListenerTests
    {
        private sealed class Rig
        {
            public MessageManager Manager;
            public ButtonBinaryStateMessageListener Listener;
            public readonly List<string> Log = new List<string>();
        }

        private static void WithRig(Action<Rig> body) => Owned.Run(owned =>
        {
            owned.LeaseActions(SystemAction.Shutdown);
            var rig = new Rig { Manager = new MessageManager() };
            owned.Register(rig.Manager);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                rig.Manager.ProcessSystemAction(SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager.ComponentMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager.StringMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager.ComponentMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager.StringMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(rig.Manager.ScriptableObjectMessagesWithoutCompletion);
            }));
            rig.Listener = owned.Host("Lucid binary state listener", false).AddComponent<ButtonBinaryStateMessageListener>();
            Set(rig.Listener, "m_onMessageTrue", Event(rig.Log, "true"));
            Set(rig.Listener, "m_onMessageFalse", Event(rig.Log, "false"));
            body(rig);
        });

        private static UnityEvent Event(List<string> log, string label)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(label));
            return unityEvent;
        }

        // Handles complete only through their own callback, which neither handler ever calls.
        private static int PublishStatus(Rig rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage button, bool isActive)
        {
            int completed = 0;
            string key = MessageNameKeys.OnHubInteractionStateChanged;
            var data = new ButtonBinaryStateMessageData(button, isActive);
            rig.Manager.StringMessagesWithCompletion.PublishMessage(in key, in data, () => completed++, -1);
            return completed;
        }

        private static int PublishText(Rig rig, string text)
        {
            int completed = 0;
            string key = MessageNameKeys.OnHubInteractionTextChanged;
            var data = new SimpleStringMessageData(text);
            rig.Manager.StringMessagesWithCompletion.PublishMessage(in key, in data, () => completed++, -1);
            return completed;
        }

        // Constructor 0x607E00 creates the text bindable with no value.
        [Test]
        public void TheTextStartsEmpty()
        {
            WithRig(rig => Assert.That(rig.Listener.CurrentText.Value, Is.Null));
        }

        // Awake 0x607950 takes the MessageManager reference and InvokeOnValid runs SubscribeToMessages
        // (0x607C40) at once for a registered manager. HandleStatusMessageData 0x607D90 compares the button
        // (0x607D9A), then fires the true or false event by IsActive (0x607D9F).
        [Test]
        public void AwakeSubscribesTheStatusMessageForTheConfiguredButton()
        {
            WithRig(rig =>
            {
                Call(rig.Listener, "Awake");

                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, true);
                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, false);
                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Back, true);
                Assert.That(rig.Log, Is.EqualTo(new[] { "true", "false" }), "Interact is the default button; Back is ignored");

                Set(rig.Listener, "m_buttonSubscribedToBinaryStateMessage", ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Back);
                rig.Log.Clear();
                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, true);
                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Back, true);
                Assert.That(rig.Log, Is.EqualTo(new[] { "true" }), "only the configured button is relayed");
            });
        }

        // HandleTextMessageData 0x607DD0 sets CurrentText to the message text through the bindable setter.
        [Test]
        public void AwakeSubscribesTheTextMessage()
        {
            WithRig(rig =>
            {
                Call(rig.Listener, "Awake");
                PublishText(rig, "Press to talk");
                Assert.That(rig.Listener.CurrentText.Value, Is.EqualTo("Press to talk"));
                PublishText(rig, "Press to leave");
                Assert.That(rig.Listener.CurrentText.Value, Is.EqualTo("Press to leave"));
                Assert.That(rig.Log, Is.Empty);
            });
        }

        // Both handlers return without calling `completed` (0x607DC0, 0x607DF1).
        [Test]
        public void NeitherHandlerCompletesTheMessage()
        {
            WithRig(rig =>
            {
                Call(rig.Listener, "Awake");
                Assert.That(PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, true), Is.Zero);
                Assert.That(PublishText(rig, "text"), Is.Zero);
            });
        }

        // The events are tested for null before each Invoke (0x607DA9, 0x607DB6).
        [Test]
        public void UnsetEventsAreSkipped()
        {
            WithRig(rig =>
            {
                Call(rig.Listener, "Awake");
                Set(rig.Listener, "m_onMessageTrue", null);
                Set(rig.Listener, "m_onMessageFalse", null);
                Assert.DoesNotThrow(() => PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, true));
                Assert.DoesNotThrow(() => PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, false));
            });
        }

        // OnDestroy 0x607A40 unsubscribes fresh delegates over the same method and target, which Delegate
        // equality matches, from both exchanges.
        [Test]
        public void OnDestroyUnsubscribesBothHandlers()
        {
            WithRig(rig =>
            {
                Call(rig.Listener, "Awake");
                Call(rig.Listener, "OnDestroy");
                PublishStatus(rig, ButtonBinaryStateMessageData.ButtonSubscribedToBinaryStateMessage.Interact, true);
                PublishText(rig, "after destroy");
                Assert.That(rig.Log, Is.Empty);
                Assert.That(rig.Listener.CurrentText.Value, Is.Null);
            });
        }
    }
}
