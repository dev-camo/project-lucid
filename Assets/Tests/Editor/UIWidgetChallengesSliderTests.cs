using System;
using System.Collections;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetChallengesSlider (Game.Runtime 0x020009a8, x86-64 0x69C220..0x69C6D0, iterator 0x020009a9) and UIWidgetChallengesSliderMini
    // (0x020009ab, 0x69C710..0x69CA80, iterator 0x020009ac) as real components on inactive owned hosts over a real Slider. The iterators
    // are stepped by hand; the elapsed-time state (<startTime>5__2, <timeElapsed>5__2) is set on the iterator because Time.time and
    // Time.deltaTime are constant inside a synchronous EditMode test. Not covered: the easing over real frame time, and the start of
    // the mini slider's coroutine on the process coroutine host.
    public sealed class UIWidgetChallengesSliderTests
    {
        private static void AssertIteratorShape(Type widget, string name, (string, int)[] fields)
        {
            Type iterator = widget.GetNestedType(name, System.Reflection.BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null, name);
            AssertMembers(iterator, 0x00100103, fields,
                new (string, int)[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1), ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
        }

        private sealed class Rig
        {
            public UIWidgetChallengesSlider Widget;
            public UIWidgetChallengesSliderMini Mini;
            public Slider Slider;
            public RectTransform SliderRect;
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            rig.Slider = owned.Host("Lucid challenges slider", false).AddComponent<Slider>();
            rig.SliderRect = (RectTransform)rig.Slider.transform;
            // GetAnchoredPosition casts the widget's transform to a RectTransform, as a widget under a canvas has.
            GameObject widgetHost = owned.Track(new GameObject("Lucid challenges slider widget", typeof(RectTransform)));
            widgetHost.SetActive(false);
            rig.Widget = widgetHost.AddComponent<UIWidgetChallengesSlider>();
            rig.Mini = owned.Host("Lucid challenges mini slider widget", false).AddComponent<UIWidgetChallengesSliderMini>();
            Set(rig.Widget, "m_slider", rig.Slider); // Reflection: serialized references
            Set(rig.Mini, "m_slider", rig.Slider);
            body(rig);
        });

        // dump.cs TypeDefIndex 2472/2471 and the DummyDll rows (fields 0x04002878..0x04002880, methods 0x060037d4..0x060037de).
        [Test]
        public void TheSliderDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetChallengesSlider), 0x00100001,
                new (string, int)[] { ("m_slider", 0x0001), ("m_animationDuration", 0x0001), ("m_parameters", 0x0001), ("m_onCompleteCurrentAnimation", 0x0001) },
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("GetAnchoredPosition", 0x0086), ("SetValue", 0x0086), ("AnimateSlider", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetChallengesSlider), 1, 2);
            Assert.That(typeof(UIWidgetChallengesSlider).BaseType, Is.EqualTo(typeof(UIWidget)));
            AssertIteratorShape(typeof(UIWidgetChallengesSlider), "<AnimateSlider>d__7",
                new (string, int)[]
                {
                    ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("target", 0x0006), ("<startTime>5__2", 0x0001),
                });
        }

        // dump.cs TypeDefIndex 2475/2474 and the DummyDll rows (fields 0x04002884..0x0400288a, methods 0x060037e0..0x060037e9).
        [Test]
        public void TheMiniSliderDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetChallengesSliderMini), 0x00100101,
                new (string, int)[] { ("m_slider", 0x0001), ("m_animationTimeSeconds", 0x0001), ("m_parameters", 0x0001) },
                new (string, int)[] { ("Setup", 0x00c6), ("AnimateNewReward", 0x0086), ("Animate", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetChallengesSliderMini), 1, 2);
            Assert.That(typeof(UIWidgetChallengesSliderMini).BaseType, Is.EqualTo(typeof(UIWidget)));
            AssertIteratorShape(typeof(UIWidgetChallengesSliderMini), "<Animate>d__5",
                new (string, int)[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006), ("<timeElapsed>5__2", 0x0001) });
        }

        // The constructors 0x69C4C0 (0.5 seconds, 0x3f000000) and 0x69C8A0 (10 seconds, 0x41200000) store the authored durations.
        [Test]
        public void TheConstructorsSetTheAnimationDurations()
        {
            Run(rig =>
            {
                Assert.That(Get<float>(rig.Widget, "m_animationDuration"), Is.EqualTo(0.5f));
                Assert.That(Get<float>(rig.Mini, "m_animationTimeSeconds"), Is.EqualTo(10f));
                Assert.That(Get<Action>(rig.Widget, "m_onCompleteCurrentAnimation"), Is.Null);
            });
        }

        // Setup 0x69C220: the slider's maximum is the parameters' maximum (an int converted to float), its value the parameters' value and
        // its width the size, with the anchors kept (SetSizeWithCurrentAnchors, horizontal axis).
        [Test]
        public void TheSliderSetupSetsTheMaximumTheValueAndTheWidth()
        {
            Run(rig =>
            {
                rig.SliderRect.anchorMin = rig.SliderRect.anchorMax = new Vector2(0.5f, 0.5f);

                rig.Widget.Setup(new UIWidgetChallengesSliderParameters(6.5f, 20, 150));

                Assert.That(rig.Slider.maxValue, Is.EqualTo(20f));
                Assert.That(rig.Slider.value, Is.EqualTo(6.5f));
                Assert.That(rig.SliderRect.rect.width, Is.EqualTo(150f));
                Assert.That(Get<UIWidgetChallengesSliderParameters>(rig.Widget, "m_parameters"), Is.Not.Null);
            });
        }

        // The mini slider's setup 0x69C710 sets the maximum and the value (a tail call into the value setter), and leaves the width alone.
        [Test]
        public void TheMiniSliderSetupSetsTheMaximumAndTheValue()
        {
            Run(rig =>
            {
                rig.SliderRect.anchorMin = rig.SliderRect.anchorMax = new Vector2(0.5f, 0.5f);
                rig.SliderRect.sizeDelta = new Vector2(77f, 5f);

                rig.Mini.Setup(new UIWidgetChallengesSliderMiniParameters(3f, 9));

                Assert.That(rig.Slider.maxValue, Is.EqualTo(9f));
                Assert.That(rig.Slider.value, Is.EqualTo(3f));
                Assert.That(rig.SliderRect.sizeDelta, Is.EqualTo(new Vector2(77f, 5f)));
            });
        }

        // Both setups cast the parameters (GetAs): a foreign object faults.
        [Test]
        public void ForeignParametersFaultInBothSetups()
        {
            Run(rig =>
            {
                Assert.Throws<InvalidCastException>(() => rig.Widget.Setup(new UIWidgetChallengesSliderMiniParameters(1f, 2)));
                Assert.Throws<InvalidCastException>(() => rig.Mini.Setup(new UIWidgetChallengesSliderParameters(1f, 2, 3)));
            });
        }

        // GetAnchoredPosition 0x69C300 reads the widget's own transform, as a RectTransform.
        [Test]
        public void TheAnchoredPositionIsTheWidgetsOwn()
        {
            Run(rig =>
            {
                ((RectTransform)rig.Widget.transform).anchoredPosition = new Vector2(12f, -3f);
                rig.SliderRect.anchoredPosition = new Vector2(99f, 99f);

                Assert.That(rig.Widget.GetAnchoredPosition(), Is.EqualTo(new Vector2(12f, -3f)));
            });
        }

        // AnimateSlider (0x69C420, MoveNext 0x69C4F0): a target above the slider's value waits (a null step per frame); a target at or below
        // it (the test is the strict target > value) finishes at once and runs the completion callback exactly once, then forgets it.
        [TestCase(5f, 5f, TestName = "ATargetEqualToTheValueFinishesAtOnce")]
        [TestCase(2f, 5f, TestName = "ATargetBelowTheValueFinishesAtOnce")]
        public void ATargetNotAboveTheValueCompletesImmediately(float target, float value)
        {
            Run(rig =>
            {
                rig.Slider.maxValue = 10f;
                rig.Slider.value = value;
                int completions = 0;
                Set(rig.Widget, "m_onCompleteCurrentAnimation", (Action)(() => completions++)); // Reflection: the callback SetValue stores
                var steps = (IEnumerator)Call(rig.Widget, "AnimateSlider", target);

                Assert.That(steps.MoveNext(), Is.False);

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(Get<Action>(rig.Widget, "m_onCompleteCurrentAnimation"), Is.Null);
                Assert.That(rig.Slider.value, Is.EqualTo(value), "the slider is not moved");
            });
        }

        [Test]
        public void ACompletionWithoutACallbackIsFine()
        {
            Run(rig =>
            {
                rig.Slider.maxValue = 10f;
                rig.Slider.value = 5f;
                var steps = (IEnumerator)Call(rig.Widget, "AnimateSlider", 1f);

                Assert.DoesNotThrow(() => steps.MoveNext());
            });
        }

        // A target above the value waits with a null step and does not complete yet; the callback stays stored.
        [Test]
        public void ATargetAboveTheValueWaitsWithoutCompleting()
        {
            Run(rig =>
            {
                rig.Slider.maxValue = 10f;
                rig.Slider.value = 5f;
                int completions = 0;
                Set(rig.Widget, "m_onCompleteCurrentAnimation", (Action)(() => completions++)); // Reflection: the callback SetValue stores
                var steps = (IEnumerator)Call(rig.Widget, "AnimateSlider", 8f);

                Assert.That(steps.MoveNext(), Is.True);

                Assert.That(steps.Current, Is.Null);
                Assert.That(completions, Is.Zero);
                Assert.That(Get<Action>(rig.Widget, "m_onCompleteCurrentAnimation"), Is.Not.Null);
                Assert.That(rig.Slider.value, Is.EqualTo(5f));
            });
        }

        // SetValue 0x69C360 stops running coroutines and then runs the previous animation's callback (once) before storing the new one;
        // the animation start on the inactive widget is refused (a logged error).
        [Test]
        public void SettingAValueFinishesThePreviousAnimationsCallbackAndStoresTheNewOne()
        {
            Run(rig =>
            {
                int first = 0, second = 0;
                Set(rig.Widget, "m_onCompleteCurrentAnimation", (Action)(() => first++)); // Reflection: the callback an earlier SetValue stored
                Action next = () => second++;
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Coroutine couldn't be started"));

                rig.Widget.SetValue(8f, next);

                Assert.That(first, Is.EqualTo(1));
                Assert.That(second, Is.Zero);
                Assert.That(Get<Action>(rig.Widget, "m_onCompleteCurrentAnimation"), Is.SameAs(next));
            });
        }

        // With no previous callback SetValue only stores the new one (even a null).
        [Test]
        public void SettingAValueWithoutAPreviousCallbackJustStoresTheNewOne()
        {
            Run(rig =>
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Coroutine couldn't be started"));

                rig.Widget.SetValue(1f, null);

                Assert.That(Get<Action>(rig.Widget, "m_onCompleteCurrentAnimation"), Is.Null);
            });
        }

        // Animate (0x69C810, MoveNext 0x69C8D0): the bar is emptied first. With a positive animation time the first step eases from the value 0
        // by zero elapsed time, so it stays at 0, and waits a frame; with no time (zero, negative or NaN: the test is the strict
        // time > elapsed) the bar is emptied and the iterator ends at once.
        [TestCase(10f, true, TestName = "APositiveAnimationTimeStartsFromEmptyAndWaits")]
        [TestCase(0f, false, TestName = "AZeroAnimationTimeEndsAfterEmptyingTheBar")]
        [TestCase(-1f, false, TestName = "ANegativeAnimationTimeEndsAfterEmptyingTheBar")]
        [TestCase(float.NaN, false, TestName = "ANaNAnimationTimeEndsAfterEmptyingTheBar")]
        public void TheMiniAnimationEmptiesTheBarFirst(float seconds, bool waits)
        {
            Run(rig =>
            {
                rig.Mini.Setup(new UIWidgetChallengesSliderMiniParameters(6f, 9));
                Set(rig.Mini, "m_animationTimeSeconds", seconds); // Reflection: serialized animation time
                var steps = (IEnumerator)Call(rig.Mini, "Animate");

                Assert.That(steps.MoveNext(), Is.EqualTo(waits));

                Assert.That(rig.Slider.value, Is.Zero, "emptied, and an elapsed time of zero eases nothing");
                if (waits) Assert.That(steps.Current, Is.Null);
            });
        }

        // AnimateSlider MoveNext 0x69C4F0 (0x69C53D..0x69C5B1): after the first wait, t = (Time.time - startTime) / duration eases the slider
        // value toward the target with the clamped smoothstep 3t^2 - 2t^3. At t = 0.25 the weight is 0.15625: 5 * 0.84375 + 8 * 0.15625 =
        // 5.46875 (a linear lerp gives 5.75).
        [Test]
        public void TheLargeSliderEasesTowardTheTargetWithASmoothstep()
        {
            Run(rig =>
            {
                rig.Slider.maxValue = 10f;
                rig.Slider.value = 5f;
                var steps = (IEnumerator)Call(rig.Widget, "AnimateSlider", 8f);
                Assert.That(steps.MoveNext(), Is.True);
                Set(steps, "<startTime>5__2", Time.time - 0.125f); // Reflection: Time.time cannot advance inside the test

                Assert.That(steps.MoveNext(), Is.True);

                Assert.That(rig.Slider.value, Is.EqualTo(5.46875f).Within(1e-3f));
            });
        }

        // The easing weight is clamped to 1 (minss at 0x69C53D..): t = 2 reaches the target exactly, so the loop ends and the callback runs
        // in the same step. An unclamped polynomial gives a negative weight, a slider value of 0 and a further wait.
        [Test]
        public void TheLargeSliderEasingWeightIsClampedAtOne()
        {
            Run(rig =>
            {
                rig.Slider.maxValue = 10f;
                rig.Slider.value = 5f;
                int completions = 0;
                Set(rig.Widget, "m_onCompleteCurrentAnimation", (Action)(() => completions++)); // Reflection: the callback SetValue stores
                var steps = (IEnumerator)Call(rig.Widget, "AnimateSlider", 8f);
                Assert.That(steps.MoveNext(), Is.True);
                Set(steps, "<startTime>5__2", Time.time - 1f); // Reflection: Time.time cannot advance inside the test

                Assert.That(steps.MoveNext(), Is.False);

                Assert.That(rig.Slider.value, Is.EqualTo(8f));
                Assert.That(completions, Is.EqualTo(1));
            });
        }

        // Animate MoveNext 0x69C8D0 (0x69C979..0x69C9DB): after the wait the slider value is lerped from its current value toward the
        // parameters' Value (not MaxValue) by elapsed / time, then the elapsed time grows by the frame time. Lerp(2, 6, 5 / 10) = 4.
        [Test]
        public void TheMiniAnimationLerpsTheCurrentValueTowardTheParametersValue()
        {
            Run(rig =>
            {
                rig.Mini.Setup(new UIWidgetChallengesSliderMiniParameters(6f, 9));
                var steps = (IEnumerator)Call(rig.Mini, "Animate"); // Reflection: private Animate, which the original reaches from its own code
                Assert.That(steps.MoveNext(), Is.True);
                rig.Slider.value = 2f;
                Set(steps, "<timeElapsed>5__2", 5f); // Reflection: elapsed time cannot advance inside the test

                Assert.That(steps.MoveNext(), Is.True);

                Assert.That(rig.Slider.value, Is.EqualTo(4f).Within(1e-4f));
                Assert.That(Get<float>(steps, "<timeElapsed>5__2"), Is.EqualTo(5f + Time.deltaTime).Within(1e-5f));
            });
        }
    }
}
