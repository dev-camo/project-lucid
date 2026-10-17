#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Source-based checks use expected behavior derived from original named
    // arm64 functions. They do not establish original-game or startup equivalence.
    public static class FSMManagerVerification
    {
        // Registry rows this run created. The original UnregisterSystem keeps rows, typed
        // caches and observers, so the fixture also drops exactly these rows afterwards.
        private static readonly List<string> s_ownedRows = new List<string>();

        public static void Run()
        {
            s_ownedRows.Clear();
            IDictionary rows = RegistryRows();
            List<DictionaryEntry> original = Entries(rows);
            object[] layout = CaptureLayout(rows);
            // The verifiers add the further rows they create to s_ownedRows.
            try
            {
                WithDefaultManagerRow(() =>
                {
                    VerifyFastActions();
                    VerifySystemReferences();
                    VerifyManagers();
                    VerifyDiagnostics();
                }, null, s_ownedRows);
            }
            finally { s_ownedRows.Clear(); }
            // The downstream verifiers clear and re-add the registry, so restore its layout again after them.
            try
            {
                ProcessUnregistrationVerification.Run();
                ProcessActionVerification.Run();
            }
            finally
            {
                if (HoldsExactly(rows, original)) RestoreLayout(rows, layout);
            }
        }

        // Runs a fixture that reaches FSMManager.GetManager (0x060003eb, x86-64 0x1ab8ba0), which
        // registers a new default manager whenever IsSystemNull<FSMManager> reports the default
        // row null. Afterwards releaseMachines removes the fixture's own machines from whatever
        // manager is registered then, looked up without creating one.
        internal static void WithDefaultManagerRow(Action body, Action<FSMManager> releaseMachines) =>
            WithDefaultManagerRow(body, releaseMachines, new List<string>());

        // The one row-ownership protocol for the fixtures. A retained empty default row belongs to
        // someone else, observers included: the row object is set aside untouched so the fixture's
        // own manager never fires them, and put back afterwards. An absent row is owned and released
        // with every other name in owned. A borrowed live manager stays registered. Once the
        // registry holds exactly its original rows again its complete layout is restored. The
        // body's own failure outranks a cleanup failure.
        private static void WithDefaultManagerRow(Action body, Action<FSMManager> releaseMachines, List<string> owned)
        {
            string defaultManager = ProcessManager.GetDefaultName<FSMManager>();
            IDictionary rows = RegistryRows();
            List<DictionaryEntry> original = Entries(rows);
            object[] layout = CaptureLayout(rows);
            object borrowedDefault = null;
            if (rows.Contains(defaultManager) && ProcessManager.IsSystemNull(defaultManager))
            {
                borrowedDefault = rows[defaultManager];
                rows.Remove(defaultManager);
            }
            if (!rows.Contains(defaultManager)) owned.Add(defaultManager);
            Exception failure = null, cleanup = null;
            try { body(); }
            catch (Exception error) { failure = error; }
            try
            {
                // IsSystemNull reads the row without registering; GetSystem then only reads it.
                if (releaseMachines != null && !ProcessManager.IsSystemNull(defaultManager))
                    releaseMachines(ProcessManager.GetSystem<FSMManager>(defaultManager));
            }
            catch (Exception error) { cleanup = error; }
            try { if (owned.Count > 0) ReleaseOwnedRows(owned); }
            catch (Exception error) { if (cleanup == null) cleanup = error; }
            finally
            {
                if (borrowedDefault != null) rows[defaultManager] = borrowedDefault;
                if (HoldsExactly(rows, original)) RestoreLayout(rows, layout);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            if (cleanup != null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        }

        // IDictionary's own enumerator yields DictionaryEntry; Cast<> would see KeyValuePair.
        private static List<DictionaryEntry> Entries(IDictionary rows)
        {
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry row in rows) entries.Add(row);
            return entries;
        }

        // The original private registry; only rows named by the caller are ever removed.
        private static IDictionary RegistryRows() =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // Order-sensitive lookups such as FindSystemRef see insertion order, and slots freed by
        // removed rows decide where later registrations land. Once the registry holds exactly its
        // original rows again, restore the dictionary's complete state, free slots included.
        internal static object[] CaptureLayout(IDictionary rows)
        {
            FieldInfo[] fields = LayoutFields(rows);
            var layout = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(rows);
                layout[i] = value is Array array ? array.Clone() : value;
            }
            return layout;
        }

        internal static void RestoreLayout(IDictionary rows, object[] layout)
        {
            FieldInfo[] fields = LayoutFields(rows);
            // Restore copies so one captured layout can be restored more than once.
            for (int i = 0; i < fields.Length; i++)
                fields[i].SetValue(rows, layout[i] is Array array ? array.Clone() : layout[i]);
        }

        private static FieldInfo[] LayoutFields(IDictionary rows) =>
            rows.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Rows the run did not create are never dropped by restoring the layout.
        internal static bool HoldsExactly(IDictionary rows, List<DictionaryEntry> original)
        {
            if (rows.Count != original.Count) return false;
            foreach (DictionaryEntry row in original)
                if (!rows.Contains(row.Key) || !ReferenceEquals(rows[row.Key], row.Value)) return false;
            return true;
        }

        // Releases every owned row under one lifecycle contract, for the original unregister and
        // anything its callbacks do (recursion, re-registration, recreation of released names):
        //  1. Ownership. Every owned name keeps a watched row until the end, so any system entering it
        //     is recorded by a startup sentinel that runs before other startup observers. At the end
        //     no owned row remains, no reference seen stays valid, and each entered system's actions
        //     are released.
        //  2. Identity. References and systems are tracked by reference identity. Only action entries
        //     whose stored key is an entered system itself are removed; equal foreign systems, their
        //     actions, foreign observers and their order are untouched.
        //  3. Notification. Each owned reference present at its row's unregister is notified through
        //     the original revoke, after the row's system loses its actions (by identity), as the
        //     original unregister does: the reference is still set while its observers run, the outer
        //     argument is fixed, an observer may remove itself, and a callback's own recursive
        //     unregister dispatches, nests and returns exactly as with the original. The fixture adds no
        //     notification of its own: rows present at the start that no callback has begun
        //     unregistering are handled this way; everything else is revoked silently, observers kept.
        //  4. Failure. The first failure is rethrown only after all owned cleanup completes.
        //  5. Bound. Reconciliation has a budget; exhausting it is recorded (never replacing an earlier
        //     failure) and the remaining owned state is revoked silently and released.
        // Only public event subscriptions and public unsubscription are used to change state.
        // The bound applies to the fixture's own reconciliation passes; recursion inside a callback is
        // the callback's own behavior and is not bounded here, as with the original unregister.
        internal static void ReleaseOwnedRows(IList<string> owned) => new Teardown(RegistryRows()).Release(owned);

        internal static int TeardownPassLimit = 1024;

        private sealed class Teardown
        {
            private readonly IDictionary rows;
            private readonly HashSet<object> notified = new HashSet<object>(new ReferenceComparer());
            private readonly HashSet<object> bracketed = new HashSet<object>(new ReferenceComparer());
            private readonly HashSet<object> initialRows = new HashSet<object>(new ReferenceComparer());
            private readonly HashSet<object> unregisteredRows = new HashSet<object>(new ReferenceComparer());
            private readonly HashSet<object> eligible = new HashSet<object>(new ReferenceComparer());
            private readonly HashSet<object> watchedRows = new HashSet<object>(new ReferenceComparer());
            private readonly List<ISystem> entered = new List<ISystem>();
            private readonly List<Sentinel> sentinels = new List<Sentinel>();
            private readonly MethodInfo revoke = typeof(ProcessManager).Assembly.GetType("Hardlight.ISystemRef")
                .GetMethod("InternalRevokeSystem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            private Exception first;
            private int budget = TeardownPassLimit;

            public Teardown(IDictionary rows) { this.rows = rows; }

            public void Release(IList<string> owned)
            {
                try
                {
                    // Every owned name keeps a watched row for the whole teardown: existing rows are
                    // kept, absent names get an empty fixture row. Any registration under an owned
                    // name, transient or not, then enters a row whose startup is already watched.
                    foreach (string name in owned)
                    {
                        if (rows.Contains(name)) initialRows.Add(rows[name]);
                        else ProcessManager.GetSystemRef(name);
                    }
                    foreach (string name in owned) Watch(rows[name]);
                    bool settled = false;
                    while (!settled && budget > 0)
                    {
                        settled = true;
                        foreach (string name in owned)
                        {
                            if (!rows.Contains(name)) ProcessManager.GetSystemRef(name);
                            Watch(rows[name]);
                        }
                        foreach (string name in owned)
                        {
                            try { if (ReleaseRow(name, rows[name])) settled = false; }
                            catch (Exception error) { Record(error); settled = false; }
                        }
                    }
                    if (!settled)
                        Record(new InvalidOperationException("Owned registry row teardown did not settle; callbacks keep re-registering owned names."));
                }
                finally
                {
                    // Silent revocation runs no callbacks, so this always completes.
                    foreach (string name in owned)
                    {
                        if (!rows.Contains(name)) continue;
                        foreach (object reference in References(rows[name])) if (IsValid(reference)) Silence(reference);
                        rows.Remove(name);
                    }
                    foreach (object reference in bracketed.Where(IsValid).ToList()) Silence(reference);
                    foreach (Sentinel sentinel in sentinels)
                        sentinel.Event.RemoveEventHandler(sentinel.Reference, sentinel.Observer);
                    try { ReleaseEnteredActions(); }
                    catch (Exception error) { Record(error); }
                }
                if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
            }

            private void Record(Exception error) { if (first == null) first = error; }

            // Places begin sentinels on the row's references and its untyped startup event, and
            // records the system it holds now.
            private void Watch(object info)
            {
                List<object> present = References(info);
                foreach (object reference in present) Bracket(reference);
                if (watchedRows.Add(info))
                    BracketEvent(present[0], "OnSystemStartup", system => Enter(system as ISystem));
                Enter(((SystemRef)present[0]).GetSafe());
            }

            // The original unregister runs once, for a row present at the start that no callback has
            // already started unregistering. Returns whether this pass found any work in the row.
            private bool ReleaseRow(string name, object info)
            {
                List<object> present = References(info);
                SystemRef untyped = (SystemRef)present[0];
                bool work = false;
                bool started = present.Any(reference => notified.Contains(reference));
                if (initialRows.Contains(info) && !started && unregisteredRows.Add(info))
                {
                    work = true;
                    eligible.UnionWith(present);
                    // The original unregister order: release the system's actions, then each
                    // reference, untyped first, notifying each once.
                    ISystem system = untyped.GetSafe();
                    if (system != null) { Enter(system); ReleaseActionsOf(system); }
                    foreach (object reference in present)
                    {
                        if (!IsValid(reference)) continue;
                        if (notified.Contains(reference)) Silence(reference);
                        else Notify(reference);
                    }
                }
                unregisteredRows.Add(info);
                // Revoke until no reference of this row is set; observers may revalidate references,
                // add typed ones, or register other owned names, which later passes visit.
                while (budget-- > 0)
                {
                    bool changed = false;
                    foreach (object reference in References(info))
                    {
                        Bracket(reference);
                        if (!IsValid(reference)) continue;
                        changed = work = true;
                        Enter(untyped.GetSafe());
                        if (notified.Contains(reference) || !eligible.Contains(reference)) Silence(reference);
                        else Notify(reference);
                    }
                    if (!changed) return work;
                }
                return true;
            }

            // Notifies a reference through the original revoke: it is still set while its observers
            // run, they may remove themselves, and a callback's own recursive unregister dispatches
            // exactly as the original does. An observer failure is recorded; reconciliation then
            // revokes whatever stays set without notifying it again.
            private void Notify(object reference)
            {
                try { revoke.Invoke(reference, null); }
                catch (TargetInvocationException error) { Record(error.InnerException); }
            }

            private void Enter(ISystem system)
            {
                if (system != null && !entered.Any(known => ReferenceEquals(known, system))) entered.Add(system);
            }

            // Revokes without notifying: the shutdown observers are taken off for the revoke and put
            // back in the same order, so no observer of an owned reference is lost.
            private void Silence(object reference)
            {
                EventInfo shutdown = ShutdownEvent(reference, out Delegate[] observers);
                foreach (Delegate observer in observers) shutdown.RemoveEventHandler(reference, observer);
                try { revoke.Invoke(reference, null); }
                finally { foreach (Delegate observer in observers) shutdown.AddEventHandler(reference, observer); }
            }

            // Removes only action entries whose stored key is the very system that entered an owned
            // row; an equal but distinct foreign system keeps its subscription.
            // Every entered system is released even if an earlier one reports a failure.
            private void ReleaseEnteredActions()
            {
                foreach (ISystem system in entered)
                {
                    try { ReleaseActionsOf(system); }
                    catch (Exception error) { Record(error); }
                }
            }

            // Removes only entries whose stored key is this very system.
            private static IDictionary ActionLookup() =>
                (IDictionary)typeof(ProcessManager).GetField("s_systemActionLookup", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            private void ReleaseActionsOf(ISystem system)
            {
                var actions = new List<SystemAction>();
                foreach (DictionaryEntry row in ActionLookup())
                    foreach (object key in ((IDictionary)row.Value).Keys)
                        if (ReferenceEquals(key, system)) actions.Add((SystemAction)row.Key);
                // One removal per stored entry: a system subscribed again after its hash changed holds
                // several entries for the same reference, each under its own stored hash.
                foreach (SystemAction action in actions)
                {
                    var callbacks = (Dictionary<ISystem, Action<object>>)ActionLookup()[action];
                    RemoveStoredKey(callbacks, system, () => system.UnsubscribeFromAction(action));
                }
                // Only after every located entry was processed is anything left reported.
                foreach (SystemAction action in actions.Distinct())
                    if (((IDictionary)ActionLookup()[action]).Keys.Cast<object>().Any(key => ReferenceEquals(key, system)))
                        throw new InvalidOperationException("An owned action subscription could not be released: " + action);
            }

            // The public unsubscription hashes the system again; a system whose hash changed after it
            // subscribed is stored under its old hash and would be missed. For this one call the
            // dictionary's comparer answers the stored hash and reference identity for that system
            // only, so the dictionary's own Remove unlinks exactly that entry and every other key,
            // slot and free-list position behaves as in an ordinary removal.
            private static void RemoveStoredKey(Dictionary<ISystem, Action<object>> callbacks, ISystem system, Action remove)
            {
                const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = callbacks.GetType();
                var entries = (Array)type.GetField("_entries", Fields).GetValue(callbacks);
                int count = (int)type.GetField("_count", Fields).GetValue(callbacks);
                FieldInfo entryKey = entries.GetType().GetElementType().GetField("key");
                FieldInfo entryHash = entries.GetType().GetElementType().GetField("hashCode");
                int stored = -1;
                for (int index = 0; index < count && stored < 0; index++)
                {
                    object entry = entries.GetValue(index);
                    if (ReferenceEquals(entryKey.GetValue(entry), system)) stored = (int)entryHash.GetValue(entry);
                }
                if (stored < 0) return;
                FieldInfo comparerField = type.GetField("_comparer", Fields);
                var original = (IEqualityComparer<ISystem>)comparerField.GetValue(callbacks);
                comparerField.SetValue(callbacks, new StoredKeyComparer(original ?? EqualityComparer<ISystem>.Default, system, stored));
                try { remove(); }
                finally { comparerField.SetValue(callbacks, original); }
            }

            // A begin sentinel placed before a reference's shutdown observers records any notification
            // of it, including one started by a callback's own unregister.
            private void Bracket(object reference)
            {
                if (bracketed.Add(reference))
                    BracketEvent(reference, "OnSystemShutdown", _ => notified.Add(reference));
            }

            private void BracketEvent(object reference, string eventName, Action<object> onCall)
            {
                Type type = reference.GetType();
                while (type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) == null)
                    type = type.BaseType;
                var observers = (Delegate)type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
                EventInfo evt = type.GetEvent(eventName);
                Delegate[] list = observers?.GetInvocationList() ?? new Delegate[0];
                foreach (Delegate observer in list) evt.RemoveEventHandler(reference, observer);
                sentinels.Add(Subscribe(reference, eventName, onCall));
                foreach (Delegate observer in list) evt.AddEventHandler(reference, observer);
            }
        }

        private static EventInfo ShutdownEvent(object reference, out Delegate[] observers)
        {
            Type type = reference.GetType();
            while (type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) == null)
                type = type.BaseType;
            var list = (Delegate)type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
            observers = list?.GetInvocationList() ?? new Delegate[0];
            return type.GetEvent("OnSystemShutdown");
        }

        private sealed class StoredKeyComparer : IEqualityComparer<ISystem>
        {
            private readonly IEqualityComparer<ISystem> inner;
            private readonly ISystem target;
            private readonly int stored;
            public StoredKeyComparer(IEqualityComparer<ISystem> inner, ISystem target, int stored)
            {
                this.inner = inner; this.target = target; this.stored = stored;
            }
            public bool Equals(ISystem x, ISystem y) =>
                ReferenceEquals(x, target) || ReferenceEquals(y, target) ? ReferenceEquals(x, y) : inner.Equals(x, y);
            public int GetHashCode(ISystem system) => ReferenceEquals(system, target) ? stored : inner.GetHashCode(system);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        private struct Sentinel
        {
            public object Reference;
            public EventInfo Event;
            public Delegate Observer;
        }

        // Public event subscription of a fixture observer to a reference's Action<T> event.
        private static Sentinel Subscribe(object reference, string eventName, Action onCall) =>
            Subscribe(reference, eventName, _ => onCall());

        private static Sentinel Subscribe(object reference, string eventName, Action<object> onCall)
        {
            EventInfo evt = reference.GetType().GetEvent(eventName);
            Type argument = evt.EventHandlerType.GetGenericArguments()[0];
            Delegate observer = (Delegate)typeof(FSMManagerVerification).GetMethod(nameof(Observer), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(argument).Invoke(null, new object[] { onCall });
            evt.AddEventHandler(reference, observer);
            return new Sentinel { Reference = reference, Event = evt, Observer = observer };
        }

        private static Action<T> Observer<T>(Action<object> onCall) => value => onCall(value);

        // The original revoke order: the untyped reference, then each typed reference.
        private static List<object> References(object info)
        {
            Type infoType = info.GetType();
            var references = new List<object> { infoType.GetField("SystemRef").GetValue(info) };
            foreach (object reference in ((IDictionary)infoType.GetField("SystemRefDictionary").GetValue(info)).Values)
                references.Add(reference);
            return references;
        }

        private static bool IsValid(object reference) =>
            (bool)reference.GetType().GetMethod("IsValid", BindingFlags.Instance | BindingFlags.Public).Invoke(reference, null);

        private static void VerifyFastActions()
        {
            var trace = new List<string>();
            FastAction<int> action = null;
            Action<int> listener = value => trace.Add("listener:" + value);
            action += listener;
            action += listener;
            action = FastAction<int>.AddUnique(action, listener);
            Check(action.GetInvocationListCount() == 2, "addition permits duplicates, AddUnique does not");
            action -= listener;
            FastAction<int>.Invoke(action, 4);
            Trace(trace, "listener:4");
            Check((action - (Action<int>)null) == null, "subtracting null returns null");
            Check(ReferenceEquals(action + (Action<int>)null, action), "adding null preserves reference");
            action.GetInvocationList().Clear();
            Check(action.GetInvocationListCount() == 0, "invocation list is exposed directly");
            FastAction<int>.Invoke(null, 0);

            FastAction<int> changing = null;
            bool first = true;
            trace.Clear();
            Action<int> added = value => trace.Add("added");
            Action<int> modify = value =>
            {
                trace.Add("modify");
                if (!first) return;
                first = false;
                changing += added;
                changing -= added;
                changing = FastAction<int>.AddUnique(changing, added);
                changing += added;
            };
            changing += modify;
            FastAction<int>.Invoke(changing, 0);
            Check(changing.GetInvocationListCount() == 2, "queued additions are applied before removals");
            FastAction<int>.Invoke(changing, 0);
            Trace(trace, "modify", "modify", "added");

            FastAction<int> failing = null;
            bool throwNow = true;
            trace.Clear();
            Action<int> throwing = value =>
            {
                if (throwNow)
                {
                    failing += added;
                    throw new InvalidOperationException("listener failure");
                }
                trace.Add("recovered");
            };
            failing += throwing;
            Throws<InvalidOperationException>(() => FastAction<int>.Invoke(failing, 0), "listener exception propagates");
            failing += listener;
            Check(failing.GetInvocationListCount() == 1, "failure leaves invocation active");
            throwNow = false;
            FastAction<int>.Invoke(failing, 2);
            Trace(trace, "recovered");
            Check(failing.GetInvocationListCount() == 3, "successful retry applies pending listeners");
            trace.Clear();
            FastAction<int>.Invoke(failing, 3);
            Trace(trace, "recovered", "added", "listener:3");
        }

        private sealed class TestSystem : ISystem { }
        private sealed class OtherSystem : ISystem { }

        private static void VerifySystemReferences()
        {
            string name = "Lucid-verification-system-" + Guid.NewGuid().ToString("N");
            s_ownedRows.Add(name);
            s_ownedRows.Add(name + "-replacement");
            s_ownedRows.Add(name + "-detached");
            var trace = new List<string>();
            var untyped = ProcessManager.GetSystemRef(name);
            var typed = ProcessManager.GetSystemRef<TestSystem>(name);
            Check(untyped.IsNull() && typed.IsNull(), "auto-register null references");
            Check(ReferenceEquals(typed, ProcessManager.GetSystemRef<TestSystem>(name)), "typed reference cached");
            Check(ProcessManager.GetDefaultName<TestSystem>() == typeof(TestSystem).ToString(), "default name uses Type.ToString");
            var wait = typed.WaitOnSystem();
            Check(wait.MoveNext() && wait.Current == null && wait.MoveNext(), "waiting yields null while system absent");
            var first = new TestSystem();
            untyped.OnSystemStartup += system =>
            {
                Check(ReferenceEquals(untyped.GetSafe(), first), "untyped assigned before startup");
                trace.Add("untyped.startup");
            };
            untyped.InvokeOnValid(system => trace.Add("untyped.valid"));
            typed.OnSystemStartup += system => trace.Add("typed.startup");
            typed.InvokeOnValid(system =>
            {
                Check(ReferenceEquals(system, first), "typed valid callback argument");
                trace.Add("typed.valid");
            });
            Check(ReferenceEquals(ProcessManager.RegisterSystem(first, name), untyped), "registration retains reference");
            Trace(trace, "untyped.startup", "untyped.valid", "typed.startup", "typed.valid");
            Check(!wait.MoveNext() && typed.IsValid() && ReferenceEquals(typed.Get<TestSystem>(), first), "wait completes and system accessible");
            typed.InvokeOnValid(system => trace.Add("immediate"));
            Check(trace[4] == "immediate", "already valid callback is immediate");
            Throws<NullReferenceException>(() => typed.InvokeOnValid(null), "null callback on valid system fails");
            trace.Clear();
            Check(ReferenceEquals(ProcessManager.RegisterSystem(first, name), untyped) && trace.Count == 0, "same object does not notify");
            var second = new TestSystem();
            Throws<InvalidOperationException>(() => ProcessManager.RegisterSystem(second, name), "replacement disallowed");
            Check(ReferenceEquals(ProcessManager.RegisterSystem(second, name, noException: true).GetSafe(), first), "suppressed replacement keeps old system");

            string replacementName = name + "-replacement";
            var replaced = ProcessManager.RegisterSystem(first, replacementName);
            var replacedTyped = ProcessManager.GetSystemRef<TestSystem>(replacementName);
            replaced.OnSystemStartup += system => trace.Add("untyped.replace");
            replacedTyped.OnSystemStartup += system => trace.Add("typed.replace");
            replaced.OnSystemShutdown += system => trace.Add("unexpected.shutdown");
            Check(ReferenceEquals(ProcessManager.RegisterSystem(second, replacementName, canReplace: true), replaced), "replacement retains untyped reference");
            Trace(trace, "untyped.replace", "typed.replace");
            Check(ReferenceEquals(replacedTyped.GetSafe(), second), "replacement updates cached typed reference");

            var incompatible = ProcessManager.GetSystemRef<OtherSystem>(replacementName);
            Check(incompatible.IsNull(), "initial incompatible typed reference is null");
            OtherSystem observed = new OtherSystem();
            incompatible.InvokeOnValid(system => observed = system);
            ProcessManager.RegisterSystem(new TestSystem(), replacementName, canReplace: true);
            Check(incompatible.IsValid() && incompatible.GetSafe() == null && observed == null, "replacement preserves untyped validity and null typed value");

            string detachedName = name + "-detached";
            var detached = ProcessManager.GetSystemRef<TestSystem>(detachedName, false);
            ProcessManager.RegisterSystem(first, detachedName);
            Check(detached.IsNull() && !ReferenceEquals(detached, ProcessManager.GetSystemRef<TestSystem>(detachedName)), "disabled auto-registration is detached");

            var standalone = new SystemRef<TestSystem>(name, first);
            standalone.OnSystemShutdown += system => Check(ReferenceEquals(standalone.GetSafe(), first), "shutdown before clearing fields");
            // The original interface is internal to HLUnityCore.Runtime. Exercise
            // its body without inventing a new public lifecycle API.
            MethodInfo revoke = typeof(SystemRef<TestSystem>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(revoke != null, "original internal revoke method exists");
            revoke.Invoke(standalone, null);
            Check(standalone.IsNull() && standalone.GetSafe() == null, "revoke clears both references");
        }

        private static void VerifyManagers()
        {
            var manager = new FSMManager();
            string name = "Lucid-verification-manager-" + Guid.NewGuid().ToString("N");
            var first = new FiniteStateMachine(name, skipAddToManager: true);
            var replacement = new FiniteStateMachine(name, skipAddToManager: true);
            manager.AddFSM(first);
            Check(manager.FSMExists(name) && manager.TryGetFSM(name, out FiniteStateMachine found) && ReferenceEquals(found, first), "manager lookup");
            Exception duplicate = Throws<Exception>(() => manager.AddFSM(replacement), "duplicate rejected");
            Check(duplicate.Message == "FSM with the following id already exists in the FSMManager: '" + name + "'", "duplicate diagnostic text");
            Check(manager.TryAcquireFSM(name, out found) && ReferenceEquals(found, first), "acquisition");
            Check(!manager.ReleaseFSM(name) && manager.FSMExists(name), "release with references remaining returns false");
            manager.AddFSM(replacement, true);
            Check(manager.TryGetFSM(name, out found) && ReferenceEquals(found, replacement), "overwrite increments count and replaces instance");
            Check(!manager.ReleaseFSM(first), "instance release uses ID even after overwrite");
            Check(manager.ReleaseFSM(name) && !manager.FSMExists(name), "final release removes lookup");
            Check(!manager.ReleaseFSM(name) && !manager.TryAcquireFSM(name, out found) && found == null, "missing release and acquire");
            manager.AddFSM(first);
            manager.TryAcquireFSM(name, out found);
            var dictionary = new Dictionary<string, FiniteStateMachine> { { "a", first }, { "b", replacement } };
            manager.ReleaseFSMs(dictionary);
            Check(dictionary.Count == 2 && manager.FSMs.Count == 0, "dictionary release retains caller data");
            manager.AddFSM(first);
            manager.ClearAllFSMs();
            Check(manager.FSMs.Count == 0 && !manager.ReleaseFSM(name), "clear both manager dictionaries");

            var global = FSMManager.GetManager();
            int before = global.FSMs.Count;
            var registered = new FiniteStateMachine(name + "-global");
            Check(global.FSMs.Count == before + 1 && global.TryGetFSM(registered.FSMId, out found) && ReferenceEquals(found, registered), "default constructor registers original manager");
            var overwritten = new FiniteStateMachine(name + "-global", isOverwriteOk: true);
            Check(global.TryGetFSM(overwritten.FSMId, out found) && ReferenceEquals(found, overwritten), "constructor forwards overwrite flag");
            Check(!global.ReleaseFSM(registered) && global.ReleaseFSM(overwritten) && global.FSMs.Count == before, "global cleanup respects counts");
            string customName = name + "-custom";
            s_ownedRows.Add(customName);
            ProcessManager.RegisterSystem(manager, customName);
            Check(ReferenceEquals(FSMManager.GetManager(customName), manager), "explicit named manager lookup");
        }

        private static void VerifyDiagnostics()
        {
            // The original LogError is a no-op with no registered HLUnityCore.
            Check(ProcessManager.IsSystemNull("Hardlight.HLUnityCore"), "diagnostic test precondition");
            Check(GraphNameLookup.LookupNameUsingId(int.MinValue) == "unknown", "unknown name returns original fallback");
            var storage = new FSMStorage(0);
            GraphStorageKey key = new GraphStorageKey("Lucid-verification-invalid-read");
            storage.SetValue(key, 1);
            Throws<InvalidCastException>(() => storage.GetValue<float>(key), "conversion logs then rethrows original cast failure");
            var action = FSMStateChangeAction.Create(FSMActionReason.Forced);
            action.ConsoleLog();
            action.Destroy();
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FSM manager verification failed: " + name);
        }
        private static T Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (T exception) { return exception; }
            throw new InvalidOperationException("FSM manager verification failed: " + name);
        }
        private static void Trace(List<string> actual, params string[] expected) =>
            Check(string.Join("|", actual) == string.Join("|", expected), "callback order expected " + string.Join("|", expected) + " got " + string.Join("|", actual));
    }
}
#endif
