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

        private const BindingFlags ReferenceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Reads a SystemRef<T> field on any closed reference type.
        private static object Read(object reference, string field)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            return type.GetField(field, ReferenceFields).GetValue(reference);
        }

        private static object Pending(object reference) => Read(reference, "m_actionOnSystemValid");

        private static void Observe<T>(SystemRef<T> reference, int kind, Action<T> work) where T : class, ISystem
        {
            if (kind == 0) reference.OnSystemStartup += work;
            else if (kind == 1) reference.OnSystemShutdown += work;
            else reference.InvokeOnValid(work);
        }

        // kind 0 startup, 1 shutdown, 2 deferred, 3 registered system; on the
        // untyped or a typed reference. A value-only IsSystemNull guard admits
        // kinds 0-2 and the typed kind 3. The registered value is a
        // constructible SystemConfiguration: admission reads only its identity,
        // and the typed kind-3 entry is the row's SystemRef<SystemConfiguration>
        // that GetSystemRef<SystemConfiguration>(HostName) would cache.
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
            var system = new SystemConfiguration();
            var untyped = new SystemRef(HostName, kind == 3 && !onTyped ? system : null);
            object typed;
            string typedKey;
            if (kind == 3 && onTyped)
            {
                typed = new SystemRef<SystemConfiguration>(HostName, system);
                typedKey = ConfigurationName;
            }
            else
            {
                var host = new SystemRef<HLUnityCore>(HostName, null);
                if (kind != 3)
                {
                    if (onTyped) Observe(host, kind, typedWork);
                    else Observe(untyped, kind, untypedWork);
                }
                typed = host;
                typedKey = HostName;
            }
            var row = new IsolatedRow(untyped);
            row.SystemRefDictionary.Add(typedKey, typed);
            IDictionary registry = new Dictionary<string, object> { { HostName, row } };
            object untypedPending = Pending(untyped), typedPending = Pending(typed);

            StringAssert.Contains("retains unowned state",
                Assert.Throws<InvalidOperationException>(() => new VerificationFixtureOwnership.LifecycleRow(registry, HostName)).Message);
            Assert.AreSame(row, registry[HostName]);
            Assert.AreSame(typed, row.SystemRefDictionary[typedKey]);
            Assert.AreEqual(1, row.SystemRefDictionary.Count);
            Assert.AreEqual(0, calls);
            Assert.AreSame(untypedPending, Pending(untyped));
            Assert.AreSame(typedPending, Pending(typed));
            Assert.AreSame(kind == 3 && !onTyped ? system : null, untyped.GetSafe());
            Assert.AreSame(kind == 3 && onTyped ? system : null, Read(typed, "m_isystem"));
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

        // A borrowed idle row keeps its prior typed entries and loses only the
        // cleared entries added while borrowed; an added entry that still holds
        // state refuses release and stays.
        [Test]
        public void BorrowedRowReleaseRemovesOnlyClearedTypedEntriesAddedWhileBorrowed()
        {
            var prior = new SystemRef<HLUnityCore>(HostName, null);
            var idleRow = new IsolatedRow(new SystemRef(HostName, null));
            idleRow.SystemRefDictionary.Add(HostName, prior);
            IDictionary registry = new Dictionary<string, object> { { HostName, idleRow } };

            var borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            idleRow.SystemRefDictionary.Add(ConfigurationName, new SystemRef<SystemConfiguration>(HostName, null));
            borrowed.Dispose();
            Assert.AreSame(idleRow, registry[HostName]);
            Assert.AreEqual(1, idleRow.SystemRefDictionary.Count);
            Assert.AreSame(prior, idleRow.SystemRefDictionary[HostName]);

            var retained = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            int calls = 0;
            var busy = new SystemRef<SystemConfiguration>(HostName, null);
            busy.InvokeOnValid(value => ++calls);
            idleRow.SystemRefDictionary.Add(ConfigurationName, busy);
            StringAssert.Contains("retains unowned state", Assert.Throws<InvalidOperationException>(retained.Dispose).Message);
            Assert.AreSame(busy, idleRow.SystemRefDictionary[ConfigurationName]);
            Assert.IsNotNull(Pending(busy));
            Assert.AreEqual(0, calls);
        }

        // A borrowed live row keeps its system and its prior typed entries, and
        // loses the typed entries the proof's GetSystemRef<T> cached while it was
        // borrowed. An added entry that holds an observer refuses release and stays.
        // The row is a uniquely named one on the shared registry that a
        // RegistryRow admits absent, registers into and removes on release.
        [Test]
        public void BorrowedLiveRowReleaseRemovesOnlyTypedEntriesAddedWhileBorrowed()
        {
            IDictionary registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string name = "ProjectLucid.Tests.BorrowedLiveRow." + Guid.NewGuid().ToString("N");
            var owner = new VerificationFixtureOwnership.RegistryRow(name);
            var others = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in registry) others.Add(entry.Key, entry.Value);
            var system = new SystemConfiguration();
            IDictionary cache = null;
            SystemRef<HLUnityCore> busy = null;
            Action<HLUnityCore> observer = value => Assert.Fail("observer ran");
            try
            {
                SystemRef live = owner.Register(system);
                object row = registry[name];
                cache = (IDictionary)row.GetType().GetField("SystemRefDictionary").GetValue(row);
                Assert.AreEqual(0, cache.Count);

                // No prior typed entry: the one the proof caches is removed.
                var borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, name, borrowLiveSystem: true);
                Assert.IsTrue(borrowed.Existed && borrowed.BorrowsLiveSystem);
                SystemRef<SystemConfiguration> added = ProcessManager.GetSystemRef<SystemConfiguration>(name);
                Assert.AreSame(system, added.GetSafe());
                Assert.AreEqual(1, cache.Count);
                borrowed.Dispose();
                Assert.AreEqual(0, cache.Count);
                Assert.AreSame(row, registry[name]);
                Assert.AreSame(live, ProcessManager.GetSystemRef(name));
                Assert.AreSame(system, live.GetSafe());
                Assert.AreSame(system, added.GetSafe(), "The removed entry is dropped, not revoked.");

                // A prior typed entry stays as admitted; only the added one goes.
                SystemRef<SystemConfiguration> prior = ProcessManager.GetSystemRef<SystemConfiguration>(name);
                borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, name, borrowLiveSystem: true);
                SystemRef<HLUnityCore> mismatched = ProcessManager.GetSystemRef<HLUnityCore>(name);
                Assert.IsNull(mismatched.GetSafe());
                Assert.AreEqual(2, cache.Count);
                borrowed.Dispose();
                Assert.AreEqual(1, cache.Count);
                Assert.AreSame(prior, cache[ProcessManager.GetDefaultName<SystemConfiguration>()]);
                Assert.AreSame(system, prior.GetSafe());
                Assert.AreSame(system, live.GetSafe());

                // An added entry left observed refuses release and stays.
                borrowed = new VerificationFixtureOwnership.LifecycleRow(registry, name, borrowLiveSystem: true);
                busy = ProcessManager.GetSystemRef<HLUnityCore>(name);
                busy.OnSystemShutdown += observer;
                StringAssert.Contains("retains unowned state", Assert.Throws<InvalidOperationException>(borrowed.Dispose).Message);
                Assert.AreSame(busy, cache[HostName]);
                Assert.AreSame(prior, cache[ProcessManager.GetDefaultName<SystemConfiguration>()]);
                Assert.AreSame(system, live.GetSafe());
            }
            finally
            {
                // Put the shared registry back: drop the refused entry before the
                // owner unregisters (which would run its observer) and removes the row.
                if (busy != null)
                {
                    busy.OnSystemShutdown -= observer;
                    cache.Remove(HostName);
                }
                owner.Dispose();
            }
            Assert.IsFalse(registry.Contains(name));
            Assert.AreEqual(others.Count, registry.Count);
            foreach (KeyValuePair<object, object> entry in others) Assert.AreSame(entry.Value, registry[entry.Key]);
        }

        // Wiring control: RunEngine admits the shared logging-host row before
        // any configuration or lifecycle step, so a dormant row with a foreign
        // startup observer is refused before the explicit Awake could register
        // into it and run the observer.
        [Test]
        public void EngineProofRefusesAnObservedDormantHostRowBeforeAnyStep()
        {
            IDictionary registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var seed = new VerificationFixtureOwnership.LifecycleRow(registry, HostName);
            SystemRef dormant = null;
            int calls = 0;
            Action<ISystem> observer = value => ++calls;
            try
            {
                dormant = ProcessManager.GetSystemRef(HostName);
                dormant.OnSystemStartup += observer;
                object row = registry[HostName];
                StringAssert.Contains("retains unowned state", Assert.Throws<InvalidOperationException>(() => LoggingHostVerification.RunEngine()).Message);
                Assert.AreSame(row, registry[HostName]);
                Assert.IsTrue(dormant.IsNull());
                Assert.AreEqual(0, calls, "No lifecycle step registered into the seeded row.");
            }
            finally
            {
                if (dormant != null) dormant.OnSystemStartup -= observer;
                seed.Dispose();
            }
        }
    }
}
