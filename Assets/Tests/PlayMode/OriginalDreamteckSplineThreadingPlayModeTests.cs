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
    // Player branch of Dreamteck.Splines.SplineThreading. The editor compile guards Run and the threadCount setter with
    // Application.isPlaying (read directly), which is true here, so these tests run the same text the shipped build runs. The
    // EditMode class OriginalDreamteckSplineThreadingTests covers the editor branch of the same methods. Every test swaps in a
    // private pool of fresh ThreadDefs, aborts every thread it started and restores the original array when it finishes.
    public sealed class OriginalDreamteckSplineThreadingPlayModeTests
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
            object worker = ThreadDefType.GetField("worker", AllMembers).GetValue(def);
            return ((ICollection)worker.GetType().GetField("instructions", AllMembers).GetValue(worker)).Count;
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


        // Original Run 0x4e38c0: restarts dead threads, queues the handler on the first thread that is not computing (or on the last
        // one), and interrupts it unless it is already computing. Application.isPlaying is true here, so the editor compile's
        // synchronous early return is skipped and this is the shipped loop.
        [Test]
        public void RunQueuesOnTheFirstIdleThreadAndWakesIt()
        {
            using (PoolScope scope = new PoolScope(2))
            {
                ManualResetEventSlim ran = new ManualResetEventSlim(false);
                int ranOn = -1;
                SplineThreading.Run(() => { ranOn = Thread.CurrentThread.ManagedThreadId; ran.Set(); });
                Assert.That(ran.Wait(WaitMilliseconds), Is.True);
                Assert.That(ranOn, Is.EqualTo(ThreadOf(scope.Def(0)).ManagedThreadId), "the first def's thread ran it");
                Assert.That(ThreadOf(scope.Def(1)), Is.Null, "the second def was never needed, so it was never started");
            }
        }

        // Original Run 0x4e38c0 and RunThread 0x4e34a0: a busy first thread sends the next handler to the next def, but every
        // handler runs under SplineThreading.locker, so the second handler only starts once the first one has returned.
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
                    SplineThreading.Run(() => { firstStarted.Set(); releaseFirst.Wait(WaitMilliseconds); });
                    Assert.That(firstStarted.Wait(WaitMilliseconds), Is.True);
                    Assert.That(Computing(scope.Def(0)), Is.True);

                    SplineThreading.Run(() => { secondOn = Thread.CurrentThread.ManagedThreadId; secondRan.Set(); });
                    Assert.That(ThreadOf(scope.Def(1)), Is.Not.Null, "the second def was started for it");
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

        // Original Run 0x4e38c0 falls back to the LAST thread when every thread is computing, queues the handler there and does not
        // interrupt it again (it is already running or waiting for the lock); the thread drains the new handler in the same pass.
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
                    SplineThreading.Run(() => { firstStarted.Set(); releaseFirst.Wait(WaitMilliseconds); });
                    Assert.That(firstStarted.Wait(WaitMilliseconds), Is.True);
                    SplineThreading.Run(() => { lock (order) { order.Add("second:" + Thread.CurrentThread.ManagedThreadId); } });
                    Assert.That(WaitUntil(() => Computing(scope.Def(1))), Is.True, "the second thread is awake, waiting for the lock");

                    SplineThreading.Run(() => { lock (order) { order.Add("third:" + Thread.CurrentThread.ManagedThreadId); } thirdRan.Set(); });
                    Assert.That(QueuedCount(scope.Def(1)), Is.GreaterThanOrEqualTo(1), "the third handler joined the last thread's queue");

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

        // Original threadCount 0x4e2db0 / 0x4e2e30: the getter is the pool length; the setter only ever grows the pool, starting each
        // added thread, and a smaller value changes nothing. Playing, the editor compile's guard passes, so Restart runs on each added
        // def as in the shipped setter.
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
                Assert.That(IsAlive(scope.Def(2)), Is.True, "added defs are started immediately");
                Assert.That(IsAlive(scope.Def(3)), Is.True);

                SplineThreading.threadCount = 3;
                Assert.That(SplineThreading.threadCount, Is.EqualTo(4));
            }
        }
    }
}
