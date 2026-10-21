using System.Collections.Generic;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branch of Dreamteck.Splines.LengthCalculator, run in play mode: SplineUser.Awake caches
    // Application.isPlaying, so a setter only queues the rebuild and Awake rebuilds immediately when buildOnAwake is on. The
    // computer holds the Linear polyline (0,0,0) -> (10,0,0) -> (10,0,10), 20 units long. Awake runs by itself on AddComponent
    // (without a spline, so it measures 0); the tests call it again after the spline and the events are in place.
    public sealed class OriginalDreamteckLengthCalculatorPlayModeTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private SplineComputer NewLine()
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            computer.RebuildImmediate();
            return computer;
        }

        private static LengthCalculator.LengthEvent NewEvent(LengthCalculator.LengthEvent.Type type, float target, int[] counter, int slot, bool enabled = true)
        {
            LengthCalculator.LengthEvent lengthEvent = new LengthCalculator.LengthEvent(type);
            lengthEvent.targetLength = target;
            lengthEvent.enabled = enabled;
            lengthEvent.onChange.AddListener(delegate { counter[slot]++; });
            return lengthEvent;
        }

        // The measured length of the line, taken from a calculator that rebuilt immediately.
        private float Measure(SplineComputer computer)
        {
            LengthCalculator probe = NewObject("probe").AddComponent<LengthCalculator>();
            probe.spline = computer;
            probe.RebuildImmediate();
            Assert.That(probe.length, Is.EqualTo(20f).Within(1e-3f));
            return probe.length;
        }

        // A calculator awake without a spline, with the four events on the measured length (the target comes from the measured value
        // so the float comparison is exact): Both on it, a disabled Growing on it, a Shrinking above it and a Both below it. The
        // spline setter only queued its rebuild, so nothing is measured yet.
        private LengthCalculator NewCalculator(SplineComputer computer, float measured, int[] counter)
        {
            LengthCalculator calculator = NewObject("calculator").AddComponent<LengthCalculator>();
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, measured, counter, 0));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, measured, counter, 1, false));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Shrinking, measured + 1f, counter, 2));
            calculator.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, measured - 1f, counter, 3));
            calculator.spline = computer;
            Assert.That(calculator.length, Is.EqualTo(0f), "nothing measured yet");
            return calculator;
        }

        // Awake 0x49c870: measures the whole clipped spline, remembers it, and fires every event whose target length is exactly
        // that length, enabled or not (it never calls Check). With buildOnAwake off SplineUser.Awake only takes the samples, so no
        // Build runs and lastLength still holds its 0 until Awake's own write: only the exact-length firing is observed, and the
        // stored lastLength is the measured length (a different prior value from 0).
        [Test]
        public void AwakeMeasuresAndFiresTheEventsWithTheExactLength()
        {
            SplineComputer computer = NewLine();
            float measured = Measure(computer);
            int[] counter = new int[4];
            LengthCalculator calculator = NewCalculator(computer, measured, counter);
            calculator.buildOnAwake = false;
            Assert.That(DreamteckGeneratorFixture.Field<float>(calculator, "lastLength"), Is.EqualTo(0f), "no build has remembered a length");

            DreamteckGeneratorFixture.Invoke(calculator, "Awake");
            Assert.That(calculator.length, Is.EqualTo(measured));
            Assert.That(counter, Is.EqualTo(new[] { 1, 1, 0, 0 }), "the matching targets fire, enabled or not");
            Assert.That(DreamteckGeneratorFixture.Field<float>(calculator, "lastLength"), Is.EqualTo(measured));
        }

        // With buildOnAwake on (the default) SplineUser.Awake rebuilds immediately while playing, before LengthCalculator.Awake
        // measures: Build 0x49c910 checks every enabled event over the change from lastLength 0 to the measured length (the Both on
        // the length and the Both below it cross; the disabled Growing and the Shrinking above do not) and remembers it, then Awake
        // fires the exact targets again, enabled or not.
        [Test]
        public void AwakeBuildChecksTheEventsFromTheOldLengthBeforeItFiresTheExactTargets()
        {
            SplineComputer computer = NewLine();
            float measured = Measure(computer);
            int[] counter = new int[4];
            LengthCalculator calculator = NewCalculator(computer, measured, counter);
            Assert.That(calculator.buildOnAwake, Is.True);

            DreamteckGeneratorFixture.Invoke(calculator, "Awake");
            Assert.That(calculator.length, Is.EqualTo(measured));
            Assert.That(counter, Is.EqualTo(new[] { 2, 1, 0, 1 }), "Build checked 0 -> 20, then Awake fired the targets equal to 20");
            Assert.That(DreamteckGeneratorFixture.Field<float>(calculator, "lastLength"), Is.EqualTo(measured));
        }
    }
}
