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
        public void RegistrationCallbackFailureAfterPublicationStillReleasesTheOwnedRow()
        {
            string name = "ProjectLucid.PartialRegistration." + Guid.NewGuid().ToString("N");
            string sentinelName = "ProjectLucid.PartialRegistrationSentinel." + Guid.NewGuid().ToString("N");
            var sentinel = new Marker();
            SystemRef sentinelReference = ProcessManager.RegisterSystem(sentinel, sentinelName);
            object sentinelRow = Registry[sentinelName];
            int sentinelShutdowns = 0, updates = 0, shutdowns = 0;
            Action<ISystem> sentinelShutdown = _ => sentinelShutdowns++;
            Action<ISystem> shutdown = _ => shutdowns++;
            sentinelReference.OnSystemShutdown += sentinelShutdown;
            sentinel.SubscribeToAction(SystemAction.Update, _ => updates++);
            var marker = new Marker();
            SystemRef reference = null;
            try
            {
                var scope = new VerificationFixtureOwnership.RegistryRow(name);
                var error = Assert.Throws<ApplicationException>(() => scope.RegisterUsing(marker, () =>
                {
                    reference = ProcessManager.RegisterSystem(marker, name);
                    reference.OnSystemShutdown += shutdown;
                    throw new ApplicationException("after publish");
                }));
                Assert.AreEqual("after publish", error.Message);
                Assert.AreSame(marker, reference.GetSafe());
                scope.Dispose();
                Assert.IsFalse(Registry.Contains(name));
                Assert.IsTrue(reference.IsNull());
                // UnregisterSystemInternal revokes the live untyped reference,
                // which notifies its shutdown observers once.
                Assert.AreEqual(1, shutdowns);
                Assert.AreSame(sentinelRow, Registry[sentinelName]);
                Assert.AreSame(sentinel, sentinelReference.GetSafe());
                sentinel.ProcessSystemAction(SystemAction.Update);
                Assert.AreEqual(1, updates);
                Assert.AreEqual(0, sentinelShutdowns);
            }
            finally
            {
                if (reference != null)
                {
                    reference.OnSystemShutdown -= shutdown;
                    ProcessManager.UnregisterSystem(marker);
                    if (Registry.Contains(name) && reference.IsNull()) Registry.Remove(name);
                }
                sentinelReference.OnSystemShutdown -= sentinelShutdown;
                ProcessManager.UnregisterSystem(sentinel);
                if (ReferenceEquals(Registry[sentinelName], sentinelRow) && sentinelReference.IsNull()) Registry.Remove(sentinelName);
            }
        }

        [Test]
        public void RegistrationCallbackFailureBeforePublicationLeavesNothingToRelease()
        {
            string name = "ProjectLucid.UnpublishedRegistration." + Guid.NewGuid().ToString("N");
            var scope = new VerificationFixtureOwnership.RegistryRow(name);
            Assert.Throws<ApplicationException>(() => scope.RegisterUsing(new Marker(), () => throw new ApplicationException("before publish")));
            Assert.IsFalse(Registry.Contains(name));
            Assert.DoesNotThrow(() => scope.Dispose());
            Assert.IsFalse(Registry.Contains(name));
        }

        [Test]
        public void RegistrationCallbackFailureAfterForeignReplacementClaimsNothing()
        {
            string name = "ProjectLucid.ReplacedRegistration." + Guid.NewGuid().ToString("N");
            var scope = new VerificationFixtureOwnership.RegistryRow(name);
            var marker = new Marker();
            var sentinel = new Marker();
            SystemRef reference = null;
            int shutdowns = 0;
            Action<ISystem> shutdown = _ => shutdowns++;
            try
            {
                Assert.Throws<ApplicationException>(() => scope.RegisterUsing(marker, () =>
                {
                    reference = ProcessManager.RegisterSystem(marker, name);
                    ProcessManager.RegisterSystem(sentinel, name, canReplace: true);
                    throw new ApplicationException("after replacement");
                }));
                object row = Registry[name];
                reference.OnSystemShutdown += shutdown;
                Assert.DoesNotThrow(() => scope.Dispose());
                Assert.AreSame(row, Registry[name]);
                Assert.AreSame(sentinel, reference.GetSafe());
                Assert.AreEqual(0, shutdowns);
            }
            finally
            {
                if (reference != null)
                {
                    reference.OnSystemShutdown -= shutdown;
                    ProcessManager.UnregisterSystem(sentinel);
                    if (Registry.Contains(name) && reference.IsNull()) Registry.Remove(name);
                }
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

        [Test]
        public void BundledLanguagesRejectABorrowedStringRequestWithoutCallingIt()
        {
            RequireBorrowedRequestRejectedBeforeMutation(() => { });
        }

        [Test]
        public void BundledLanguagesRejectAThrowingBorrowedStringRequestWithoutCallingIt()
        {
            RequireBorrowedRequestRejectedBeforeMutation(() => throw new InvalidProgramException("borrowed request sentinel"));
        }

        // The sentinel is subscribed through the public event and removed by its
        // own delegate identity, so no other subscriber is touched. Rejection
        // must precede every fixture mutation: zero sentinel calls, no row, and
        // every StringTable static and the Language event left as they were.
        private static void RequireBorrowedRequestRejectedBeforeMutation(Action effect)
        {
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
            string tableName = ProcessManager.GetDefaultName<Hardlight.Localisation.StringTable>();
            string languageName = ProcessManager.GetDefaultName<Hardlight.Localisation.Language>();
            Assert.IsFalse(Registry.Contains(tableName) || Registry.Contains(languageName), "Control requires cold language rows.");
            // Witness that the sentinel is the only rejected state: the same
            // guards accept the current state while the request is absent.
            Assert.IsNull(typeof(Hardlight.Localisation.StringTable).GetField("OnRequestStringTable", declared).GetValue(null),
                "Control requires no other request subscriber.");
            Assert.DoesNotThrow(() => StringTableVerification.RequireColdState(Registry,
                MonoSingleton<Hardlight.Localisation.StringTable>.Instance, MonoSingleton<Hardlight.Localisation.Language>.Instance,
                Hardlight.Localisation.StringTable.StringsTableHandlers, null));
            Assert.DoesNotThrow(() => StringTableVerification.RequireOriginalReadiness((Func<bool>)typeof(Hardlight.Localisation.StringTable)
                .GetField("ShouldInvokeStringHandlers", declared).GetValue(null)));
            FieldInfo[] statics = Array.FindAll(typeof(Hardlight.Localisation.StringTable).GetFields(declared), f => !f.IsLiteral);
            FieldInfo languageEvent = typeof(Hardlight.Localisation.Language).GetField("OnLanguageChanged", declared);
            int calls = 0;
            Action sentinel = () => { calls++; effect(); };
            Hardlight.Localisation.StringTable.OnRequestStringTable += sentinel;
            try
            {
                object[] before = Array.ConvertAll(statics, f => f.GetValue(null));
                object languageBefore = languageEvent.GetValue(null);
                int rowsBefore = Registry.Count;
                var error = Assert.Throws<InvalidOperationException>(() => BundledStringTableVerification.Run());
                Assert.AreEqual("Fixture requires unowned static state: " + tableName, error.Message);
                Assert.AreEqual(0, calls);
                Assert.AreEqual(rowsBefore, Registry.Count);
                Assert.IsFalse(Registry.Contains(tableName) || Registry.Contains(languageName));
                for (int i = 0; i < statics.Length; ++i)
                {
                    object now = statics[i].GetValue(null);
                    Assert.IsTrue(ReferenceEquals(now, before[i]) || (now != null && now.GetType().IsValueType && now.Equals(before[i])),
                        "StringTable static changed: " + statics[i].Name);
                }
                Assert.AreSame(languageBefore, languageEvent.GetValue(null));
            }
            finally
            {
                Hardlight.Localisation.StringTable.OnRequestStringTable -= sentinel;
            }
        }
    }
}
