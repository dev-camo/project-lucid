using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original HLInput BaseControllerProvider polling, 06000058 with natural
    // iterator 0200001a. The iterator is advanced by hand on an owned provider;
    // no coroutine host, Unity joystick query or controller device is involved.
    public sealed class OriginalControllerPollingGapTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.NonPublic;

        // Counts every live interface read the original body performs.
        private sealed class CountingNames : IReadOnlyList<string>
        {
            public string[] Names = new string[0];
            public int CountReads;
            public int IndexReads;
            public int Count { get { CountReads++; return Names.Length; } }
            public string this[int index] { get { IndexReads++; return Names[index]; } }
            public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("The original body never enumerates the names.");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class OwnedProvider : BaseControllerProvider
        {
            public readonly CountingNames Names = new CountingNames();
            public int NameCalls;
            public OwnedProvider() : base(0.5f) { }
            public override IReadOnlyList<string> GetControllerNames() { NameCalls++; return Names; }
        }

        // MoveNext x86-64 0x1a1b570: one Count read compared with the stored
        // count (+0x10); on change store it, store names[count - 1] or null
        // (+0x38), invoke the FastAction (0x1a1b73a), then an empty loop that
        // reads the live Count once per index plus once to exit (0x1a1b750).
        // Every pass yields the constructor's WaitForSeconds (+0x18). Fails if
        // the notification fires on every poll, the name is refreshed without a
        // count change, the empty loop is dropped, or the loop runs before the
        // notification.
        [Test]
        public void PollingNotifiesOnlyOnCountChangesAndKeepsTheEmptyCountLoop()
        {
            var provider = new OwnedProvider();
            int notifications = 0;
            Action duringNotification = null;
            provider.OnControllerConnectionUpdate = new FastAction();
            provider.OnControllerConnectionUpdate += () => { notifications++; duringNotification?.Invoke(); };
            object wait = typeof(BaseControllerProvider).GetField("m_controllerConnectionPollingWait", Own).GetValue(provider);
            IEnumerator polling = (IEnumerator)typeof(BaseControllerProvider).GetMethod("ControllerConnectionPolling", Own).Invoke(provider, null);
            Assert.That(wait, Is.TypeOf<WaitForSeconds>());
            Assert.That(provider.NameCalls, Is.EqualTo(0), "The iterator is lazy.");

            // Zero names match the initial zero count: one Count read, no notification.
            Assert.That(polling.MoveNext(), Is.True);
            Assert.That(polling.Current, Is.SameAs(wait));
            Assert.That(provider.NameCalls, Is.EqualTo(1));
            Assert.That(provider.Names.CountReads, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(0));
            Assert.That(LastName(provider), Is.Null);

            // Two names: one compare read, then three loop reads after the notification.
            provider.Names.Names = new[] { "first", "second" };
            Assert.That(polling.MoveNext(), Is.True);
            Assert.That(polling.Current, Is.SameAs(wait));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(provider.Names.CountReads, Is.EqualTo(1 + 1 + 3));
            Assert.That(provider.Names.IndexReads, Is.EqualTo(1));
            Assert.That(LastName(provider), Is.EqualTo("second"));

            // Same count with different names: no notification and no name refresh.
            provider.Names.Names = new[] { "third", "fourth" };
            Assert.That(polling.MoveNext(), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(provider.Names.CountReads, Is.EqualTo(5 + 1));
            Assert.That(provider.Names.IndexReads, Is.EqualTo(1));
            Assert.That(LastName(provider), Is.EqualTo("second"));

            // One name; the listener grows the live list before the empty loop,
            // which therefore reads Count four times rather than twice.
            provider.Names.Names = new[] { "fifth" };
            duringNotification = () => provider.Names.Names = new[] { "fifth", "sixth", "seventh" };
            Assert.That(polling.MoveNext(), Is.True);
            duringNotification = null;
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(provider.Names.CountReads, Is.EqualTo(6 + 1 + 4));
            Assert.That(LastName(provider), Is.EqualTo("fifth"), "The name is stored before the notification.");

            // The stored count is still one, so the grown list notifies again.
            Assert.That(polling.MoveNext(), Is.True);
            Assert.That(notifications, Is.EqualTo(3));
            Assert.That(LastName(provider), Is.EqualTo("seventh"));
            Assert.That(provider.Names.CountReads, Is.EqualTo(11 + 1 + 4));

            // Back to zero clears the name and still reads Count once to exit the loop.
            provider.Names.Names = new string[0];
            Assert.That(polling.MoveNext(), Is.True);
            Assert.That(polling.Current, Is.SameAs(wait));
            Assert.That(notifications, Is.EqualTo(4));
            Assert.That(LastName(provider), Is.Null);
            Assert.That(provider.Names.CountReads, Is.EqualTo(16 + 1 + 1));
            Assert.That(provider.NameCalls, Is.EqualTo(6));
        }

        // The original getter was stripped; read the retained backing field.
        private static string LastName(BaseControllerProvider provider) => (string)typeof(BaseControllerProvider)
            .GetField("<LastAddedControllerName>k__BackingField", Own).GetValue(provider);
    }
}
