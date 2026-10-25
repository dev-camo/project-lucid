using System;
using System.Collections;
using System.Reflection;
using Hardlight.Utils;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class CoroutineSchedulerNullRemovalTests
    {
        // Original 0x0600116e @ 0x1B31480 returns on null before accessing any fields.
        [TestCase(false)]
        [TestCase(true)]
        public void NullRemovalPreservesSchedulerAndEveryNode(bool populated)
        {
            var scheduler = new CoroutineScheduler();
            var fibers = new CountingFiber[populated ? 3 : 0];
            var nodes = new CoroutineNode[fibers.Length];
            if (populated)
            {
                fibers[0] = new CountingFiber(7);
                nodes[0] = scheduler.StartCoroutine(fibers[0]);
                fibers[1] = new CountingFiber(2.5f);
                nodes[1] = scheduler.StartCoroutine(fibers[1]);
                fibers[2] = new CountingFiber(nodes[0]);
                nodes[2] = scheduler.StartCoroutine(fibers[2]);
            }
            scheduler.UpdateAllCoroutines(10, 3.5f);

            CoroutineNode head = ReadSchedulerField<CoroutineNode>(scheduler, "m_first");
            int frame = ReadSchedulerField<int>(scheduler, "m_currentFrame");
            float time = ReadSchedulerField<float>(scheduler, "m_currentTime");
            var states = new NodeState[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                states[i] = new NodeState(nodes[i], fibers[i]);

            MethodInfo remove = typeof(CoroutineScheduler).GetMethod("RemoveCoroutine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(remove, Is.Not.Null);
            Assert.DoesNotThrow(() => remove.Invoke(scheduler, new object[] { null }));

            Assert.That(ReadSchedulerField<CoroutineNode>(scheduler, "m_first"), Is.SameAs(head));
            Assert.That(ReadSchedulerField<int>(scheduler, "m_currentFrame"), Is.EqualTo(frame));
            Assert.That(ReadSchedulerField<float>(scheduler, "m_currentTime"), Is.EqualTo(time));
            Assert.That(scheduler.HasCoroutines(), Is.EqualTo(populated));
            for (int i = 0; i < nodes.Length; i++)
                states[i].AssertUnchanged(nodes[i], fibers[i]);
        }

        private static T ReadSchedulerField<T>(CoroutineScheduler scheduler, string name)
        {
            FieldInfo field = typeof(CoroutineScheduler).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(scheduler);
        }

        private sealed class CountingFiber : IEnumerator, IDisposable
        {
            private readonly object command;
            public int Advances { get; private set; }
            public int Reads { get; private set; }
            public int Disposals { get; private set; }

            public CountingFiber(object command) => this.command = command;
            public object Current { get { Reads++; return command; } }
            public bool MoveNext() { Advances++; return true; }
            public void Dispose() => Disposals++;
            public void Reset() => throw new NotSupportedException();
        }

        private sealed class NodeState
        {
            private readonly CoroutineNode previous, next, waitForCoroutine;
            private readonly IEnumerator fiber;
            private readonly bool finished;
            private readonly int waitForFrame, advances, reads, disposals;
            private readonly float waitForTime;

            public NodeState(CoroutineNode node, CountingFiber counter)
            {
                previous = node.m_listPrevious;
                next = node.m_listNext;
                fiber = node.m_fiber;
                finished = node.m_finished;
                waitForFrame = node.m_waitForFrame;
                waitForTime = node.m_waitForTime;
                waitForCoroutine = node.m_waitForCoroutine;
                advances = counter.Advances;
                reads = counter.Reads;
                disposals = counter.Disposals;
            }

            public void AssertUnchanged(CoroutineNode node, CountingFiber counter)
            {
                Assert.That(node.m_listPrevious, Is.SameAs(previous));
                Assert.That(node.m_listNext, Is.SameAs(next));
                Assert.That(node.m_fiber, Is.SameAs(fiber));
                Assert.That(node.m_finished, Is.EqualTo(finished));
                Assert.That(node.m_waitForFrame, Is.EqualTo(waitForFrame));
                Assert.That(node.m_waitForTime, Is.EqualTo(waitForTime));
                Assert.That(node.m_waitForCoroutine, Is.SameAs(waitForCoroutine));
                Assert.That(counter.Advances, Is.EqualTo(advances));
                Assert.That(counter.Reads, Is.EqualTo(reads));
                Assert.That(counter.Disposals, Is.EqualTo(disposals));
            }
        }
    }
}
