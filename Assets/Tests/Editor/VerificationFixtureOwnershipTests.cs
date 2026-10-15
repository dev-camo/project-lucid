using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class VerificationFixtureOwnershipTests
    {
        private sealed class Marker : ISystem { }
        private static IDictionary Registry => (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [Test]
        public void SystemRefEntryGuardRejectsBorrowedLoggingStateWithoutCallingIt()
        {
            int calls = 0;
            Action foreign = () => { calls++; throw new InvalidOperationException("foreign logging callback"); };
            Assert.Throws<InvalidOperationException>(() => SystemRefVerification.RequireColdLoggingHost(foreign));
            SystemRefVerification.RequireColdLoggingHost(null);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void GuardPreservesBorrowedRowsReferencesAndCallbacks()
        {
            var owner = new Marker();
            var untyped = new SystemRef("borrowed", owner);
            var typed = new SystemRef<Marker>("borrowed", owner);
            int callbacks = 0;
            untyped.OnSystemShutdown += _ => callbacks++;
            typed.OnSystemShutdown += _ => callbacks++;
            var registry = new Hashtable { ["borrowed"] = untyped };
            Action foreign = () => { callbacks++; throw new InvalidOperationException("foreign"); };
            Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.RequireAbsent(registry, "borrowed", typed, foreign));
            Assert.AreSame(untyped, registry["borrowed"]);
            Assert.AreSame(owner, untyped.GetSafe());
            Assert.AreSame(owner, typed.GetSafe());
            Assert.AreEqual(0, callbacks);
        }

        [Test]
        public void EmptyBorrowedRowAndForeignStaticStateAreAlsoRejected()
        {
            var empty = new SystemRef("borrowed", null);
            var registry = new Hashtable { ["borrowed"] = empty };
            Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.RequireAbsent(registry, "borrowed"));
            Assert.AreSame(empty, registry["borrowed"]);
            int callbacks = 0;
            Func<bool> foreign = () => { callbacks++; return false; };
            Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.RequireAbsent(new Hashtable(), "absent", foreign));
            Assert.AreEqual(0, callbacks);
        }

        [Test]
        public void CollisionRejectionKeepsOwnedSentinelObserversAndActionSubscription()
        {
            // This sentinel is owned by this regression, under a fresh key;
            // the attempted second fixture must treat it as borrowed state.
            string name = "ProjectLucid.OwnershipSentinel." + Guid.NewGuid().ToString("N");
            var sentinel = new Marker();
            SystemRef untyped = ProcessManager.RegisterSystem(sentinel, name);
            object row = Registry[name];
            var typed = ProcessManager.GetSystemRef<Marker>(name);
            int callbacks = 0, updates = 0;
            Action<ISystem> shutdown = _ => callbacks++;
            Action<Marker> typedShutdown = _ => callbacks++;
            untyped.OnSystemShutdown += shutdown;
            typed.OnSystemShutdown += typedShutdown;
            sentinel.SubscribeToAction(SystemAction.Update, _ => updates++);
            try
            {
                sentinel.ProcessSystemAction(SystemAction.Update);
                Assert.Throws<InvalidOperationException>(() => new VerificationFixtureOwnership.RegistryRow(name));
                Assert.AreSame(row, Registry[name]);
                Assert.AreSame(sentinel, untyped.GetSafe());
                Assert.AreSame(sentinel, typed.GetSafe());
                Assert.IsTrue(ProcessManager.IsSystemValid(name));
                sentinel.ProcessSystemAction(SystemAction.Update);
                Assert.AreEqual(2, updates);
                Assert.AreEqual(0, callbacks);
            }
            finally
            {
                untyped.OnSystemShutdown -= shutdown;
                typed.OnSystemShutdown -= typedShutdown;
                ProcessManager.UnregisterSystem(sentinel);
                if (ReferenceEquals(Registry[name], row) && untyped.IsNull()) Registry.Remove(name);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedAcquisitionDoesNotReleaseASeparateOwner(bool useRegistrationCallback)
        {
            string name = "ProjectLucid.AcquisitionSentinel." + Guid.NewGuid().ToString("N");
            var scope = new VerificationFixtureOwnership.RegistryRow(name);
            var sentinel = new Marker();
            SystemRef reference = ProcessManager.RegisterSystem(sentinel, name);
            object row = Registry[name];
            int shutdowns = 0;
            Action<ISystem> shutdown = _ => shutdowns++;
            reference.OnSystemShutdown += shutdown;
            try
            {
                int attemptedCallbacks = 0;
                if (useRegistrationCallback)
                    Assert.Throws<InvalidOperationException>(() => scope.RegisterUsing(new Marker(), () => attemptedCallbacks++));
                else Assert.Throws<InvalidOperationException>(() => scope.Register(new Marker()));
                scope.Dispose();
                Assert.AreSame(row, Registry[name]);
                Assert.AreSame(sentinel, reference.GetSafe());
                Assert.AreEqual(0, shutdowns);
                Assert.AreEqual(0, attemptedCallbacks);
            }
            finally
            {
                reference.OnSystemShutdown -= shutdown;
                ProcessManager.UnregisterSystem(sentinel);
                if (ReferenceEquals(Registry[name], row) && reference.IsNull()) Registry.Remove(name);
            }
        }

        [Test]
        public void DisposalRejectsAReplacementOwnerWithoutRevokingItsReferences()
        {
            string name = "ProjectLucid.ReplacementSentinel." + Guid.NewGuid().ToString("N");
            var scope = new VerificationFixtureOwnership.RegistryRow(name);
            SystemRef reference = scope.Register(new Marker());
            object row = Registry[name];
            var sentinel = new Marker();
            ProcessManager.RegisterSystem(sentinel, name, canReplace: true);
            var typed = ProcessManager.GetSystemRef<Marker>(name);
            int shutdowns = 0;
            Action<ISystem> shutdown = _ => shutdowns++;
            Action<Marker> typedShutdown = _ => shutdowns++;
            reference.OnSystemShutdown += shutdown;
            typed.OnSystemShutdown += typedShutdown;
            try
            {
                Assert.Throws<InvalidOperationException>(() => scope.Dispose());
                Assert.AreSame(row, Registry[name]);
                Assert.AreSame(sentinel, reference.GetSafe());
                Assert.AreSame(sentinel, typed.GetSafe());
                Assert.AreEqual(0, shutdowns);
            }
            finally
            {
                reference.OnSystemShutdown -= shutdown;
                typed.OnSystemShutdown -= typedShutdown;
                ProcessManager.UnregisterSystem(sentinel);
                if (ReferenceEquals(Registry[name], row) && reference.IsNull()) Registry.Remove(name);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StringTableGuardNeverCallsBorrowedReadiness(bool throws)
        {
            int calls = 0;
            Func<bool> foreign = () => { calls++; if (throws) throw new InvalidOperationException("foreign readiness"); return false; };
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireOriginalReadiness(foreign));
            Assert.AreEqual(0, calls);
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireOriginalReadiness(foreign + foreign));
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireOriginalReadiness(null));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void StringTableGuardAcceptsOnlyTheOriginalReadinessInitializer()
        {
            // Positive control: the generated static-lambda delegate the
            // original field initializer installs, rebuilt here rather than
            // read from shared state another fixture or scene may extend.
            // It is never invoked.
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Type closure = typeof(Hardlight.Localisation.StringTable).GetNestedType("<>c", declared);
            MethodInfo method = closure.GetMethod("<.cctor>b__69_0", declared);
            object target = closure.GetField("<>9", declared).GetValue(null);
            var original = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), target, method);
            Assert.DoesNotThrow(() => StringTableVerification.RequireOriginalReadiness(original));
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireOriginalReadiness(original + original));
            Func<bool> sameShape = () => true;
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireOriginalReadiness(sameShape));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void StringTableColdGuardRejectsRetainedRowsAndForeignReferences(int state)
        {
            var registry = new Hashtable();
            var row = new SystemRef("retained row", null);
            int callbacks = 0;
            row.OnSystemStartup += _ => callbacks++;
            row.OnSystemShutdown += _ => callbacks++;
            Action request = () => { callbacks++; throw new InvalidOperationException("foreign request"); };
            string tableName = ProcessManager.GetDefaultName<Hardlight.Localisation.StringTable>();
            string languageName = ProcessManager.GetDefaultName<Hardlight.Localisation.Language>();
            if (state == 0) registry.Add(tableName, row);
            if (state == 1) registry.Add(languageName, row);
            object foreign = new object();
            Assert.Throws<InvalidOperationException>(() => StringTableVerification.RequireColdState(registry,
                state == 2 ? foreign : null, state == 3 ? foreign : null,
                state == 4 ? foreign : null, state == 5 ? request : null));
            Assert.AreEqual(0, callbacks);
            Assert.IsTrue(row.IsNull());
            if (state == 0) Assert.AreSame(row, registry[tableName]);
            if (state == 1) Assert.AreSame(row, registry[languageName]);
        }

        [Test]
        public void StringTableColdGuardAcceptsEmptyStateAndUnrelatedRows()
        {
            // Positive control for the rejection cases above: only the two
            // default-name rows and the four shared references are guarded.
            var registry = new Hashtable();
            Assert.DoesNotThrow(() => StringTableVerification.RequireColdState(registry, null, null, null, null));
            var unrelated = new SystemRef("unrelated row", null);
            registry.Add("ProjectLucid.UnrelatedRow." + Guid.NewGuid().ToString("N"), unrelated);
            Assert.DoesNotThrow(() => StringTableVerification.RequireColdState(registry, null, null, null, null));
            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(unrelated.IsNull());
        }
    }
}
