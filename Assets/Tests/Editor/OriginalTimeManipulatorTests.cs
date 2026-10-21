using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TimeScaledTestHarness;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeManipulator (TypeDef 0x020007f4, x86-64 0x5F8450..0x5F87AF) and its <ScaleTime>d__8
    // iterator (0x020007f5, MoveNext 0x5F87C0). The component is real, on an owned inactive host that never wakes,
    // so its Awake (which reads the runtime time configuration) is replaced by assigning the fields it fills. The
    // iterator is stepped by hand, as the engine would between fixed updates. Stacking an override on the manager
    // needs the stack operations its Awake registers, so the cases keep the handle null (the manager's update
    // calls ignore a null handle) and ApplyTimeScaling runs with an absent manager. The playing coroutine host is
    // not covered.
    public sealed class OriginalTimeManipulatorTests
    {
        private static void WithManipulator(Action<Scene, TimeManipulator, TimeSetting, TimeSetting> body) => WithManager(scene =>
        {
            Assert.That(Time.fixedUnscaledDeltaTime, Is.InRange(0.01f, 0.03f), "the cases step the fixed time of the project settings");
            var manipulator = scene.Owned.Host("Lucid owned time manipulator", false).AddComponent<TimeManipulator>();
            var current = new TimeSetting();
            current.SetCategory(scene.Category, 1f);
            var blended = new TimeSetting();
            blended.SetCategory(scene.Category, 0f);
            var target = new TimeSetting();
            target.SetCategory(scene.Category, 3f);
            Set(manipulator, "m_timeManagerRef", new SystemRef<TimeManager>(ProcessManager.GetDefaultName<TimeManager>(), scene.Manager));
            Set(manipulator, "m_currentSetting", current);
            Set(manipulator, "m_blendedSetting", blended);
            body(scene, manipulator, current, target);
        });

        private static float Blended(Scene scene, TimeManipulator manipulator) =>
            Get<TimeSetting>(manipulator, "m_blendedSetting").OverrideDictionaryLookup[scene.Category];

        private static IEnumerator ScaleTime(TimeManipulator manipulator, TimeSetting from, TimeSetting to, AnimationCurve curve, bool resetAfter) =>
            (IEnumerator)Call(manipulator, "ScaleTime", from, to, curve, resetAfter);

        // 0x5f87c0: the curve value at the elapsed time blends from the current setting to the target (1 + 0.5 * 2),
        // one step per fixed update; once the elapsed time passes the last key (0x5f897b ucomiss end, elapsed;
        // jae), the value at the last key is applied and the callback runs.
        [Test]
        public void ScaleTimeBlendsAlongTheCurveThenSettlesOnTheLastKey()
        {
            WithManipulator((scene, manipulator, current, target) =>
            {
                int completed = 0;
                Set(manipulator, "m_onComplete", (Action)(() => completed++)); // ApplyTimeScaling stores it, but then needs the coroutine host
                var curve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.03f, 1f));
                IEnumerator steps = ScaleTime(manipulator, current, target, curve, false);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.InstanceOf<WaitForFixedUpdate>());
                Assert.That(Blended(scene, manipulator), Is.EqualTo(2f), "the curve is 0.5 at time 0");
                Assert.That(steps.MoveNext(), Is.True, "0.0167 of 0.03 has elapsed");
                Assert.That(Blended(scene, manipulator), Is.GreaterThan(2f).And.LessThan(3f));
                Assert.That(completed, Is.EqualTo(0));
                Assert.That(steps.MoveNext(), Is.False, "0.0333 is past the last key");
                Assert.That(Blended(scene, manipulator), Is.EqualTo(3f), "the curve is 1 at the last key");
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x5f8989: with resetAfter the final blend is not applied; CleanUp runs instead, then the callback.
        [Test]
        public void ScaleTimeWithResetAfterSkipsTheFinalBlendAndCleansUp()
        {
            WithManipulator((scene, manipulator, current, target) =>
            {
                int completed = 0;
                Set(manipulator, "m_onComplete", (Action)(() => completed++)); // see ScaleTimeBlendsAlongTheCurveThenSettlesOnTheLastKey
                var curve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.03f, 1f));
                IEnumerator steps = ScaleTime(manipulator, current, target, curve, true);

                while (steps.MoveNext()) { }
                Assert.That(Blended(scene, manipulator), Is.LessThan(3f), "the last in-loop blend stays");
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x5f8887: a curve whose last key is at time 0 steps once (0 <= 0), then the next elapsed time passes it.
        [Test]
        public void AnInstantCurveStepsOnceBeforeSettling()
        {
            WithManipulator((scene, manipulator, current, target) =>
            {
                int completed = 0;
                Set(manipulator, "m_onComplete", (Action)(() => completed++)); // see ScaleTimeBlendsAlongTheCurveThenSettlesOnTheLastKey
                IEnumerator steps = ScaleTime(manipulator, current, target, new AnimationCurve(new Keyframe(0f, 0.5f)), false);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(Blended(scene, manipulator), Is.EqualTo(2f));
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // 0x5f86d0: a held handle is kept while the manager reference is null (IsNull), because the removal needs it.
        [Test]
        public void CleanUpKeepsTheHandleWhileTheManagerIsAbsent()
        {
            WithManipulator((scene, manipulator, current, target) =>
            {
                Set(manipulator, "m_timeManagerRef", ProcessManager.GetSystemRef<TimeManager>(null, true)); // the row Awake takes, idle here
                var handle = new StackableDataHandle();
                Set(manipulator, "m_stackableDataHandle", handle); // ApplyTimeScaling stacks it only on a live manager
                manipulator.CleanUp();
                Assert.That(Get<StackableDataHandle>(manipulator, "m_stackableDataHandle"), Is.SameAs(handle));
            });
        }

        // 0x5f8510: the callback is stored first, nothing is stacked without a valid manager, and the coroutine is
        // started on the static CoroutineUtils host with no guard (a missing host faults, 0x06001172).
        [Test]
        public void ApplyTimeScalingStoresTheCallbackThenNeedsTheCoroutineHost()
        {
            WithManipulator((scene, manipulator, current, target) =>
            {
                FieldInfo host = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
                object priorHost = host.GetValue(null);
                scene.Owned.Lease(new ReleaseAction(() => host.SetValue(null, priorHost)));
                host.SetValue(null, null);
                Set(manipulator, "m_timeManagerRef", ProcessManager.GetSystemRef<TimeManager>(null, true)); // the row Awake takes, idle here
                Action callback = () => { };

                Assert.Throws<NullReferenceException>(() => manipulator.ApplyTimeScaling(target, new AnimationCurve(), callback));
                Assert.That(Get<Action>(manipulator, "m_onComplete"), Is.SameAs(callback));
                Assert.That(Get<StackableDataHandle>(manipulator, "m_stackableDataHandle"), Is.Null, "no manager, nothing stacked");
            });
        }
    }
}
