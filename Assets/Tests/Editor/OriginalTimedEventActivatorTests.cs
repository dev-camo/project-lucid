using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimedEventActivator (0x020007ef, x86-64 0x5F7310..0x5F74E0) and its
    // private TimedEvent (0x020007f0, 0x5F7490). InternalUpdate and the hooks are called
    // directly. The Editor may send OnValidate while the component is added, and
    // TimeScaledComponent_SDT.OnValidate reads its s_config lookup, so each case installs an owned,
    // empty TimeCategoryLookup in that cache and restores the prior value. OnEnable runs with the
    // inherited m_ignoreEnable set, so the base returns before asking for the TimeManager.
    public sealed class OriginalTimedEventActivatorTests
    {
        private static readonly Type TimedEventType = typeof(TimedEventActivator).GetNestedType("TimedEvent", BindingFlags.NonPublic);
        private static readonly FieldInfo TimeConfig = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);

        private static void WithActivator(Action<TimedEventActivator, List<string>> body) => Owned.Run(owned =>
        {
            object prior = TimeConfig.GetValue(null);
            try
            {
                TimeConfig.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                var activator = owned.Host("Lucid timed event activator", false).AddComponent<TimedEventActivator>();
                body(activator, new List<string>());
            }
            finally
            {
                TimeConfig.SetValue(null, prior);
            }
        });

        private static object TimedEvent(float time, List<string> log, string label)
        {
            object timedEvent = Activator.CreateInstance(TimedEventType);
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(label));
            Set(timedEvent, "m_time", time);
            Set(timedEvent, "m_event", unityEvent);
            return timedEvent;
        }

        private static IList Events(TimedEventActivator activator, params object[] timedEvents)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TimedEventType));
            foreach (object timedEvent in timedEvents) list.Add(timedEvent);
            Set(activator, "m_timedEvents", list);
            return list;
        }

        private static (float time, int index, bool canUpdate) State(TimedEventActivator activator) =>
            (Get<float>(activator, "m_time"), Get<int>(activator, "m_timedEventIndex"), Get<bool>(activator, "m_canUpdate"));

        private static void Update(TimedEventActivator activator, float deltaTime) => Call(activator, "InternalUpdate", deltaTime);

        // Metadata of both TypeDefs. The constructor 0x5F74E0 stores m_loop = true and a new list
        // before the inlined TimeScaledComponent_SDT initializer (Environment, 0x34dab335).
        [Test]
        public void DeclarationsAndConstructorDefaults()
        {
            AssertMembers(typeof(TimedEventActivator), 0x00100001,
                new[] { ("m_delay", 0x0001), ("m_loop", 0x0001), ("m_timedEvents", 0x0001), ("m_manuallyStart", 0x0001),
                        ("m_time", 0x0001), ("m_timedEventIndex", 0x0001), ("m_canUpdate", 0x0001) },
                new[] { ("OnEnable", 0x00c4), ("OnDisable", 0x00c4), ("StartSequence", 0x0086), ("StopSequence", 0x0086),
                        ("InternalUpdate", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(TimedEventActivator), 2, 1);
            Assert.That(typeof(TimedEventActivator).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            AssertMembers(TimedEventType, 0x00102003,
                new[] { ("m_time", 0x0001), ("m_event", 0x0001) },
                new[] { ("Update", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(TimedEventType, 2, 1);
            Assert.That(Tooltip(Field(typeof(TimedEventActivator), "m_loop")),
                Is.EqualTo("Determines whether to loop at the end or to finish, disabling the component."));
            Assert.That(Tooltip(Field(TimedEventType, "m_time")), Is.EqualTo("Time until this event is invoked."));

            WithActivator((activator, log) =>
            {
                Assert.That(Get<bool>(activator, "m_loop"), Is.True);
                Assert.That(Get<IList>(activator, "m_timedEvents"), Is.Not.Null.And.Empty);
                Assert.That(State(activator), Is.EqualTo((0f, 0, false)));
                Assert.That(activator.TimeCategoryEnum, Is.EqualTo(TimeCategory.Environment));
            });
        }

        // Update 0x5F7490 keeps waiting only when the clock is below the event time (ucomiss
        // m_time, time; ja). Otherwise it subtracts its time, invokes and returns true, so a
        // NaN clock or event time fires. The table separates that from an ordered time >= m_time.
        [Test]
        public void TimedEventFiresUnlessTheClockIsBelowItsTime()
        {
            MethodInfo update = TimedEventType.GetMethod("Update");
            foreach ((float eventTime, float clock, bool fires, float after) in new[]
            {
                (2f, 1f, false, 1f), (2f, 2f, true, 0f), (2f, 3.5f, true, 1.5f),
                (2f, float.NaN, true, float.NaN), (float.NaN, 5f, true, float.NaN),
            })
            {
                var log = new List<string>();
                object timedEvent = TimedEvent(eventTime, log, "fired");
                object[] arguments = { clock };
                Assert.That(update.Invoke(timedEvent, arguments), Is.EqualTo(fires), eventTime + " at " + clock);
                Assert.That((float)arguments[0], Is.EqualTo(after), "clock after " + eventTime + " at " + clock);
                Assert.That(log.Count, Is.EqualTo(fires ? 1 : 0));
            }
        }

        // StartSequence 0x5F7350 stores -m_delay (xorps with the sign mask), index 0 and true;
        // StopSequence 0x5F73A0 clears the flag. OnEnable 0x5F7310 starts unless m_manuallyStart
        // after the base hook; OnDisable 0x5F7380 clears the flag after the base hook.
        [Test]
        public void SequenceStartsAtMinusTheDelayUnlessStartedManually()
        {
            WithActivator((activator, log) =>
            {
                Set(activator, "m_delay", 1.5f);
                Set(activator, "m_timedEventIndex", 4);
                activator.StartSequence();
                Assert.That(State(activator), Is.EqualTo((-1.5f, 0, true)));
                activator.StopSequence();
                Assert.That(State(activator), Is.EqualTo((-1.5f, 0, false)));

                Set(activator, "m_delay", 2f);
                Set(activator, "m_manuallyStart", true);
                Set(activator, "m_ignoreEnable", true);
                Call(activator, "OnEnable");
                Assert.That(Get<bool>(activator, "m_ignoreEnable"), Is.False, "the base hook ran first");
                Assert.That(State(activator), Is.EqualTo((-1.5f, 0, false)), "a manual start waits for StartSequence");

                Set(activator, "m_manuallyStart", false);
                Set(activator, "m_ignoreEnable", true);
                Call(activator, "OnEnable");
                Assert.That(State(activator), Is.EqualTo((-2f, 0, true)));
                Call(activator, "OnDisable");
                Assert.That(State(activator), Is.EqualTo((-2f, 0, false)));
            });
        }

        // InternalUpdate 0x5F73B0 returns while m_canUpdate is false or the advanced clock is
        // below zero. It then fires every elapsed event in turn; past the last one it wraps to
        // index 0 when looping and otherwise disables the component, leaving the index past the
        // end and m_canUpdate set (Edit Mode sends no OnDisable; at runtime set_enabled(false)
        // runs OnDisable 0x5F7380, which clears m_canUpdate).
        [Test]
        public void UpdateFiresElapsedEventsAndWrapsOrDisablesAtTheEnd()
        {
            WithActivator((activator, log) =>
            {
                Events(activator, TimedEvent(1f, log, "first"), TimedEvent(2f, log, "second"));
                Update(activator, 5f);
                Assert.That(log, Is.Empty, "not started");
                Assert.That(State(activator), Is.EqualTo((0f, 0, false)));

                Set(activator, "m_delay", 1f);
                activator.StartSequence();
                Update(activator, 0.5f);
                Assert.That((log.Count, State(activator)), Is.EqualTo((0, (-0.5f, 0, true))), "the delay is still running");
                Update(activator, 1f);
                Assert.That((log.Count, State(activator)), Is.EqualTo((0, (0.5f, 0, true))));
                Update(activator, 1f);
                Assert.That(log, Is.EqualTo(new[] { "first" }));
                Assert.That(State(activator), Is.EqualTo((0.5f, 1, true)));
                Update(activator, 1.5f);
                Assert.That(log, Is.EqualTo(new[] { "first", "second" }));
                Assert.That(State(activator), Is.EqualTo((0f, 0, true)), "looping wraps to the first event");
                Assert.That(activator.enabled, Is.True);

                log.Clear();
                Events(activator, TimedEvent(1f, log, "a"), TimedEvent(1f, log, "b"), TimedEvent(1f, log, "c"));
                Set(activator, "m_delay", 0f);
                activator.StartSequence();
                Update(activator, 2.5f);
                Assert.That(log, Is.EqualTo(new[] { "a", "b" }), "every elapsed event fires in one update");
                Assert.That(State(activator), Is.EqualTo((0.5f, 2, true)));

                log.Clear();
                Set(activator, "m_loop", false);
                Update(activator, 0.5f);
                Assert.That(log, Is.EqualTo(new[] { "c" }));
                Assert.That(activator.enabled, Is.False);
                Assert.That(State(activator), Is.EqualTo((0f, 3, true)));
            });
        }

        // The event count is read once (0x5f740b) before the loop, so an event added while the
        // last one fires is not reached; a NaN clock passes the negative test and fires each event.
        [Test]
        public void UpdateUsesTheCountReadBeforeFiringAndANaNClockFiresEverything()
        {
            WithActivator((activator, log) =>
            {
                Set(activator, "m_loop", false);
                IList events = Events(activator);
                object grows = TimedEvent(0f, log, "grows");
                Get<UnityEvent>(grows, "m_event").AddListener(() => events.Add(TimedEvent(0f, log, "added")));
                events.Add(grows);
                activator.StartSequence();
                Update(activator, 0f);
                Assert.That(log, Is.EqualTo(new[] { "grows" }));
                Assert.That(events.Count, Is.EqualTo(2));
                Assert.That(activator.enabled, Is.False);

                log.Clear();
                activator.enabled = true;
                Events(activator, TimedEvent(1f, log, "first"), TimedEvent(2f, log, "second"));
                activator.StartSequence();
                Update(activator, float.NaN);
                Assert.That(log, Is.EqualTo(new[] { "first", "second" }));
                Assert.That(activator.enabled, Is.False);
            });
        }
    }
}
