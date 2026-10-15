using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The FSM manager fixture must leave no fixture-owned registry row behind and must
    // never fire or replace observers on rows it borrows. Each case restores the registry's
    // complete dictionary state so later fixtures see the same rows and free slots.
    public sealed class FSMManagerVerificationOwnershipTests
    {
        private static IDictionary Rows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        private static IDictionary ActionRows() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemActionLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // Snapshot of the action table: the outer per-action rows and each inner subscriber
        // dictionary, so action-injection cases leave no row, entry or free slot behind.
        private sealed class ActionLayout
        {
            private readonly IDictionary outer = ActionRows();
            private readonly object[] outerLayout;
            private readonly List<KeyValuePair<IDictionary, object[]>> inner;

            public ActionLayout()
            {
                outerLayout = FSMManagerVerification.CaptureLayout(outer);
                inner = outer.Values.Cast<IDictionary>()
                    .Select(rows => new KeyValuePair<IDictionary, object[]>(rows, FSMManagerVerification.CaptureLayout(rows))).ToList();
            }

            public void Restore()
            {
                foreach (KeyValuePair<IDictionary, object[]> rows in inner) FSMManagerVerification.RestoreLayout(rows.Key, rows.Value);
                FSMManagerVerification.RestoreLayout(outer, outerLayout);
            }
        }

        // Distinct instances with the same id compare equal, like systems with value equality.
        private sealed class EqualSystem : ISystem
        {
            private readonly int id;
            public EqualSystem(int id) { this.id = id; }
            public override bool Equals(object other) => other is EqualSystem system && system.id == id;
            public override int GetHashCode() => id;
        }

        [Test]
        public void RepeatedRunsLeaveRegistryRowsAndSystemsUnchanged()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string gap = "Lucid-ownership-gap-" + Guid.NewGuid().ToString("N");
            string probe = "Lucid-ownership-probe-" + Guid.NewGuid().ToString("N");
            try
            {
                // A slot freed before the run decides where the next registration lands.
                ProcessManager.GetSystemRef(gap);
                ProcessManager.GetSystemRef(Guid.NewGuid().ToString("N"));
                rows.Remove(gap);
                Dictionary<string, bool> before = rows.Keys.Cast<string>().ToDictionary(name => name, name => ProcessManager.IsSystemNull(name));
                List<string> order = rows.Keys.Cast<string>().ToList();
                object[] runLayout = FSMManagerVerification.CaptureLayout(rows);
                ProcessManager.GetSystemRef(probe);
                List<string> expected = rows.Keys.Cast<string>().ToList();
                FSMManagerVerification.RestoreLayout(rows, runLayout);
                FSMManagerVerification.Run();
                FSMManagerVerification.Run();
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "registry rows and their order");
                ProcessManager.GetSystemRef(probe);
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(expected), "free slots that predate the run");
                foreach (KeyValuePair<string, bool> row in before)
                    Assert.That(ProcessManager.IsSystemNull(row.Key), Is.EqualTo(row.Value), row.Key);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void BorrowedEmptyDefaultManagerRowKeepsItsReferencesAndObservers()
        {
            IDictionary rows = Rows();
            string name = ProcessManager.GetDefaultName<FSMManager>();
            // An earlier fixture may have left a default manager row; set it aside for this case.
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            rows.Remove(name);
            // This test's own empty row stands in for one retained by another fixture.
            SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>();
            SystemRef untyped = ProcessManager.GetSystemRef(name);
            object row = rows[name];
            // A later row makes the borrowed row's position observable.
            string later = "Lucid-ownership-order-" + Guid.NewGuid().ToString("N");
            ProcessManager.GetSystemRef(later);
            List<string> order = rows.Keys.Cast<string>().ToList();
            int calls = 0;
            untyped.OnSystemStartup += system => calls++;
            untyped.OnSystemShutdown += system => calls++;
            typed.OnSystemStartup += system => calls++;
            typed.OnSystemShutdown += system => calls++;
            try
            {
                FSMManagerVerification.Run();
                Assert.That(calls, Is.Zero, "borrowed observers ran");
                Assert.That(rows[name], Is.SameAs(row));
                Assert.That(ProcessManager.GetSystemRef<FSMManager>(), Is.SameAs(typed));
                Assert.That(ProcessManager.GetSystemRef(name), Is.SameAs(untyped));
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "registry order");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        private sealed class ForeignSystem : ISystem { }

        [Test]
        public void FailureDuringRunStillReleasesOwnedRowsAndRestoresTheRegistry()
        {
            IDictionary rows = Rows();
            const string coreName = "Hardlight.HLUnityCore";
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            try
            {
                // An existing core row belongs to someone else; set it aside untouched so the
                // test-owned failure row never reuses its SystemInfo or fires its observers.
                rows.Remove(coreName);
                // A registered HLUnityCore breaks the diagnostics precondition, after the
                // fixture has already acquired its own rows and references.
                SystemRef core = ProcessManager.RegisterSystem(new ForeignSystem(), coreName);
                List<string> order = rows.Keys.Cast<string>().ToList();
                List<object> values = rows.Values.Cast<object>().ToList();
                var failure = Assert.Throws<InvalidOperationException>(FSMManagerVerification.Run);
                Assert.That(failure.Message, Is.EqualTo("FSM manager verification failed: diagnostic test precondition"));
                Assert.That(rows.Keys.Cast<string>(), Is.EqualTo(order), "no owned row survives and order is kept");
                Assert.That(rows.Values.Cast<object>(), Is.EqualTo(values), "row objects are unchanged");
                Assert.That(core.IsValid(), Is.True, "the foreign row keeps its system");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void FailureInjectionLeavesABorrowedEmptyCoreRowUntouched()
        {
            IDictionary rows = Rows();
            const string coreName = "Hardlight.HLUnityCore";
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            try
            {
                // A retained empty core row with untyped and typed references and an observer.
                rows.Remove(coreName);
                SystemRef untyped = ProcessManager.GetSystemRef(coreName);
                SystemRef<ForeignSystem> typed = ProcessManager.GetSystemRef<ForeignSystem>(coreName);
                object borrowed = rows[coreName];
                object[] borrowedLayout = FSMManagerVerification.CaptureLayout(rows);
                int startups = 0;
                untyped.OnSystemStartup += system => startups++;
                typed.OnSystemStartup += system => startups++;
                FailureDuringRunStillReleasesOwnedRowsAndRestoresTheRegistry();
                FSMManagerVerification.RestoreLayout(rows, borrowedLayout);
                Assert.That(rows[coreName], Is.SameAs(borrowed));
                Assert.That(startups, Is.Zero, "borrowed startup observers ran");
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True, "borrowed references became valid");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ShutdownObserverReplacementDoesNotLeaveAnOwnedReferenceValid()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = "Lucid-ownership-reentrant-" + Guid.NewGuid().ToString("N");
            try
            {
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                int replacements = 0, shutdowns = 0;
                untyped.OnSystemShutdown += system => shutdowns++;
                // A public typed observer registers a replacement under the same owned name.
                typed.OnSystemShutdown += system =>
                {
                    replacements++;
                    ProcessManager.RegisterSystem(new FSMManager(), name);
                };
                FSMManagerVerification.ReleaseOwnedRows(new[] { name });
                Assert.That(rows.Contains(name), Is.False);
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True, "a revalidated owned reference survived");
                Assert.That(replacements, Is.EqualTo(1));
                Assert.That(shutdowns, Is.EqualTo(1), "the untyped observer was notified twice");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void FailureAndReentrantReplacementLeaveNoOwnedReferenceValid()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = "Lucid-ownership-failure-reentrant-" + Guid.NewGuid().ToString("N");
            try
            {
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                var injected = new InvalidOperationException("injected shutdown failure");
                int untypedCalls = 0, typedCalls = 0;
                untyped.OnSystemShutdown += system => { untypedCalls++; throw injected; };
                typed.OnSystemShutdown += system => { typedCalls++; ProcessManager.RegisterSystem(new FSMManager(), name); };
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { name }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(rows.Contains(name), Is.False);
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True, "a revalidated owned reference survived failure cleanup");
                Assert.That(untypedCalls, Is.EqualTo(1));
                Assert.That(typedCalls, Is.EqualTo(1));
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ReplacementActionSubscriptionsAreReleasedWithTheOwnedRow()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            string name = "Lucid-ownership-replacement-action-" + Guid.NewGuid().ToString("N");
            var replacement = new FSMManager();
            int actionCalls = 0;
            try
            {
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                typed.OnSystemShutdown += system =>
                {
                    replacement.SubscribeToAction(SystemAction.Update, context => actionCalls++);
                    ProcessManager.RegisterSystem(replacement, name);
                };
                FSMManagerVerification.ReleaseOwnedRows(new[] { name });
                Assert.That(rows.Contains(name), Is.False);
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
                replacement.ProcessSystemAction(SystemAction.Update);
                Assert.That(actionCalls, Is.Zero, "a replacement's action subscription survived teardown");
            }
            finally
            {
                replacement.UnsubscribeFromAction(SystemAction.Update);
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        // A system whose hash changes after it subscribes stays stored under its old hash.
        private sealed class MutableHashSystem : ISystem
        {
            public int Hash = 1;
            public override bool Equals(object other) => ReferenceEquals(this, other);
            public override int GetHashCode() => Hash;
        }

        [Test]
        public void MutableHashReplacementLosesItsActionWithTheOwnedRow()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            string name = "Lucid-ownership-mutable-hash-" + Guid.NewGuid().ToString("N");
            var replacement = new MutableHashSystem();
            var foreign = new MutableHashSystem { Hash = 2 };
            int foreignCalls = 0;
            try
            {
                foreign.SubscribeToAction(SystemAction.Update, context => foreignCalls++);
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                typed.OnSystemShutdown += system =>
                {
                    replacement.SubscribeToAction(SystemAction.Update, context => { });
                    replacement.Hash = 2;
                    ProcessManager.RegisterSystem(replacement, name);
                };
                FSMManagerVerification.ReleaseOwnedRows(new[] { name });
                var updates = (IDictionary)ActionRows()[SystemAction.Update];
                Assert.That(updates.Keys.Cast<object>().Any(key => ReferenceEquals(key, replacement)), Is.False,
                    "the owned action entry survived its system's hash change");
                // A foreign system with the same current hash keeps its subscription.
                Assert.That(updates.Keys.Cast<object>().Any(key => ReferenceEquals(key, foreign)), Is.True);
                foreign.ProcessSystemAction(SystemAction.Update);
                Assert.That(foreignCalls, Is.EqualTo(1));
                Assert.That(rows.Contains(name), Is.False);
            }
            finally
            {
                foreign.UnsubscribeFromAction(SystemAction.Update);
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        [Test]
        public void PassLimitIsRecordedWhileLaterRowsAndActionsAreStillReleased()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            int limit = FSMManagerVerification.TeardownPassLimit;
            string first = "Lucid-ownership-limit-a-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            var replacement = new FSMManager();
            int actionCalls = 0;
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef<FSMManager> firstTyped = ProcessManager.GetSystemRef<FSMManager>(first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                firstTyped.OnSystemShutdown += system =>
                {
                    replacement.SubscribeToAction(SystemAction.Update, context => actionCalls++);
                    ProcessManager.RegisterSystem(replacement, first);
                };
                // Too small a budget for the revalidation this callback causes.
                FSMManagerVerification.TeardownPassLimit = 1;
                var failure = Assert.Throws<InvalidOperationException>(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                StringAssert.Contains("did not settle", failure.Message);
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False, "a later owned row survived the limit");
                Assert.That(firstRef.IsNull() && firstTyped.IsNull() && secondRef.IsNull(), Is.True);
                replacement.ProcessSystemAction(SystemAction.Update);
                Assert.That(actionCalls, Is.Zero, "the limit skipped action release");
            }
            finally
            {
                FSMManagerVerification.TeardownPassLimit = limit;
                replacement.UnsubscribeFromAction(SystemAction.Update);
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        [Test]
        public void PassLimitKeepsAnEarlierCallbackFailure()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            int limit = FSMManagerVerification.TeardownPassLimit;
            string first = "Lucid-ownership-limit-failure-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                var injected = new InvalidOperationException("original failure");
                firstRef.OnSystemShutdown += system => throw injected;
                FSMManagerVerification.TeardownPassLimit = 1;
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                Assert.That(thrown, Is.SameAs(injected), "the limit replaced the earlier failure");
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                Assert.That(firstRef.IsNull() && secondRef.IsNull(), Is.True);
            }
            finally
            {
                FSMManagerVerification.TeardownPassLimit = limit;
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ReferenceActivatedDuringTeardownIsNotNotifiedTwice()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = "Lucid-ownership-activated-" + Guid.NewGuid().ToString("N");
            try
            {
                // An owned non-FSM system leaves the typed FSMManager reference null at the start.
                SystemRef untyped = ProcessManager.RegisterSystem(new ForeignSystem(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                Assert.That(typed.IsNull(), Is.True);
                var injected = new InvalidOperationException("typed failure");
                int typedCalls = 0;
                untyped.OnSystemShutdown += system => ProcessManager.RegisterSystem(new FSMManager(), name, canReplace: true);
                typed.OnSystemShutdown += system => { typedCalls++; throw injected; };
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { name }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(typedCalls, Is.EqualTo(1), "the activated reference was notified twice");
                Assert.That(rows.Contains(name), Is.False);
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void CallbackCreatedReferenceNotifiedElsewhereIsNotNotifiedAgain()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string first = "Lucid-ownership-transient-a-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            SystemRef<FSMManager> transient = null;
            int transientCalls = 0;
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                ProcessManager.RegisterSystem(new FSMManager(), second);
                // A callback creates a reference on another owned row, unregisters that row (which
                // notifies the new reference) and then registers a replacement, revalidating it.
                firstRef.OnSystemShutdown += system =>
                {
                    transient = ProcessManager.GetSystemRef<FSMManager>(second);
                    transient.OnSystemShutdown += value => transientCalls++;
                    ProcessManager.UnregisterSystem(second);
                    ProcessManager.RegisterSystem(new FSMManager(), second);
                };
                FSMManagerVerification.ReleaseOwnedRows(new[] { first, second });
                Assert.That(transient, Is.Not.Null, "the callback ran");
                Assert.That(transientCalls, Is.EqualTo(1), "the transient reference was notified again");
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                Assert.That(firstRef.IsNull() && transient.IsNull(), Is.True);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void TransientReplacementInALaterRowLosesItsActions()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            string first = "Lucid-ownership-transient-action-a-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            var transient = new FSMManager();
            int actionCalls = 0;
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                ProcessManager.RegisterSystem(new FSMManager(), second);
                // Before second's turn, a callback registers a transient system there and replaces it.
                firstRef.OnSystemShutdown += system =>
                {
                    transient.SubscribeToAction(SystemAction.Update, context => actionCalls++);
                    ProcessManager.RegisterSystem(transient, second, canReplace: true);
                    ProcessManager.RegisterSystem(new FSMManager(), second, canReplace: true);
                };
                FSMManagerVerification.ReleaseOwnedRows(new[] { first, second });
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                transient.ProcessSystemAction(SystemAction.Update);
                Assert.That(actionCalls, Is.Zero, "the transient system kept its action subscription");
            }
            finally
            {
                transient.UnsubscribeFromAction(SystemAction.Update);
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        [Test]
        public void TransientSystemIsTrackedWhenAnEarlierStartupObserverThrows()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            string first = "Lucid-ownership-startup-throw-a-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            var transient = new FSMManager();
            int actionCalls = 0;
            var injected = new InvalidOperationException("startup failure");
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                // A foreign startup observer on the later row throws before any observer after it.
                secondRef.OnSystemStartup += system => throw injected;
                firstRef.OnSystemShutdown += system =>
                {
                    transient.SubscribeToAction(SystemAction.Update, context => actionCalls++);
                    ProcessManager.RegisterSystem(transient, second, canReplace: true);
                };
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                transient.ProcessSystemAction(SystemAction.Update);
                Assert.That(actionCalls, Is.Zero, "the transient system kept its action subscription");
            }
            finally
            {
                transient.UnsubscribeFromAction(SystemAction.Update);
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        [Test]
        public void RecursiveUnregisterInsideAShutdownObserverMatchesTheOriginal()
        {
            // A guarded observer that unregisters its own row: the original notifies it again from the
            // nested unregister. The fixture must produce the same trace and add no notification.
            SystemRef released = null;
            Func<List<string>, string[]> scenario = trace =>
            {
                string name = "Lucid-ownership-recursive-" + Guid.NewGuid().ToString("N");
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                released = untyped;
                int calls = 0;
                // Guarded so recursion is bounded by the observer, never by stack overflow.
                untyped.OnSystemShutdown += system =>
                {
                    int call = ++calls;
                    trace.Add("call " + call + ":" + !untyped.IsNull());
                    if (call == 1) ProcessManager.UnregisterSystem(name);
                    trace.Add("end " + call + ":" + !untyped.IsNull());
                };
                return new[] { name };
            };
            List<string> original = Teardown(true, scenario);
            Assert.That(original, Is.EqualTo(new[] { "call 1:True", "call 2:True", "end 2:True", "end 1:False" }), "original public unregister");
            Assert.That(Teardown(false, scenario), Is.EqualTo(original));
            Assert.That(released.IsNull(), Is.True);
        }

        [Test]
        public void EqualForeignSystemKeepsItsActionSubscription()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var actions = new ActionLayout();
            string name = "Lucid-ownership-equal-" + Guid.NewGuid().ToString("N");
            var foreign = new EqualSystem(7);
            int foreignCalls = 0;
            try
            {
                foreign.SubscribeToAction(SystemAction.Update, context => foreignCalls++);
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                // A distinct system equal to the foreign one enters the owned row without subscribing.
                untyped.OnSystemShutdown += system => ProcessManager.RegisterSystem(new EqualSystem(7), name, canReplace: true);
                FSMManagerVerification.ReleaseOwnedRows(new[] { name });
                foreign.ProcessSystemAction(SystemAction.Update);
                Assert.That(foreignCalls, Is.EqualTo(1), "cleanup removed an equal foreign system's action");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
                actions.Restore();
            }
        }

        [Test]
        public void ActionInjectionCasesPreserveAnAbsentActionRow()
        {
            var actions = new ActionLayout();
            try
            {
                ActionRows().Remove(SystemAction.Update);
                ReplacementActionSubscriptionsAreReleasedWithTheOwnedRow();
                TransientReplacementInALaterRowLosesItsActions();
                Assert.That(ActionRows().Contains(SystemAction.Update), Is.False, "an action case left an outer action row behind");
            }
            finally
            {
                actions.Restore();
            }
        }

        private static Delegate[] ShutdownObservers(object reference)
        {
            Type type = reference.GetType();
            while (type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) == null)
                type = type.BaseType;
            var list = (Delegate)type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
            return list?.GetInvocationList() ?? new Delegate[0];
        }

        [Test]
        public void OwnedReferenceObserversAreRestoredInOrder()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = "Lucid-ownership-observers-" + Guid.NewGuid().ToString("N");
            try
            {
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                var injected = new InvalidOperationException("untyped failure");
                Action<ISystem> first = system => { }, failing = system => throw injected, last = system => { };
                Action<FSMManager> typedObserver = system => { };
                untyped.OnSystemShutdown += first;
                untyped.OnSystemShutdown += failing;
                untyped.OnSystemShutdown += last;
                typed.OnSystemShutdown += typedObserver;
                Assert.That(Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { name })), Is.SameAs(injected));
                // The failing reference was silenced and the typed one notified normally; both keep
                // exactly their original observers in order, with no fixture wrapper or sentinel left.
                Assert.That(ShutdownObservers(untyped), Is.EqualTo(new Delegate[] { first, failing, last }));
                Assert.That(ShutdownObservers(typed), Is.EqualTo(new Delegate[] { typedObserver }));
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ObserverThatRemovesItselfStaysRemoved()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string name = "Lucid-ownership-self-removal-" + Guid.NewGuid().ToString("N");
            try
            {
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                int calls = 0;
                Action<ISystem> before = system => { }, after = system => { };
                Action<ISystem> leaving = null;
                leaving = system => { calls++; untyped.OnSystemShutdown -= leaving; };
                untyped.OnSystemShutdown += before;
                untyped.OnSystemShutdown += leaving;
                untyped.OnSystemShutdown += after;
                FSMManagerVerification.ReleaseOwnedRows(new[] { name });
                Assert.That(calls, Is.EqualTo(1));
                // As with the original public unregister, the observer's own removal is kept.
                Assert.That(ShutdownObservers(untyped), Is.EqualTo(new Delegate[] { before, after }));
                Assert.That(untyped.IsNull() && !rows.Contains(name), Is.True);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void OwnReferenceIsValidWhileItsObserversRun()
        {
            // The original revoke notifies before it clears, so observers see their own reference set.
            // After an observer's recursive unregister, the nested dispatch has already cleared it, so the
            // outer tail sees it cleared; the fixture must match the original trace exactly.
            Func<List<string>, string[]> scenario = seen =>
            {
                string name = "Lucid-ownership-validity-" + Guid.NewGuid().ToString("N");
                SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>(name);
                int recursions = 0;
                untyped.OnSystemShutdown += system => seen.Add("u1:" + !untyped.IsNull() + ":" + !ProcessManager.IsSystemNull(name));
                untyped.OnSystemShutdown += system =>
                {
                    if (++recursions == 1) ProcessManager.UnregisterSystem(name);
                };
                untyped.OnSystemShutdown += system => seen.Add("u3:" + !untyped.IsNull());
                typed.OnSystemShutdown += system => seen.Add("t:" + !typed.IsNull());
                return new[] { name };
            };
            List<string> original = Teardown(true, scenario);
            Assert.That(original.First(), Is.EqualTo("u1:True:True"), "original public unregister");
            Assert.That(original, Does.Contain("u3:False"), "original outer tail after the nested unregister");
            Assert.That(Teardown(false, scenario), Is.EqualTo(original));
        }

        // Runs a scenario's teardown either through the original public unregister of its first row
        // (the baseline) or through the fixture, and returns the observed trace. Rows are released and
        // the registry layout restored in both modes.
        private static List<string> Teardown(bool original, Func<List<string>, string[]> scenario)
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            var trace = new List<string>();
            try
            {
                string[] owned = scenario(trace);
                if (original) ProcessManager.UnregisterSystem(owned[0]);
                else FSMManagerVerification.ReleaseOwnedRows(owned);
                return new List<string>(trace);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        private static string[] ReplacingRecursion(List<string> trace)
        {
            string name = "Lucid-ownership-argument-" + Guid.NewGuid().ToString("N");
            var original = new FSMManager();
            var replacement = new FSMManager();
            SystemRef untyped = ProcessManager.RegisterSystem(original, name);
            int calls = 0;
            untyped.OnSystemShutdown += system =>
            {
                if (++calls != 1) return;
                ProcessManager.RegisterSystem(replacement, name, canReplace: true);
                ProcessManager.UnregisterSystem(name);
            };
            untyped.OnSystemShutdown += system =>
                trace.Add(ReferenceEquals(system, original) ? "original" : ReferenceEquals(system, replacement) ? "replacement" : "other");
            return new[] { name };
        }

        [Test]
        public void ResumedOuterObserverGetsOriginalArgument()
        {
            // A multicast invocation's argument is fixed when it begins: the outer dispatch's later
            // observer receives the system that began it, even after a callback replaced the row's system.
            List<string> baseline = Teardown(true, ReplacingRecursion);
            Assert.That(baseline.Last(), Is.EqualTo("original"), "original public unregister");
            List<string> fixture = Teardown(false, ReplacingRecursion);
            Assert.That(fixture.Last(), Is.EqualTo("original"));
            Assert.That(fixture, Does.Not.Contain("other"));
        }

        private static string[] NestedRows(List<string> trace)
        {
            string a = "Lucid-ownership-nest-a-" + Guid.NewGuid().ToString("N"), b = a + "-b";
            SystemRef first = ProcessManager.RegisterSystem(new FSMManager(), a);
            SystemRef second = ProcessManager.RegisterSystem(new FSMManager(), b);
            int aCalls = 0, bCalls = 0;
            first.OnSystemShutdown += system => { trace.Add("A.begin"); if (++aCalls == 1) ProcessManager.UnregisterSystem(b); };
            first.OnSystemShutdown += system => trace.Add("A.tail");
            second.OnSystemShutdown += system => { trace.Add("B.begin"); if (++bCalls == 1) ProcessManager.UnregisterSystem(b); };
            second.OnSystemShutdown += system => trace.Add("B.tail");
            return new[] { a, b };
        }

        [Test]
        public void NestedDispatchTailFinishesBeforeCallerTail()
        {
            // A's observer unregisters B, whose observer unregisters B again: B's dispatch is nested
            // inside A's, so B's tail completes before A's tail resumes.
            foreach (bool original in new[] { true, false })
            {
                List<string> trace = Teardown(original, NestedRows);
                string mode = original ? "original public unregister" : "fixture";
                Assert.That(trace.Take(2), Is.EqualTo(new[] { "A.begin", "B.begin" }), mode);
                Assert.That(trace.Last(), Is.EqualTo("A.tail"), mode);
                Assert.That(trace.Count(entry => entry == "A.tail"), Is.EqualTo(1), mode);
                Assert.That(trace.LastIndexOf("B.tail"), Is.LessThan(trace.IndexOf("A.tail")), mode);
            }
        }

        [Test]
        public void GuardedRecursiveObserverContinuesAfterNestedCall()
        {
            // The observer that makes a guarded same-row unregister completes its own remaining work.
            foreach (bool original in new[] { true, false })
            {
                List<string> trace = Teardown(original, log =>
                {
                    string name = "Lucid-ownership-continue-" + Guid.NewGuid().ToString("N");
                    SystemRef untyped = ProcessManager.RegisterSystem(new FSMManager(), name);
                    int calls = 0;
                    untyped.OnSystemShutdown += system =>
                    {
                        if (++calls != 1) return;
                        ProcessManager.UnregisterSystem(name);
                        log.Add("continued");
                    };
                    return new[] { name };
                });
                Assert.That(trace, Is.EqualTo(new[] { "continued" }), original ? "original public unregister" : "fixture");
            }
        }

        [Test]
        public void LaterRowCallbackRecreatingAReleasedNameIsSwept()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string first = "Lucid-ownership-cross-a-" + Guid.NewGuid().ToString("N"), second = first + "-b";
            SystemRef recreated = null;
            try
            {
                ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                secondRef.OnSystemShutdown += system => recreated = ProcessManager.RegisterSystem(new FSMManager(), first);
                FSMManagerVerification.ReleaseOwnedRows(new[] { first, second });
                Assert.That(recreated, Is.Not.Null, "the callback ran");
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False, "a re-created owned row survived");
                Assert.That(recreated.IsNull() && secondRef.IsNull(), Is.True);
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }

        [Test]
        public void ThrowingShutdownObserverDoesNotKeepLaterOwnedRowsRegistered()
        {
            IDictionary rows = Rows();
            object[] layout = FSMManagerVerification.CaptureLayout(rows);
            string first = "Lucid-ownership-test-" + Guid.NewGuid().ToString("N");
            string second = first + "-second";
            try
            {
                SystemRef firstRef = ProcessManager.RegisterSystem(new FSMManager(), first);
                SystemRef<FSMManager> firstTyped = ProcessManager.GetSystemRef<FSMManager>(first);
                SystemRef secondRef = ProcessManager.RegisterSystem(new FSMManager(), second);
                var injected = new InvalidOperationException("injected shutdown failure");
                int untypedCalls = 0, typedCalls = 0;
                // Persistent observers throw on every call, on both the untyped and a typed reference.
                firstRef.OnSystemShutdown += system => { untypedCalls++; throw injected; };
                firstTyped.OnSystemShutdown += system => { typedCalls++; throw new InvalidOperationException("typed"); };
                Exception thrown = Assert.Catch(() => FSMManagerVerification.ReleaseOwnedRows(new[] { first, second }));
                Assert.That(thrown, Is.SameAs(injected));
                Assert.That(rows.Contains(first) || rows.Contains(second), Is.False);
                Assert.That(secondRef.IsNull(), Is.True, "later owned row was not revoked");
                // No owned reference survives, and each observer is notified exactly once.
                Assert.That(firstRef.IsNull() && firstTyped.IsNull(), Is.True, "failing row's references were not revoked");
                Assert.That(untypedCalls, Is.EqualTo(1), "throwing untyped observer was retried");
                Assert.That(typedCalls, Is.EqualTo(1), "typed observer was not notified exactly once");
            }
            finally
            {
                FSMManagerVerification.RestoreLayout(rows, layout);
            }
        }
    }
}
