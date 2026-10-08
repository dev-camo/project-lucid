using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Verification
{
    // Independent fixed observations of genuine original APIs. No production provider is replaced.
    public static class OriginalBoundMessagingVerification
    {
        private sealed class Counter
        {
            public int Count;
            public void Equal<T>(T expected, T actual, string label)
            {
                ++Count;
                if (!EqualityComparer<T>.Default.Equals(expected, actual))
                    throw new InvalidOperationException(label + ": expected " + expected + ", observed " + actual);
            }
            public void Same(object expected, object actual, string label)
            {
                ++Count;
                if (!ReferenceEquals(expected, actual)) throw new InvalidOperationException(label + ": reference changed");
            }
            public void Throws<T>(Action action, string label) where T : Exception
            {
                ++Count;
                try { action(); }
                catch (T) { return; }
                throw new InvalidOperationException(label + ": expected " + typeof(T).Name);
            }
        }
        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }
        private static IList Cache<T>(ExchangeHandleBoundCallbackArg<T> handle)
            => (IList)Field(handle.GetType(), "m_subscriberCache").GetValue(handle);
        private static IDictionary Bound<T>(ExchangeHandleBoundCallbackArg<T> handle)
            => (IDictionary)Field(handle.GetType(), "m_boundCallbackArgSubscribers").GetValue(handle);
        private static string Trace(List<string> values) => string.Join(",", values);

        public static int OrderingFlagsAndMissingBindings()
        {
            var c = new Counter(); var trace = new List<string>(); string key = "bound", missing = "missing";
            var handle = new ExchangeHandleBoundCallbackArg<string>();
            c.Equal(true, handle.Valid, "constructor validity");
            handle.SubscribeToMessage((in string value) => trace.Add("U:" + value));
            handle.SubscribeToMessage(in key, (in string value) => trace.Add("B:" + value));
            handle.PublishMessage(in key);
            c.Equal("U:bound,B:bound", Trace(trace), "default Both order");
            trace.Clear(); handle.PublishMessage(in key, MessageBroadcastType.Bound);
            c.Equal("B:bound", Trace(trace), "bound only");
            trace.Clear(); handle.PublishMessage(in key, MessageBroadcastType.Unbound);
            c.Equal("U:bound", Trace(trace), "unbound only");
            trace.Clear(); handle.PublishMessage(in key, (MessageBroadcastType)0);
            c.Equal("", Trace(trace), "zero flags"); c.Equal(0, Cache(handle).Count, "zero cache");
            handle.PublishMessage(in key, (MessageBroadcastType)16);
            c.Equal("", Trace(trace), "unknown flags"); c.Equal(0, Cache(handle).Count, "unknown cache");
            c.Equal(false, handle.UnsubscribeFromMessage(in missing, null), "missing unsubscribe");
            c.Equal(1, Bound(handle).Count, "unsubscribe does not create key");
            handle.PublishMessage(in missing, MessageBroadcastType.Bound);
            c.Equal(2, Bound(handle).Count, "publish creates missing bound key");
            c.Equal(0, ((IList)Bound(handle)[missing]).Count, "created empty list");
            c.Equal(1, (int)MessageBroadcastType.Unbound, "Unbound literal");
            c.Equal(2, (int)MessageBroadcastType.Bound, "Bound literal");
            c.Equal(3, (int)MessageBroadcastType.Both, "Both literal");
            return c.Count;
        }

        public static int DuplicateNullAndCallbackFaultPrefixes()
        {
            var c = new Counter(); string key = "key"; var trace = new List<string>();
            var handle = new ExchangeHandleBoundCallbackArg<string>();
            MessageCallback<string> callback = (in string value) => trace.Add("same");
            handle.SubscribeToMessage(in key, callback); handle.SubscribeToMessage(in key, callback);
            handle.PublishMessage(in key, MessageBroadcastType.Bound);
            c.Equal("same,same", Trace(trace), "duplicates retained");
            c.Equal(true, handle.UnsubscribeFromMessage(in key, callback), "first duplicate remove");
            c.Equal(1, ((IList)Bound(handle)[key]).Count, "one duplicate remains");
            trace.Clear(); handle.PublishMessage(in key, MessageBroadcastType.Bound);
            c.Equal("same", Trace(trace), "remaining duplicate");
            c.Equal(true, handle.UnsubscribeFromMessage(in key, callback), "second duplicate remove");
            c.Equal(false, handle.UnsubscribeFromMessage(in key, callback), "third remove false");
            c.Equal(1, Bound(handle).Count, "empty key retained");
            handle.SubscribeToMessage(in key, callback); handle.SubscribeToMessage(in key, null);
            handle.SubscribeToMessage(in key, (in string value) => trace.Add("late"));
            trace.Clear(); c.Throws<NullReferenceException>(() => handle.PublishMessage(in key, MessageBroadcastType.Bound), "null callback faults");
            c.Equal("same", Trace(trace), "null fault after prior callback only");
            c.Equal(3, ((IList)Bound(handle)[key]).Count, "fault retains callback list");
            var throwing = new ExchangeHandleBoundCallbackArg<string>();
            throwing.SubscribeToMessage((in string value) => { trace.Add("throw"); throw new ApplicationException("sentinel"); });
            throwing.SubscribeToMessage((in string value) => trace.Add("after"));
            trace.Clear(); c.Throws<ApplicationException>(() => throwing.PublishMessage(in key), "user callback faults unchanged");
            c.Equal("throw", Trace(trace), "callback exception prefix");
            c.Equal(2, Cache(throwing).Count, "Both cache built before callback fault");
            return c.Count;
        }

        public static int LiveMutationAndReentrantCache()
        {
            var c = new Counter(); string key = "outer", nested = "nested"; var trace = new List<string>();
            var appended = new ExchangeHandleBoundCallbackArg<string>(); bool added = false;
            appended.SubscribeToMessage((in string value) => { trace.Add("first"); if (!added) { added = true; appended.SubscribeToMessage((in string later) => trace.Add("added")); } });
            appended.PublishMessage(in key, MessageBroadcastType.Unbound);
            c.Equal("first,added", Trace(trace), "appended callback visible in same publication");
            var removing = new ExchangeHandleBoundCallbackArg<string>(); MessageCallback<string> self = null;
            self = (in string value) => { trace.Add("self"); removing.UnsubscribeFromMessage(self); };
            removing.SubscribeToMessage(self); removing.SubscribeToMessage((in string value) => trace.Add("middle"));
            removing.SubscribeToMessage((in string value) => trace.Add("last"));
            trace.Clear(); removing.PublishMessage(in key, MessageBroadcastType.Unbound);
            c.Equal("self,last", Trace(trace), "remove shifts and skips next callback");
            trace.Clear(); removing.PublishMessage(in key, MessageBroadcastType.Unbound);
            c.Equal("middle,last", Trace(trace), "subsequent publication retained survivors");
            var reentrant = new ExchangeHandleBoundCallbackArg<string>();
            reentrant.SubscribeToMessage((in string value) => { trace.Add("A:" + value); if (value == key) reentrant.PublishMessage(in nested, MessageBroadcastType.Unbound); });
            reentrant.SubscribeToMessage((in string value) => trace.Add("B:" + value));
            reentrant.SubscribeToMessage(in key, (in string value) => trace.Add("bound:" + value));
            trace.Clear(); reentrant.PublishMessage(in key);
            c.Equal("A:outer,A:nested,B:nested,B:outer", Trace(trace), "nested publication replaces shared cache and skips outer bound list");
            c.Equal(1, Cache(reentrant).Count, "nested cache retained");
            c.Equal(1, ((IList)Bound(reentrant)[key]).Count, "skipped bound callback still registered");
            return c.Count;
        }

        public static int InvalidationAndNullKeyFaultState()
        {
            var c = new Counter(); string key = "key", missing = "after", nullKey = null; int invoked = 0;
            var handle = new ExchangeHandleBoundCallbackArg<string>();
            handle.SubscribeToMessage((in string value) => ++invoked);
            handle.SubscribeToMessage(in key, (in string value) => ++invoked);
            handle.PublishMessage(in key); var cache = Cache(handle); var boundList = (IList)cache[1];
            c.Equal(2, invoked, "initial Both dispatch");
            handle.Invalidate(); c.Equal(false, handle.Valid, "invalidated");
            c.Same(cache, Cache(handle), "shared cache reference retained"); c.Equal(2, cache.Count, "cache entries survive invalidation");
            c.Equal(0, ((IList)cache[0]).Count, "inherited list cleared");
            c.Same(boundList, cache[1], "old bound list retained by cache"); c.Equal(1, boundList.Count, "old bound list not cleared");
            c.Equal(0, Bound(handle).Count, "dictionary cleared");
            handle.PublishMessage(in missing);
            c.Equal(2, invoked, "invalid publication does not add old callbacks");
            c.Equal(1, Bound(handle).Count, "invalid publication still creates missing binding");
            handle.Invalidate(); c.Equal(0, Bound(handle).Count, "second invalidate clears new binding");
            var nullHandle = new ExchangeHandleBoundCallbackArg<string>();
            nullHandle.SubscribeToMessage((in string value) => ++invoked);
            c.Throws<ArgumentNullException>(() => nullHandle.PublishMessage(in nullKey), "null bound key");
            c.Equal(1, Cache(nullHandle).Count, "null-key failure retains unbound cache prefix");
            c.Equal(0, Bound(nullHandle).Count, "null key not inserted");
            c.Equal(2, invoked, "key lookup faults before callback invocation");
            return c.Count;
        }

        public static int ReadonlyHandlesAndWidgetParameterCasts()
        {
            var c = new Counter(); string key = new string(new[] { 'k' });
            MessageCallback<string> callback = (in string value) => { };
            var handle = new SubscribeHandleBoundCallbackArg<string>(callback, key);
            c.Same(callback, (MessageCallback<string>)handle, "callback conversion"); c.Same(key, (string)handle, "object conversion");
            var empty = default(SubscribeHandleBoundCallbackArg<string>);
            c.Same(null, (MessageCallback<string>)empty, "default callback"); c.Same(null, (string)empty, "default object");
            IUIWidgetParameters parameters = new ReferenceParameters();
            c.Same(parameters, parameters.GetAs<ReferenceParameters>(), "reference parameter cast identity");
            c.Throws<InvalidCastException>(() => parameters.GetAs<OtherReferenceParameters>(), "incompatible reference cast");
            IUIWidgetParameters nullParameters = null;
            c.Same(null, nullParameters.GetAs<ReferenceParameters>(), "null reference parameter");
            c.Throws<NullReferenceException>(() => nullParameters.GetAs<ValueParameters>(), "null value parameter unbox");
            c.Throws<InvalidCastException>(() => parameters.GetAs<ValueParameters>(), "wrong value parameter unbox");
            parameters = new ValueParameters { Number = 17 };
            c.Equal(17, parameters.GetAs<ValueParameters>().Number, "genuine interface supports value parameters");
            var field = typeof(UIModernMessage).GetField("Type", BindingFlags.Instance | BindingFlags.NonPublic);
            c.Equal(true, field != null && field.IsInitOnly, "original private readonly message field");
            for (int i = 0; i < 14; ++i)
            {
                var message = new UIModernMessage((UIModernEventType)i);
                c.Equal(i, (int)(UIModernEventType)field.GetValue(message), "message event field " + i);
            }
            return c.Count;
        }
        // Only test values implementing the genuine empty parameter contract; no runtime owner is replaced.
        private sealed class ReferenceParameters : IUIWidgetParameters { }
        private sealed class OtherReferenceParameters : IUIWidgetParameters { }
        private struct ValueParameters : IUIWidgetParameters { public int Number; }

        public static int ExchangeApisAndProcessSubscriptionRestoration()
        {
            var c = new Counter();
            var field = typeof(ProcessManager).GetField("s_systemActionLookup", BindingFlags.Static | BindingFlags.NonPublic);
            var actions = (IDictionary)field.GetValue(null); var prior = new List<DictionaryEntry>();
            var innerSnapshots = new Dictionary<object, List<DictionaryEntry>>();
            foreach (DictionaryEntry entry in actions)
            {
                prior.Add(entry);
                var snapshot = new List<DictionaryEntry>();
                foreach (DictionaryEntry pair in (IDictionary)entry.Value) snapshot.Add(pair);
                innerSnapshots.Add(entry.Key, snapshot);
            }
            // Existing inner rows are detached intact; only newly owned rows are modified.
            actions.Clear();
            try
            {
                string message = "message", key = "object", missing = "missing"; int ordinary = 0, unbound = 0, bound = 0;
                var exchange = new MessageExchangeBoundCallbackArg<string>();
                c.Equal(1, actions.Count, "constructor adds shutdown row only");
                c.Equal(1, ((IDictionary)actions[SystemAction.Shutdown]).Count, "one shutdown subscription");
                MessageCallback callback = () => ++ordinary;
                var ordinaryHandle = exchange.SubscribeToMessage(in message, callback);
                c.Same(callback, (MessageCallback)ordinaryHandle, "ordinary returned callback");
                c.Same(exchange.GetExchangeHandle(in message), exchange.GetExchangeHandle(in message), "ordinary cache identity");
                exchange.PublishMessage(in message); c.Equal(1, ordinary, "ordinary dispatch");
                c.Equal(true, exchange.UnsubscribeFromMessage(in message, callback), "ordinary unsubscribe");
                c.Equal(false, exchange.UnsubscribeFromMessage(in message, callback), "ordinary repeated unsubscribe");
                MessageCallback<string> u = (in string value) => ++unbound, b = (in string value) => ++bound;
                var unboundHandle = exchange.SubscribeToMessage<string>(in message, u);
                var boundHandle = exchange.SubscribeToMessage(in message, in key, b);
                c.Same(u, (MessageCallback<string>)unboundHandle, "unbound returned callback");
                c.Same(b, (MessageCallback<string>)boundHandle, "bound returned callback");
                c.Same(key, (string)boundHandle, "bound returned object");
                var cached = exchange.GetExchangeHandle<string>(in message);
                c.Same(cached, exchange.GetExchangeHandle<string>(in message), "generic cache identity");
                exchange.PublishMessage(in message, in key); c.Equal(1, unbound, "exchange unbound dispatch"); c.Equal(1, bound, "exchange bound dispatch");
                c.Equal(true, exchange.UnsubscribeFromMessage(in message, in boundHandle), "readonly handle unsubscribe");
                c.Equal(false, exchange.UnsubscribeFromMessage(in message, in key, b), "bound repeat unsubscribe");
                c.Equal(true, exchange.UnsubscribeFromMessage<string>(in message, u), "unbound unsubscribe");
                c.Equal(false, exchange.UnsubscribeFromMessage(in missing, callback), "missing exchange unsubscribe creates empty handle");
                c.Equal(true, exchange.GetExchangeHandle(in missing).Valid, "missing handle retained");
                var shutdown = typeof(MessageExchangeBase<string>).GetMethod("OnShutdown", BindingFlags.Instance | BindingFlags.NonPublic);
                shutdown.Invoke(exchange, new object[] { null });
                c.Equal(false, cached.Valid, "real shutdown invalidates cached handle");
                c.Equal(1, ((IDictionary)actions[SystemAction.Shutdown]).Count, "shutdown preserves original subscription");
            }
            finally
            {
                actions.Clear(); foreach (var entry in prior) actions.Add(entry.Key, entry.Value);
            }
            c.Same(actions, field.GetValue(null), "outer action lookup restored identity");
            c.Equal(prior.Count, actions.Count, "prior row count restored");
            bool rowIdentities = true, innerEntries = true;
            foreach (var entry in prior)
            {
                rowIdentities &= ReferenceEquals(entry.Value, actions[entry.Key]);
                var row = (IDictionary)entry.Value;
                var snapshot = innerSnapshots[entry.Key];
                innerEntries &= row.Count == snapshot.Count;
                foreach (var pair in snapshot) innerEntries &= row.Contains(pair.Key) && ReferenceEquals(pair.Value, row[pair.Key]);
            }
            c.Equal(true, rowIdentities, "all prior inner row identities restored");
            c.Equal(true, innerEntries, "all prior inner entries unchanged");
            return c.Count;
        }
    }
}
