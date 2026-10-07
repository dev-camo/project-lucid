using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hardlight;

namespace ProjectLucid.Editor
{
    // Each verification invocation owns its handles, counters, and cleanup.
    // The fixed API expectations describe the shipped declarations. Compiler-generated
    // names and native body binding require separate preservation evidence.
    public static class OriginalMessageCompletionVerification
    {
        public static int RunOriginalApi()
        {
            return new CompletionApiVerification().Run() + new CompletionGenericVerification().Run();
        }

        public static int RunOriginalSynchronousBoundaries()
        {
            return new Verification().RunSynchronous();
        }

        // Unity executes this IEnumerator while its synchronization context remains free
        // to resume the original Task.Delay continuations. No task is synchronously waited.
        public static IEnumerator RunOriginalTimeoutBoundaries(Action<int> completed)
        {
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));
            var verification = new Verification();
            yield return verification.RunTimeouts();
            completed(verification.CheckCount);
        }

        private sealed class Verification
        {
            private int checks;
            private readonly List<IExchangeHandle> handles = new List<IExchangeHandle>();
            private readonly List<ISystem> systems = new List<ISystem>();
            public int CheckCount => checks;
            private T Own<T>(T handle)
                where T : IExchangeHandle
            {
                handles.Add(handle);
                return handle;
            }

            private T OwnSystem<T>(T system)
                where T : ISystem
            {
                systems.Add(system);
                return system;
            }

            private void Cleanup()
            {
                foreach (IExchangeHandle handle in handles)
                    handle.Invalidate();
                foreach (ISystem system in systems)
                {
                    try
                    {
                        system.ProcessSystemAction(SystemAction.Shutdown);
                    }
                    finally
                    {
                        system.UnsubscribeFromAction(SystemAction.Shutdown);
                    }
                }
            }

            private void Check(bool value, string reason)
            {
                checks++;
                if (!value)
                    throw new InvalidOperationException(reason);
            }

            private void ExpectFault<T>(Action action, string reason)
                where T : Exception
            {
                try
                {
                    action();
                }
                catch (T)
                {
                    Check(true, reason);
                    return;
                }

                throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + reason);
            }

            private static Type CompletionBase(object handle) => handle.GetType().BaseType;
            private static object FieldValue(object handle, string name) => CompletionBase(handle).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(handle);
            private static int Count(object collection) => (int)collection.GetType().GetProperty("Count").GetValue(collection);
            private static object PendingData(object handle) => ((IDictionary)FieldValue(handle, "m_completionDataLookup")).Values.Cast<object>().Single();
            private static object DebugData(object data) => data.GetType().GetField("m_completionDataDebug", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
            public int RunSynchronous()
            {
                try
                {
                    VerifyEmptyPublish();
                    VerifyPendingAndDuplicateCompletions();
                    VerifyDebugPooling();
                    VerifyInvalidation();
                    VerifySubscriberMutationAndFaults();
                    VerifyReentrantTimeout();
                    VerifyReadonlyArgumentsAndRouting();
                    return checks;
                }
                finally
                {
                    Cleanup();
                }
            }

            private void VerifyEmptyPublish()
            {
                var order = new List<string>();
                var empty = Own(new ExchangeHandleWithCompletion());
                empty.PublishMessage(() => order.Add("empty"));
                Check(order.SequenceEqual(new[]{"empty"}), "empty finishes synchronously");
                Check(Count(FieldValue(empty, "m_completionDataLookup")) == 0 && Count(FieldValue(empty, "m_completionDataPool")) == 0, "empty no state allocation");
                ExpectFault<NullReferenceException>(() => empty.PublishMessage(null), "empty null finished callback faults");
            }

            private void VerifyPendingAndDuplicateCompletions()
            {
                var order = new List<string>();
                var h = Own(new ExchangeHandleWithCompletion());
                Action a = null, b = null;
                h.SubscribeToMessage(done =>
                {
                    order.Add("first");
                    a = done;
                });
                h.SubscribeToMessage(done =>
                {
                    order.Add("second");
                    b = done;
                });
                int finished = 0;
                h.PublishMessage(() =>
                {
                    order.Add("finish");
                    finished++;
                });
                Check(order.Last() == "second" && finished == 0, "publish waits for completions");
                Check(ReferenceEquals(a, b), "nondebug subscribers share callback");
                a();
                Check(finished == 0, "first completion insufficient");
                a();
                Check(finished == 1, "duplicate callback meets count early");
                b();
                Check(finished == 1, "remaining callback after completion ignored");
                Check(Count(FieldValue(h, "m_completionDataPool")) == 1 && Count(FieldValue(h, "m_completionDataLookup")) == 0, "completed state returned to pool");
                var stale = a;
                h.PublishMessage(() => finished++);
                stale();
                Check(finished == 1, "stale callback ignores reused ID");
                a();
                b();
                Check(finished == 2, "fresh reused completion succeeds");
            }

            private void VerifyDebugPooling()
            {
                var d = Own(new ExchangeHandleWithCompletion());
                d.EnableDebugInformation(true);
                Action da = null, db = null;
                d.SubscribeToMessage(c => da = c);
                d.SubscribeToMessage(c => db = c);
                int df = 0;
                d.PublishMessage(() => df++);
                Check(!ReferenceEquals(da, db), "debug subscriber callbacks separate");
                var dd = PendingData(d);
                Check(DebugData(dd) != null, "debug record allocated");
                var debugList = (IList)DebugData(dd).GetType().GetField("m_subscribers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DebugData(dd));
                Check(debugList.Count == 2 && debugList[0] != null, "debug copies subscriber list");
                da();
                Check(debugList[0] == null && debugList[1] != null && df == 0, "debug index cleared before complete");
                da();
                Check(df == 1, "debug duplicate also completes early");
                d.EnableDebugInformation(false);
                d.PublishMessage(() => df++);
                Check(DebugData(PendingData(d)) != null, "pooled debug state stays enabled after toggle");
                da();
                db();
                Check(df == 2, "debug reused state completes");
                var nd = Own(new ExchangeHandleWithCompletion());
                Action nc = null;
                nd.SubscribeToMessage(c => nc = c);
                nd.PublishMessage(() =>
                {
                });
                var noDebug = PendingData(nd);
                nc();
                nd.EnableDebugInformation(true);
                nd.PublishMessage(() =>
                {
                });
                Check(ReferenceEquals(noDebug, PendingData(nd)) && DebugData(PendingData(nd)) == null, "pooled nondebug state stays nondebug after toggle");
                nc();
            }

            private void VerifyInvalidation()
            {
                var inv = Own(new ExchangeHandleWithCompletion());
                Action ic = null;
                int iff = 0;
                inv.SubscribeToMessage(c => ic = c);
                inv.PublishMessage(() => iff++);
                int beforeId = (int)FieldValue(inv, "m_nextMessageID");
                inv.Invalidate();
                Check(!inv.Valid && Count(FieldValue(inv, "m_completionDataLookup")) == 0 && Count(FieldValue(inv, "m_completionDataPool")) == 0, "invalidate clears validity and collections");
                ic();
                Check(iff == 0, "invalidate suppresses pending callback");
                Check((int)FieldValue(inv, "m_nextMessageID") == beforeId, "invalidate retains ID");
                inv.PublishMessage(() => iff++);
                Check(iff == 1, "invalid empty publish still invokes finished");
                ExpectFault<NullReferenceException>(() => inv.SubscribeToMessage(null), "invalid null subscriber original fault");
            }

            private void VerifySubscriberMutationAndFaults()
            {
                var mutation = Own(new ExchangeHandleWithCompletion());
                MessageCallbackWithCompletion second = c =>
                {
                };
                mutation.SubscribeToMessage(c => mutation.UnsubscribeFromMessage(second));
                mutation.SubscribeToMessage(second);
                ExpectFault<ArgumentOutOfRangeException>(() => mutation.PublishMessage(() =>
                {
                }), "live removal captured count faults");
                Check(Count(FieldValue(mutation, "m_completionDataLookup")) == 1, "throw skips timeout and retains pending data");
                mutation.Invalidate();
                var add = Own(new ExchangeHandleWithCompletion());
                int added = 0;
                add.SubscribeToMessage(c =>
                {
                    add.SubscribeToMessage(x => added++);
                    c();
                });
                add.PublishMessage(() =>
                {
                });
                Check(added == 0, "new subscriber outside captured count not invoked");
                var nul = Own(new ExchangeHandleWithCompletion());
                nul.SubscribeToMessage(null);
                ExpectFault<NullReferenceException>(() => nul.PublishMessage(() =>
                {
                }), "valid null callback stored and faults when invoked");
                Check(Count(FieldValue(nul, "m_completionDataLookup")) == 1, "null subscriber leaves pending state");
                nul.Invalidate();
                var thrower = Own(new ExchangeHandleWithCompletion());
                int end = 0;
                thrower.SubscribeToMessage(c => c());
                ExpectFault<InvalidOperationException>(() => thrower.PublishMessage(() =>
                {
                    end++;
                    throw new InvalidOperationException();
                }), "finished exception propagated synchronously");
                Check(end == 1 && Count(FieldValue(thrower, "m_completionDataLookup")) == 0 && Count(FieldValue(thrower, "m_completionDataPool")) == 1, "state pooled before throwing finished callback");
            }

            private void VerifyReentrantTimeout()
            {
                var reentrant = Own(new ExchangeHandleWithCompletion());
                int calls = 0, outer = 0, inner = 0;
                Action pending = null;
                reentrant.SubscribeToMessage(c =>
                {
                    if (calls++ == 0)
                        c();
                    else
                        pending = c;
                });
                reentrant.PublishMessage(() =>
                {
                    outer++;
                    reentrant.PublishMessage(() => inner++, -1);
                }, 0);
                Check(outer == 1 && inner == 1 && calls == 2, "outer zero timeout acts on reentrant pooled record");
                pending();
                Check(inner == 1, "late reentrant callback ignored after zero timeout");
            }

            private void VerifyReadonlyArgumentsAndRouting()
            {
                string key = "arity";
                int v = 7, w = 8, x = 9, y = 10, zv = 11;
                var e = OwnSystem(new MessageExchangeWithCompletion<string>());
                int total = 0, complete = 0;
                MessageCallbackWithCompletion cb0 = c =>
                {
                    total++;
                    c();
                };
                e.SubscribeToMessage(in key, cb0);
                e.PublishMessage(in key, () => complete++);
                Check(total == 1 && complete == 1, "exchange arity0");
                Check(e.UnsubscribeFromMessage(in key, cb0) && !e.UnsubscribeFromMessage(in key, cb0), "exchange unsubscribe0 removes first match");
                MessageCallbackWithCompletion<int> cb1 = (in int p, Action c) =>
                {
                    total += p;
                    c();
                };
                e.SubscribeToMessage<int>(in key, cb1);
                e.PublishMessage(in key, in v, () => complete++);
                Check(total == 8 && complete == 2, "exchange arity1 readonly arg");
                Check(e.UnsubscribeFromMessage<int>(in key, cb1), "unsubscribe1");
                MessageCallbackWithCompletion<int, int> cb2 = (in int p, in int q, Action c) =>
                {
                    total += p + q;
                    c();
                };
                e.SubscribeToMessage<int, int>(in key, cb2);
                e.PublishMessage(in key, in v, in w, () => complete++);
                Check(total == 23 && complete == 3, "exchange arity2");
                Check(e.UnsubscribeFromMessage<int, int>(in key, cb2), "unsubscribe2");
                MessageCallbackWithCompletion<int, int, int> cb3 = (in int p, in int q, in int r, Action c) =>
                {
                    total += p + q + r;
                    c();
                };
                e.SubscribeToMessage<int, int, int>(in key, cb3);
                e.PublishMessage(in key, in v, in w, in x, () => complete++);
                Check(total == 47 && complete == 4, "exchange arity3");
                Check(e.UnsubscribeFromMessage<int, int, int>(in key, cb3), "unsubscribe3");
                MessageCallbackWithCompletion<int, int, int, int> cb4 = (in int p, in int q, in int r, in int s, Action c) =>
                {
                    total += p + q + r + s;
                    c();
                };
                e.SubscribeToMessage<int, int, int, int>(in key, cb4);
                e.PublishMessage(in key, in v, in w, in x, in y, () => complete++);
                Check(total == 81 && complete == 5, "exchange arity4");
                Check(e.UnsubscribeFromMessage<int, int, int, int>(in key, cb4), "unsubscribe4");
                MessageCallbackWithCompletion<int, int, int, int, int> cb5 = (in int p, in int q, in int r, in int s, in int t, Action c) =>
                {
                    total += p + q + r + s + t;
                    c();
                };
                e.SubscribeToMessage<int, int, int, int, int>(in key, cb5);
                e.PublishMessage(in key, in v, in w, in x, in y, in zv, () => complete++);
                Check(total == 126 && complete == 6, "exchange arity5");
                Check(e.UnsubscribeFromMessage<int, int, int, int, int>(in key, cb5), "unsubscribe5");
                Check(ReferenceEquals(e.GetExchangeHandle<int>(in key), e.GetExchangeHandle<int>(in key)), "same exchange key/signature cached handle");
                Check(!ReferenceEquals(e.GetExchangeHandle<int>(in key), e.GetExchangeHandle<int, int>(in key)), "different signatures have distinct handles");
                var de = OwnSystem(new MessageExchangeWithCompletion<string>(true));
                var dh = de.GetExchangeHandle(in key);
                Check((bool)FieldValue(dh, "m_includeDebugInformation"), "exchange debug option enabled in override");
                var ne = OwnSystem(new MessageExchangeWithCompletion<string>());
                Check(!(bool)FieldValue(ne.GetExchangeHandle(in key), "m_includeDebugInformation"), "exchange default debug false");
            }

            private static IEnumerator AwaitWithoutBlocking(Task task)
            {
                var deadline = Stopwatch.StartNew();
                while (!task.IsCompleted)
                {
                    if (deadline.Elapsed.TotalSeconds > 5)
                        throw new TimeoutException("Completion fixture continuation did not resume within five seconds.");
                    yield return null;
                }

                // The task is already complete; GetResult propagates a fault without blocking.
                task.GetAwaiter().GetResult();
            }

            public IEnumerator RunTimeouts()
            {
                try
                {
                    foreach (int timeout in new[]{-1, -2, int.MinValue})
                    {
                        var negative = Own(new ExchangeHandleWithCompletion());
                        Action done = null;
                        int finished = 0;
                        negative.SubscribeToMessage(callback => done = callback);
                        negative.PublishMessage(() => finished++, timeout);
                        yield return AwaitWithoutBlocking(Task.Delay(20));
                        Check(finished == 0, "Every negative timeout skips the delay: " + timeout);
                        done();
                        Check(finished == 1, "A negative timeout leaves the subscriber callback active: " + timeout);
                    }

                    var zero = Own(new ExchangeHandleWithCompletion());
                    Action afterZero = null;
                    int zeroFinished = 0;
                    zero.SubscribeToMessage(callback => afterZero = callback);
                    zero.PublishMessage(() => zeroFinished++, 0);
                    Check(zeroFinished == 1, "An already completed zero-delay task finishes within PublishMessage");
                    afterZero();
                    Check(zeroFinished == 1, "A callback after zero timeout is ignored");
                    var timed = Own(new ExchangeHandleWithCompletion());
                    var signal = new TaskCompletionSource<bool>();
                    Action afterTimeout = null;
                    int timedFinished = 0;
                    timed.SubscribeToMessage(callback => afterTimeout = callback);
                    timed.PublishMessage(() =>
                    {
                        timedFinished++;
                        signal.TrySetResult(true);
                    }, 20);
                    Check(timedFinished == 0, "A positive timeout remains pending during PublishMessage");
                    yield return AwaitWithoutBlocking(signal.Task);
                    Check(timedFinished == 1, "A positive timeout completes after its continuation resumes");
                    afterTimeout();
                    Check(timedFinished == 1, "A late subscriber cannot repeat the timed completion");
                    var reused = Own(new ExchangeHandleWithCompletion());
                    Action current = null;
                    int reusedFinished = 0;
                    reused.SubscribeToMessage(callback => current = callback);
                    reused.PublishMessage(() => reusedFinished++, 20);
                    current();
                    reused.PublishMessage(() => reusedFinished++, -1);
                    yield return AwaitWithoutBlocking(Task.Delay(80));
                    Check(reusedFinished == 1, "An old timeout cannot finish a reused record with a different message ID");
                    current();
                    Check(reusedFinished == 2, "The reused record's current subscriber still completes it");
                    var invalidated = Own(new ExchangeHandleWithCompletion());
                    var invalidatedSignal = new TaskCompletionSource<bool>();
                    invalidated.SubscribeToMessage(callback =>
                    {
                    });
                    invalidated.PublishMessage(() => invalidatedSignal.TrySetResult(true), 20);
                    invalidated.Invalidate();
                    yield return AwaitWithoutBlocking(Task.Delay(80));
                    Check(!invalidatedSignal.Task.IsCompleted, "Invalidation suppresses an already scheduled timeout completion");
                }
                finally
                {
                    Cleanup();
                }
            }
        }
    }
}
