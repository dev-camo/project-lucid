using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine.TestTools;

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
            // The sentinel's subscription and dispatch create the Update row when it is absent.
            using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
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
            // The sentinel's subscription and dispatch create the Update row when it is absent.
            using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
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
        }

        [Test]
        public void RegistrationCallbackFailureBeforePublicationLeavesNothingToRelease()
        {
            string name = "ProjectLucid.UnpublishedRegistration." + Guid.NewGuid().ToString("N");
            var scope = new VerificationFixtureOwnership.RegistryRow(name);
            var marker = new Marker();
            SystemRef reference = null;
            try
            {
                Assert.Throws<ApplicationException>(() => scope.RegisterUsing(marker, () => throw new ApplicationException("before publish")));
                Assert.IsFalse(Registry.Contains(name));
                // The failed attempt leaves the lease unacquired, so a later
                // registration acquires the row and Dispose releases it.
                Assert.DoesNotThrow(() => scope.RegisterUsing(marker, () => reference = ProcessManager.RegisterSystem(marker, name)));
                Assert.AreSame(marker, reference.GetSafe());
                scope.Dispose();
                Assert.IsFalse(Registry.Contains(name));
                Assert.IsTrue(reference.IsNull());
            }
            finally
            {
                if (reference != null)
                {
                    ProcessManager.UnregisterSystem(marker);
                    if (Registry.Contains(name) && reference.IsNull()) Registry.Remove(name);
                }
            }
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

        private static Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> Actions =>
            VerificationFixtureOwnership.ActionRows.SharedActionLookup;

        // Sets one shared action row aside so a control starts from an absent row,
        // and puts the same row object back afterwards. A row the control leaves
        // under that action is its own: removed when empty, otherwise reported.
        private sealed class AsideActionRow : IDisposable
        {
            private readonly SystemAction m_action;
            private readonly Dictionary<ISystem, Action<object>> m_aside;

            public AsideActionRow(SystemAction action)
            {
                m_action = action;
                if (Actions.TryGetValue(action, out m_aside)) Actions.Remove(action);
            }

            // The borrowed row goes back before anything is reported. With no
            // borrowed row, a row the control left with subscribers stays in
            // place, as ActionRows leaves one.
            public void Dispose()
            {
                Actions.TryGetValue(m_action, out Dictionary<ISystem, Action<object>> left);
                bool leftSubscribers = left != null && left.Count != 0;
                if (m_aside != null) Actions[m_action] = m_aside;
                else if (left != null && !leftSubscribers) Actions.Remove(m_action);
                if (leftSubscribers) throw new InvalidOperationException("Control left subscribers on its own row: " + m_action);
            }
        }

        // SubscribeToAction creates the absent row and UnsubscribeFromAction keeps
        // it empty: the old cleanup shape, an unsubscribe alone, leaves the row.
        [Test]
        public void ActionRowsRemoveAnAbsentRowTheFixtureCreated()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var marker = new Marker();
                var scope = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
                marker.SubscribeToAction(SystemAction.Update, _ => { });
                marker.UnsubscribeFromAction(SystemAction.Update);
                Assert.IsTrue(Actions.TryGetValue(SystemAction.Update, out Dictionary<ISystem, Action<object>> row) && row.Count == 0,
                    "Unsubscribing keeps the created empty row");
                scope.Dispose();
                Assert.IsFalse(Actions.ContainsKey(SystemAction.Update));
            }
        }

        [Test]
        public void ActionRowsRemoveTheCreatedRowWhenTheFixtureFailsAfterConstruction()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var primary = new InvalidProgramException("owned failure after the constructor subscribed");
                var error = Assert.Throws<InvalidProgramException>(() =>
                {
                    using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
                    {
                        EntityActivationManager manager = null;
                        try
                        {
                            manager = new EntityActivationManager();
                            Assert.IsTrue(Actions[SystemAction.Update].ContainsKey(manager), "The constructor subscribed");
                            throw primary;
                        }
                        finally
                        {
                            if (manager != null) manager.UnsubscribeFromAction(SystemAction.Update);
                        }
                    }
                });
                Assert.AreSame(primary, error);
                Assert.IsFalse(Actions.ContainsKey(SystemAction.Update));
            }
        }

        // A cleanup that removes any empty row would also remove this borrowed one.
        [Test]
        public void ActionRowsKeepABorrowedEmptyRow()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var borrowed = new Dictionary<ISystem, Action<object>>();
                Actions.Add(SystemAction.Update, borrowed);
                var marker = new Marker();
                using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
                {
                    marker.SubscribeToAction(SystemAction.Update, _ => { });
                    marker.UnsubscribeFromAction(SystemAction.Update);
                }
                Assert.AreSame(borrowed, Actions[SystemAction.Update]);
                Assert.AreEqual(0, borrowed.Count);
            }
        }

        [Test]
        public void ActionRowsKeepABorrowedRowItsSubscribersAndCallbacks()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var sentinel = new Marker();
                var marker = new Marker();
                int sentinelUpdates = 0;
                Action<object> callback = _ => sentinelUpdates++;
                sentinel.SubscribeToAction(SystemAction.Update, callback);
                Dictionary<ISystem, Action<object>> borrowed = Actions[SystemAction.Update];
                try
                {
                    using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
                    {
                        marker.SubscribeToAction(SystemAction.Update, _ => { });
                        marker.UnsubscribeFromAction(SystemAction.Update);
                    }
                    Assert.AreSame(borrowed, Actions[SystemAction.Update]);
                    Assert.AreEqual(1, borrowed.Count);
                    Assert.AreSame(callback, borrowed[sentinel]);

                    // A fixture subscription left in the borrowed row is reported, not removed.
                    var scope = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
                    Action<object> left = _ => { };
                    marker.SubscribeToAction(SystemAction.Update, left);
                    StringAssert.Contains("Update subscribers changed", Assert.Throws<InvalidOperationException>(scope.Dispose).Message);
                    Assert.AreSame(borrowed, Actions[SystemAction.Update]);
                    Assert.AreSame(left, borrowed[marker]);
                    Assert.AreSame(callback, borrowed[sentinel]);
                    Assert.AreEqual(0, sentinelUpdates);
                }
                finally
                {
                    marker.UnsubscribeFromAction(SystemAction.Update);
                    sentinel.UnsubscribeFromAction(SystemAction.Update);
                }
            }
        }

        [Test]
        public void ActionRowsLeaveUnrelatedActionRowsUntouched()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var unrelated = new List<KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>>>(Actions);
                var unrelatedPairs = unrelated.ConvertAll(row => new List<KeyValuePair<ISystem, Action<object>>>(row.Value));
                var marker = new Marker();
                using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
                {
                    marker.SubscribeToAction(SystemAction.Update, _ => { });
                    marker.UnsubscribeFromAction(SystemAction.Update);
                }
                Assert.AreEqual(unrelated.Count, Actions.Count);
                for (int i = 0; i < unrelated.Count; ++i)
                {
                    Assert.AreSame(unrelated[i].Value, Actions[unrelated[i].Key]);
                    CollectionAssert.AreEqual(unrelatedPairs[i], new List<KeyValuePair<ISystem, Action<object>>>(unrelated[i].Value));
                }
            }
        }

        [Test]
        public void ActionRowsKeepAndReportACreatedRowThatGainedASubscriber()
        {
            using (new AsideActionRow(SystemAction.Update))
            {
                var foreign = new Marker();
                int updates = 0;
                Action<object> callback = _ => updates++;
                var scope = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
                try
                {
                    foreign.SubscribeToAction(SystemAction.Update, callback);
                    StringAssert.Contains("Update row gained subscribers", Assert.Throws<InvalidOperationException>(scope.Dispose).Message);
                    Assert.AreSame(callback, Actions[SystemAction.Update][foreign]);
                    Assert.AreEqual(0, updates);
                }
                finally
                {
                    foreign.UnsubscribeFromAction(SystemAction.Update);
                }
            }
        }

        // ReleaseEmptyRow inspects the untyped reference as well as each typed
        // entry: GetSystemRef(name) hands out the untyped reference, and a removed
        // row strands its work, because registration builds a new row. Before, an
        // idle-looking untyped reference with observers or deferred work was removed.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ReleaseEmptyRowRefusesUntypedObserversAndDeferredWork(int state)
        {
            string name = "ProjectLucid.UntypedReleaseSentinel." + Guid.NewGuid().ToString("N");
            VerificationFixtureOwnership.RequireAbsent(Registry, name);
            SystemRef untyped = ProcessManager.GetSystemRef(name);
            object row = Registry[name];
            int calls = 0;
            Action<ISystem> observer = _ => calls++;
            if (state == 0) untyped.OnSystemStartup += observer;
            if (state == 1) untyped.OnSystemShutdown += observer;
            if (state == 2) untyped.InvokeOnValid(observer);
            var marker = new Marker();
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() => VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row));
                StringAssert.Contains("retains state", error.Message);
                Assert.AreSame(row, Registry[name]);
                Assert.AreSame(untyped, ProcessManager.GetSystemRef(name, false));
                Assert.AreEqual(0, calls);

                // The retained row still delivers its work to a genuine registration once.
                ProcessManager.RegisterSystem(marker, name);
                Assert.AreSame(untyped, ProcessManager.GetSystemRef(name, false));
                Assert.AreEqual(state == 1 ? 0 : 1, calls);
                ProcessManager.UnregisterSystem(name);
                Assert.AreEqual(1, calls);
            }
            finally
            {
                untyped.OnSystemStartup -= observer;
                untyped.OnSystemShutdown -= observer;
                ProcessManager.UnregisterSystem(name);
            }
            // With its own work gone the same row is idle and released.
            VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
            Assert.IsFalse(Registry.Contains(name));
        }

        // The engine fixture registered its host and its CheckedGet rows directly,
        // and unregistered the host name unconditionally. A live system or an
        // observed null row under one of those names is now refused before any
        // engine object, registry change or callback.
        // State 0 borrows the host row; state n borrows PrefixRows[n - 1], so every
        // admitted row has a case (tools/tests checks the count).
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(int state)
        {
            string name = state == 0 ? ProcessManagerCheckedGetVerification.HostName
                : ProcessManagerCheckedGetVerification.RowPrefix + ProcessManagerCheckedGetVerification.PrefixRows[state - 1];
            var sentinel = new Marker();
            int startups = 0, shutdowns = 0, updates = 0;
            Action<ISystem> startup = _ => startups++;
            Action<ISystem> shutdown = _ => shutdowns++;
            Action<object> update = _ => updates++;
            using (new VerificationFixtureOwnership.ActionRows(SystemAction.Update))
            {
                var lease = new VerificationFixtureOwnership.RegistryRow(name);
                SystemRef untyped = state == 0 ? lease.Register(sentinel) : ProcessManager.GetSystemRef(name);
                object row = Registry[name];
                SystemRef<Marker> typed = ProcessManager.GetSystemRef<Marker>(name);
                untyped.OnSystemStartup += startup;
                untyped.OnSystemShutdown += shutdown;
                if (state == 0) sentinel.SubscribeToAction(SystemAction.Update, update);
                try
                {
                    var error = Assert.Throws<InvalidOperationException>(ProcessManagerCheckedGetVerification.Run);
                    Assert.AreEqual("Fixture requires an absent registry row: " + name, error.Message);
                    Assert.AreSame(row, Registry[name]);
                    Assert.AreSame(untyped, ProcessManager.GetSystemRef(name, false));
                    Assert.AreSame(typed, ProcessManager.GetSystemRef<Marker>(name, false));
                    Assert.AreSame(state == 0 ? sentinel : null, untyped.GetSafe());
                    Assert.AreSame(state == 0 ? sentinel : null, typed.GetSafe());
                    if (state == 0) Assert.AreSame(update, Actions[SystemAction.Update][sentinel]);
                    Assert.AreEqual(0, startups + shutdowns + updates);
                }
                finally
                {
                    untyped.OnSystemStartup -= startup;
                    untyped.OnSystemShutdown -= shutdown;
                    if (state == 0) sentinel.UnsubscribeFromAction(SystemAction.Update);
                    if (state == 0) lease.Dispose();
                    else VerificationFixtureOwnership.ReleaseEmptyRow(Registry, name, row);
                }
            }
            Assert.IsFalse(Registry.Contains(name));
        }

        // Fault injection after acquisition: the pool lease returns only actions
        // spawned inside it, including a reused LIFO slot, and leaves a borrowed
        // lease and its parent graph in place; the failure propagates unchanged.
        [Test]
        public void ActionPoolLeaseReturnsOnlyItsOwnActionsAfterAFailure()
        {
            var borrowedParent = FSMStateChangeAction.Create(FSMActionReason.Forced);
            var borrowed = FSMStateChangeAction.Create(FSMActionReason.TransitionFired, parentAction: borrowedParent);
            try
            {
                int prior = Hardlight.Pooling.ObjectPool<FSMStateChangeAction>.UsedObjectCount;
                var primary = new InvalidProgramException("owned failure after pool acquisition");
                var error = Assert.Throws<InvalidProgramException>(() =>
                {
                    using (new VerificationFixtureOwnership.ActionPoolLease())
                    {
                        var released = FSMStateChangeAction.Create(FSMActionReason.Forced);
                        released.Destroy();
                        var reused = FSMStateChangeAction.Create(FSMActionReason.ClearUser);
                        Assert.AreSame(released, reused, "LIFO reuse hands the released slot back out");
                        var parent = FSMStateChangeAction.Create(FSMActionReason.Forced);
                        FSMStateChangeAction.Create(FSMActionReason.TransitionFired, parentAction: parent).Clone();
                        Assert.AreEqual(prior + 5, Hardlight.Pooling.ObjectPool<FSMStateChangeAction>.UsedObjectCount);
                        throw primary;
                    }
                });
                Assert.AreSame(primary, error);
                Assert.AreEqual(prior, Hardlight.Pooling.ObjectPool<FSMStateChangeAction>.UsedObjectCount);
                List<FSMStateChangeAction> used = VerificationFixtureOwnership.ActionPoolLease.UsedActions();
                Assert.IsTrue(used.Contains(borrowed) && used.Contains(borrowedParent));
                Assert.AreSame(borrowedParent, borrowed.ParentAction);
                Assert.AreEqual(FSMActionReason.TransitionFired, borrowed.Reason);
            }
            finally
            {
                borrowed.Destroy(true);
            }
        }

        private static Dictionary<int, string> s_leasedTable;

        [Test]
        public void StaticDictionaryLeaseRestoresTheAdmittedTableAfterAFailure()
        {
            string value = new string('a', 1);
            var table = new Dictionary<int, string> { [1] = value };
            s_leasedTable = table;
            try
            {
                var primary = new InvalidProgramException("owned failure after a cached miss");
                var error = Assert.Throws<InvalidProgramException>(() =>
                {
                    using (new VerificationFixtureOwnership.StaticDictionaryLease(typeof(VerificationFixtureOwnershipTests), nameof(s_leasedTable)))
                    {
                        s_leasedTable[2] = "miss";
                        s_leasedTable[1] = "mutated";
                        s_leasedTable = new Dictionary<int, string>();
                        throw primary;
                    }
                });
                Assert.AreSame(primary, error);
                Assert.AreSame(table, s_leasedTable);
                CollectionAssert.AreEqual(new[] { 1 }, table.Keys);
                Assert.AreSame(value, table[1]);
            }
            finally
            {
                s_leasedTable = null;
            }
        }

        // Case 0: the body fails and a step fails after it. Case 1: the body passes
        // and two steps fail. Case 2: nothing fails. Every step runs in order, the
        // first failure propagates as the same object, and a later failure is only
        // logged. A plain finally would surface the step's failure in case 0; a
        // cleanup that stops at its first failure would skip the later steps.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RunThenCleanUpRunsEveryStepAndKeepsTheFirstFailure(int failures)
        {
            var trace = new List<string>();
            var body = new InvalidProgramException("body");
            var first = new InvalidOperationException("first step");
            var second = new InvalidOperationException("second step");
            Action run = () => VerificationFixtureOwnership.RunThenCleanUp(
                () => { trace.Add("body"); if (failures == 0) throw body; },
                () => { trace.Add("first"); if (failures != 2) throw first; },
                () => { trace.Add("second"); if (failures == 1) throw second; },
                () => trace.Add("last"));
            if (failures == 0)
            {
                LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("^Fixture cleanup failed after an earlier failure: System.InvalidOperationException: first step"));
                Assert.AreSame(body, Assert.Throws<InvalidProgramException>(() => run()));
            }
            else if (failures == 1)
            {
                LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("^Fixture cleanup failed after an earlier failure: System.InvalidOperationException: second step"));
                Assert.AreSame(first, Assert.Throws<InvalidOperationException>(() => run()));
            }
            else Assert.DoesNotThrow(() => run());
            CollectionAssert.AreEqual(new[] { "body", "first", "second", "last" }, trace);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
