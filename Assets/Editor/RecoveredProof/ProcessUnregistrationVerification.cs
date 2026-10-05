#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Native-derived registry teardown proof. Action storage is seeded through
    // reflection because the original subscription/dispatch API is unresolved.
    public static class ProcessUnregistrationVerification
    {
        private sealed class TestSystem : ISystem { }
        private sealed class OtherSystem : ISystem { }
        private sealed class EquivalentSystem : ISystem
        {
            public override bool Equals(object value) => value is EquivalentSystem;
            public override int GetHashCode() => 1;
        }
        private static IDictionary Registry => (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> Actions =>
            (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager).GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        public static void Run()
        {
            // Isolate the bounded registry fixture, preserving all pre-existing
            // rows and references. No original app or saved content is involved.
            IDictionary registry = Registry;
            var saved = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in registry) saved.Add(entry);
            var savedActions = new Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>(Actions);
            registry.Clear(); Actions.Clear();
            try
            {
                VerifyOrderAndRetention(); Reset();
                VerifyIdentityAndAliases(); Reset();
                VerifyFailureAndReplacement(); Reset();
                VerifyLiveAndSnapshotEnumeration(); Reset();
                VerifyUnsubscribeBoundaries();
            }
            finally
            {
                registry.Clear(); foreach (DictionaryEntry entry in saved) registry.Add(entry.Key, entry.Value);
                Actions.Clear(); foreach (var entry in savedActions) Actions.Add(entry.Key, entry.Value);
            }
            Debug.Log("Original ProcessManager unregistration source-based verification passed. Engine dispatch and game bootstrap remain unresolved.");
        }

        private static void Reset() { Registry.Clear(); Actions.Clear(); }
        private static void VerifyOrderAndRetention()
        {
            var first = new TestSystem(); var second = new TestSystem();
            SystemRef reference = ProcessManager.RegisterSystem(first, "order");
            SystemRef<TestSystem> typed = ProcessManager.GetSystemRef<TestSystem>("order");
            var actions = new Dictionary<ISystem, Action<object>> { { first, _ => throw new Exception("must never dispatch") } };
            Actions.Add(SystemAction.Update, actions);
            var trace = new List<string>();
            reference.OnSystemShutdown += value =>
            {
                Check(ReferenceEquals(value, first) && ReferenceEquals(reference.GetSafe(), first) && ReferenceEquals(typed.GetSafe(), first), "untyped shutdown sees original references");
                Check(!actions.ContainsKey(first), "unsubscribe occurs before shutdown callback");
                trace.Add("untyped");
            };
            typed.OnSystemShutdown += value =>
            {
                Check(reference.IsNull() && ReferenceEquals(value, first) && ReferenceEquals(typed.GetSafe(), first), "typed shutdown follows cleared untyped reference");
                trace.Add("typed");
            };
            ProcessManager.UnregisterSystem("order");
            Check(trace.Count == 2 && trace[0] == "untyped" && trace[1] == "typed", "untyped then typed callback order");
            Check(reference.IsNull() && typed.IsNull() && Registry.Count == 1 && Actions.Count == 1 && actions.Count == 0, "rows and empty action dictionaries retained");
            Check(ReferenceEquals(reference, ProcessManager.RegisterSystem(second, "order")) && ReferenceEquals(typed, ProcessManager.GetSystemRef<TestSystem>("order")) && ReferenceEquals(typed.GetSafe(), second), "replacement after unregister reuses cached references");
            ProcessManager.UnregisterSystem("absent");
            Throws<ArgumentNullException>(() => ProcessManager.UnregisterSystem((string)null), "null name dictionary failure");
            Reset();
            ProcessManager.RegisterSystem(first);
            ProcessManager.UnregisterSystem<TestSystem>();
            Check(ProcessManager.IsSystemNull<TestSystem>(), "generic unregister uses original default name");
        }

        private static void VerifyIdentityAndAliases()
        {
            var first = new EquivalentSystem(); var equal = new EquivalentSystem();
            SystemRef a = ProcessManager.RegisterSystem(first, "alias-a");
            SystemRef b = ProcessManager.RegisterSystem(first, "alias-b");
            SystemRef distinct = ProcessManager.RegisterSystem(equal, "equal-but-distinct");
            int notifications = 0;
            a.OnSystemShutdown += _ => ++notifications; b.OnSystemShutdown += _ => ++notifications;
            ProcessManager.UnregisterSystem(first);
            Check(a.IsNull() && b.IsNull() && notifications == 2 && ReferenceEquals(distinct.GetSafe(), equal), "object overload processes all reference aliases without using Equals");
            ProcessManager.GetSystemRef("empty");
            ProcessManager.UnregisterSystem((ISystem)null);
            Check(Registry.Count == 4 && ReferenceEquals(distinct.GetSafe(), equal), "null object matches empty rows without removing rows");
        }

        private static void VerifyFailureAndReplacement()
        {
            var first = new TestSystem(); var replacement = new TestSystem();
            SystemRef reference = ProcessManager.RegisterSystem(first, "throw");
            SystemRef<TestSystem> typed = ProcessManager.GetSystemRef<TestSystem>("throw");
            var actions = new Dictionary<ISystem, Action<object>> { { first, _ => { } } };
            Actions.Add(SystemAction.Shutdown, actions);
            Action<ISystem> throwing = _ => throw new InvalidOperationException("shutdown fixture");
            reference.OnSystemShutdown += throwing;
            Throws<InvalidOperationException>(() => ProcessManager.UnregisterSystem("throw"), "shutdown failure propagates");
            Check(actions.Count == 0 && ReferenceEquals(reference.GetSafe(), first) && ReferenceEquals(typed.GetSafe(), first), "failure retains refs after earlier action removal");
            reference.OnSystemShutdown -= throwing;
            reference.OnSystemShutdown += _ => ProcessManager.RegisterSystem(replacement, "throw", canReplace: true);
            ISystem typedShutdownArgument = null;
            typed.OnSystemShutdown += value => typedShutdownArgument = value;
            ProcessManager.UnregisterSystem("throw");
            Check(reference.IsNull() && typed.IsNull() && ReferenceEquals(typedShutdownArgument, replacement), "outer revoke clears reentrant replacement after callbacks");

            Reset();
            reference = ProcessManager.RegisterSystem(first, "typed-throw");
            typed = ProcessManager.GetSystemRef<TestSystem>("typed-throw");
            var incompatible = ProcessManager.GetSystemRef<OtherSystem>("typed-throw");
            // Native replacement makes the incompatible cache valid with a null
            // typed value, so a skipped later revoke has observable validity.
            ProcessManager.RegisterSystem(replacement, "typed-throw", canReplace: true);
            typed.OnSystemShutdown += _ => throw new InvalidOperationException("typed fixture");
            Throws<InvalidOperationException>(() => ProcessManager.UnregisterSystem("typed-throw"), "typed shutdown failure propagates");
            Check(reference.IsNull() && ReferenceEquals(typed.GetSafe(), replacement) && incompatible.IsValid(), "typed failure prevents later cached revokes");
        }

        private static void VerifyLiveAndSnapshotEnumeration()
        {
            var first = new TestSystem(); var added = new TestSystem();
            SystemRef reference = ProcessManager.RegisterSystem(first, "live");
            reference.OnSystemShutdown += _ => ProcessManager.RegisterSystem(added, "added");
            Throws<InvalidOperationException>(() => ProcessManager.UnregisterSystem(first), "object overload live enumerator invalidation");
            Check(reference.IsNull() && ReferenceEquals(ProcessManager.GetSystemRef("added").GetSafe(), added), "live enumeration failure retains callback mutations");

            Reset();
            reference = ProcessManager.RegisterSystem(first, "snapshot-first");
            var later = ProcessManager.RegisterSystem(new TestSystem(), "snapshot-later");
            var replacement = new TestSystem();
            reference.OnSystemShutdown += _ =>
            {
                ProcessManager.RegisterSystem(added, "added");
                ProcessManager.RegisterSystem(replacement, "snapshot-later", canReplace: true);
            };
            ISystem observed = null;
            later.OnSystemShutdown += system => observed = system;
            ProcessManager.UnregisterAllSystems();
            Check(reference.IsNull() && later.IsNull() && ReferenceEquals(observed, replacement), "snapshot entries remain shared SystemInfo objects");
            Check(ReferenceEquals(ProcessManager.GetSystemRef("added").GetSafe(), added) && Registry.Count == 3, "unregister-all snapshot excludes new rows and retains all registry keys");
        }

        private static void VerifyUnsubscribeBoundaries()
        {
            var system = new TestSystem();
            Check(!system.UnsubscribeFromAction(SystemAction.Update), "missing action removal returns false");
            Actions.Add(SystemAction.Update, new Dictionary<ISystem, Action<object>> { { system, _ => { } } });
            Actions.Add(SystemAction.Configure, new Dictionary<ISystem, Action<object>> { { system, _ => { } } });
            Check(system.UnsubscribeFromAction(SystemAction.Update) && !system.UnsubscribeFromAction(SystemAction.Update) && Actions.Count == 2, "single unsubscribe returns removal result and retains empty action row");
            ProcessManager.UnsubscribeFromAllActions(system);
            Check(Actions[SystemAction.Configure].Count == 0 && Actions.Count == 2, "all unsubscribe retains empty action rows");
            Throws<ArgumentNullException>(() => ProcessManager.UnsubscribeFromAllActions(null), "null unsubscribe reaches inner dictionary");
            Actions.Clear(); ProcessManager.UnsubscribeFromAllActions(null);
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Process unregister verification failed: " + message); }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Process unregister verification failed: " + message);
        }
    }
}
#endif
