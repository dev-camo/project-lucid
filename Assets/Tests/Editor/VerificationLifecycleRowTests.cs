using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Controls for VerificationFixtureOwnership.LifecycleRow, the admission
    // LoggingHostVerification uses for the logging-host and configuration rows.
    // Every row is detached and every registry is test-owned.
    public sealed class VerificationLifecycleRowTests
    {
        private sealed class IsolatedRow
        {
            public readonly SystemRef SystemRef;
            public readonly IDictionary SystemRefDictionary = new Dictionary<string, object>();
            public IsolatedRow(SystemRef reference) { SystemRef = reference; }
        }

        private static string HostName => ProcessManager.GetDefaultName<HLUnityCore>();
        private static string ConfigurationName => ProcessManager.GetDefaultName<SystemConfiguration>();

        private static object Pending<T>(SystemRef<T> reference) where T : class, ISystem =>
            typeof(SystemRef<T>).GetField("m_actionOnSystemValid", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);

        private static void Observe<T>(SystemRef<T> reference, int kind, Action<T> work) where T : class, ISystem
        {
            if (kind == 0) reference.OnSystemStartup += work;
            else if (kind == 1) reference.OnSystemShutdown += work;
            else reference.InvokeOnValid(work);
        }

        // kind 0 startup, 1 shutdown, 2 deferred, 3 registered system; on the
        // untyped or the typed reference. A value-only IsSystemNull guard
        // admits kinds 0-2 and the typed kind 3.
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(false, 3)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        [TestCase(true, 3)]
        public void LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(bool onTyped, int kind)
        {
            int calls = 0;
            Action<ISystem> untypedWork = value => { ++calls; throw new InvalidOperationException("foreign work"); };
            Action<HLUnityCore> typedWork = value => { ++calls; throw new InvalidOperationException("foreign work"); };
            var system = (HLUnityCore)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(HLUnityCore));
            var untyped = new SystemRef(HostName, kind == 3 && !onTyped ? system : null);
            var typed = new SystemRef<HLUnityCore>(HostName, kind == 3 && onTyped ? system : null);
            if (kind != 3)
            {
                if (onTyped) Observe(typed, kind, typedWork);
                else Observe(untyped, kind, untypedWork);
            }
            var row = new IsolatedRow(untyped);
            row.SystemRefDictionary.Add(HostName, typed);
            IDictionary registry = new Dictionary<string, object> { { HostName, row } };
            object untypedPending = Pending(untyped), typedPending = Pending(typed);

            StringAssert.Contains("retains unowned state",
                Assert.Throws<InvalidOperationException>(() => new VerificationFixtureOwnership.LifecycleRow(registry, HostName)).Message);
            Assert.AreSame(row, registry[HostName]);
            Assert.AreSame(typed, row.SystemRefDictionary[HostName]);
            Assert.AreEqual(1, row.SystemRefDictionary.Count);
            Assert.AreEqual(0, calls);
            Assert.AreSame(untypedPending, Pending(untyped));
            Assert.AreSame(typedPending, Pending(typed));
            Assert.AreSame(kind == 3 && !onTyped ? system : null, untyped.GetSafe());
            Assert.AreSame(kind == 3 && onTyped ? system : null, typed.GetSafe());
        }

        // RunEngine borrows a registered configuration (AddConfig then never
        // registers), but a dormant one would be registered into, so its
        // observers and deferred work refuse it.
        [Test]
        public void ConfigurationRowIsBorrowedWhenLiveAndRefusedWhenDormantWithForeignWork()
        {
            int calls = 0;
            Action<ISystem> work = value => ++calls;
            var live = new SystemRef(ConfigurationName, new SystemConfiguration());
            live.OnSystemShutdown += work;
            var liveRow = new IsolatedRow(live);
            IDictionary registry = new Dictionary<string, object> { { ConfigurationName, liveRow } };
            var borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, ConfigurationName, borrowLiveSystem: true);
            Assert.IsTrue(borrowed.Existed && borrowed.BorrowsLiveSystem);
            borrowed.Dispose();
            Assert.AreSame(liveRow, registry[ConfigurationName]);
            Assert.IsTrue(live.IsValid());
            Assert.Throws<InvalidOperationException>(() => new VerificationFixtureOwnership.LifecycleRow(registry, ConfigurationName),
                "Without borrowLiveSystem a registered row is refused.");

            var dormant = new SystemRef(ConfigurationName, null);
            dormant.InvokeOnValid(work);
            var dormantRow = new IsolatedRow(dormant);
            registry[ConfigurationName] = dormantRow;
            object pending = Pending(dormant);
            Assert.Throws<InvalidOperationException>(() => new VerificationFixtureOwnership.LifecycleRow(registry, ConfigurationName, borrowLiveSystem: true));
            Assert.AreSame(dormantRow, registry[ConfigurationName]);
            Assert.AreSame(pending, Pending(dormant));
            Assert.AreEqual(0, calls);
        }

        // An absent row is the proof's own and is removed once idle; an idle
        // borrowed row stays. A created row that still holds state stays and is
        // reported rather than removed or cleared.
        [Test]
        public void ReleaseRemovesOnlyAnIdleCreatedRow()
        {
            IDictionary registry = new Dictionary<string, object>();
            var created = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            Assert.IsFalse(created.Existed);
            var createdRow = new IsolatedRow(new SystemRef(HostName, null));
            registry.Add(HostName, createdRow);
            created.Dispose();
            Assert.IsFalse(registry.Contains(HostName));

            var idleRow = new IsolatedRow(new SystemRef(HostName, null));
            registry.Add(HostName, idleRow);
            var borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            Assert.IsTrue(borrowed.Existed && !borrowed.BorrowsLiveSystem);
            borrowed.Dispose();
            Assert.AreSame(idleRow, registry[HostName]);

            registry.Remove(HostName);
            var retained = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            int calls = 0;
            var busy = new SystemRef(HostName, null);
            busy.InvokeOnValid(value => ++calls);
            var busyRow = new IsolatedRow(busy);
            registry.Add(HostName, busyRow);
            Assert.Throws<InvalidOperationException>(retained.Dispose);
            Assert.AreSame(busyRow, registry[HostName]);
            Assert.IsNotNull(Pending(busy));
            Assert.AreEqual(0, calls);
        }
    }
}
