using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branches of Dreamteck.Splines.SplineProjector, run in play mode where Application.isPlaying is true:
    // SplineUser caches it in Awake, so a setter only queues the rebuild, and the tracer's trigger and node reports run (the edit
    // mode twins in OriginalDreamteckSplineProjectorTests cover the editor branch). The computer holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10), 20 units long. Reset is not called by AddComponent in play mode, so a projector starts
    // with an empty project target field. The tests are synchronous, so no engine Update runs between the calls.
    public sealed class OriginalDreamteckSplineProjectorPlayModeTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ")");
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

        // A projector that follows the given target, already projected onto the computer.
        private SplineProjector NewProjector(SplineComputer computer, Transform target)
        {
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            projector.projectTarget = target;
            projector.spline = computer;
            projector.RebuildImmediate();
            return projector;
        }

        private Transform NewTarget(Vector3 position)
        {
            GameObject target = NewObject("target");
            target.transform.position = position;
            return target.transform;
        }

        private static T Read<T>(object target, string name)
        {
            return DreamteckGeneratorFixture.Field<T>(target, name);
        }

        // Settling the queued rebuild lets a test see whether a setter queues a new one.
        private static bool RebuildQueued(SplineUser user)
        {
            return Read<bool>(user, "rebuild");
        }

        // The setters 0x4cb440 (mode), 0x4cb480 (autoProject: only switching on), 0x4cb4c0 (subdivide: only in the Accurate mode),
        // 0x4cb570 (projectTarget) and 0x4cb6e0 (targetObject) queue a rebuild for a changed value; an equal value does nothing.
        [Test]
        public void SettersQueueARebuildOnlyWhereTheShippedOnesDo()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);
            Assert.That(RebuildQueued(projector), Is.False, "RebuildImmediate settled the queue");

            projector.autoProject = true;
            Assert.That(RebuildQueued(projector), Is.False, "unchanged");
            projector.autoProject = false;
            Assert.That(RebuildQueued(projector), Is.False, "switching off does not rebuild");
            projector.autoProject = true;
            Assert.That(RebuildQueued(projector), Is.True, "switching on does");
            projector.RebuildImmediate();

            projector.subdivide = 6;
            Assert.That(projector.subdivide, Is.EqualTo(6));
            Assert.That(RebuildQueued(projector), Is.False, "the Cached mode ignores the subdivision");
            projector.mode = SplineProjector.Mode.Accurate;
            Assert.That(RebuildQueued(projector), Is.True, "a changed mode rebuilds");
            projector.RebuildImmediate();
            projector.mode = SplineProjector.Mode.Accurate;
            Assert.That(RebuildQueued(projector), Is.False, "unchanged");
            projector.subdivide = 7;
            Assert.That(RebuildQueued(projector), Is.True, "the Accurate mode rebuilds for a new subdivision");
            projector.RebuildImmediate();
            projector.subdivide = 7;
            Assert.That(RebuildQueued(projector), Is.False, "unchanged");

            projector.projectTarget = target;
            Assert.That(RebuildQueued(projector), Is.False, "unchanged");
            projector.projectTarget = NewTarget(Vector3.zero);
            Assert.That(RebuildQueued(projector), Is.True);
            projector.RebuildImmediate();

            GameObject mover = NewObject("mover");
            projector.targetObject = mover;
            Assert.That(projector.targetObject, Is.SameAs(mover));
            Assert.That(RebuildQueued(projector), Is.True);
            projector.RebuildImmediate();
            projector.targetObject = mover;
            Assert.That(RebuildQueued(projector), Is.False, "unchanged");
        }

        // OnSplineChanged 0x4cc2a0: a new computer is projected onto at once from the raw project target field (a projector
        // without one faults); nothing happens for a cleared computer.
        [Test]
        public void ANewComputerIsProjectedOntoAtOnce()
        {
            SplineComputer computer = NewLine();
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            projector.projectTarget = NewTarget(new Vector3(4f, 0f, 2f));
            projector.spline = computer;
            Near(projector.result.position, new Vector3(4f, 0f, 0f), "projected position");
            // The user has fetched no samples yet (set_spline only queues the rebuild), so ClipPercent 0x4cf500 reports 0.
            Assert.That(projector.result.percent, Is.EqualTo(0.0), "clipped over no samples");

            // With the samples fetched, the next computer is clipped properly (Cached mode).
            projector.RebuildImmediate();
            projector.spline = NewLine();
            Assert.That(projector.result.percent, Is.EqualTo(0.2).Within(1e-4), "Cached mode");
            Near(projector.result.position, new Vector3(4f, 0f, 0f), "projected position");

            SplineProjector accurate = NewObject("accurate").AddComponent<SplineProjector>();
            accurate.mode = SplineProjector.Mode.Accurate;
            accurate.subdivide = 5;
            accurate.projectTarget = NewTarget(new Vector3(12f, 0f, 5f));
            accurate.spline = computer;
            accurate.RebuildImmediate();
            accurate.spline = NewLine();
            Assert.That(accurate.result.percent, Is.EqualTo(0.75).Within(2e-3), "Accurate mode on the second segment");
            Near(accurate.result.position, new Vector3(10f, 0f, 5f), "projected position");

            accurate.spline = null;
            Assert.That(accurate.result.percent, Is.EqualTo(0.75).Within(2e-3), "a cleared computer leaves the result");

            // Reset (which fills the field in the editor) is not called by AddComponent in play mode, so the field stays empty.
            SplineProjector headless = NewObject("headless").AddComponent<SplineProjector>();
            Assert.That(DreamteckGeneratorFixture.Field<Transform>(headless, "_projectTarget") == null, Is.True, "no project target in play mode");
            Assert.Throws<NullReferenceException>(() => headless.spline = computer, "no project target field to read");
        }

        // CalculateProjection 0x4cbeb0 on a looping range (loopSamples, clipFrom >= clipTo; native 0x4cc00d..0x4cc041): reaching the
        // end reports the triggers up to the end (unclipped 1.0) and then continues from the start, so a trigger fires when it lies
        // in [last, end] or in [start, new] - not when it only lies between the two clipped percents. Expected firings are
        // evaluated from the Forward rule (position >= from and to >= position) over the unclipped ends.
        [Test]
        public void ReachingTheEndOfALoopingRangeContinuesFromTheStart()
        {
            SplineComputer computer = NewObject("square").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f)),
                new SplinePoint(new Vector3(0f, 0f, 10f))
            });
            computer.Close();
            computer.RebuildImmediate();
            double[] positions = { 0.05, 0.17, 0.3, 0.5, 0.7, 0.85, 0.95 };
            bool[] fired = new bool[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                int slot = i;
                computer.AddTrigger(0, positions[i], SplineTrigger.Type.Forward).AddListener(delegate (SplineUser user) { fired[slot] = true; });
            }

            // The range 0.8 .. 0.2 loops through the start of the closed square. The projection starts at spline percent 0.15, (6,0,0),
            // just before the range end and jumps to 0.9, (0,0,4), just after the range start. The unclipped ends of the two
            // parts are [0.15, clipTo 0.2] and [clipFrom 0.8, 0.9].
            Transform target = NewTarget(new Vector3(6f, 0f, 0f));
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            projector.projectTarget = target;
            projector.spline = computer;
            projector.clipFrom = 0.8;
            projector.clipTo = 0.2;
            projector.loopSamples = true;
            projector.useTriggers = true;
            projector.RebuildImmediate();
            projector.CalculateProjection();
            int ends = 0;
            projector.onEndReached += delegate { ends++; };
            double last = projector.result.percent;
            Array.Clear(fired, 0, fired.Length);

            target.position = new Vector3(0f, 0f, 4f);
            projector.CalculateProjection();
            double current = projector.result.percent;
            Assert.That(ends, Is.EqualTo(1), "the end event is raised once (last " + last + ", new " + current + ")");

            Func<double, double, bool[]> forward = (from, to) =>
            {
                bool[] crossed = new bool[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    crossed[i] = positions[i] >= from && to >= positions[i];
                }

                return crossed;
            };
            bool[] wrapped = new bool[positions.Length];
            bool[] up = forward(projector.UnclipPercent(last), projector.UnclipPercent(1.0));
            bool[] down = forward(projector.UnclipPercent(0.0), projector.UnclipPercent(current));
            for (int i = 0; i < wrapped.Length; i++)
            {
                wrapped[i] = up[i] || down[i];
            }

            bool[] single = forward(projector.UnclipPercent(last), projector.UnclipPercent(current));
            Assert.That(wrapped, Is.Not.EqualTo(single), "the scenario separates the wrap from a single check");
            Assert.That(fired, Is.EqualTo(wrapped));
        }

        // CalculateProjection 0x4cbeb0 reports the triggers and nodes between the previous and the new percent (the tracer's own
        // CheckTriggers / CheckNodes), invokes the queued triggers (with the projector) and then the nodes.
        [Test]
        public void CalculateProjectionReportsTheTriggersAndNodesItPassed()
        {
            SplineComputer computer = NewLine();
            SplineTrigger trigger = computer.AddTrigger(0, 0.5, SplineTrigger.Type.Double);
            List<SplineUser> crossed = new List<SplineUser>();
            trigger.AddListener(delegate (SplineUser user) { crossed.Add(user); });
            GameObject nodeOwner = NewObject("node");
            nodeOwner.transform.position = new Vector3(10f, 0f, 0f);
            Node node = nodeOwner.AddComponent<Node>();
            computer.ConnectNode(node, 1);

            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);
            projector.useTriggers = true;
            List<int> points = new List<int>();
            projector.onNode += delegate (List<SplineTracer.NodeConnection> passed)
            {
                foreach (SplineTracer.NodeConnection connection in passed)
                {
                    Assert.That(connection.node, Is.SameAs(node));
                    points.Add(connection.point);
                }
            };

            target.position = new Vector3(10f, 0f, 4f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.7).Within(1e-4));
            Assert.That(crossed.Count, Is.EqualTo(1));
            Assert.That(crossed[0], Is.SameAs(projector));
            Assert.That(points, Is.EqualTo(new[] { 1 }));

            target.position = new Vector3(10f, 0f, 6f);
            projector.CalculateProjection();
            Assert.That(crossed.Count, Is.EqualTo(1), "0.7 to 0.8 crosses nothing");
            Assert.That(points.Count, Is.EqualTo(1));
            target.position = new Vector3(4f, 0f, 2f);
            projector.CalculateProjection();
            Assert.That(crossed.Count, Is.EqualTo(2), "Double triggers fire backwards too");
        }
    }
}
