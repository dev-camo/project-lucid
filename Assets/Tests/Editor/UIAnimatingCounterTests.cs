using System.Collections;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIAnimatingCounter (Game.Runtime 02000842, bodies 0x60A4C0..0x60AB80) against the 1.10.1 x86-64
    // bodies cited per case. The counter lives on an owned host that is inactive unless a case says so.
    //
    // The frame time is not controllable in Edit Mode, so the animation is driven with a negative duration:
    // the loop test (0x60AA49) then fails at once and the final Refresh reaches t = 1 without any frame.
    // Interim values are checked by setting m_elapsedSeconds, which only the frame loop writes otherwise.
    public sealed class UIAnimatingCounterTests
    {
        private static UIAnimatingCounter NewCounter(Owned owned, bool active = false)
        {
            var counter = owned.Host("Lucid animating counter", false).AddComponent<UIAnimatingCounter>();
            if (active) counter.gameObject.SetActive(true);
            return counter;
        }

        private static UnityEvent Event(List<string> log, string label)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(label));
            return unityEvent;
        }

        // The value shown at an elapsed time, for a fresh counter that runs from `start` to `end` over four seconds.
        private static int ValueAt(Owned owned, int start, int end, float elapsedSeconds)
        {
            UIAnimatingCounter counter = NewCounter(owned);
            counter.SetUp(start, end, null, 4f);
            Set(counter, "m_elapsedSeconds", elapsedSeconds);
            Call(counter, "Refresh");
            return counter.CurrentValue.Value;
        }

        // Constructor 0x60A930: the animation time is 2 seconds (0x40000000) and both bindables are empty.
        [Test]
        public void ConstructionKeepsTwoSecondsAndEmptyBindings()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned);
                Assert.That(Get<float>(counter, "m_animationTimeSeconds"), Is.EqualTo(2f));
                Assert.That(counter.CurrentValue.Value, Is.Zero);
                Assert.That(counter.CurrentValueString.Value, Is.Null);
            });
        }

        // SetUp 0x60A4C0 stores the amounts, clears the elapsed time and format, then Refresh (0x60A52C)
        // shows the start amount: a null or empty format uses the invariant ToString (0x60A72D), otherwise
        // string.Format (0x60A757).
        [Test]
        public void SetUpShowsTheStartAmountAtOnce()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned);
                counter.SetUp(5, 9, null);
                Assert.That(counter.CurrentValue.Value, Is.EqualTo(5));
                Assert.That(counter.CurrentValueString.Value, Is.EqualTo("5"));

                UIAnimatingCounter formatted = NewCounter(owned);
                formatted.SetUp(7, 9, "x{0}y");
                Assert.That(formatted.CurrentValueString.Value, Is.EqualTo("x7y"));

                UIAnimatingCounter empty = NewCounter(owned);
                empty.SetUp(-3, 9, "");
                Assert.That(empty.CurrentValueString.Value, Is.EqualTo("-3"));
            });
        }

        // The duration replaces the animation time only when it is not 0 (0x60A50E..0x60A518).
        [Test]
        public void OnlyANonZeroDurationReplacesTheAnimationTime()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned);
                counter.SetUp(0, 1, null);
                Assert.That(Get<float>(counter, "m_animationTimeSeconds"), Is.EqualTo(2f));
                counter.SetUp(0, 1, null, 4f);
                Assert.That(Get<float>(counter, "m_animationTimeSeconds"), Is.EqualTo(4f));
                counter.SetUp(0, 1, null, 0f);
                Assert.That(Get<float>(counter, "m_animationTimeSeconds"), Is.EqualTo(4f));
                counter.SetUp(0, 1, null, -1f);
                Assert.That(Get<float>(counter, "m_animationTimeSeconds"), Is.EqualTo(-1f));
            });
        }

        // Refresh 0x60A6B0..0x60A6D5 rewrites the string only when the value changed or the string is empty.
        [Test]
        public void TheStringIsRewrittenOnlyWhenTheValueChangesOrIsEmpty()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned);
                counter.SetUp(5, 9, "a{0}");
                Assert.That(counter.CurrentValueString.Value, Is.EqualTo("a5"));

                counter.SetUp(5, 9, "b{0}");
                Assert.That(counter.CurrentValueString.Value, Is.EqualTo("a5"), "same value and a non-empty string");

                counter.CurrentValueString.Value = "";
                counter.SetUp(5, 9, "b{0}");
                Assert.That(counter.CurrentValueString.Value, Is.EqualTo("b5"), "an empty string is refilled");

                counter.SetUp(6, 9, "b{0}");
                Assert.That(counter.CurrentValueString.Value, Is.EqualTo("b6"), "a new value rewrites it");
            });
        }

        // The shown value is Mathf.RoundToInt(Mathf.Lerp(start, end, elapsed / time)): the native code
        // clamps the ratio (0x60A58C..0x60A5A0), interpolates in single precision (0x60A5A3..0x60A5DB) and
        // rounds the double with ties to even (Math.Round: 0x60A604..0x60A693).
        [Test]
        public void RoundingTiesGoToTheEvenNeighbour()
        {
            Owned.Run(owned =>
            {
                Assert.That(ValueAt(owned, 0, 10, 1f), Is.EqualTo(2), "2.5");
                Assert.That(ValueAt(owned, 0, 14, 1f), Is.EqualTo(4), "3.5");
                Assert.That(ValueAt(owned, 0, 2, 1f), Is.EqualTo(0), "0.5");
                Assert.That(ValueAt(owned, 0, 6, 1f), Is.EqualTo(2), "1.5");
                Assert.That(ValueAt(owned, 0, -10, 1f), Is.EqualTo(-2), "-2.5");
                Assert.That(ValueAt(owned, 0, 10, 1.5f), Is.EqualTo(4), "3.75");
            });
        }

        [Test]
        public void ProgressIsClampedBetweenTheStartAndEndAmounts()
        {
            Owned.Run(owned =>
            {
                Assert.That(ValueAt(owned, 2, 10, 2f), Is.EqualTo(6), "halfway");
                Assert.That(ValueAt(owned, 2, 10, 8f), Is.EqualTo(10), "past the end");
                Assert.That(ValueAt(owned, 2, 10, -4f), Is.EqualTo(2), "before the start");
            });
        }

        // StartAnimating 0x60A870 stops coroutines and, on an inactive object, only raises the
        // run-when-enabled flag (0x60A8FA). The coroutine 0x60AA10 then clears the flag, restarts the
        // elapsed time, refreshes, raises the start event and yields null; with a negative duration its next
        // step skips the loop, sets the elapsed time to the duration, refreshes to the end amount and raises
        // the end event.
        [Test]
        public void AnInactiveCounterAnimatesLaterFromStartToEnd()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned);
                var log = new List<string>();
                Set(counter, "m_onStart", Event(log, "start"));
                Set(counter, "m_onEnd", Event(log, "end"));
                counter.SetUp(3, 12, null, -1f);

                counter.StartAnimating();
                Assert.That(Get<bool>(counter, "m_animateWhenEnabled"), Is.True);
                Assert.That(log, Is.Empty);

                var animation = (IEnumerator)Call(counter, "AnimateCoroutine");
                Assert.That(animation.MoveNext(), Is.True);
                Assert.That(animation.Current, Is.Null);
                Assert.That(Get<bool>(counter, "m_animateWhenEnabled"), Is.False);
                Assert.That(log, Is.EqualTo(new[] { "start" }));
                Assert.That(counter.CurrentValue.Value, Is.EqualTo(3));

                Assert.That(animation.MoveNext(), Is.False);
                Assert.That(log, Is.EqualTo(new[] { "start", "end" }));
                Assert.That(counter.CurrentValue.Value, Is.EqualTo(12));
                Assert.That(Get<float>(counter, "m_elapsedSeconds"), Is.EqualTo(-1f));
            });
        }

        // An active object starts the coroutine at once, which runs to its first yield.
        [Test]
        public void AnActiveCounterRunsTheStartOfItsAnimationAtOnce()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned, true);
                var log = new List<string>();
                Set(counter, "m_onStart", Event(log, "start"));
                counter.SetUp(4, 8, null, -1f);

                counter.StartAnimating();
                Assert.That(log, Is.EqualTo(new[] { "start" }));
                Assert.That(Get<bool>(counter, "m_animateWhenEnabled"), Is.False);
                Assert.That(counter.CurrentValue.Value, Is.EqualTo(4));
            });
        }

        // OnEnable 0x60A780 starts the animation only when the flag is set.
        [Test]
        public void EnablingStartsTheDeferredAnimationOnly()
        {
            Owned.Run(owned =>
            {
                UIAnimatingCounter counter = NewCounter(owned, true);
                var log = new List<string>();
                Set(counter, "m_onStart", Event(log, "start"));
                counter.SetUp(4, 8, null, -1f);

                Call(counter, "OnEnable");
                Assert.That(log, Is.Empty, "no deferred animation");

                Set(counter, "m_animateWhenEnabled", true); // only StartAnimating on an inactive object sets it, and this host is active
                Call(counter, "OnEnable");
                Assert.That(log, Is.EqualTo(new[] { "start" }));
            });
        }
    }
}
