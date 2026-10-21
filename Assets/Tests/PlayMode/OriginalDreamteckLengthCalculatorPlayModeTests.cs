using System.Collections.Generic;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branch of Dreamteck.Splines.LengthCalculator, run in play mode: SplineUser.Awake caches
    // Application.isPlaying, so a setter only queues the rebuild and Awake rebuilds immediately when buildOnAwake is on. The
    // computer holds the Linear polyline (0,0,0) -> (10,0,0) -> (10,0,10), 20 units long. The engine runs Awake once, when the
    // configured inactive calculator is activated.
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

        private static LengthCalculator.LengthEvent NewEvent(LengthCalculator.LengthEvent.Type type, float target, bool enabled = true)
        {
            LengthCalculator.LengthEvent lengthEvent = new LengthCalculator.LengthEvent(type);
            lengthEvent.targetLength = target;
            lengthEvent.enabled = enabled;
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

        // A calculator whose Awake has not run yet, configured the way a saved scene holds it: the spline, buildOnAwake and the four
        // events on the measured length (the target comes from the measured value so the float comparison is exact): Both on it, a
        // disabled Growing on it, a Shrinking above it and a Both below it. The configuration is made on an inactive template and
        // copied by Instantiate into an inactive clone (only the serialized state travels; _length and lastLength start at their
        // initialisers), and the counters are attached to the clone's events. Activating the clone makes the engine run Awake once.
        // The template is never activated; setting its spline before any Awake takes the editor-compile immediate rebuild, which is
        // why the counted component is the clone.
        private LengthCalculator NewInactiveCalculator(SplineComputer computer, float measured, int[] counter, bool buildOnAwake)
        {
            GameObject template = NewObject("template");
            template.SetActive(false);
            LengthCalculator configured = template.AddComponent<LengthCalculator>();
            configured.buildOnAwake = buildOnAwake;
            configured.spline = computer;
            configured.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, measured));
            configured.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Growing, measured, false));
            configured.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Shrinking, measured + 1f));
            configured.AddEvent(NewEvent(LengthCalculator.LengthEvent.Type.Both, measured - 1f));

            GameObject clone = Object.Instantiate(template);
            _owned.Add(clone);
            Assert.That(clone.activeSelf, Is.False);
            LengthCalculator calculator = clone.GetComponent<LengthCalculator>();
            Assert.That(calculator.spline, Is.SameAs(computer));
            Assert.That(calculator.buildOnAwake, Is.EqualTo(buildOnAwake));
            Assert.That(calculator.lengthEvents.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                calculator.lengthEvents[i].onChange.AddListener(delegate { counter[slot]++; });
            }

            Assert.That(calculator.length, Is.EqualTo(0f), "nothing measured yet");
            Assert.That(DreamteckGeneratorFixture.Field<float>(calculator, "lastLength"), Is.EqualTo(0f), "no build has remembered a length");
            return calculator;
        }

        // Awake 0x49c870: measures the whole clipped spline, remembers it, and fires every event whose target length is exactly
        // that length, enabled or not (it never calls Check). With buildOnAwake off SplineUser.Awake only takes the samples, so no
        // Build runs and lastLength still holds its 0 until Awake's own write: only the exact-length firing is observed, and the
        // stored lastLength is the measured length.
        [Test]
        public void AwakeMeasuresAndFiresTheEventsWithTheExactLength()
        {
            SplineComputer computer = NewLine();
            float measured = Measure(computer);
            int[] counter = new int[4];
            LengthCalculator calculator = NewInactiveCalculator(computer, measured, counter, false);

            calculator.gameObject.SetActive(true);
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
            LengthCalculator calculator = NewInactiveCalculator(computer, measured, counter, true);

            calculator.gameObject.SetActive(true);
            Assert.That(calculator.length, Is.EqualTo(measured));
            Assert.That(counter, Is.EqualTo(new[] { 2, 1, 0, 1 }), "Build checked 0 -> 20, then Awake fired the targets equal to 20");
            Assert.That(DreamteckGeneratorFixture.Field<float>(calculator, "lastLength"), Is.EqualTo(measured));
        }
    }
}
