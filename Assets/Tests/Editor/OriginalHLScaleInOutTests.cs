using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.OriginalInOutTestSupport;
using AnimState = Hardlight.HLUIAnimationInOut.AnimState;
using PlayDirection = Hardlight.HLUIAnimationInOut.PlayDirection;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.HLScaleInOut (TypeDef 0x020000a9, dump.cs 11994) and its
    // iterator <RunAnimation>d__8, against the x86-64 bodies 0x1B624D0..0x1B62DAA. Fixture curves are
    // straight lines that start before time zero so a missing clamp is visible:
    // x(t) = 1 + 0.5 t on [-2, 4] (end time 4) and y(t) = 5 + t on [-2, 2] (end time 2).
    public sealed class OriginalHLScaleInOutTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly Owned Owned = new Owned();
            public readonly HLScaleInOut Scale;
            public readonly Transform Target;

            public Fixture(string name, bool active, bool awake = true)
            {
                try
                {
                    Scale = Owned.Host(name, active).AddComponent<HLScaleInOut>();
                    Target = Owned.Host(name + "-target", false).transform;
                    Set(Scale, "m_targetTransform", Target);
                    Set(Scale, "m_xCurve", Owned.Linear(-2f, 0f, 4f, 3f));
                    Set(Scale, "m_yCurve", Owned.Linear(-2f, 3f, 2f, 7f));
                    WireEvents(Owned, Scale);
                    if (awake) Call(Scale, "Awake");
                    Target.localScale = new Vector3(9f, 9f, 9f);
                }
                catch { Dispose(); throw; }
            }

            public PlayDirection Direction
            {
                get => Get<PlayDirection>(Scale, "m_playDirection");
                set => Set(Scale, "m_playDirection", value);
            }

            public AnimState State
            {
                set => Call(Scale, "set_CurrentState", value);
            }

            public IEnumerator Run() => (IEnumerator)Call(Scale, "RunAnimation");

            public void Dispose() => Owned.Dispose();
        }

        static void AssertScale(Transform target, float x, float y)
        {
            Assert.That(target.localScale.x, Is.EqualTo(x).Within(1e-5f), "x");
            Assert.That(target.localScale.y, Is.EqualTo(y).Within(1e-5f), "y");
            Assert.That(target.localScale.z, Is.EqualTo(1f), "z is the One constant");
        }

        // dump.cs 11994: sealed, NullChecks before ArrayBoundsChecks, the tooltip before SerializeField on
        // the target, ContextMenu("ScaleIn"/"ScaleOut") on the animate pair, AnimationIteration private,
        // and <RunAnimation>d__8 (fields 0..2, Awake 3 .. ForceFinishAnimation 7, RunAnimation 8).
        [Test]
        public void ScaleRetainsWholeDeclaration()
        {
            Type type = typeof(HLScaleInOut);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLUIAnimationInOut)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100101));
            OriginalHLUIAnimationInOutTests.CheckOptions(type, 1, 2);
            Assert.That(type.GetCustomAttributesData().Count, Is.EqualTo(2));
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_targetTransform", "m_xCurve", "m_yCurve" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(Transform), typeof(HLCurve), typeof(HLCurve) }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            CustomAttributeData[] target = fields[0].GetCustomAttributesData().ToArray();
            Assert.That(target.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(target[0].ConstructorArguments.Single().Value, Is.EqualTo("If this transform is null, it will select itself at runtime"));
            Assert.That(fields[1].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(fields[2].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "Awake", "OnEnable", "AnimateIn", "AnimateOut", "ForceFinishAnimation", "RunAnimation", "AnimationIteration", "SetFinalState",
            }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 0xC4, 0xC4, 0xC6, 0xC6, 0xC6, 0xC4, 0x81, 0xC4 }));
            Assert.That(methods[6].GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "currentTime", "endTime" }));
            Assert.That(methods[6].GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(float), typeof(float) }));
            Assert.That(methods[2].GetCustomAttribute<ContextMenu>().menuItem, Is.EqualTo("ScaleIn"));
            Assert.That(methods[3].GetCustomAttribute<ContextMenu>().menuItem, Is.EqualTo("ScaleOut"));
            Assert.That(methods[2].GetCustomAttributesData().Count, Is.EqualTo(1));
            Assert.That(new[] { 0, 1, 4, 6, 7 }.SelectMany(i => methods[i].GetCustomAttributesData()), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "<RunAnimation>d__8" }));
            OriginalHLUIAnimationInOutTests.CheckIterator(type, "<RunAnimation>d__8", "<animatingIn>5__2", "<timer>5__3", "<longestEndTime>5__4");
        }

        // Awake 0x060004ef @0x1B624D0: base Awake (waits built), then a null target becomes this
        // transform; an assigned target is kept.
        [Test]
        public void AwakeDefaultsTheTargetToItsOwnTransform()
        {
            using (var fixture = new Fixture("Scale-awake", false, awake: false))
            {
                Set(fixture.Scale, "m_targetTransform", null);
                Set(fixture.Scale, "m_animateOutDelaySeconds", 0.5f);
                Call(fixture.Scale, "Awake");
                Assert.That(Get(fixture.Scale, "m_targetTransform"), Is.SameAs(fixture.Scale.transform));
                Assert.That(Seconds(Get<WaitForSeconds>(fixture.Scale, "m_animateOutDelayWait")), Is.EqualTo(0.5f));
                Set(fixture.Scale, "m_targetTransform", fixture.Target);
                Call(fixture.Scale, "Awake");
                Assert.That(Get(fixture.Scale, "m_targetTransform"), Is.SameAs(fixture.Target));
            }
        }

        // AnimationIteration 0x060004f5 @0x1B628C0: time = In ? current : end - current, clamped to
        // [0, end] (minss/cmpltss/andnps), then localScale = (x(time), y(time), 1). The end argument,
        // not the curves, bounds the time. Cases: (direction, current, end) -> time.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(int index)
        {
            var cases = new[]
            {
                (PlayDirection.In, 0.5f, 1f, 0.5f),
                (PlayDirection.In, 3f, 1f, 1f),
                (PlayDirection.In, -1f, 1f, 0f),
                (PlayDirection.Out, 0.25f, 1f, 0.75f),
                (PlayDirection.Out, 3f, 1f, 0f),
                (PlayDirection.Out, -1f, 1f, 1f),
            };
            (PlayDirection direction, float current, float end, float time) = cases[index];
            using (var fixture = new Fixture("Scale-iteration", false))
            {
                fixture.Direction = direction;
                Call(fixture.Scale, "AnimationIteration", current, end);
                AssertScale(fixture.Target, 1f + 0.5f * time, 5f + time);
                Assert.That(fixture.Owned.Log, Is.Empty);
            }
        }

        // ForceFinishAnimation 0x060004f3 @0x1B626A0: nothing at all while inactive in the hierarchy
        // or without a direction; otherwise each axis is sampled at its own curve's end (In: x(4) = 3,
        // y(2) = 7) or at zero (Out: x(0) = 1, y(0) = 5), SetFinalState runs and the direction clears.
        // The activeInHierarchy test (0x1B626C3) precedes SafeStopCoroutine (0x1B626E2), so a hidden
        // object keeps its tracked coroutine handle.
        [Test]
        public void ForceFinishRequiresAnActiveHierarchyAndADirection()
        {
            using (var hidden = new Fixture("Scale-force-hidden", false))
            using (var idle = new Fixture("Scale-force-idle", true))
            {
                hidden.Direction = PlayDirection.In;
                Coroutine tracked = StartedCoroutine(hidden.Scale);
                Set(hidden.Scale, "m_trackedAnimation", tracked);
                hidden.Scale.ForceFinishAnimation();
                Assert.That(Get(hidden.Scale, "m_trackedAnimation"), Is.SameAs(tracked), "no stop while hidden");
                Assert.That(hidden.Direction, Is.EqualTo(PlayDirection.In));
                Assert.That(hidden.Target.localScale, Is.EqualTo(new Vector3(9f, 9f, 9f)));
                Assert.That(hidden.Scale.CurrentState, Is.EqualTo(AnimState.None));
                Assert.That(hidden.Owned.Log, Is.Empty);

                idle.Scale.ForceFinishAnimation();
                Assert.That(idle.Target.localScale, Is.EqualTo(new Vector3(9f, 9f, 9f)));
                Assert.That(idle.Owned.Log, Is.Empty);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ForceFinishSamplesEachAxisAtItsOwnEnd(int direction)
        {
            using (var fixture = new Fixture("Scale-force", true))
            {
                fixture.Direction = (PlayDirection)direction;
                Set(fixture.Scale, "m_normalisedInOutProgress", 0.5f);
                fixture.Scale.ForceFinishAnimation();
                if (direction == 1) AssertScale(fixture.Target, 3f, 7f);
                else AssertScale(fixture.Target, 1f, 5f);
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
                Assert.That(fixture.Scale.CurrentState, Is.EqualTo(direction == 1 ? AnimState.In : AnimState.Out));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { direction == 1 ? "onAnimateInComplete:In 1" : "onAnimateOutComplete:Out 0" }));
            }
        }

        // 0x1B626F7..0x1B62790 reads each curve's own last key (x +0xB8, y +0xC0). With y ping-ponging
        // on [-2, 2], its own end gives y(2) = 7, where the longer end 4 would mirror back to y(0) = 5.
        [Test]
        public void ForceFinishInSamplesThePingPongAxisAtItsOwnEnd()
        {
            using (var fixture = new Fixture("Scale-force-pingpong", true))
            {
                Set(fixture.Scale, "m_yCurve", fixture.Owned.Linear(-2f, 3f, 2f, 7f, WrapMode.PingPong));
                fixture.Direction = PlayDirection.In;
                fixture.Scale.ForceFinishAnimation();
                AssertScale(fixture.Target, 3f, 7f);
            }
        }

        // AnimateIn 0x060004f1 @0x1B625F0: after the In/AnimatingIn guard, a set force-activate flag
        // activates an inactive object before Animate, which then starts the run (its first step is
        // synchronous). Without the flag the object stays inactive and only the direction is stored;
        // a guarded state returns before any activation.
        [Test]
        public void AnimateInActivatesTheObjectOnlyWhenFlaggedAndNotGuarded()
        {
            using (var plain = new Fixture("Scale-in-plain", false))
            using (var guarded = new Fixture("Scale-in-guarded", false))
            using (var forced = new Fixture("Scale-in-forced", false))
            {
                plain.Scale.AnimateIn();
                Assert.That(plain.Scale.gameObject.activeSelf, Is.False);
                Assert.That(plain.Direction, Is.EqualTo(PlayDirection.In));
                Assert.That(plain.Owned.Log, Is.Empty);

                Set(guarded.Scale, "m_forceActivateGameObjectBeforeAnimation", true);
                guarded.State = AnimState.AnimatingIn;
                guarded.Scale.AnimateIn();
                Assert.That(guarded.Scale.gameObject.activeSelf, Is.False);
                Assert.That(guarded.Direction, Is.EqualTo(PlayDirection.None));

                Set(forced.Scale, "m_forceActivateGameObjectBeforeAnimation", true);
                forced.Scale.AnimateIn();
                Assert.That(forced.Scale.gameObject.activeSelf, Is.True);
                Assert.That(Get(forced.Scale, "m_trackedAnimation"), Is.Not.Null);
                Assert.That(forced.Scale.CurrentState, Is.EqualTo(AnimState.AnimatingIn));
                Assert.That(forced.Owned.Log, Is.EqualTo(new[] { "onAnimateIn:AnimatingIn 0" }));
                forced.Scale.StopAllCoroutines();
            }
        }

        // AnimateOut 0x060004f2 has no force-activate step.
        [Test]
        public void AnimateOutNeverActivatesTheObject()
        {
            using (var fixture = new Fixture("Scale-out", false))
            {
                Set(fixture.Scale, "m_forceActivateGameObjectBeforeAnimation", true);
                fixture.Scale.AnimateOut();
                Assert.That(fixture.Scale.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.Out));
                fixture.State = AnimState.AnimatingOut;
                fixture.Direction = PlayDirection.None;
                fixture.Scale.AnimateOut();
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
            }
        }

        // MoveNext 0x060004fa @0x1B62A10 (jump table 0x1B62D40): state 0 sets the animating state, fires
        // the direction's start event and always yields its delay wait (states 1/2); the resume fires
        // the matching *Start event, restarts the timer at zero whatever the progress, and runs the
        // first iteration against max(x end, y end) = 4 before yielding null (state 3).
        [TestCase(1)]
        [TestCase(2)]
        public void RunAlwaysWaitsThenRestartsFromZeroAgainstTheLongerCurve(int direction)
        {
            using (var fixture = new Fixture("Scale-run", false))
            {
                bool animatingIn = direction == 1;
                fixture.Direction = (PlayDirection)direction;
                fixture.State = animatingIn ? AnimState.AnimatingOut : AnimState.AnimatingIn;
                Set(fixture.Scale, "m_normalisedInOutProgress", 0.5f);
                IEnumerator run = fixture.Run();
                Assert.That(run.GetType().Name, Is.EqualTo("<RunAnimation>d__8"));
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.Current, Is.SameAs(Get(fixture.Scale, animatingIn ? "m_animateInDelayWait" : "m_animateOutDelayWait")), "no reversal shortcut");
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { animatingIn ? "onAnimateIn:AnimatingIn 0.5" : "onAnimateOut:AnimatingOut 0.5" }));
                Assert.That(fixture.Target.localScale, Is.EqualTo(new Vector3(9f, 9f, 9f)));

                float delta = Time.deltaTime;
                Assume.That(delta, Is.LessThan(1f), "the expected samples assume one EditMode delta below 1 second");
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.Current, Is.Null);
                Assert.That(fixture.Owned.Log.Last(), Is.EqualTo(animatingIn ? "onAnimateInStart:AnimatingIn 0.5" : "onAnimateOutStart:AnimatingOut 0.5"));
                Assert.That(Hoisted(run, "longestEndTime").GetValue(run), Is.EqualTo(4f));
                Assert.That(Hoisted(run, "timer").GetValue(run), Is.EqualTo(delta));
                float time = animatingIn ? delta : 4f - delta;
                // The time is clamped to the longer end (4) only; past its last key at 2 the
                // ClampForever y curve holds 7.
                AssertScale(fixture.Target, 1f + 0.5f * time, 5f + Mathf.Min(time, 2f));
                Assert.That(Get(fixture.Scale, "m_normalisedInOutProgress"), Is.EqualTo(0.5f), "the scale run never writes progress");
            }
        }

        // 0x1B62BE9 `maxss`: with x ending at 1 and y at 2 the shared end is y's 2, not x's.
        [Test]
        public void RunUsesTheYEndWhenTheYCurveIsLonger()
        {
            using (var fixture = new Fixture("Scale-run-longer-y", false))
            {
                Set(fixture.Scale, "m_xCurve", fixture.Owned.Linear(-2f, 0f, 1f, 1.5f));
                fixture.Direction = PlayDirection.In;
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(Hoisted(run, "longestEndTime").GetValue(run), Is.EqualTo(2f));
            }
        }

        // State 3 (0x1B62B4A): once timer >= longest end, clear +0x80 (0x1B62D01), SetFinalState(animatingIn)
        // and clear the direction (0x1B62D2D). Single-key curves end at time 0, so the first check ends it.
        [Test]
        public void RunFinishesWithTheFinalStateAndClearsTheDirection()
        {
            using (var fixture = new Fixture("Scale-run-end", false))
            {
                Set(fixture.Scale, "m_xCurve", fixture.Owned.Curve(WrapMode.ClampForever, new Keyframe(0f, 2f)));
                Set(fixture.Scale, "m_yCurve", fixture.Owned.Curve(WrapMode.ClampForever, new Keyframe(0f, 4f)));
                fixture.Direction = PlayDirection.Out;
                Set(fixture.Scale, "m_trackedAnimation", StartedCoroutine(fixture.Scale));
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.MoveNext(), Is.True);
                AssertScale(fixture.Target, 2f, 4f);
                Assert.That(run.MoveNext(), Is.False);
                Assert.That(Get(fixture.Scale, "m_trackedAnimation"), Is.Null);
                Assert.That(fixture.Scale.CurrentState, Is.EqualTo(AnimState.Out));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateOut:AnimatingOut 0", "onAnimateOutStart:AnimatingOut 0", "onAnimateOutComplete:Out 0" }));
                Assert.Throws<NotSupportedException>(() => run.Reset());
            }
        }

        // OnEnable 0x060004f0 mirrors HLAlphaInOut: an initial Out force finishes (scale (1, 5, 1),
        // complete-out) before the inlined base adopts Out; the matching target dispatches nothing.
        [Test]
        public void OnEnableForceFinishesAnInitialOut()
        {
            using (var fixture = new Fixture("Scale-onenable", true))
            {
                Set(fixture.Scale, "m_initialState", AnimState.Out);
                Set(fixture.Scale, "m_animateToStateOnEnable", AnimState.Out);
                Call(fixture.Scale, "OnEnable");
                AssertScale(fixture.Target, 1f, 5f);
                Assert.That(fixture.Scale.CurrentState, Is.EqualTo(AnimState.Out));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateOutComplete:Out 0" }));
            }
        }

        // OnEnable 0x060004f0 @0x1B6256F: an initial In force finishes in on an active object (scale
        // (3, 7, 1), complete-in) and the matching target dispatches nothing.
        [Test]
        public void OnEnableForceFinishesAnInitialIn()
        {
            using (var fixture = new Fixture("Scale-onenable-in", true))
            {
                Set(fixture.Scale, "m_initialState", AnimState.In);
                Set(fixture.Scale, "m_animateToStateOnEnable", AnimState.In);
                Call(fixture.Scale, "OnEnable");
                AssertScale(fixture.Target, 3f, 7f);
                Assert.That(fixture.Scale.CurrentState, Is.EqualTo(AnimState.In));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateInComplete:In 1" }));
            }
        }

        // SetFinalState 0x060004f6 inlines its own ScheduleAutoAnimateOut (0x1B6297D..0x1B629B9) in the
        // in branch only.
        [Test]
        public void SetFinalStateInSchedulesTheAutoAnimateOutOnlyWhenFlagged()
        {
            using (var fixture = new Fixture("Scale-final-auto", true))
            {
                Set(fixture.Scale, "m_autoAnimateOutAfterTimeout", true);
                Call(fixture.Scale, "SetFinalState", false);
                Assert.That(Get(fixture.Scale, "m_trackedAutoAnimateOut"), Is.Null);
                Call(fixture.Scale, "SetFinalState", true);
                Assert.That(Get(fixture.Scale, "m_trackedAutoAnimateOut"), Is.Not.Null);
                Assert.That(fixture.Scale.CurrentState, Is.EqualTo(AnimState.In), "the timeout has not elapsed");
                fixture.Scale.StopAllCoroutines();
            }
        }
    }
}
