#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class ProcessActionVerification
    {
        private sealed class TestSystem : ISystem { public TestSystem() { } }
        private sealed class OtherSystem : ISystem { }
        private sealed class ConstructedValue
        {
            public static int Count;
            public ConstructedValue() { ++Count; }
        }
        private static IDictionary Registry => (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> Actions =>
            (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager).GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static List<Action<object>> ActionList => (List<Action<object>>)typeof(ProcessManager).GetField("s_actionList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static FieldInfo ProgressField => typeof(ProcessManager).GetField("s_systemActionInProgress", BindingFlags.Static | BindingFlags.NonPublic);
        private static bool InProgress => (bool)ProgressField.GetValue(null);

        public static void Run()
        {
            var rows = new List<DictionaryEntry>(); foreach (DictionaryEntry entry in Registry) rows.Add(entry);
            var actions = new Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>(Actions);
            var snapshot = new List<Action<object>>(ActionList); bool flag = InProgress;
            Reset();
            try
            {
                VerifyDictionaryConstruction(); Reset();
                VerifySubscriptionsAndSingleDispatch(); Reset();
                VerifyGlobalSnapshotAndFailure(); Reset();
                VerifyNestedDispatch(); Reset();
                VerifyResetOrdering(); Reset();
                VerifyQueriesAndDefaultNameQuirk();
            }
            finally
            {
                Reset(); foreach (DictionaryEntry entry in rows) Registry.Add(entry.Key, entry.Value);
                foreach (var entry in actions) Actions.Add(entry.Key, entry.Value);
                ActionList.AddRange(snapshot); ProgressField.SetValue(null, flag);
            }
            Debug.Log("Original ProcessManager action/query source-based verification passed. Original application initialization and full gameplay remain unresolved.");
        }
        private static void Reset() { Registry.Clear(); Actions.Clear(); ActionList.Clear(); ProgressField.SetValue(null, false); }
        private static void VerifyDictionaryConstruction()
        {
            ConstructedValue.Count = 0;
            IDictionary<string, ConstructedValue> map = new Dictionary<string, ConstructedValue> { { "null", null } };
            Check(map.TryGetOrNew("null") == null && ConstructedValue.Count == 0, "existing null value is retained");
            var value = map.TryGetOrNew("new");
            Check(value != null && ReferenceEquals(map["new"], value) && ReferenceEquals(map.TryGetOrNew("new"), value) && ConstructedValue.Count == 1, "new value constructed once and retained");
            Throws<ArgumentNullException>(() => map.TryGetOrNew(null), "null key reaches dictionary");
            Throws<NullReferenceException>(() => ((IDictionary<string, ConstructedValue>)null).TryGetOrNew("key"), "null dictionary dereference");
        }
        private static void VerifySubscriptionsAndSingleDispatch()
        {
            var system = new TestSystem(); object context = new object(); int calls = 0;
            system.SubscribeToAction(SystemAction.Configure, _ => throw new Exception("replaced"));
            system.SubscribeToAction(SystemAction.Configure, value => { Check(ReferenceEquals(value, context) && !InProgress, "single dispatch forwards context without flag"); ++calls; });
            Check(Actions[SystemAction.Configure].Count == 1, "subscription overwrites same system key");
            system.ProcessSystemAction(SystemAction.Configure, context);
            Check(calls == 1 && Registry.Count == 0, "action subscription does not register a system");
            system.ProcessSystemAction(SystemAction.Initialise);
            Check(Actions.ContainsKey(SystemAction.Initialise) && Actions[SystemAction.Initialise].Count == 0, "missing action dispatch creates an empty row");
            system.SubscribeToAction(SystemAction.Update, null);
            Throws<NullReferenceException>(() => system.ProcessSystemAction(SystemAction.Update), "stored null callback single dispatch fails");
            Throws<ArgumentNullException>(() => ProcessManager.SubscribeToAction(null, SystemAction.Shutdown, null), "null system subscription reaches inner indexer");
            Check(Actions.ContainsKey(SystemAction.Shutdown), "null subscription retains newly created outer row");
            Throws<ArgumentNullException>(() => ProcessManager.ProcessSystemAction(null, SystemAction.AppInitialise), "null system dispatch reaches inner lookup");
            Actions[SystemAction.AppShutdown] = null;
            Throws<NullReferenceException>(() => system.SubscribeToAction(SystemAction.AppShutdown, _ => { }), "stored null action row not repaired");
        }
        private static void VerifyGlobalSnapshotAndFailure()
        {
            var first = new TestSystem(); var second = new TestSystem(); var added = new TestSystem();
            var trace = new List<string>(); object context = new object();
            first.SubscribeToAction(SystemAction.Update, value =>
            {
                Check(InProgress && ReferenceEquals(value, context), "global callback flag and context"); trace.Add("first");
                second.SubscribeToAction(SystemAction.Update, _ => trace.Add("new-second"));
                added.SubscribeToAction(SystemAction.Update, _ => trace.Add("added"));
                first.UnsubscribeFromAction(SystemAction.Update);
            });
            second.SubscribeToAction(SystemAction.Update, _ => trace.Add("old-second"));
            ProcessManager.ProcessSystemAction(SystemAction.Update, context);
            Trace(trace, "first", "old-second");
            Check(!InProgress && ActionList.Count == 2, "success resets flag and retains completed snapshot");
            trace.Clear(); ProcessManager.ProcessSystemAction(SystemAction.Update);
            Trace(trace, "new-second", "added");
            Reset();
            first.SubscribeToAction(SystemAction.Configure, _ => throw new InvalidOperationException("action fixture"));
            second.SubscribeToAction(SystemAction.Configure, _ => trace.Add("must not run"));
            trace.Clear(); Throws<InvalidOperationException>(() => ProcessManager.ProcessSystemAction(SystemAction.Configure), "global callback failure propagates");
            Check(InProgress && ActionList.Count == 2 && trace.Count == 0, "failure leaves flag and snapshot with remaining callbacks skipped");
            first.SubscribeToAction(SystemAction.Configure, _ => trace.Add("recovered"));
            second.UnsubscribeFromAction(SystemAction.Configure);
            ProcessManager.ProcessSystemAction(SystemAction.Configure);
            Trace(trace, "recovered"); Check(!InProgress, "later call proceeds despite prior in-progress flag");
            first.SubscribeToAction(SystemAction.Shutdown, null);
            Throws<NullReferenceException>(() => ProcessManager.ProcessSystemAction(SystemAction.Shutdown), "null global callback fails");
            Check(InProgress, "null callback failure retains progress flag");
        }
        private static void VerifyNestedDispatch()
        {
            var first = new TestSystem(); var second = new TestSystem(); var trace = new List<string>();
            first.SubscribeToAction(SystemAction.Configure, _ =>
            {
                trace.Add("outer"); ProcessManager.ProcessSystemAction(SystemAction.Initialise); trace.Add("after-inner");
                Check(!InProgress, "inner success clears shared flag while outer callback is active");
            });
            second.SubscribeToAction(SystemAction.Configure, _ => trace.Add("skipped"));
            second.SubscribeToAction(SystemAction.Initialise, _ => trace.Add("inner"));
            Throws<InvalidOperationException>(() => ProcessManager.ProcessSystemAction(SystemAction.Configure), "nested global dispatch invalidates outer shared-list enumerator");
            Trace(trace, "outer", "inner", "after-inner"); Check(!InProgress && ActionList.Count == 1, "nested snapshot and flag retained after outer invalidation");
        }
        private static void VerifyResetOrdering()
        {
            var first = new TestSystem(); var added = new TestSystem(); SystemRef addedRef = null;
            var reference = ProcessManager.RegisterSystem(first, "first");
            first.SubscribeToAction(SystemAction.Update, _ => { });
            reference.OnSystemShutdown += _ =>
            {
                Check(!Actions[SystemAction.Update].ContainsKey(first), "reset unregister removes actions before shutdown");
                addedRef = ProcessManager.RegisterSystem(added, "new-during-shutdown");
                added.SubscribeToAction(SystemAction.Update, _ => { });
            };
            ProcessManager.ProcessSystemAction(SystemAction.Update);
            ProgressField.SetValue(null, true);
            ProcessManager.ForceReset();
            Check(reference.IsNull() && addedRef.IsValid() && Registry.Count == 0 && Actions.Count == 0 && ActionList.Count == 0 && !InProgress, "reset clears new rows without revoking entries outside unregister snapshot");
            Reset();
            reference = ProcessManager.RegisterSystem(first, "failure");
            var later = ProcessManager.RegisterSystem(new TestSystem(), "later");
            first.SubscribeToAction(SystemAction.Update, _ => { });
            reference.OnSystemShutdown += _ => throw new InvalidOperationException("reset fixture");
            ActionList.Add(_ => { }); ProgressField.SetValue(null, true);
            Throws<InvalidOperationException>(() => ProcessManager.ForceReset(), "reset shutdown failure propagates");
            Check(reference.IsValid() && later.IsValid() && Registry.Count == 2 && Actions.Count == 1 && Actions[SystemAction.Update].Count == 0 && ActionList.Count == 1 && InProgress, "reset failure stops before collection clears and flag reset");
        }
        private static void VerifyQueriesAndDefaultNameQuirk()
        {
            ProcessManager.ForEachSystem(null); ProcessManager.ForEachSystemSafe(null);
            Check(ProcessManager.FindSystemRef(null) == null && ProcessManager.FindSystemRefSafe(null) == null, "null callbacks do not fail on empty enumeration");
            var empty = ProcessManager.GetSystemRef("empty");
            var first = new TestSystem(); var second = new TestSystem();
            var a = ProcessManager.RegisterSystem(first, "a"); var alias = ProcessManager.RegisterSystem(first, "alias");
            ProcessManager.RegisterSystem(new OtherSystem(), "other");
            Check(!ProcessManager.IsSystemValid("missing") && !ProcessManager.IsSystemValid("empty") && ProcessManager.IsSystemValid<TestSystem>("a"), "valid query never creates missing rows");
            int all = 0, safe = 0;
            ProcessManager.ForEachSystem((system, reference) => { ++all; if (system == null) Check(ReferenceEquals(reference, empty), "non-safe callback sees null system row"); });
            ProcessManager.ForEachSystemSafe((system, reference) => { ++safe; Check(system != null, "safe callback excludes null values"); });
            Check(all == 4 && safe == 3 && ReferenceEquals(ProcessManager.FindSystemRef((system, reference) => system == null), empty), "live query iteration includes retained aliases");
            Check(ReferenceEquals(ProcessManager.FindSystemRefSafe((system, reference) => system is TestSystem), a), "safe find returns first type match");
            var references = ProcessManager.GetSystemRefsOfType<TestSystem>(); var systems = ProcessManager.GetSystemsOfType<TestSystem>();
            Check(references.Count == 2 && ReferenceEquals(references[0], a) && ReferenceEquals(references[1], alias) && systems.Count == 2 && ReferenceEquals(systems[0], first) && ReferenceEquals(systems[1], first), "type queries preserve matching alias duplication");
            Check(ProcessManager.GetSystemRefOfType<TestSystem>() == null && ReferenceEquals(ProcessManager.GetSystemRefOfType<ISystem>(), a) && ReferenceEquals(ProcessManager.GetSystemOfType<TestSystem>(), first), "type-reference query casts untyped reference while system query converts contained system");
            Check(ProcessManager.GetSystemOfType<ConstructibleMissing>() == null, "missing type query returns null");
            Throws<NullReferenceException>(() => ProcessManager.ForEachSystem(null), "null nonempty callback failure");
            Throws<NullReferenceException>(() => ProcessManager.FindSystemRefSafe(null), "null safe callback fails at first non-null system");
            Throws<InvalidOperationException>(() => ProcessManager.ForEachSystemSafe((system, reference) => ProcessManager.RegisterSystem(new TestSystem(), "added")), "live foreach registry mutation invalidates enumerator");
            Check(ProcessManager.GetSystemSafe<TestSystem>("other") == null && ReferenceEquals(ProcessManager.GetSystemSafe<TestSystem>("a"), first), "safe query uses cached typed reference");
            var defaultSystem = ProcessManager.RegisterSystem(second);
            Check(ReferenceEquals(ProcessManager.GetSystemAutoCreate<TestSystem>("a"), second), "existing custom-name auto-create routes through default-name getter");
            TestSystem created = ProcessManager.GetSystemAutoCreate<TestSystem>("brand-new");
            Check(created != null && !ReferenceEquals(created, second) && ReferenceEquals(created, ProcessManager.GetSystemRef("brand-new").GetSafe()), "missing named auto-create registers and returns constructed system");
        }
        private sealed class ConstructibleMissing : ISystem { public ConstructibleMissing() { } }
        private static void Trace(List<string> trace, params string[] expected) { Check(trace.Count == expected.Length, "trace length"); for (int i = 0; i < expected.Length; ++i) Check(trace[i] == expected[i], "trace " + i); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Process action verification failed: " + message); }
        private static void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException("Process action verification failed: " + message); }
    }
}
#endif
