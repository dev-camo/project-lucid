using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineThreading and its nested ThreadDef / Worker / delegates. The nested types are internal in the
    // original, so the tests reach them by reflection. Every test that touches the static pool swaps in a private array of fresh
    // ThreadDefs and restores the original array (and aborts every thread it started) when it finishes, so no worker thread and
    // no queued handler outlives the test. Touching SplineThreading at all runs its static constructor, which subscribes
    // Application.quitting exactly as the shipped class does.
    public sealed class OriginalDreamteckSplineThreadingTests
    {
        private const int WaitMilliseconds = 10000;

        private static readonly BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type ThreadDefType
        {
            get { return typeof(SplineThreading).GetNestedType("ThreadDef", BindingFlags.NonPublic); }
        }

        private static FieldInfo ThreadsField
        {
            get { return typeof(SplineThreading).GetField("threads", AllMembers); }
        }

        private static object NewThreadDef()
        {
            return Activator.CreateInstance(ThreadDefType, true);
        }

        private static Thread ThreadOf(object def)
        {
            return (Thread)ThreadDefType.GetField("thread", AllMembers).GetValue(def);
        }

        private static bool IsAlive(object def)
        {
            return (bool)ThreadDefType.GetProperty("isAlive", AllMembers).GetValue(def, null);
        }

        private static bool Computing(object def)
        {
            return (bool)ThreadDefType.GetProperty("computing", AllMembers).GetValue(def, null);
        }

        private static int QueuedCount(object def)
        {
            object worker = WorkerOf(def);
            return ((ICollection)worker.GetType().GetField("instructions", AllMembers).GetValue(worker)).Count;
        }

        private static object WorkerOf(object def)
        {
            return ThreadDefType.GetField("worker", AllMembers).GetValue(def);
        }

        private static void Invoke(object def, string method, params object[] arguments)
        {
            try
            {
                ThreadDefType.GetMethod(method, AllMembers).Invoke(def, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(WaitMilliseconds);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    return false;
                }

                Thread.Sleep(1);
            }

            return true;
        }

        private static void StopThread(object def)
        {
            Thread thread = ThreadOf(def);
            if (thread != null && thread.IsAlive)
            {
                thread.Abort();
                thread.Join(WaitMilliseconds);
            }
        }

        // Replaces SplineThreading.threads with `count` fresh, unstarted ThreadDefs for one test, and on disposal aborts every thread
        // of the pool then in place (the pool grows by replacement in threadCount) and restores the original array.
        private sealed class PoolScope : IDisposable
        {
            private readonly Array original;

            public PoolScope(int count)
            {
                original = (Array)ThreadsField.GetValue(null);
                Array pool = Array.CreateInstance(ThreadDefType, count);
                for (int i = 0; i < count; i++)
                {
                    pool.SetValue(NewThreadDef(), i);
                }

                ThreadsField.SetValue(null, pool);
            }

            public Array Pool
            {
                get { return (Array)ThreadsField.GetValue(null); }
            }

            public object Def(int index)
            {
                return Pool.GetValue(index);
            }

            public void Dispose()
            {
                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    foreach (object def in Pool)
                    {
                        StopThread(def);
                    }
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = previous;
                    ThreadsField.SetValue(null, original);
                }
            }
        }

        // Original static constructor 0x4e3170 / ThreadDef constructor 0x4e2fd0 / Worker constructor 0x4e3fb0: the pool starts with two
        // ThreadDefs (other tests or editor code may already have grown or started the live pool, so only its minimum size is
        // asserted, and idleness is checked on a freshly constructed def); a fresh def has a worker holding an empty queue and a
        // thread-start delegate, and no thread until Restart.
        [Test]
        public void LivePoolHasAtLeastTwoDefsAndAFreshDefIsIdle()
        {
            Array pool = (Array)ThreadsField.GetValue(null);
            Assert.That(pool.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(typeof(SplineThreading).GetField("locker", AllMembers).GetValue(null), Is.Not.Null);
            Assert.That(typeof(SplineThreading).GetField("locker", AllMembers).IsInitOnly, Is.True);

            foreach (object def in pool)
            {
                Assert.That(def, Is.Not.Null);
            }

            object fresh = NewThreadDef();
            Assert.That(ThreadOf(fresh), Is.Null, "no thread before Restart");
            Assert.That(IsAlive(fresh), Is.False);
            Assert.That(Computing(fresh), Is.False);
            Assert.That(ThreadDefType.GetField("start", AllMembers).GetValue(fresh), Is.InstanceOf<ParameterizedThreadStart>());
            object worker = WorkerOf(fresh);
            Assert.That(worker, Is.Not.Null);
            Assert.That(((ICollection)worker.GetType().GetField("instructions", AllMembers).GetValue(worker)).Count, Is.EqualTo(0));
            Assert.That((bool)worker.GetType().GetField("computing", AllMembers).GetValue(worker), Is.False);
        }

        // Original Queue 0x4e3c40 enqueues on the worker's queue; nothing runs until a thread is interrupted.
        [Test]
        public void QueueAppendsToTheWorkerQueueInOrder()
        {
            object def = NewThreadDef();
            SplineThreading.EmptyHandler first = () => { };
            SplineThreading.EmptyHandler second = () => { };
            Invoke(def, "Queue", first);
            Invoke(def, "Queue", second);
            object worker = WorkerOf(def);
            IEnumerable queue = (IEnumerable)worker.GetType().GetField("instructions", AllMembers).GetValue(worker);
            List<object> queued = new List<object>();
            foreach (object handler in queue)
            {
                queued.Add(handler);
            }

            Assert.That(queued.Count, Is.EqualTo(2));
            Assert.That(ReferenceEquals(queued[0], first), Is.True);
            Assert.That(ReferenceEquals(queued[1], second), Is.True);
        }

        // Original Restart 0x4e30f0 creates and starts a thread on RunThread with the def's worker; RunThread 0x4e34a0 sets computing
        // false and sleeps until interrupted, then marks the worker computing, drains the queue under the shared lock (skipping null
        // handlers) and goes back to sleep.
        [Test]
        public void RestartedThreadSleepsUntilInterruptedThenDrainsTheQueueOnItsOwnThread()
        {
            using (PoolScope scope = new PoolScope(1))
            {
                object def = scope.Def(0);
                Invoke(def, "Restart");
                Thread thread = ThreadOf(def);
                Assert.That(thread, Is.Not.Null);
                Assert.That(IsAlive(def), Is.True);
                Assert.That(Computing(def), Is.False);

                List<int> threadIds = new List<int>();
                ManualResetEventSlim done = new ManualResetEventSlim(false);
                Invoke(def, "Queue", (SplineThreading.EmptyHandler)null);
                Invoke(def, "Queue", (SplineThreading.EmptyHandler)(() => { lock (threadIds) { threadIds.Add(Thread.CurrentThread.ManagedThreadId); } }));
                Invoke(def, "Queue", (SplineThreading.EmptyHandler)(() => { lock (threadIds) { threadIds.Add(Thread.CurrentThread.ManagedThreadId); } done.Set(); }));
                Thread.Sleep(50);
                Assert.That(threadIds.Count, Is.EqualTo(0), "queued handlers wait for the interrupt");

                Invoke(def, "Interrupt");
                Assert.That(done.Wait(WaitMilliseconds), Is.True);
                Assert.That(threadIds, Is.EqualTo(new[] { thread.ManagedThreadId, thread.ManagedThreadId }), "both handlers ran on the worker thread, the null one was skipped");
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.Not.EqualTo(thread.ManagedThreadId));
                Assert.That(WaitUntil(() => !Computing(def)), Is.True, "the worker clears computing and sleeps again");
                Assert.That(IsAlive(def), Is.True);

                // A second batch runs after a second interrupt.
                ManualResetEventSlim again = new ManualResetEventSlim(false);
                Invoke(def, "Queue", (SplineThreading.EmptyHandler)(() => again.Set()));
                Invoke(def, "Interrupt");
                Assert.That(again.Wait(WaitMilliseconds), Is.True);
            }
        }

        // Original RunThread 0x4e34a0: the sleep's catch(ThreadInterruptedException) and catch(Exception) are siblings, and the lock
        // region inside the first has only its finally pad (0x4e36ce, rethrow 0x4e3715, then _Unwind_Resume 0x4e38a9). A handler
        // exception therefore escapes the thread without the THREAD EXCEPTION or "Thread stopped" logs, with computing still set.
        [Test]
        public void AHandlerExceptionEscapesUnloggedAndLeavesTheWorkerComputing()
        {
            using (PoolScope scope = new PoolScope(1))
            {
                object def = scope.Def(0);
                Invoke(def, "Restart");
                Thread thread = ThreadOf(def);
                List<string> messages = new List<string>();
                Application.LogCallback capture = (condition, stackTrace, type) => { lock (messages) { messages.Add(condition); } };
                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                Application.logMessageReceivedThreaded += capture;
                try
                {
                    Invoke(def, "Queue", (SplineThreading.EmptyHandler)(() => { throw new InvalidOperationException("dreamteck-test"); }));
                    Invoke(def, "Interrupt");
                    Assert.That(thread.Join(WaitMilliseconds), Is.True, "the thread ended");
                    Assert.That(IsAlive(def), Is.False);
                    Assert.That(Computing(def), Is.True, "the catch clauses never ran, so computing was not cleared");
                    lock (messages)
                    {
                        Assert.That(messages.Exists(m => m.StartsWith("THREAD EXCEPTION")), Is.False);
                        Assert.That(messages.Contains("Thread stopped"), Is.False);
                    }
                }
                finally
                {
                    Application.logMessageReceivedThreaded -= capture;
                    LogAssert.ignoreFailingMessages = previous;
                }
            }
        }

        // Original RunThread 0x4e34a0: an exception other than ThreadInterruptedException raised while sleeping (here a thread abort)
        // reaches catch(Exception) at 0x4e3765, which logs "THREAD EXCEPTION " + message when the message is not empty (string
        // inequality against ""). Mono re-raises the abort at the end of the catch, so only the error log is asserted.
        [Test]
        public void AnAbortDuringTheSleepLogsTheThreadException()
        {
            using (PoolScope scope = new PoolScope(1))
            {
                object def = scope.Def(0);
                Invoke(def, "Restart");
                Thread thread = ThreadOf(def);
                List<string> errors = new List<string>();
                Application.LogCallback capture = (condition, stackTrace, type) =>
                {
                    if (type == LogType.Error)
                    {
                        lock (errors) { errors.Add(condition); }
                    }
                };
                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                Application.logMessageReceivedThreaded += capture;
                try
                {
                    Assert.That(WaitUntil(() => thread.ThreadState.HasFlag(ThreadState.WaitSleepJoin)), Is.True, "the worker is asleep");
                    thread.Abort();
                    Assert.That(thread.Join(WaitMilliseconds), Is.True, "the thread ended");
                    lock (errors)
                    {
                        Assert.That(errors.Exists(m => m.StartsWith("THREAD EXCEPTION ")), Is.True);
                    }
                }
                finally
                {
                    Application.logMessageReceivedThreaded -= capture;
                    LogAssert.ignoreFailingMessages = previous;
                }
            }
        }

        // Original Interrupt 0x4e3ca0 dereferences the thread (a def that was never started faults); Abort 0x4e3e50 only aborts a live
        // thread and ignores a missing or finished one; Restart replaces the thread.
        [Test]
        public void InterruptNeedsAThreadAbortIgnoresDeadOnesAndRestartReplaces()
        {
            object idle = NewThreadDef();
            Assert.Throws<NullReferenceException>(() => Invoke(idle, "Interrupt"));
            Invoke(idle, "Abort");
            Assert.That(ThreadOf(idle), Is.Null);

            using (PoolScope scope = new PoolScope(1))
            {
                object def = scope.Def(0);
                Invoke(def, "Restart");
                Thread first = ThreadOf(def);
                Invoke(def, "Restart");
                Thread second = ThreadOf(def);
                Assert.That(ReferenceEquals(first, second), Is.False, "Restart builds a new Thread");
                Assert.That(first.IsAlive, Is.True, "the replaced thread keeps running");

                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    Invoke(def, "Abort");
                    Assert.That(second.Join(WaitMilliseconds), Is.True, "Abort stops the live thread");
                    Assert.That(IsAlive(def), Is.False);
                    Invoke(def, "Abort");
                }
                finally
                {
                    // The replaced thread is no longer in the pool, so the scope would not stop it.
                    if (first.IsAlive)
                    {
                        first.Abort();
                        first.Join(WaitMilliseconds);
                    }

                    LogAssert.ignoreFailingMessages = previous;
                }
                Assert.That(first.IsAlive, Is.False);
            }
        }

        // Original Run 0x4e38c0 restarts dead threads, queues the handler on the first thread that is not computing (or on the last
        // one) and interrupts it. That loop is the text after the `#if UNITY_EDITOR` block in Run; this test covers the editor
        // branch only (EditMode, Application.isPlaying false, read directly): Run calls the handler synchronously and starts no pool
        // thread. The shipped selection is covered by OriginalDreamteckSplineThreadingPlayModeTests.RunQueuesOnTheFirstIdleThreadAndWakesIt.
        [Test]
        public void RunQueuesOnTheFirstIdleThreadAndWakesIt()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                int ranOn = -1;
                int calls = 0;
                SplineThreading.Run(() => { ranOn = Thread.CurrentThread.ManagedThreadId; calls++; });
                Assert.That(calls, Is.EqualTo(1), "the handler ran before Run returned");
                Assert.That(ranOn, Is.EqualTo(Thread.CurrentThread.ManagedThreadId), "on the calling thread, not a pool thread");
                Assert.That(ThreadOf(scope.Def(0)), Is.Null, "no pool thread was restarted");
                Assert.That(ThreadOf(scope.Def(1)), Is.Null);
                Assert.That(QueuedCount(scope.Def(0)), Is.EqualTo(0));
                Assert.That(QueuedCount(scope.Def(1)), Is.EqualTo(0));
            }
        }

        // Original RunThread 0x4e34a0: every handler runs under SplineThreading.locker, so while the first thread computes, a handler
        // on a second awakened thread only starts once the first one has returned. The defs are driven through ThreadDef.Queue and
        // Interrupt (the lock is RunThread's, independent of Run). Run's own selection of the next thread is player-branch text,
        // covered by OriginalDreamteckSplineThreadingPlayModeTests.RunMovesToTheNextThreadWhileTheFirstIsComputingAndHandlersShareTheLock;
        // in the editor branch Run calls its handler synchronously and neither waits for the lock nor touches the pool.
        [Test]
        public void RunMovesToTheNextThreadWhileTheFirstIsComputingAndHandlersShareTheLock()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                ManualResetEventSlim firstStarted = new ManualResetEventSlim(false);
                ManualResetEventSlim releaseFirst = new ManualResetEventSlim(false);
                ManualResetEventSlim secondRan = new ManualResetEventSlim(false);
                int secondOn = -1;
                try
                {
                    Invoke(scope.Def(0), "Restart");
                    Invoke(scope.Def(0), "Queue", (SplineThreading.EmptyHandler)(() => { firstStarted.Set(); releaseFirst.Wait(WaitMilliseconds); }));
                    Invoke(scope.Def(0), "Interrupt");
                    Assert.That(firstStarted.Wait(WaitMilliseconds), Is.True);
                    Assert.That(Computing(scope.Def(0)), Is.True);

                    int editorRanOn = -1;
                    SplineThreading.Run(() => { editorRanOn = Thread.CurrentThread.ManagedThreadId; });
                    Assert.That(editorRanOn, Is.EqualTo(Thread.CurrentThread.ManagedThreadId), "editor branch: Run does not wait for the lock or move on");
                    Assert.That(ThreadOf(scope.Def(1)), Is.Null);

                    Invoke(scope.Def(1), "Restart");
                    Invoke(scope.Def(1), "Queue", (SplineThreading.EmptyHandler)(() => { secondOn = Thread.CurrentThread.ManagedThreadId; secondRan.Set(); }));
                    Invoke(scope.Def(1), "Interrupt");
                    Assert.That(secondRan.Wait(200), Is.False, "the shared lock holds the second handler back");

                    releaseFirst.Set();
                    Assert.That(secondRan.Wait(WaitMilliseconds), Is.True);
                    Assert.That(secondOn, Is.EqualTo(ThreadOf(scope.Def(1)).ManagedThreadId));
                    Assert.That(secondOn, Is.Not.EqualTo(ThreadOf(scope.Def(0)).ManagedThreadId));
                }
                finally
                {
                    releaseFirst.Set();
                }
            }
        }

        // Original Run 0x4e38c0 falls back to the LAST thread when every thread is computing (player-branch text, covered by
        // OriginalDreamteckSplineThreadingPlayModeTests.RunQueuesOnTheLastThreadWhenEveryThreadIsBusy). This test covers RunThread
        // 0x4e34a0's drain loop through the calls Run makes (Queue without Interrupt on an already computing def): a handler queued
        // while the thread is awake is drained in the same pass, in queue order. In the editor branch Run itself runs its handler
        // synchronously and leaves the queues alone.
        [Test]
        public void RunQueuesOnTheLastThreadWhenEveryThreadIsBusy()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                ManualResetEventSlim firstStarted = new ManualResetEventSlim(false);
                ManualResetEventSlim releaseFirst = new ManualResetEventSlim(false);
                ManualResetEventSlim thirdRan = new ManualResetEventSlim(false);
                List<string> order = new List<string>();
                try
                {
                    Invoke(scope.Def(0), "Restart");
                    Invoke(scope.Def(0), "Queue", (SplineThreading.EmptyHandler)(() => { firstStarted.Set(); releaseFirst.Wait(WaitMilliseconds); }));
                    Invoke(scope.Def(0), "Interrupt");
                    Assert.That(firstStarted.Wait(WaitMilliseconds), Is.True);

                    Invoke(scope.Def(1), "Restart");
                    Invoke(scope.Def(1), "Queue", (SplineThreading.EmptyHandler)(() => { lock (order) { order.Add("second:" + Thread.CurrentThread.ManagedThreadId); } }));
                    Invoke(scope.Def(1), "Interrupt");
                    Assert.That(WaitUntil(() => Computing(scope.Def(1))), Is.True, "the second thread is awake, waiting for the lock");

                    Invoke(scope.Def(1), "Queue", (SplineThreading.EmptyHandler)(() => { lock (order) { order.Add("third:" + Thread.CurrentThread.ManagedThreadId); } thirdRan.Set(); }));
                    int queuedBefore = QueuedCount(scope.Def(1));
                    Assert.That(queuedBefore, Is.GreaterThanOrEqualTo(1), "the third handler joined the last thread's queue");

                    int syncCalls = 0;
                    SplineThreading.Run(() => syncCalls++);
                    Assert.That(syncCalls, Is.EqualTo(1), "editor branch: Run calls the handler at once");
                    Assert.That(QueuedCount(scope.Def(1)), Is.EqualTo(queuedBefore));

                    releaseFirst.Set();
                    Assert.That(thirdRan.Wait(WaitMilliseconds), Is.True);
                    int lastThread = ThreadOf(scope.Def(1)).ManagedThreadId;
                    Assert.That(order, Is.EqualTo(new[] { "second:" + lastThread, "third:" + lastThread }), "the last thread ran both, in queue order");
                }
                finally
                {
                    releaseFirst.Set();
                }
            }
        }

        // Original threadCount 0x4e2db0 / 0x4e2e30: the getter is the pool length; the setter only ever grows the pool and a smaller
        // value changes nothing. The shipped setter calls Restart on each added def; the editor compile guards that call with
        // `if (Application.isPlaying)` (read directly), so this test covers the editor branch: the added defs exist and hold no
        // thread. The shipped start is covered by OriginalDreamteckSplineThreadingPlayModeTests.ThreadCountOnlyGrowsAndStartsTheNewThreads.
        [Test]
        public void ThreadCountOnlyGrowsAndStartsTheNewThreads()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                Assert.That(SplineThreading.threadCount, Is.EqualTo(2));
                SplineThreading.threadCount = 1;
                Assert.That(SplineThreading.threadCount, Is.EqualTo(2), "a smaller count is ignored");
                Assert.That(SplineThreading.threadCount, Is.EqualTo(scope.Pool.Length));

                SplineThreading.threadCount = 4;
                Assert.That(SplineThreading.threadCount, Is.EqualTo(4));
                Assert.That(ThreadOf(scope.Def(0)), Is.Null, "existing defs are not started by the setter");
                Assert.That(ThreadOf(scope.Def(1)), Is.Null);
                Assert.That(scope.Def(2), Is.Not.Null, "added defs are fresh ThreadDefs");
                Assert.That(scope.Def(3), Is.Not.Null);
                Assert.That(ThreadOf(scope.Def(2)), Is.Null, "edit mode: the editor-only isPlaying guard skips the Restart");
                Assert.That(IsAlive(scope.Def(2)), Is.False);
                Assert.That(IsAlive(scope.Def(3)), Is.False);

                SplineThreading.threadCount = 3;
                Assert.That(SplineThreading.threadCount, Is.EqualTo(4));
            }
        }

        // Original PrewarmThreads 0x4e3cc0 restarts every def whose thread is not alive and leaves live ones alone.
        [Test]
        public void PrewarmThreadsStartsOnlyTheThreadsThatAreNotAlive()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                SplineThreading.PrewarmThreads();
                Thread first = ThreadOf(scope.Def(0));
                Thread second = ThreadOf(scope.Def(1));
                Assert.That(IsAlive(scope.Def(0)), Is.True);
                Assert.That(IsAlive(scope.Def(1)), Is.True);

                SplineThreading.PrewarmThreads();
                Assert.That(ReferenceEquals(ThreadOf(scope.Def(0)), first), Is.True, "live threads are kept");
                Assert.That(ReferenceEquals(ThreadOf(scope.Def(1)), second), Is.True);

                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    StopThread(scope.Def(0));
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = previous;
                }

                Assert.That(IsAlive(scope.Def(0)), Is.False);
                SplineThreading.PrewarmThreads();
                Assert.That(IsAlive(scope.Def(0)), Is.True, "the dead thread was restarted");
                Assert.That(ReferenceEquals(ThreadOf(scope.Def(0)), first), Is.False);
                Assert.That(ReferenceEquals(ThreadOf(scope.Def(1)), second), Is.True);
            }
        }

        // Original Stop 0x4e33c0 aborts every live thread and leaves defs without a thread alone; Quitting 0x4e3360 (the
        // Application.quitting handler) simply calls Stop.
        [Test]
        public void StopAndQuittingAbortLiveThreads()
        {
            using (PoolScope scope = new PoolScope(3))
            {
                Invoke(scope.Def(0), "Restart");
                Invoke(scope.Def(1), "Restart");
                Thread first = ThreadOf(scope.Def(0));
                Thread second = ThreadOf(scope.Def(1));

                bool previous = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    SplineThreading.Stop();
                    Assert.That(first.Join(WaitMilliseconds), Is.True);
                    Assert.That(second.Join(WaitMilliseconds), Is.True);
                    Assert.That(ThreadOf(scope.Def(2)), Is.Null, "a def without a thread is skipped");

                    Invoke(scope.Def(0), "Restart");
                    Thread restarted = ThreadOf(scope.Def(0));
                    typeof(SplineThreading).GetMethod("Quitting", AllMembers).Invoke(null, null);
                    Assert.That(restarted.Join(WaitMilliseconds), Is.True, "Quitting stops the pool");
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = previous;
                }
            }
        }

        // Original delegates: EmptyHandler is a public nested `void ()` delegate; ThreadDef.BoolHandler an internal `void (bool flag)`.
        [Test]
        public void NestedDelegateTypesKeepTheirSignatures()
        {
            Type empty = typeof(SplineThreading.EmptyHandler);
            Assert.That(empty.IsNestedPublic, Is.True);
            Assert.That(empty.IsSealed, Is.True);
            Assert.That(typeof(MulticastDelegate).IsAssignableFrom(empty), Is.True);
            Assert.That(empty.GetMethod("Invoke").GetParameters().Length, Is.EqualTo(0));
            Assert.That(empty.GetMethod("Invoke").ReturnType, Is.EqualTo(typeof(void)));

            Type boolHandler = ThreadDefType.GetNestedType("BoolHandler", BindingFlags.NonPublic);
            Assert.That(boolHandler, Is.Not.Null);
            Assert.That(boolHandler.IsSealed, Is.True);
            Assert.That(typeof(MulticastDelegate).IsAssignableFrom(boolHandler), Is.True);
            ParameterInfo[] parameters = boolHandler.GetMethod("Invoke").GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(1));
            Assert.That(parameters[0].Name, Is.EqualTo("flag"));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(bool)));
            Assert.That(boolHandler.GetMethod("Invoke").ReturnType, Is.EqualTo(typeof(void)));

            Type worker = ThreadDefType.GetNestedType("Worker", BindingFlags.NonPublic);
            Assert.That(worker, Is.Not.Null);
            Assert.That(worker.GetField("instructions", AllMembers).FieldType, Is.EqualTo(typeof(Queue<SplineThreading.EmptyHandler>)));
            Assert.That(typeof(SplineThreading).IsAbstract && typeof(SplineThreading).IsSealed, Is.True, "static class");
        }
    }
}
