using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The registered services the on-screen controls talk to: the CharacterManager, SaveManager, App and
    // DataManager of CharacterTriggerTestSupport.WithManager (the fixtures of OwnedCharacterManagers), a character
    // that is not yet current, and a real MessageManager registered into its absent row. The MessageManager
    // constructor subscribes Shutdown, so its action subscriptions are withdrawn before its row is released.
    internal static class OnScreenControlTestServices
    {
        internal sealed class Services
        {
            internal Owned Owned;
            internal CharacterManager Characters;
            internal MessageManager Messages;
            internal SaveManager Save;
            internal Character Character;
        }

        internal static void With(Action<Services> body) => WithManager((owned, characters, character) =>
        {
            var services = new Services { Owned = owned, Characters = characters, Character = character };
            services.Save = ProcessManager.GetSystem<SaveManager>();
            services.Messages = RegisterMessageManager(owned);
            body(services);
        });

        // The detector fixtures' registration: row first, the action clean-up released before the row.
        internal static MessageManager RegisterMessageManager(Owned owned)
        {
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageManager>());
            owned.Lease(row);
            var manager = new MessageManager();
            owned.Lease(new ReleaseAction(() =>
            {
                manager.ProcessSystemAction(SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(manager);
                ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.ScriptableObjectMessagesWithoutCompletion);
            }));
            row.Register(manager);
            return manager;
        }

        // Records every message of type T published under the sender, and completes it.
        internal static List<T> Capture<T>(MessageManager manager, Component sender)
        {
            var received = new List<T>();
            manager.ComponentMessagesWithCompletion.SubscribeToMessage(sender, new MessageCallbackWithCompletion<T>(
                (in T message, Action completed) =>
                {
                    received.Add(message);
                    completed?.Invoke();
                }));
            return received;
        }

        // A screen controls request published under the sender; returns how many times the publisher's
        // completion callback has run once the subscribers have finished.
        internal static int Request(MessageManager manager, Component sender, ScreenControlsGroup group)
        {
            int completions = 0;
            manager.ComponentMessagesWithCompletion.PublishMessage<ScreenControlsRequestMessage>(
                sender, new ScreenControlsRequestMessage { OnScreenControlsGroup = group }, () => completions++, -1);
            return completions;
        }

        // The Target of every handler the manager currently holds for character changes.
        internal static object[] CharacterChangeTargets(CharacterManager manager)
        {
            var handlers = Get<Action<Character>>(manager, "m_onCharacterChange");
            if (handlers == null) return new object[0];
            var targets = new List<object>();
            foreach (Delegate handler in handlers.GetInvocationList()) targets.Add(handler.Target);
            return targets.ToArray();
        }
    }
}
