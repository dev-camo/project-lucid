using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine.Events;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnScreenControlGroupListener (02000836) against its 1.10.1 x86-64 bodies (0x607FE0..0x608590). The listener is a
    // component on an owned inactive host; each case calls the original methods itself. The message cases use the
    // services of OnScreenControlTestServices. Not covered: an Awake before the MessageManager exists, whose
    // deferred subscription leaves pending work in the registry row that the fixture ownership does not admit.
    public sealed class OnScreenControlGroupListenerTests
    {
        private sealed class Events
        {
            internal readonly List<string> Log = new List<string>();
            internal readonly UnityEvent Show = new UnityEvent();
            internal readonly UnityEvent Hide = new UnityEvent();
            internal Events() { Show.AddListener(() => Log.Add("show-event")); Hide.AddListener(() => Log.Add("hide-event")); }
        }

        private static OnScreenControlGroupListenerProbe NewListener(Owned owned, Events events, params ScreenControlsGroup[] groups)
        {
            var listener = owned.Host("Lucid owned control group listener", false).AddComponent<OnScreenControlGroupListenerProbe>();
            Set(listener, "m_controlGroups", new List<ScreenControlsGroup>(groups)); // serialized in the original
            Set(listener, "m_onShowEvent", events.Show);
            Set(listener, "m_onHideEvent", events.Hide);
            return listener;
        }

        // Show 0x608510 and Hide 0x608550: the event is invoked when it exists, then the completion when it exists.
        [Test]
        public void ShowAndHideRaiseTheirEventBeforeTheCompletion()
        {
            Owned.Run(owned =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(owned, events);
                listener.ShowNow(() => events.Log.Add("completed"));
                listener.HideNow(() => events.Log.Add("completed"));
                Assert.That(events.Log, Is.EqualTo(new[] { "show-event", "completed", "hide-event", "completed" }));

                events.Log.Clear();
                Assert.DoesNotThrow(() => { listener.ShowNow(null); listener.HideNow(null); }, "the completion is optional");
                Assert.That(events.Log, Is.EqualTo(new[] { "show-event", "hide-event" }));

                Set(listener, "m_onShowEvent", null);
                Set(listener, "m_onHideEvent", null);
                events.Log.Clear();
                Assert.DoesNotThrow(() => { listener.ShowNow(() => events.Log.Add("completed")); listener.HideNow(() => events.Log.Add("completed")); }, "the events are optional");
                Assert.That(events.Log, Is.EqualTo(new[] { "completed", "completed" }));
            });
        }

        // OnToggleScreenControlGroup 0x608340 shows when the message's group is in the list (List.Contains, then xor 1
        // selects vtable slot 4 Show for true and slot 5 Hide for false) and passes the completion on.
        [Test]
        public void ToggleShowsForListedGroupsAndHidesForOthers()
        {
            Owned.Run(owned =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(owned, events, ScreenControlsGroup.InAir);
                Action completed = () => events.Log.Add("completed");

                listener.Toggle(new ScreenControlGroupMessageData { GroupToEnable = ScreenControlsGroup.InAir }, completed);
                Assert.That(events.Log, Is.EqualTo(new[] { "show-event", "completed" }));
                events.Log.Clear();
                listener.Toggle(new ScreenControlGroupMessageData { GroupToEnable = ScreenControlsGroup.Default }, completed);
                Assert.That(events.Log, Is.EqualTo(new[] { "hide-event", "completed" }));
                events.Log.Clear();
                listener.Toggle(new ScreenControlGroupMessageData { GroupToEnable = ScreenControlsGroup.None }, null);
                Assert.That(events.Log, Is.EqualTo(new[] { "hide-event" }));
            });
        }

        [Test]
        public void AnEmptyGroupListAlwaysHides()
        {
            Owned.Run(owned =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(owned, events);
                listener.Toggle(new ScreenControlGroupMessageData { GroupToEnable = ScreenControlsGroup.Default }, null);
                Assert.That(events.Log, Is.EqualTo(new[] { "hide-event" }));
            });
        }

        // OnEnable 0x608440 evaluates the container's last announced group (its field +0x70, GroupToEnable at +0x10), or
        // the Default group (0x23E4013A) without a container or before the container has announced anything.
        [Test]
        public void EnablingAppliesTheDefaultGroupWithoutAContainer()
        {
            Owned.Run(owned =>
            {
                var shown = new Events();
                Call(NewListener(owned, shown, ScreenControlsGroup.Default), "OnEnable");
                Assert.That(shown.Log, Is.EqualTo(new[] { "show-event" }));

                var hidden = new Events();
                Call(NewListener(owned, hidden, ScreenControlsGroup.InAir), "OnEnable");
                Assert.That(hidden.Log, Is.EqualTo(new[] { "hide-event" }));
            });
        }

        [Test]
        public void EnablingWithAContainerThatHasNotAnnouncedUsesTheDefaultGroup()
        {
            Owned.Run(owned =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(owned, events, ScreenControlsGroup.Default);
                Set(listener, "m_uiContainerOnScreenControls", owned.Host("Lucid owned on-screen controls", false).AddComponent<UIContainerOnScreenControls>());
                Call(listener, "OnEnable");
                Assert.That(events.Log, Is.EqualTo(new[] { "show-event" }));
            });
        }

        // A listener subscribed to a container follows the groups the container announces (Awake 0x607FE0 subscribes
        // through InvokeOnValid, which runs at once for a registered MessageManager), uses the container's last
        // announcement when enabled, and stops listening on destroy (0x608110).
        [Test]
        public void ListenerFollowsTheContainersAnnouncementsUntilDestroyed()
        {
            With(services =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(services.Owned, events, ScreenControlsGroup.Default);
                var container = services.Owned.Host("Lucid owned on-screen controls", false).AddComponent<UIContainerOnScreenControls>();
                Set(listener, "m_subscribeToMessagesOnComponent", container); // serialized in the original
                Set(listener, "m_uiContainerOnScreenControls", container);
                Call(listener, "Awake");

                Call(container, "Awake");
                Assert.That(events.Log, Is.EqualTo(new[] { "show-event" }), "the container announces the Default group");

                events.Log.Clear();
                int completions = 0;
                Call(container, "PublishToggleScreenGroupMessage", ScreenControlsGroup.InAir, (Action)(() => completions++));
                Assert.That(events.Log, Is.EqualTo(new[] { "hide-event" }));
                Assert.That(completions, Is.EqualTo(1), "the publisher's completion follows the listener's");

                events.Log.Clear();
                Call(listener, "OnEnable");
                Assert.That(events.Log, Is.EqualTo(new[] { "hide-event" }), "enabled while the container's last group is InAir");

                Call(listener, "OnDestroy");
                events.Log.Clear();
                Call(container, "PublishToggleScreenGroupMessage", ScreenControlsGroup.Default, (Action)(() => { }));
                Assert.That(events.Log, Is.Empty);
            });
        }

        // Awake 0x607FE0 subscribes only when a publisher component is assigned (Unity inequality with null), and
        // OnDestroy 0x608110 leaves at once without one.
        [Test]
        public void WithoutAPublisherNothingIsSubscribed()
        {
            With(services =>
            {
                var events = new Events();
                OnScreenControlGroupListenerProbe listener = NewListener(services.Owned, events, ScreenControlsGroup.Default);
                var container = services.Owned.Host("Lucid owned on-screen controls", false).AddComponent<UIContainerOnScreenControls>();
                Call(listener, "Awake");
                Call(container, "Awake");
                Assert.That(events.Log, Is.Empty);
                Assert.DoesNotThrow(() => Call(listener, "OnDestroy"));
            });
        }
    }
}
