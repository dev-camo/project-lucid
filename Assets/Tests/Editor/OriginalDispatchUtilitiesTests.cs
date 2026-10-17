using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // HLUnityCore.Runtime dispatch utilities, each case checked against the
    // shipped x86-64 bodies named in its comment. Every ProcessManager action
    // subscription, registry row and generic static reference made here is
    // owned by this fixture and removed in finally.
    [TestFixture]
    public sealed class OriginalDispatchUtilitiesTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> ActionLookup =>
            (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                .GetField("s_systemActionLookup", AnyStatic).GetValue(null);

        // Restores ProcessManager.s_systemActionLookup: keys added while
        // leased are removed, and rows created while leased are dropped when
        // they end up empty. Prior keys and rows are left untouched.
        sealed class ActionLookupLease : IDisposable
        {
            readonly Dictionary<SystemAction, HashSet<ISystem>> m_prior = new Dictionary<SystemAction, HashSet<ISystem>>();

            public ActionLookupLease()
            {
                foreach (KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>> row in ActionLookup)
                    m_prior.Add(row.Key, new HashSet<ISystem>(row.Value.Keys));
            }

            public void Dispose()
            {
                foreach (SystemAction action in ActionLookup.Keys.ToList())
                {
                    Dictionary<ISystem, Action<object>> row = ActionLookup[action];
                    m_prior.TryGetValue(action, out HashSet<ISystem> prior);
                    foreach (ISystem added in row.Keys.Where(system => prior == null || !prior.Contains(system)).ToList()) row.Remove(added);
                    if (prior == null && row.Count == 0) ActionLookup.Remove(action);
                }
            }
        }

        static Action<object> UpdateCallback(ISystem system) => ActionLookup[SystemAction.Update][system];

        // ------------------------------------------------------------------ UpdatePublisher

        sealed class RecordingSubscriber : IUpdateSubscriber
        {
            public readonly List<float> Calls = new List<float>();
            public void OnUpdate(float delta) => Calls.Add(delta);
        }

        // Test-only publisher over the genuine protected constructor; its
        // clock is the supplied delta.
        sealed class ManualPublisher : UpdatePublisher<int>
        {
            public readonly List<int> Advanced = new List<int>();
            float m_delta;
            protected override void AdvanceUpdateInfo(int updateType, UpdateInfo updateInfo)
            {
                Advanced.Add(updateType);
                updateInfo.OnUpdate(m_delta);
            }
            public void Tick(float delta) { m_delta = delta; OnUpdate(null); }
        }

        // UpdateInfo.OnUpdate 0x06000f0b (Int32Enum 0xCB3CF0) adds the delta
        // to the timer, then loops while timer >= rate (ucomiss/jae at
        // 0xcb3d38), subtracting the rate and passing the rate (xmm0 reloaded
        // from +0x10) to the subscriber. UpdateInfo's ctor (0xCB3CA0) zeroes
        // the timer. Mutations caught: one callback per tick (if instead of
        // while), forwarding the frame delta, or dropping the remainder.
        [Test]
        public void UpdateInfoCatchesUpWithOneRateSizedCallbackPerElapsedPeriod()
        {
            using (new ActionLookupLease())
            {
                var publisher = new ManualPublisher();
                var subscriber = new RecordingSubscriber();
                publisher.SubscribeToUpdate(0.5f, 7, subscriber);
                publisher.Tick(0.25f);
                Assert.That(subscriber.Calls, Is.Empty, "0.25 of 0.5 elapsed");
                publisher.Tick(1f);
                Assert.That(subscriber.Calls, Is.EqualTo(new[] { 0.5f, 0.5f }), "1.25 elapsed: two periods, 0.25 kept");
                publisher.Tick(0.25f);
                Assert.That(subscriber.Calls, Is.EqualTo(new[] { 0.5f, 0.5f, 0.5f }), "the kept remainder completes a period");
                publisher.Tick(0.375f);
                Assert.That(subscriber.Calls.Count, Is.EqualTo(3), "an exact period leaves no remainder");
                Assert.That(publisher.Advanced, Is.EqualTo(new[] { 7, 7, 7, 7 }), "OnUpdate passes the handle's update type each tick");
            }
        }

        // SubscribeToUpdate 0x06000f02 (shared 0x7C2F60): Dictionary.Add,
        // then the constrained OnUpdate(0) call (0x7c3240) only when
        // updateImmediately; the timer is untouched by it. UpdateHandle's
        // Equals/GetHashCode (0xCB3610/0xCB35E0/0xCB3740) use only the
        // subscriber, so a second subscription of the same subscriber under
        // another update type is a duplicate key and an unrelated update type
        // still unsubscribes it (Remove, 0xCB4030).
        [Test]
        public void HandlesAreKeyedBySubscriberAloneAndImmediateUpdatePassesZero()
        {
            using (new ActionLookupLease())
            {
                var publisher = new ManualPublisher();
                var immediate = new RecordingSubscriber();
                var deferred = new RecordingSubscriber();
                UpdatePublisher<int>.UpdateHandle handle = publisher.SubscribeToUpdate(1f, 1, immediate, true);
                Assert.That(immediate.Calls, Is.EqualTo(new[] { 0f }), "immediate call before return");
                Assert.That(handle.UpdateType, Is.EqualTo(1));
                publisher.SubscribeToUpdate(1f, 2, deferred);
                Assert.That(deferred.Calls, Is.Empty, "no call without updateImmediately");
                publisher.Tick(0.75f);
                Assert.That(immediate.Calls, Is.EqualTo(new[] { 0f }), "the immediate call did not advance the timer");

                Assert.Throws<ArgumentException>(() => publisher.SubscribeToUpdate(1f, 3, immediate), "same subscriber, other type: duplicate key");
                var unrelated = new UpdatePublisher<int>.UpdateHandle(99, immediate);
                Assert.That(unrelated.Equals(handle), Is.True);
                Assert.That(unrelated.Equals((object)handle), Is.True);
                Assert.That(unrelated.Equals((object)immediate), Is.False, "a non-handle is not equal");
                Assert.That(unrelated.GetHashCode(), Is.EqualTo(immediate.GetHashCode()));
                Assert.That(new UpdatePublisher<int>.UpdateHandle(1, deferred).Equals(handle), Is.False, "another subscriber is not equal");
                Assert.That(publisher.UnsubscribeFromUpdate(unrelated), Is.True);
                Assert.That(publisher.UnsubscribeFromUpdate(handle), Is.False, "already removed");
                publisher.Tick(0.5f);
                Assert.That(immediate.Calls, Is.EqualTo(new[] { 0f }), "unsubscribed");
                Assert.That(deferred.Calls, Is.EqualTo(new[] { 1f }), "the other subscription remains");
            }
        }

        // UpdatePublisher ctor 0x06000f01 (0xCB3F40) binds the virtual
        // OnUpdate (vtable +0x190) to SystemAction.Update (esi=5) through
        // ProcessManager.SubscribeToAction (0x1b11e20).
        [Test]
        public void ConstructorSubscribesTheVirtualOnUpdateToTheUpdateAction()
        {
            using (new ActionLookupLease())
            {
                var publisher = new ManualPublisher();
                var subscriber = new RecordingSubscriber();
                publisher.SubscribeToUpdate(2f, 4, subscriber);
                Assert.That(ActionLookup[SystemAction.Update].ContainsKey(publisher), Is.True);
                Assert.That(ActionLookup.Where(row => row.Key != SystemAction.Update).Any(row => row.Value.ContainsKey(publisher)), Is.False, "Update only");
                UpdateCallback(publisher)(null);
                Assert.That(publisher.Advanced, Is.EqualTo(new[] { 4 }));
            }
        }

        // ------------------------------------------------------------------ Observable

        sealed class RecordingComparer : IEqualityComparer<int>
        {
            public readonly List<(int, int)> Calls = new List<(int, int)>();
            public bool Result;
            public bool Equals(int x, int y) { Calls.Add((x, y)); return Result; }
            public int GetHashCode(int value) => value;
        }

        // Observable ctor 0x06000c5f (shared 0x18C1710) installs the cached
        // empty <.ctor>b__6_0 before Object's ctor and falls back to
        // EqualityComparer<T>.Default (rgctx[9]) for a null comparer. Assign
        // 0x06000c60 (0x18C19E0) calls comparer.Equals(m_value, value) and
        // only on false stores then calls ValueUpdated, which invokes the event
        // unconditionally (0x18C1BF0). Mutations caught: reversed comparer
        // arguments, storing an "equal" value, a missing default comparer or
        // a null event without subscribers.
        [Test]
        public void AssignConsultsTheComparerWithTheCurrentValueFirst()
        {
            var comparer = new RecordingComparer();
            var observable = new Observable<int>(5, comparer);
            var seen = new List<int>();
            Action<int> listener = value => seen.Add(value);
            observable.OnValueUpdated += listener;
            observable.Assign(6);
            Assert.That(comparer.Calls, Is.EqualTo(new[] { (5, 6) }));
            Assert.That((int)observable, Is.EqualTo(6));
            Assert.That(seen, Is.EqualTo(new[] { 6 }));
            comparer.Result = true;
            observable.Assign(9);
            Assert.That(comparer.Calls.Last(), Is.EqualTo((6, 9)));
            Assert.That((int)observable, Is.EqualTo(6), "an equal value is not stored");
            Assert.That(seen, Is.EqualTo(new[] { 6 }), "nor notified");
            observable.ValueUpdated();
            Assert.That(seen, Is.EqualTo(new[] { 6, 6 }), "ValueUpdated always notifies");
            observable.OnValueUpdated -= listener;
            Assert.DoesNotThrow(observable.ValueUpdated, "the initializer delegate remains");

            var text = new Observable<string>("a", null);
            var notified = 0;
            text.OnValueUpdated += _ => notified++;
            text.Assign(new string('a', 1));
            Assert.That(notified, Is.Zero, "default comparer: value equality");
            text.Assign("b");
            Assert.That((string)text, Is.EqualTo("b"));
            Assert.That(notified, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ RefCounter

        sealed class RecordingCounter : RefCounter<object>
        {
            public readonly List<string> Trace = new List<string>();
            public object Next;
            public bool Throw;
            protected override object OnFirstAcquire()
            {
                Trace.Add("first@" + RefCount);
                if (Throw) throw new InvalidOperationException("factory");
                return Next;
            }
            protected override void OnLastRelease(object acquiredValue)
            {
                Trace.Add("last@" + RefCount + (ReferenceEquals(acquiredValue, Next) ? ":next" : ":other"));
            }
            public object Reference => typeof(RefCounter<object>).GetField("m_reference", AnyInstance).GetValue(this);
        }

        // RefCounter 0x06000d62..64 (shared 0xBC8D20/0xBC8E60/0xBC8FC0).
        // Acquire stores OnFirstAcquire's result when the count is zero, then
        // increments. Release ignores counts <= 0 (jle 0xbc8f91), decrements,
        // and at zero passes the reference to OnLastRelease before bzero-ing
        // it. ForceRelease sets the count to zero before the callback.
        [Test]
        public void AcquireReleaseAndForceReleaseFollowTheOriginalCountOrder()
        {
            var counter = new RecordingCounter { Next = new object() };
            object first = counter.Next;
            Assert.That(counter.Acquire(), Is.SameAs(first));
            Assert.That(counter.Acquire(), Is.SameAs(first));
            Assert.That(counter.RefCount, Is.EqualTo(2));
            Assert.That(counter.Trace, Is.EqualTo(new[] { "first@0" }), "one factory call");
            counter.Release();
            Assert.That(counter.Trace.Count, Is.EqualTo(1), "not the last release");
            counter.Release();
            Assert.That(counter.Trace.Last(), Is.EqualTo("last@0:next"), "count is zero inside the callback");
            Assert.That(counter.Reference, Is.Null, "reference cleared after the callback");
            counter.Release();
            Assert.That(counter.RefCount, Is.Zero, "an extra release is ignored");
            Assert.That(counter.Trace.Count, Is.EqualTo(2));

            counter.Next = new object();
            counter.Acquire();
            counter.Acquire();
            counter.ForceRelease();
            Assert.That(counter.Trace.Skip(2), Is.EqualTo(new[] { "first@0", "last@0:next" }), "ForceRelease zeroes before the callback");
            Assert.That(counter.Reference, Is.Null);
            counter.ForceRelease();
            Assert.That(counter.Trace.Count, Is.EqualTo(4), "nothing to force");

            counter.Throw = true;
            Assert.Throws<InvalidOperationException>(() => counter.Acquire());
            Assert.That(counter.RefCount, Is.Zero, "a throwing factory is not counted");
            counter.Throw = false;
            counter.Acquire();
            Assert.That(counter.Trace.Last(), Is.EqualTo("first@0"), "the next acquire retries the factory");
        }

        // ------------------------------------------------------------------ CoroutineLoopWrapper

        // CoroutineLoopWrapper ctor 0x06000f2e (0x1B1C050) stores both
        // timers from timeoutSeconds, selects the clock by jump table
        // (Delta/Unscaled/Fixed/FixedUnscaled -> b__6_0..3) and invokes the
        // start callback last. MoveNext 0x06000f2f (0x1B1C4A0) times out
        // when 0 >= timer (ucomiss/jae, so NaN continues), invoking the
        // optional callback and returning false without the condition;
        // otherwise it subtracts the clock then returns the condition.
        [Test]
        public void LoopWrapperChecksTheBudgetBeforeEachStep()
        {
            float step = Time.fixedDeltaTime;
            Assume.That(step, Is.GreaterThan(0f));
            int conditionCalls = 0, timeouts = 0, starts = 0;
            bool condition = true;
            var wrapper = new CoroutineLoopWrapper(() => { conditionCalls++; return condition; }, 2.5f * step,
                CoroutineLoopWrapper.UpdateType.FixedDelta, () => timeouts++, () => starts++);
            Assert.That(starts, Is.EqualTo(1), "start callback runs in the constructor");
            Assert.That(new[] { wrapper.MoveNext(), wrapper.MoveNext(), wrapper.MoveNext() }, Is.All.True);
            Assert.That(conditionCalls, Is.EqualTo(3));
            Assert.That(timeouts, Is.Zero, "the third step went below zero but was still allowed");
            Assert.That(wrapper.MoveNext(), Is.False);
            Assert.That(wrapper.MoveNext(), Is.False);
            Assert.That(timeouts, Is.EqualTo(2), "every exhausted step reports the timeout");
            Assert.That(conditionCalls, Is.EqualTo(3), "the condition is not read after the timeout");
            wrapper.Reset();
            condition = false;
            Assert.That(wrapper.MoveNext(), Is.False, "the condition ends the loop");
            Assert.That(timeouts, Is.EqualTo(2), "without a timeout");
            Assert.That(((IEnumerator)wrapper).Current, Is.Null);
            Assert.That(starts, Is.EqualTo(1));

            int zeroCalls = 0;
            var zero = new CoroutineLoopWrapper(() => { zeroCalls++; return true; }, 0f);
            Assert.That(zero.MoveNext(), Is.False, "a zero budget times out first, null callback allowed");
            Assert.That(zeroCalls, Is.Zero);
            var nan = new CoroutineLoopWrapper(() => true, float.NaN, CoroutineLoopWrapper.UpdateType.FixedDelta, () => timeouts++);
            Assert.That(nan.MoveNext() && nan.MoveNext(), Is.True, "a NaN budget never times out");
            Assert.That(timeouts, Is.EqualTo(2));
        }

        // The ctor's default case (0x1b1c40d) boxes the value and throws
        // ArgumentOutOfRangeException("updateType", value, "Provided TimeType
        // is not handled.") before the start callback.
        [Test]
        public void LoopWrapperRejectsUndefinedUpdateTypesBeforeTheStartCallback()
        {
            int starts = 0;
            var undefined = (CoroutineLoopWrapper.UpdateType)4;
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new CoroutineLoopWrapper(() => true, 1f, undefined, null, () => starts++));
            Assert.That(error.ParamName, Is.EqualTo("updateType"));
            Assert.That(error.ActualValue, Is.EqualTo(undefined));
            Assert.That(error.Message, Does.StartWith("Provided TimeType is not handled."));
            Assert.That(starts, Is.Zero);
        }

        // ------------------------------------------------------------------ CoroutineScheduler

        static IEnumerator Waits(List<string> trace, string name, params object[] yields)
        {
            trace.Add(name + "0");
            for (int i = 0; i < yields.Length; ++i)
            {
                yield return yields[i];
                trace.Add(name + (i + 1));
            }
        }

        // UpdateCoroutine 0x0600116c (0x1B31080) reads Current once: null or
        // int unboxes as int? (UnBoxNullable 0x1b31372) with 1 for null, plus
        // the current frame; float adds the current time. UpdateAllCoroutines
        // 0x0600116b (0x1B30F90) resumes on frame >= wait, time >= wait, a
        // finished node, or the all-unset initial state, resetting the wait.
        [Test]
        public void SchedulerResumesOnFrameAndTimeWaitsAndRemovesFinishedNodes()
        {
            var trace = new List<string>();
            var scheduler = new CoroutineScheduler();
            Assert.That(scheduler.StartCoroutine(null), Is.Null);
            Assert.That(scheduler.HasCoroutines(), Is.False);
            CoroutineNode node = scheduler.StartCoroutine(Waits(trace, "a", null, 3, 0.5f));
            Assert.That(trace, Is.Empty, "StartCoroutine does not step");
            Assert.That(node.m_waitForFrame, Is.EqualTo(-1));
            Assert.That(node.m_waitForTime, Is.EqualTo(-1f));
            scheduler.UpdateAllCoroutines(10, 0f);
            Assert.That(node.m_waitForFrame, Is.EqualTo(11), "null waits one frame");
            scheduler.UpdateAllCoroutines(10, 0f);
            Assert.That(trace, Is.EqualTo(new[] { "a0" }));
            scheduler.UpdateAllCoroutines(11, 0f);
            Assert.That(node.m_waitForFrame, Is.EqualTo(14), "int adds to the current frame");
            scheduler.UpdateAllCoroutines(13, 9f);
            Assert.That(trace.Count, Is.EqualTo(2), "a frame wait ignores time");
            scheduler.UpdateAllCoroutines(14, 2f);
            Assert.That(node.m_waitForFrame, Is.EqualTo(-1));
            Assert.That(node.m_waitForTime, Is.EqualTo(2.5f), "float adds to the current time");
            scheduler.UpdateAllCoroutines(99, 2.25f);
            Assert.That(trace.Count, Is.EqualTo(3));
            Assert.That(node.m_finished, Is.False);
            scheduler.UpdateAllCoroutines(100, 2.5f);
            Assert.That(trace, Is.EqualTo(new[] { "a0", "a1", "a2", "a3" }));
            Assert.That(node.m_finished, Is.True);
            Assert.That(scheduler.HasCoroutines(), Is.False);
        }

        // AddCoroutine 0x0600116d (0x1B30F00) pushes at the head, so newer
        // coroutines step first. A CoroutineNode yield (exact GetType ==
        // typeof(CoroutineNode), 0x1b312b2) waits until that node's finished
        // flag is seen, one update after it finished. RemoveCoroutine
        // 0x0600116e (0x1B31480) unlinks the head without clearing the new
        // head's previous link.
        [Test]
        public void SchedulerStepsNewestFirstAndWaitsForFinishedNodes()
        {
            var trace = new List<string>();
            var scheduler = new CoroutineScheduler();
            CoroutineNode b = scheduler.StartCoroutine(Waits(trace, "b", 2));
            IEnumerator WaitForB() { trace.Add("a0"); yield return b; trace.Add("a1"); }
            CoroutineNode a = scheduler.StartCoroutine(WaitForB());
            Assert.That(a.m_listNext, Is.SameAs(b));
            Assert.That(b.m_listPrevious, Is.SameAs(a));
            scheduler.UpdateAllCoroutines(0, 0f);
            Assert.That(trace, Is.EqualTo(new[] { "a0", "b0" }), "head first");
            Assert.That(a.m_waitForCoroutine, Is.SameAs(b));
            scheduler.UpdateAllCoroutines(2, 0f);
            Assert.That(trace, Is.EqualTo(new[] { "a0", "b0", "b1" }), "a was visited before b finished");
            Assert.That(b.m_finished, Is.True);
            Assert.That(b.m_listPrevious, Is.Null);
            Assert.That(b.m_listNext, Is.Null);
            Assert.That(a.m_listNext, Is.Null, "the tail was unlinked from its previous node");
            scheduler.UpdateAllCoroutines(3, 0f);
            Assert.That(trace.Last(), Is.EqualTo("a1"));
            Assert.That(a.m_waitForCoroutine, Is.Null);
            Assert.That(scheduler.HasCoroutines(), Is.False);

            var second = new CoroutineScheduler();
            CoroutineNode x = second.StartCoroutine(Waits(trace, "x", 5));
            CoroutineNode y = second.StartCoroutine(Waits(trace, "y"));
            second.UpdateAllCoroutines(0, 0f);
            Assert.That(y.m_finished, Is.True, "the head finished");
            Assert.That(x.m_listPrevious, Is.SameAs(y), "the new head keeps its stale previous link");
            Assert.That(second.HasCoroutines(), Is.True);
            second.StopAllCoroutines();
            Assert.That(second.HasCoroutines(), Is.False);
            Assert.That(x.m_finished, Is.False, "stopping does not finish nodes");
        }

        sealed class DerivedNode : CoroutineNode
        {
            public DerivedNode() : base(null) { }
        }

        // The else branch (0x1b313ea) throws ArgumentException with the
        // literal prefix and GetType().ToString() after the fiber advanced;
        // the exact-type test rejects CoroutineNode subclasses and is/unbox
        // tests reject other numeric types. The node stays scheduled with its
        // waits unset, so the next update steps it again.
        [Test]
        public void SchedulerRejectsOtherYieldTypesAfterAdvancingTheFiber()
        {
            var trace = new List<string>();
            var scheduler = new CoroutineScheduler();
            CoroutineNode node = scheduler.StartCoroutine(Waits(trace, "n", "text", 5L, new DerivedNode(), 1.0));
            foreach (string type in new[] { "System.String", "System.Int64", typeof(DerivedNode).ToString(), "System.Double" })
            {
                var error = Assert.Throws<ArgumentException>(() => scheduler.UpdateAllCoroutines(0, 0f));
                Assert.That(error.Message, Does.StartWith("CoroutineScheduler: Unexpected coroutine yield type: " + type));
                Assert.That(node.m_waitForFrame, Is.EqualTo(-1));
                Assert.That(node.m_waitForTime, Is.EqualTo(-1f));
                Assert.That(node.m_waitForCoroutine, Is.Null);
            }
            Assert.That(trace, Is.EqualTo(new[] { "n0", "n1", "n2", "n3" }), "each throw follows one MoveNext");
            scheduler.UpdateAllCoroutines(0, 0f);
            Assert.That(node.m_finished, Is.True);
        }

        // ------------------------------------------------------------------ MainThreadGenericDispatcher

        public sealed class OwnedPayload
        {
            public readonly string Name;
            public OwnedPayload(string name) { Name = name; }
        }
        sealed class FirstDispatcher : MainThreadGenericDispatcher<OwnedPayload> { }
        sealed class SecondDispatcher : MainThreadGenericDispatcher<OwnedPayload> { }

        static FieldInfo DispatcherRefField => typeof(MainThreadGenericDispatcher<OwnedPayload>).GetField("s_dispatcherRef", AnyStatic);

        static ISystem Registered(string name) => ProcessManager.GetSystemRef(name, false).GetSafe();

        // Coverage gap: the non-generic MainThreadDispatcher (0x0200027d) has
        // no case here. Its static readonly SystemRef<MainThreadDispatcher> is
        // created once per domain by the .cctor (0x06000f98), which also adds
        // the domain-wide "Hardlight.MainThreadDispatcher" row; removing that
        // row leaves the cached reference unresolvable for the rest of the
        // domain, so no fixture can restore it. The generic case below is not
        // an equivalent oracle (GetSafe<T2> on an untyped reference versus
        // TryGet on the .cctor's typed reference).

        // AddAction 0x06000f9b (shared 0x7B73D0) reads s_dispatcherRef as the
        // requested dispatcher type (SystemRef.GetSafe<T2>), otherwise creates
        // one (Activator.CreateInstance) and stores RegisterSystem(dispatcher,
        // null, false, false) (0x7b75f7). The ctor (0x17BB590) subscribes
        // Update; Update (0x17BB680) drains the queue in order, including
        // actions queued during the drain. A different dispatcher type
        // replaces the shared reference; returning to the first type then
        // collides with its still-valid row and RegisterSystem throws before
        // the reference is stored.
        [Test]
        public void GenericDispatcherQueuesPerDispatcherTypeAndSharesOneReference()
        {
            string first = typeof(FirstDispatcher).ToString();
            string second = typeof(SecondDispatcher).ToString();
            IDictionary registry = BorrowedSystemRowTypedCache.Registry;
            Assert.That(registry.Contains(first) || registry.Contains(second), Is.False, "owned registry names");
            Assert.That(DispatcherRefField.GetValue(null), Is.Null, "owned closed generic reference");
            var lease = new ActionLookupLease();
            try
            {
                var log = new List<string>();
                Action<OwnedPayload> record = payload => log.Add(payload.Name);
                MainThreadGenericDispatcher<OwnedPayload>.AddAction<FirstDispatcher>(record, new OwnedPayload("p1"));
                ISystem firstDispatcher = Registered(first);
                Assert.That(firstDispatcher, Is.InstanceOf<FirstDispatcher>());
                Assert.That(DispatcherRefField.GetValue(null), Is.SameAs(ProcessManager.GetSystemRef(first, false)));
                MainThreadGenericDispatcher<OwnedPayload>.AddAction<FirstDispatcher>(payload =>
                {
                    log.Add(payload.Name);
                    MainThreadGenericDispatcher<OwnedPayload>.AddAction<FirstDispatcher>(record, new OwnedPayload("nested"));
                }, new OwnedPayload("p2"));
                Assert.That(Registered(first), Is.SameAs(firstDispatcher), "the second call reuses the dispatcher");
                Assert.That(log, Is.Empty, "queued until Update");
                UpdateCallback(firstDispatcher)(null);
                Assert.That(log, Is.EqualTo(new[] { "p1", "p2", "nested" }));

                MainThreadGenericDispatcher<OwnedPayload>.AddAction<SecondDispatcher>(record, new OwnedPayload("p3"));
                ISystem secondDispatcher = Registered(second);
                Assert.That(secondDispatcher, Is.InstanceOf<SecondDispatcher>());
                Assert.That(DispatcherRefField.GetValue(null), Is.SameAs(ProcessManager.GetSystemRef(second, false)), "replaced");
                UpdateCallback(firstDispatcher)(null);
                Assert.That(log.Count, Is.EqualTo(3), "p3 is not on the first dispatcher");
                UpdateCallback(secondDispatcher)(null);
                Assert.That(log.Last(), Is.EqualTo("p3"));

                Assert.Throws<InvalidOperationException>(() =>
                    MainThreadGenericDispatcher<OwnedPayload>.AddAction<FirstDispatcher>(record, new OwnedPayload("p4")));
                Assert.That(Registered(first), Is.SameAs(firstDispatcher), "the original first row is kept");
                Assert.That(DispatcherRefField.GetValue(null), Is.SameAs(ProcessManager.GetSystemRef(second, false)), "not stored after the throw");
                Assert.That(ActionLookup[SystemAction.Update].Keys.OfType<FirstDispatcher>().Count(), Is.EqualTo(2),
                    "the rejected dispatcher's constructor still subscribed Update");
            }
            finally
            {
                if (registry.Contains(first)) ProcessManager.UnregisterSystem(first);
                if (registry.Contains(second)) ProcessManager.UnregisterSystem(second);
                registry.Remove(first);
                registry.Remove(second);
                DispatcherRefField.SetValue(null, null);
                lease.Dispose();
            }
        }

        // ------------------------------------------------------------------ MessageExchangeStruct

        struct OwnedMessage { public int Value; }
        struct OtherMessage { public int Value; }

        // MessageExchangeStruct 0x06000bc6..ca (shared 0x979660/0x979580/
        // 0x979610/0x9796D0/0x9794E0): typeof(T) is the exchange key; the
        // parameterless PublishMessage publishes a zeroed T (bzero 0x9795d0)
        // through the in overload.
        [Test]
        public void StructExchangeKeysHandlesByPayloadTypeAndPublishesDefaults()
        {
            using (new ActionLookupLease())
            {
                var exchange = new MessageExchangeStruct();
                try
                {
                    var seen = new List<int>();
                    MessageCallback<OwnedMessage> callback = (in OwnedMessage message) => seen.Add(message.Value);
                    SubscribeHandle<MessageCallback<OwnedMessage>> handle = exchange.SubscribeToMessage(callback);
                    Assert.That((MessageCallback<OwnedMessage>)handle, Is.SameAs(callback));
                    Assert.That(exchange.GetExchangeHandle<OwnedMessage>(), Is.SameAs(exchange.GetExchangeHandle<OwnedMessage>()));
                    Assert.That(exchange.GetExchangeHandle<OtherMessage>(), Is.Not.SameAs(exchange.GetExchangeHandle<OwnedMessage>()));
                    exchange.PublishMessage(new OwnedMessage { Value = 4 });
                    exchange.PublishMessage<OwnedMessage>();
                    exchange.PublishMessage(new OtherMessage { Value = 8 });
                    Assert.That(seen, Is.EqualTo(new[] { 4, 0 }));
                    Assert.That(exchange.UnsubscribeFromMessage(callback), Is.True);
                    Assert.That(exchange.UnsubscribeFromMessage(callback), Is.False);
                    exchange.PublishMessage(new OwnedMessage { Value = 5 });
                    Assert.That(seen, Is.EqualTo(new[] { 4, 0 }));
                }
                finally
                {
                    // The exchange's own Shutdown callback frees its weak message handles.
                    ActionLookup[SystemAction.Shutdown][exchange](null);
                    exchange.UnsubscribeFromAction(SystemAction.Shutdown);
                }
            }
        }

        // ------------------------------------------------------------------ declaration order

        static string[] NestedNames(Type type) => type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray();
        static string[] LambdaNames(Type type) =>
            type.GetNestedType("<>c", BindingFlags.NonPublic).GetMethods(AnyInstance | BindingFlags.DeclaredOnly).Select(m => m.Name).Where(n => n.StartsWith("<")).ToArray();

        // Original metadata names (dump.cs TypeDefIndex 9193-9198, 9235, 9353,
        // 9364): Roslyn numbers closures and state machines by the member
        // ordinal, so these constrain the member declaration order: the nested
        // types come before TryGetCompletionData / SetData in
        // ExchangeHandleWithCompletionBase and CompletionData, and the enums
        // before the constructors in CoroutineLoopWrapper and
        // UpdatePublisherDeltaTime (their position relative to the fields is
        // not recoverable). Observable's operator precedes its event by
        // method token (0x06000c5c < 0x06000c5d).
        [Test]
        public void CompilerGeneratedNamesMatchTheOriginalMemberOrdinals()
        {
            Type completionBase = typeof(ExchangeHandleWithCompletionBase<>);
            Assert.That(NestedNames(completionBase), Is.EquivalentTo(new[] { "ICompletionData", "CompletionData", "<>c__DisplayClass8_0" }));
            Type completionData = completionBase.GetNestedType("CompletionData", BindingFlags.NonPublic);
            Assert.That(NestedNames(completionData), Is.EquivalentTo(new[]
                { "CompletionDataDebug", "<>c__DisplayClass9_0", "<>c__DisplayClass12_0", "<RunTimeout>d__11", "<Timeout>d__14" }));
            Assert.That(LambdaNames(typeof(Observable<>)), Is.EquivalentTo(new[] { "<.ctor>b__6_0" }));
            Assert.That(LambdaNames(typeof(CoroutineLoopWrapper)), Is.EquivalentTo(new[] { "<.ctor>b__6_0", "<.ctor>b__6_1", "<.ctor>b__6_2", "<.ctor>b__6_3" }));
            Assert.That(LambdaNames(typeof(UpdatePublisherDeltaTime)), Is.EquivalentTo(new[] { "<.ctor>b__3_0", "<.ctor>b__3_1", "<.ctor>b__3_2" }));
        }
    }
}
