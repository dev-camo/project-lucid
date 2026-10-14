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
            string defaultManager = ProcessManager.GetDefaultName<FSMManager>();
            IDictionary rows = RegistryRows();
            var original = new List<DictionaryEntry>();
            foreach (DictionaryEntry row in rows) original.Add(row);
            object[] layout = CaptureLayout(rows);
            // A retained empty default row belongs to someone else, observers included. Set the
            // row object aside untouched so the fixture's own default manager never fires them.
            object borrowedDefault = null;
            if (rows.Contains(defaultManager) && ProcessManager.IsSystemNull(defaultManager))
            {
                borrowedDefault = rows[defaultManager];
                rows.Remove(defaultManager);
            }
            if (!rows.Contains(defaultManager)) s_ownedRows.Add(defaultManager);
            Exception failure = null, cleanup = null;
            try
            {
                VerifyFastActions();
                VerifySystemReferences();
                VerifyManagers();
                VerifyDiagnostics();
            }
            catch (Exception error) { failure = error; }
            try { ReleaseOwnedRows(s_ownedRows); }
            catch (Exception error) { cleanup = error; }
            finally
            {
                s_ownedRows.Clear();
                if (borrowedDefault != null) rows[defaultManager] = borrowedDefault;
                if (HoldsExactly(rows, original)) RestoreLayout(rows, layout);
            }
            // The fixture's own failure outranks a cleanup failure.
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            if (cleanup != null) ExceptionDispatchInfo.Capture(cleanup).Throw();
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
        private static bool HoldsExactly(IDictionary rows, List<DictionaryEntry> original)
        {
            if (rows.Count != original.Count) return false;
            foreach (DictionaryEntry row in original)
                if (!rows.Contains(row.Key) || !ReferenceEquals(rows[row.Key], row.Value)) return false;
            return true;
        }

        // Unregister each owned row through the original API, then reconcile everything its
        // callbacks did before dropping the row. One failed row never keeps later rows registered,
        // and the first failure is rethrown after every row is released.
        internal static void ReleaseOwnedRows(IList<string> owned)
        {
            IDictionary rows = RegistryRows();
            Exception first = null;
            foreach (string row in owned)
            {
                if (!rows.Contains(row)) continue;
                try { ReleaseOwnedRow(row, rows[row], ref first); }
                finally { rows.Remove(row); }
            }
            if (first != null) ExceptionDispatchInfo.Capture(first).Throw();
        }

        // Ownership of a row's teardown, for the original unregister and anything its observers do:
        //  - every reference is notified at most once; a reference whose observers already ran
        //    (or threw) is detached through the public event and revoked silently;
        //  - shutdown observers may register replacements under the owned name, revalidating
        //    references or adding typed ones, so the row is reconciled until no reference is set;
        //  - every system that entered the row during teardown loses its action subscriptions,
        //    as the original unregister does for the system it removes.
        // Observers are tracked only through public event subscriptions; no private field is written.
        private static void ReleaseOwnedRow(string row, object info, ref Exception first)
        {
            List<object> initial = References(info);
            var completed = new HashSet<object>(new ReferenceComparer());
            var entered = new List<ISystem>();
            var sentinels = new List<Sentinel>();
            object untyped = initial[0];
            try
            {
                foreach (object reference in initial)
                    sentinels.Add(Subscribe(reference, "OnSystemShutdown", () => completed.Add(reference)));
                sentinels.Add(Subscribe(untyped, "OnSystemStartup", system => { if (system != null) entered.Add((ISystem)system); }));
                bool[] validAtStart = initial.Select(IsValid).ToArray();
                try { ProcessManager.UnregisterSystem(row); }
                catch (Exception error) { first = first ?? error; }
                // Notified: observers completed, or the first reference whose observers threw.
                var notified = new HashSet<object>(completed, new ReferenceComparer());
                for (int i = 0; i < initial.Count; i++)
                {
                    if (!validAtStart[i] || completed.Contains(initial[i])) continue;
                    notified.Add(initial[i]);
                    break;
                }
                Reconcile(info, notified, entered, ref first);
            }
            finally
            {
                foreach (Sentinel sentinel in sentinels)
                    sentinel.Event.RemoveEventHandler(sentinel.Reference, sentinel.Observer);
            }
            foreach (ISystem system in entered)
                ProcessManager.UnsubscribeFromAllActions(system);
        }

        private const int ReconcileLimit = 32;

        private static void Reconcile(object info, HashSet<object> notified, List<ISystem> entered, ref Exception first)
        {
            MethodInfo revoke = typeof(ProcessManager).Assembly.GetType("Hardlight.ISystemRef")
                .GetMethod("InternalRevokeSystem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int pass = 0; pass < ReconcileLimit; pass++)
            {
                bool changed = false;
                foreach (object reference in References(info))
                {
                    if (!IsValid(reference)) continue;
                    changed = true;
                    ISystem current = ((SystemRef)info.GetType().GetField("SystemRef").GetValue(info)).GetSafe();
                    if (current != null && !entered.Contains(current)) entered.Add(current);
                    if (!notified.Add(reference))
                    {
                        DetachShutdownObservers(reference);
                        revoke.Invoke(reference, null);
                        continue;
                    }
                    try { revoke.Invoke(reference, null); }
                    catch (TargetInvocationException error)
                    {
                        first = first ?? error.InnerException;
                        DetachShutdownObservers(reference);
                        revoke.Invoke(reference, null);
                    }
                }
                if (!changed) return;
            }
            throw new InvalidOperationException("Owned registry row teardown did not settle; observers keep re-registering.");
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

        // Reads the observer list and removes each observer through the public event accessor;
        // no private field is written.
        private static void DetachShutdownObservers(object reference)
        {
            Type type = reference.GetType();
            while (type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) == null)
                type = type.BaseType;
            var observers = (Delegate)type.GetField("OnSystemShutdown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).GetValue(reference);
            if (observers == null) return;
            EventInfo shutdown = type.GetEvent("OnSystemShutdown");
            foreach (Delegate observer in observers.GetInvocationList())
                shutdown.RemoveEventHandler(reference, observer);
        }

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
