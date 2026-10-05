using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded native-derived checks of the settings dependency used by original authored Boot.
    public static class StackableDataVerification
    {
        private static int checks;
        private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action callback, string message) where T : Exception
        {
            checks++;
            try { callback(); } catch (T) { return; }
            throw new InvalidOperationException(message);
        }
        private static OrderedDictionary Dictionary(StackableData data) => (OrderedDictionary)typeof(StackableData).GetField("m_dataDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
        private static IDictionary Cache(StackableData data) => (IDictionary)typeof(StackableData).GetField("m_stackableCache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
        private static bool Dirty(StackableData data, int id) => (bool)Cache(data)[id].GetType().GetField("IsDirty").GetValue(Cache(data)[id]);
        public static void Run()
        {
            checks = 0;
            VerifyPresenceAndOrder(); VerifyCache(); VerifyNotifications(); VerifyOperations(); VerifyMetadata();
            Debug.Log("Original StackableData verified checks=" + checks);
        }
        public static int RunManaged()
        {
            checks = 0;
            VerifyPresenceAndOrder(); VerifyCache(); VerifyNotifications(); VerifyOperations(); VerifyMetadata();
            return checks;
        }
        private static void VerifyPresenceAndOrder()
        {
            var data = new StackableData();
            Check(Dictionary(data).Count == 1, "constructor allocates empty base override");
            Check(!data.HasData(7), "empty base has no data");
            Check(!data.TryGet(7, out int missing, false, 99) && missing == 99, "cache-disabled missing returns supplied default");
            Check(Cache(data).Count == 0, "cache-disabled missing performs no cache insertion");
            Check(!data.TryGet(7, out missing, true, 98) && missing == 98 && Cache(data).Count == 0, "failed retrieval does not cache absence");
            var first = data.AddOverride(7, 30);
            var second = data.AddOverride(7, 50);
            Check(data.Get<int>(7) == 50, "latest uses newest matching override");
            data.SetRetrievalOperation<int>(7, StackableData.RetrievalOperation.Base);
            Check(data.Get<int>(7) == 30, "base falls through empty base to earliest matching override");
            data.SetBaseValue(7, 10);
            Check(data.Get<int>(7) == 10, "base value becomes earliest");
            data.SetRetrievalOperation<int>(7, StackableData.RetrievalOperation.Latest);
            Check(data.Get<int>(7) == 50 && data.GetOverride<int>(first, 7) == 30, "direct override ignores aggregate cache");
            var list = new List<int> { -1, -2 };
            Check(ReferenceEquals(data.GetStack(7, list), list) && string.Join(",", list) == "10,30,50", "stack clears supplied list and returns ascending order");
            Check(data.GetStack<int>(7, values => values[0] * 100 + values[2]) == 1050, "stack callback sees complete ordered values");
            Throws<NullReferenceException>(() => data.GetStack<int>(7, (Func<List<int>, int>)null), "null callback fails only after stack traversal");
            data.AddOverride(first, 7, "wrong type");
            Check(data.HasData(7) && data.GetStack<int>(7, (List<int>)null).Count == 2, "presence includes wrong type while typed stack skips it");
            Throws<NullReferenceException>(() => data.GetOverride<int>(first, 7), "wrong typed direct override is dereferenced");
            Throws<NullReferenceException>(() => data.GetOverride<int>(second, 999), "missing direct value is dereferenced");
            Dictionary(data)[second] = new Dictionary<int, StackableData.StackableDataContainer> { [91] = null };
            Check(data.HasData(91) && data.GetStack<int>(91, (List<int>)null).Count == 0, "null container counts as untyped presence only");
            Dictionary(data)[first] = "invalid dictionary";
            Check(!data.HasData(101), "invalid ordered entries ignored by presence");
            data.RemoveOverrides(first);
            Check(Dictionary(data).Contains(first), "remove invalid entry remains silent and leaves handle");
            data.RemoveOverrides(new StackableDataHandle());
            Check(Dictionary(data).Count == 3, "remove absent handle remains silent");
        }
        private static void VerifyCache()
        {
            var data = new StackableData();
            data.SetRetrievalOperation<int>(1, StackableData.RetrievalOperation.Addition, 123);
            Check(Dirty(data, 1), "operation registration dirties new default cache");
            Check(data.TryGet(1, out int value, false, -1) && value == 123, "default cache is present even without data");
            Check(!data.TryGet(1, out value, true, -2) && value == -2 && Dirty(data, 1), "failed dirty update returns supplied default and retains dirty cache");
            Check(data.TryGet(1, out value, false) && value == 123, "failure retains previous cache value");
            var handle = data.AddOverride(1, 8);
            Check(data.Get<int>(1) == 8 && !Dirty(data, 1), "successful update clears dirty flag");
            object cached = Cache(data)[1];
            data.SetRetrievalOperation<string>(1, StackableData.RetrievalOperation.Addition, "ignored");
            Check(ReferenceEquals(Cache(data)[1], cached) && !Dirty(data, 1), "same operation does not replace type/default or dirty cache");
            Check(!data.TryGet(1, out string text, false, "fallback") && text == "fallback", "wrong clean cache type is absent");
            data.AddOverride(handle, 1, "now string");
            data.SetRetrievalOperation<string>(1, StackableData.RetrievalOperation.Latest);
            Check(data.Get<string>(1) == "now string" && !ReferenceEquals(Cache(data)[1], cached), "dirty typed update replaces wrong cache type");
            data.RemoveOverrides(handle);
            Check(data.TryGet(1, out text, false) && text == "now string", "removed dirty cache can be read stale without update");
            Check(!data.TryGet(1, out text, true, "missing") && text == "missing" && Dirty(data, 1), "removed data doesn't erase stale dirty cache");
            data.AddOverride(2, (string)null);
            Check(data.TryGet(2, out text) && text == null, "typed null value is present and cacheable");
            data.SetRetrievalOperation<int>(88, (StackableData.RetrievalOperation)77, 200);
            Check(!data.TryGet(88, out value, true, 77) && value == 77, "invalid operation without matching data doesn't throw");
            data.AddOverride(88, 2);
            try { data.Get<int>(88); throw new InvalidOperationException("expected invalid-operation error"); }
            catch (ArgumentOutOfRangeException e) { Check(e.ParamName == "retrievalOperation" && Equals(e.ActualValue, (StackableData.RetrievalOperation)77), "invalid enum error retains original name and boxed enum"); }
        }
        private static void VerifyNotifications()
        {
            var data = new StackableData();
            var trace = new List<int>();
            var handle = data.CreateOverride();
            data.AddOverride(handle, 1, 4); data.AddOverride(handle, 2, 5);
            data.Get<int>(1); data.Get<int>(2);
            Action<int> listener = id => { Check(Dirty(data, id), "notification follows dirty bit"); trace.Add(id); };
            data.OnDataUpdated += listener;
            data.AddOverride(handle, 1, 9);
            Check(string.Join(",", trace) == "1", "add override notifies once");
            data.ClearOverrides(handle);
            Check(string.Join(",", trace) == "1,1,2" && !data.HasData(1), "clear copies keys, clears first, then notifies in key order");
            data.OnDataUpdated -= listener;
            data.AddOverride(handle, 1, 1);
            Check(trace.Count == 3, "event unsubscribe follows original removal");
            var another = data.CreateOverride();
            data.AddOverride(handle, 2, 2); data.AddOverride(another, 3, 3);
            var reentrant = new List<int>();
            data.OnDataUpdated += id => { reentrant.Add(id); if (id == 1) data.ClearOverrides(another); };
            data.ClearOverrides(handle);
            Check(string.Join(",", reentrant) == "1,3", "reentrant clear replaces shared keys and suppresses remaining outer notification");
            var remove = new StackableData();
            var removing = remove.CreateOverride();
            remove.AddOverride(removing, 6, 1); remove.AddOverride(removing, 7, 2);
            var raw = (Dictionary<int, StackableData.StackableDataContainer>)Dictionary(remove)[removing];
            remove.OnDataUpdated += id => { Check(!Dictionary(remove).Contains(removing), "handle removed before callback"); raw.Add(8, new StackableData.StackableDataContainer<int>(3)); };
            Throws<InvalidOperationException>(() => remove.RemoveOverrides(removing), "original dictionary enumerator notices callback mutation");
        }
        private static void VerifyOperations()
        {
            var data = new StackableData();
            data.SetBaseValue(1, 3, StackableData.RetrievalOperation.Addition); data.AddOverride(1, 4);
            Check(data.Get<int>(1) == 7, "integer addition across ordered stack");
            data.SetRetrievalOperation<int>(1, StackableData.RetrievalOperation.Multiply);
            Check(data.Get<int>(1) == 12, "integer multiplication");
            data.AddOverride(1, 0);
            Check(data.Get<int>(1) == 0, "latest zero multiplication exits before older entries");
            data.SetBaseValue(2, int.MaxValue, StackableData.RetrievalOperation.Addition); data.AddOverride(2, 1);
            Check(data.Get<int>(2) == int.MinValue, "integer wrapping is retained");
            data.SetBaseValue(3, long.MaxValue, StackableData.RetrievalOperation.Addition); data.AddOverride(3, 1L);
            Check(data.Get<long>(3) == long.MinValue, "Int64 operations retain width and wrap");
            data.SetBaseValue(4, false, StackableData.RetrievalOperation.LogicalOr); data.AddOverride(4, true);
            Check(data.Get<bool>(4), "Boolean OR");
            data.SetRetrievalOperation<bool>(4, StackableData.RetrievalOperation.LogicalAnd);
            Check(!data.Get<bool>(4), "Boolean AND");
            data.SetBaseValue(5, 1.0f, StackableData.RetrievalOperation.Addition); data.AddOverride(5, -1e20f); data.AddOverride(5, 1e20f);
            Check(data.Get<float>(5) == 1f, "Single addition folds newest to oldest");
            data.SetBaseValue(6, new Vector3(1e10f, 1e10f, 1e10f), StackableData.RetrievalOperation.Multiply);
            data.AddOverride(6, new Vector3(1e-6f, 0, 0));
            Check(data.Get<Vector3>(6).x == 1e-6f, "Vector approximate-zero exits before older large multiplier");
            data.SetBaseValue(7, "a", StackableData.RetrievalOperation.LogicalAnd);
            try { data.Get<string>(7); throw new InvalidOperationException("missing operation did not throw"); }
            catch (NotImplementedException e) { Check(e.Message == "No LogicalAnd operation available for type: System.String", "missing operation error uses exact type formatting"); }
            var additions = (Dictionary<Type, Delegate>)typeof(StackableData).GetField("Additions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            bool hadPriorAddition = additions.TryGetValue(typeof(string), out Delegate priorAddition);
            try
            {
                StackableData.RegisterTypeOperations<string>(addition: (StackableData.StackableDataContainer<string> container, ref StackableData.ResultCarrier<string> result) =>
                { result.SetValue(result.HasValue ? result.Value + container.Value : container.Value); return StackableData.OperationAction.Continue; });
                data.SetRetrievalOperation<string>(7, StackableData.RetrievalOperation.Addition); data.AddOverride(7, "b");
                Check(data.Get<string>(7) == "ba", "custom operation executes reverse ordered fold");
                StackableData.RegisterTypeOperations<string>();
                data.AddOverride(7, "c");
                Check(data.Get<string>(7) == "cba", "null operation registrations preserve prior mapping");
            }
            finally
            {
                if (hadPriorAddition) additions[typeof(string)] = priorAddition;
                else additions.Remove(typeof(string));
            }
        }
        private static void VerifyMetadata()
        {
            Type t = typeof(StackableData);
            Check(!t.Attributes.HasFlag(TypeAttributes.BeforeFieldInit), "explicit native static constructor semantics");
            foreach (string name in new[] { "m_dataDictionary", "m_stackableCache", "m_baseStackableDataHandle", "m_updatedIds" })
                Check(t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).IsInitOnly, "original readonly field " + name);
            Check(t.GetField("OnDataUpdated", BindingFlags.NonPublic | BindingFlags.Instance) != null, "compiler backing field retains native name");
            Check(typeof(StackableData.ResultCarrier<int>).GetProperty("Value").SetMethod.IsPrivate, "result setter original visibility");
            Check(typeof(StackableData).GetCustomAttributes(typeof(Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute), false).Length == 2, "original type options retained");
        }
    }
}
