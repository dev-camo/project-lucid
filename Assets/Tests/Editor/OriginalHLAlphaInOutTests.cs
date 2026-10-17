using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.OriginalInOutTestSupport;
using AnimState = Hardlight.HLUIAnimationInOut.AnimState;
using PlayDirection = Hardlight.HLUIAnimationInOut.PlayDirection;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.HLAlphaInOut (TypeDef 0x020000a3, dump.cs 11988) and its
    // iterator <RunAnimation>d__4, against the x86-64 bodies 0x1B61300..0x1B61A2A. Each case owns a
    // component on its own GameObject, a CanvasGroup, an Image on an inactive host (so no canvas
    // rebuild is queued) and a curve asset whose keys are a straight line from (0, 0.25) to
    // (2, 0.75), i.e. alpha(t) = 0.25 + 0.25 t clamped to [0, 2].
    public sealed class OriginalHLAlphaInOutTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly Owned Owned = new Owned();
            public readonly HLAlphaInOut Alpha;
            public readonly CanvasGroup Group;
            public readonly Image Image;

            public Fixture(string name, bool active)
            {
                try
                {
                    Alpha = Owned.Host(name, active, typeof(CanvasGroup)).AddComponent<HLAlphaInOut>();
                    Group = Alpha.GetComponent<CanvasGroup>();
                    Image = Owned.Host(name + "-graphic", false, typeof(Image)).GetComponent<Image>();
                    Set(Alpha, "m_targetCanvasGroup", Group);
                    Set(Alpha, "m_targetGraphic", Image);
                    Set(Alpha, "m_alphaCurve", Owned.Linear(0f, 0.25f, 2f, 0.75f));
                    WireEvents(Owned, Alpha);
                    Call(Alpha, "Awake");
                    Group.alpha = 0.5f;
                    Image.color = new Color(0.1f, 0.2f, 0.3f, 0.5f);
                }
                catch { Dispose(); throw; }
            }

            public PlayDirection Direction
            {
                get => Get<PlayDirection>(Alpha, "m_playDirection");
                set => Set(Alpha, "m_playDirection", value);
            }

            public float Progress
            {
                get => Get<float>(Alpha, "m_normalisedInOutProgress");
                set => Set(Alpha, "m_normalisedInOutProgress", value);
            }

            public AnimState State
            {
                set => Call(Alpha, "set_CurrentState", value);
            }

            public IEnumerator Run() => (IEnumerator)Call(Alpha, "RunAnimation");

            public void Dispose() => Owned.Dispose();
        }

        // dump.cs 11988: sealed, Il2CppSetOption(ArrayBoundsChecks) before (NullChecks), three private
        // serialized targets, the six overrides in order and <RunAnimation>d__4 (fields 0..2, then
        // OnEnable 3 and RunAnimation 4).
        [Test]
        public void AlphaRetainsWholeDeclaration()
        {
            Type type = typeof(HLAlphaInOut);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLUIAnimationInOut)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100101), "public sealed beforefieldinit");
            OriginalHLUIAnimationInOutTests.CheckOptions(type, 2, 1);
            Assert.That(type.GetCustomAttributesData().Count, Is.EqualTo(2));
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_targetGraphic", "m_targetCanvasGroup", "m_alphaCurve" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(Graphic), typeof(CanvasGroup), typeof(HLCurve) }));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.IsPrivate, Is.True, field.Name);
                Assert.That(field.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }), field.Name);
            }
            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "OnEnable", "RunAnimation", "AnimateIn", "AnimateOut", "ForceFinishAnimation", "SetFinalState" }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 0xC4, 0xC4, 0xC6, 0xC6, 0xC6, 0xC4 }));
            Assert.That(methods.Where(m => m.Name != "RunAnimation").SelectMany(m => m.GetCustomAttributesData()), Is.Empty);
            Assert.That(type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName));
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "<RunAnimation>d__4" }));
            OriginalHLUIAnimationInOutTests.CheckIterator(type, "<RunAnimation>d__4", "<animatingIn>5__2", "<timer>5__3", "<endTime>5__4");
        }

        // AnimateIn 0x060004d4 @0x1B61470: `dec; cmp 2; jb return` skips In and AnimatingIn, otherwise
        // +0x78 = In and Animate (slot 7). AnimateOut 0x060004d5 skips Out and AnimatingOut. The owner
        // is inactive, so only the direction store is visible.
        [TestCase(0, 1, 2)]
        [TestCase(1, 0, 2)]
        [TestCase(2, 0, 2)]
        [TestCase(3, 1, 0)]
        [TestCase(4, 1, 0)]
        public void AnimateInAndOutAreGuardedByTheCurrentState(int state, int inDirection, int outDirection)
        {
            using (var fixture = new Fixture("Alpha-guards", false))
            {
                fixture.State = (AnimState)state;
                fixture.Alpha.AnimateIn();
                Assert.That(fixture.Direction, Is.EqualTo((PlayDirection)inDirection), "AnimateIn");
                fixture.Direction = PlayDirection.None;
                fixture.Alpha.AnimateOut();
                Assert.That(fixture.Direction, Is.EqualTo((PlayDirection)outDirection), "AnimateOut");
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo((AnimState)state));
                Assert.That(fixture.Owned.Log, Is.Empty);
            }
        }

        // ForceFinishAnimation 0x060004d6 @0x1B614D0: In samples the curve at its last key time (2 ->
        // 0.75), Out at time zero (0.25); the canvas group then the graphic take the alpha (the graphic
        // keeps its rgb), SetFinalState(direction == In) runs and the direction is cleared. SetFinalState
        // 0x060004d7 stores progress before the state and fires the completion event after both.
        [TestCase(1)]
        [TestCase(2)]
        public void ForceFinishSnapsToTheCurveEndForTheDirection(int direction)
        {
            float alpha = direction == 1 ? 0.75f : 0.25f;
            AnimState state = direction == 1 ? AnimState.In : AnimState.Out;
            string completion = direction == 1 ? "onAnimateInComplete:In 1" : "onAnimateOutComplete:Out 0";
            using (var fixture = new Fixture("Alpha-force", false))
            {
                fixture.Direction = (PlayDirection)direction;
                fixture.Progress = 0.5f;
                fixture.Alpha.ForceFinishAnimation();
                Assert.That(fixture.Group.alpha, Is.EqualTo(alpha).Within(1e-6f));
                Assert.That(fixture.Image.color.a, Is.EqualTo(alpha).Within(1e-6f));
                Assert.That(fixture.Image.color.r, Is.EqualTo(0.1f));
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(state));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.None));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { completion }));
            }
        }

        // 0x1B614F5: with no direction only the coroutine stop runs; no alpha, state or event changes.
        [Test]
        public void ForceFinishWithoutADirectionOnlyStops()
        {
            using (var fixture = new Fixture("Alpha-force-none", false))
            {
                fixture.State = AnimState.AnimatingOut;
                fixture.Progress = 0.5f;
                fixture.Alpha.ForceFinishAnimation();
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.5f));
                Assert.That(fixture.Image.color.a, Is.EqualTo(0.5f));
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(AnimState.AnimatingOut));
                Assert.That(fixture.Progress, Is.EqualTo(0.5f));
                Assert.That(fixture.Owned.Log, Is.Empty);
            }
        }

        // Null targets are skipped by UIUtils.SetAlpha (0x060005dd/de), so a curve alone still finishes.
        [Test]
        public void ForceFinishToleratesMissingTargets()
        {
            using (var fixture = new Fixture("Alpha-force-null", false))
            {
                Set(fixture.Alpha, "m_targetCanvasGroup", null);
                Set(fixture.Alpha, "m_targetGraphic", null);
                fixture.Direction = PlayDirection.In;
                fixture.Alpha.ForceFinishAnimation();
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(AnimState.In));
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.5f));
            }
        }

        // OnEnable 0x060004d2 @0x1B61300: an initial In (Out) stores direction In (Out) and force
        // finishes before the inlined base body adopts the initial state and dispatches the target.
        // Initial In with target Out: complete-in fires first, then AnimateOut stores Out.
        [Test]
        public void OnEnableForceFinishesTheInitialStateBeforeDispatchingTheTarget()
        {
            using (var fixture = new Fixture("Alpha-onenable", false))
            {
                Set(fixture.Alpha, "m_initialState", AnimState.In);
                Set(fixture.Alpha, "m_animateToStateOnEnable", AnimState.Out);
                Call(fixture.Alpha, "OnEnable");
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateInComplete:In 1" }));
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(AnimState.In));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.Out));
            }
        }

        // 0x1B6130C..0x1B61314: initial None, AnimatingIn or AnimatingOut skip the pre-step entirely.
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void OnEnableOnlyPreFinishesTheTwoRestingStates(int initial)
        {
            using (var fixture = new Fixture("Alpha-onenable-skip", false))
            {
                Set(fixture.Alpha, "m_initialState", (AnimState)initial);
                Set(fixture.Alpha, "m_animateToStateOnEnable", (AnimState)initial);
                Call(fixture.Alpha, "OnEnable");
                Assert.That(fixture.Owned.Log, Is.Empty);
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.5f));
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo((AnimState)initial));
            }
        }

        // SetFinalState(true) ends with the inlined ScheduleAutoAnimateOut (0x1B615E9): with the flag
        // set on an active owner the auto animate out is tracked; SetFinalState(false) never schedules.
        [Test]
        public void SetFinalStateInSchedulesTheAutoAnimateOutOnlyWhenFlagged()
        {
            using (var fixture = new Fixture("Alpha-final-auto", true))
            {
                Set(fixture.Alpha, "m_autoAnimateOutAfterTimeout", true);
                Call(fixture.Alpha, "SetFinalState", false);
                Assert.That(Get(fixture.Alpha, "m_trackedAutoAnimateOut"), Is.Null);
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateOutComplete:Out 0" }));
                Call(fixture.Alpha, "SetFinalState", true);
                Assert.That(Get(fixture.Alpha, "m_trackedAutoAnimateOut"), Is.Not.Null);
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(AnimState.In), "the timeout has not elapsed");
                fixture.Alpha.StopAllCoroutines();
            }
        }

        // MoveNext 0x060004db @0x1B616F0, state 0: animatingIn = direction == In; CurrentState and the
        // start event follow the direction; the timer resumes from the progress (in: end * progress,
        // out: (1 - progress) * end). The first yield is the direction's delay wait unless the run
        // reverses a running animation (r12/r13 at 0x1B61742..0x1B618AF): In over AnimatingOut and Out
        // over AnimatingIn go straight into the loop and yield null.
        // first: 0 = the in delay wait, 1 = the out delay wait, 2 = straight into the loop (null).
        [TestCase(0, 1, 0)]
        [TestCase(2, 1, 0)]
        [TestCase(4, 1, 2)]
        [TestCase(3, 2, 1)]
        [TestCase(4, 2, 1)]
        [TestCase(2, 2, 2)]
        public void FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(int state, int direction, int first)
        {
            using (var fixture = new Fixture("Alpha-delay", false))
            {
                fixture.State = (AnimState)state;
                fixture.Direction = (PlayDirection)direction;
                fixture.Progress = 0.25f;
                IEnumerator run = fixture.Run();
                Assert.That(run.GetType().Name, Is.EqualTo("<RunAnimation>d__4"));
                Assert.That(run.MoveNext(), Is.True);
                bool animatingIn = direction == 1;
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(animatingIn ? AnimState.AnimatingIn : AnimState.AnimatingOut));
                Assert.That(fixture.Owned.Log.First(), Does.StartWith(animatingIn ? "onAnimateIn:" : "onAnimateOut:"));
                object expected = first == 0 ? Get(fixture.Alpha, "m_animateInDelayWait") : first == 1 ? Get(fixture.Alpha, "m_animateOutDelayWait") : null;
                Assert.That(run.Current, Is.SameAs(expected));
                Assert.That(fixture.Owned.Log.Count, Is.EqualTo(1), "no start event in this type");
                float timer = (float)Hoisted(run, "timer").GetValue(run);
                float resumed = animatingIn ? 2f * 0.25f : (1f - 0.25f) * 2f;
                if (first == 2) Assert.That(timer, Is.EqualTo(resumed + Time.deltaTime).Within(1e-6f));
                else Assert.That(timer, Is.EqualTo(resumed));
                Assert.That(Hoisted(run, "endTime").GetValue(run), Is.EqualTo(2f));
                Assert.That(Hoisted(run, "animatingIn").GetValue(run), Is.EqualTo(animatingIn));
            }
        }

        // Loop body 0x1B618B5..0x1B6195B: timer += delta; time = in ? timer : end - timer; progress =
        // time / end; alpha = curve(time) to the group then the graphic; yield null (state 2). Out from
        // progress 0.75 starts at timer 0.5, so the first step samples at 1.5 - delta, not at timer.
        [TestCase(1)]
        [TestCase(2)]
        public void LoopStepSamplesAtTheInTimeAndUpdatesProgress(int direction)
        {
            float progress = direction == 1 ? 0.25f : 0.75f;
            using (var fixture = new Fixture("Alpha-loop", false))
            {
                float delta = Time.deltaTime;
                Assume.That(delta, Is.LessThan(0.5f), "two EditMode deltas stay inside the 2 second curve");
                fixture.State = direction == 1 ? AnimState.AnimatingOut : AnimState.AnimatingIn;
                fixture.Direction = (PlayDirection)direction;
                fixture.Progress = progress;
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(run.Current, Is.Null);
                float timer = (direction == 1 ? 2f * progress : (1f - progress) * 2f) + delta;
                float time = direction == 1 ? timer : 2f - timer;
                Assert.That(fixture.Progress, Is.EqualTo(time / 2f).Within(1e-6f));
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.25f + 0.25f * time).Within(1e-5f));
                Assert.That(fixture.Image.color.a, Is.EqualTo(0.25f + 0.25f * time).Within(1e-5f));
                Assert.That(run.MoveNext(), Is.True, "timer is still below the end time");
                Assert.That(fixture.Owned.Log.Count, Is.EqualTo(1), "no completion while looping");
            }
        }

        // 0x1B617E5: the loop condition is checked only after a yield (do/while). Resuming in from
        // progress 1 starts at the end time, still runs one body (alpha at the clamped end) and then
        // SetFinalState(true) and clears +0x80 (0x1B61811..0x1B6182D) and returns false.
        [Test]
        public void RunCompletesAfterOneBodyWhenResumedAtTheEnd()
        {
            using (var fixture = new Fixture("Alpha-complete", false))
            {
                fixture.State = AnimState.AnimatingOut;
                fixture.Direction = PlayDirection.In;
                fixture.Progress = 1f;
                IEnumerator run = fixture.Run();
                Assert.That(run.MoveNext(), Is.True);
                Assert.That(fixture.Group.alpha, Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That(run.MoveNext(), Is.False);
                Assert.That(fixture.Alpha.CurrentState, Is.EqualTo(AnimState.In));
                Assert.That(fixture.Progress, Is.EqualTo(1f));
                Assert.That(fixture.Direction, Is.EqualTo(PlayDirection.In), "the run does not clear the direction");
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "onAnimateIn:AnimatingIn 1", "onAnimateInComplete:In 1" }));
                Assert.That(run.MoveNext(), Is.False);
                Assert.Throws<NotSupportedException>(() => run.Reset());
            }
        }
    }
}
