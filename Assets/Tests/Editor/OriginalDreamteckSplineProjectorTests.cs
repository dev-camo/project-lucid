using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineProjector, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long with cached samples at percents 0, 0.5 and 1 (positions p0, p1, p2). A point
    // (x,0,z) with x in [0,10] projects onto the first segment at percent x / 20; (10,0,z) with z in [0,10] onto the second at
    // 0.5 + z / 20. The projection arithmetic in the Cached mode is the sample-chord walk of SampleCollection.Project; the
    // expected percents below are the geometric ones, which that walk reaches for these points. The projectors are plain components
    // on owned game objects and TearDown destroys everything the test made.
    public sealed class OriginalDreamteckSplineProjectorTests
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

        private static bool IsMissing(object value)
        {
            return value == null || (value is UnityEngine.Object unityObject && unityObject == null);
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

        private static FieldInfo Field(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            Assert.Fail("no field " + name);
            return null;
        }

        private static T Read<T>(object target, string name)
        {
            return (T)Field(target, name).GetValue(target);
        }

        private static void Write(object target, string name, object value)
        {
            Field(target, name).SetValue(target, value);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == arguments.Length);
                if (method != null)
                {
                    return method.Invoke(target, arguments);
                }
            }

            Assert.Fail("no method " + name);
            return null;
        }

        // Settling the queued rebuild lets a test see whether a setter queues a new one.
        private static bool RebuildQueued(SplineUser user)
        {
            return Read<bool>(user, "rebuild");
        }

        // Constructor 0x4cc550: Cached mode, auto project on, 4 subdivisions, zero offsets and last position. The project target
        // getter 0x4cb500 falls back to the own transform; the target object getter 0x4cb610 has no fallback.
        [Test]
        public void FreshProjectorKeepsTheShippedDefaults()
        {
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            Assert.That(projector.mode, Is.EqualTo(SplineProjector.Mode.Cached));
            Assert.That(projector.autoProject, Is.True);
            Assert.That(projector.subdivide, Is.EqualTo(4));
            Assert.That(projector.projectTarget, Is.SameAs(projector.transform), "the own transform stands in");
            // The engine calls Reset when the editor adds the component, and Reset 0x4cba80 fills the field with the own transform.
            Assert.That(Read<Transform>(projector, "_projectTarget"), Is.SameAs(projector.transform), "Reset filled the field");
            Write(projector, "_projectTarget", null);
            Assert.That(IsMissing(Read<Transform>(projector, "_projectTarget")), Is.True, "the field can be empty");
            Assert.That(projector.projectTarget, Is.SameAs(projector.transform), "and the getter falls back to the own transform");
            Assert.That(IsMissing(projector.targetObject), Is.True);
            Assert.That(projector._offset, Is.EqualTo(Vector2.zero));
            Assert.That(projector._rotationOffset, Is.EqualTo(Vector3.zero));
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(Vector3.zero));
            Assert.That(projector.physicsMode, Is.EqualTo(SplineTracer.PhysicsMode.Transform));
            Assert.That(projector is SplineTracer, Is.True);
        }

        // Reset 0x4cba80 only points the project target at the own transform (it does not call the base).
        [Test]
        public void ResetTargetsTheOwnTransform()
        {
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            Transform other = NewTarget(Vector3.zero);
            projector.projectTarget = other;
            Assert.That(projector.projectTarget, Is.SameAs(other));
            Invoke(projector, "Reset");
            Assert.That(Read<Transform>(projector, "_projectTarget"), Is.SameAs(projector.transform));
            Assert.That(projector.projectTarget, Is.SameAs(projector.transform));
        }

        // The setters 0x4cb440 (mode), 0x4cb480 (autoProject: only switching on), 0x4cb4c0 (subdivide: only in the Accurate mode),
        // 0x4cb570 (projectTarget) and 0x4cb6e0 (targetObject) queue a rebuild for a changed value; an equal value does nothing.
        // The queue is the player text of Rebuild; the editor compile rebuilds at once while the cached play state is false, so the
        // flag is set to what play mode caches (DreamteckPlayState).
        [Test]
        public void SettersQueueARebuildOnlyWhereTheShippedOnesDo()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = DreamteckPlayState.Cache(NewProjector(computer, target));
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

        // get_targetObject 0x4cb610: a target kept in the legacy applyTarget field moves to targetObject (as its game object) the
        // first time it is read, and the legacy field is cleared. GetTransform / GetRigidbody / GetRigidbody2D 0x4cbac0/0x4cbb40/
        // 0x4cbbe0 answer nothing without a target object and read its components otherwise.
        [Test]
        public void TargetObjectTakesOverTheLegacyApplyTarget()
        {
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            Assert.That(IsMissing(Invoke(projector, "GetTransform")), Is.True, "no target object");
            Assert.That(IsMissing(Invoke(projector, "GetRigidbody")), Is.True);
            Assert.That(IsMissing(Invoke(projector, "GetRigidbody2D")), Is.True);

            Transform legacy = NewTarget(Vector3.zero);
            Write(projector, "applyTarget", legacy);
            Assert.That(projector.targetObject, Is.SameAs(legacy.gameObject));
            Assert.That(IsMissing(Read<Transform>(projector, "applyTarget")), Is.True, "the legacy field is cleared");
            Assert.That(projector.targetObject, Is.SameAs(legacy.gameObject));
            Assert.That(Invoke(projector, "GetTransform"), Is.SameAs(legacy));

            Rigidbody body = legacy.gameObject.AddComponent<Rigidbody>();
            Assert.That(Invoke(projector, "GetRigidbody"), Is.SameAs(body));
            Assert.That(IsMissing(Invoke(projector, "GetRigidbody2D")), Is.True);

            GameObject explicitTarget = NewObject("explicit");
            Write(projector, "applyTarget", NewTarget(Vector3.zero));
            projector.targetObject = explicitTarget;
            Assert.That(projector.targetObject, Is.SameAs(explicitTarget), "a set target object wins over the legacy field");
            Assert.That(Invoke(projector, "GetTransform"), Is.SameAs(explicitTarget.transform));

            projector.physicsMode = SplineTracer.PhysicsMode.Rigidbody;
            Rigidbody explicitBody = explicitTarget.AddComponent<Rigidbody>();
            projector.targetObject = null;
            projector.targetObject = explicitTarget;
            Assert.That(Read<Rigidbody>(projector, "targetRigidbody"), Is.SameAs(explicitBody), "the physics mode's component is fetched");
        }

        // OnSplineChanged 0x4cc2a0: a new computer is projected onto at once from the raw project target field (a projector
        // without one faults); nothing happens for a cleared computer. Set_spline only queues the rebuild in the player build (the
        // editor compile rebuilds at once), so the projectors have the cached play state set (DreamteckPlayState).
        [Test]
        public void ANewComputerIsProjectedOntoAtOnce()
        {
            SplineComputer computer = NewLine();
            SplineProjector projector = DreamteckPlayState.Cache(NewObject("projector").AddComponent<SplineProjector>());
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

            SplineProjector accurate = DreamteckPlayState.Cache(NewObject("accurate").AddComponent<SplineProjector>());
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

            // Reset filled the field, so the read of a project target field that is empty needs it cleared.
            SplineProjector headless = DreamteckPlayState.Cache(NewObject("headless").AddComponent<SplineProjector>());
            Write(headless, "_projectTarget", null);
            Assert.Throws<NullReferenceException>(() => headless.spline = computer, "no project target field to read");
        }

        // OnSplineChanged 0x4cc2a0 projects over [clipFrom, clipTo] (0x4cc327..0x4cc3a6) and then clips the percent. The line is
        // 20 units long; clipFrom 0.5 starts the cached projection at the second chord, but SampleCollection.Project 0x4d1190
        // still looks at the chord before the closest sample (0x4d15ba), so (4,0,2) is reported on the first chord at (4,0,0).
        // The clipped percent of (12,0,5) is (0.75 - 0.5) / 0.5, not the raw 0.75. The clip only works once the user has
        // fetched samples, hence the first RebuildImmediate.
        [Test]
        public void ANewComputerIsProjectedOverTheClipRangeAndClipped()
        {
            SplineProjector start = NewObject("start").AddComponent<SplineProjector>();
            start.clipFrom = 0.5;
            start.projectTarget = NewTarget(new Vector3(4f, 0f, 2f));
            start.spline = NewLine();
            start.RebuildImmediate();
            start.spline = NewLine();
            Near(start.result.position, new Vector3(4f, 0f, 0f), "the chord before the closest sample still counts");
            Assert.That(start.result.percent, Is.EqualTo(0.0).Within(1e-4), "clipped below the range start");

            SplineProjector inside = NewObject("inside").AddComponent<SplineProjector>();
            inside.clipFrom = 0.5;
            inside.projectTarget = NewTarget(new Vector3(12f, 0f, 5f));
            inside.spline = NewLine();
            inside.RebuildImmediate();
            inside.spline = NewLine();
            Near(inside.result.position, new Vector3(10f, 0f, 5f), "projected position");
            Assert.That(inside.result.percent, Is.EqualTo(0.5).Within(1e-3), "clipped percent");
        }

        // PostBuild 0x4cc290 and CalculateProjection 0x4cbeb0: a rebuild projects the project target, the result takes the
        // projected sample and the target object (when set) is moved there (offsets are the motion module's, zero here).
        [Test]
        public void CalculateProjectionPlacesTheTargetObjectOnTheProjection()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);
            Assert.That(projector.result.percent, Is.EqualTo(0.2).Within(1e-4));
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(4f, 0f, 2f)));

            GameObject mover = NewObject("mover");
            mover.transform.position = new Vector3(9f, 9f, 9f);
            projector.targetObject = mover;
            target.position = new Vector3(6f, 0f, 1f);
            projector.RebuildImmediate();
            Assert.That(projector.result.percent, Is.EqualTo(0.3).Within(1e-4));
            Near(mover.transform.position, new Vector3(6f, 0f, 0f), "the mover sits on the projection");
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(6f, 0f, 1f)));

            target.position = new Vector3(12f, 0f, 5f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.75).Within(1e-4), "second segment");
            Near(mover.transform.position, new Vector3(10f, 0f, 5f), "second segment position");
        }

        // The Accurate branch of Project() 0x4cc3f0 projects onto the curve itself (Calculate) with the subdivision.
        [Test]
        public void AccurateModeProjectsOntoTheCurve()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);
            projector.mode = SplineProjector.Mode.Accurate;
            projector.subdivide = 6;
            target.position = new Vector3(12f, 0f, 5f);
            projector.RebuildImmediate();
            Assert.That(projector.result.percent, Is.EqualTo(0.75).Within(2e-3));
            Near(projector.result.position, new Vector3(10f, 0f, 5f), "projected position");
            target.position = new Vector3(4f, 0f, 2f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.2).Within(2e-3));
        }

        // CalculateProjection 0x4cbeb0: reaching the clip start (percent at or below clipFrom) raises onBeginningReached, reaching
        // the end raises onEndReached; each only when the percent moved by more than float rounding, so staying put raises
        // nothing. The beginning is tested first, so with a subscriber for it the end is not looked at.
        [Test]
        public void ReachingTheEndsRaisesTheReachEventsOnce()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);
            int beginnings = 0;
            int ends = 0;
            projector.onBeginningReached += delegate { beginnings++; };
            projector.onEndReached += delegate { ends++; };

            target.position = new Vector3(-3f, 0f, 0f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(beginnings, Is.EqualTo(1));
            Assert.That(ends, Is.EqualTo(0));
            target.position = new Vector3(-4f, 0f, 0f);
            projector.CalculateProjection();
            Assert.That(beginnings, Is.EqualTo(1), "still at the beginning: the percent did not move");

            target.position = new Vector3(10f, 0f, 12f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(ends, Is.EqualTo(1));
            Assert.That(beginnings, Is.EqualTo(1));
            target.position = new Vector3(10f, 0f, 15f);
            projector.CalculateProjection();
            Assert.That(ends, Is.EqualTo(1), "still at the end");

            target.position = new Vector3(4f, 0f, 2f);
            projector.CalculateProjection();
            Assert.That(beginnings + ends, Is.EqualTo(2), "leaving an end raises nothing");

            SplineProjector silent = NewProjector(computer, target);
            target.position = new Vector3(-3f, 0f, 0f);
            Assert.DoesNotThrow(() => silent.CalculateProjection(), "no subscribers");
        }

        // CalculateProjection 0x4cbeb0 on a looping range (loopSamples, clipFrom >= clipTo; native 0x4cc00d..0x4cc041): reaching the
        // end reports the triggers up to the end (unclipped 1.0) and then continues from the start, so a trigger fires when it lies
        // in [last, end] or in [start, new] - not when it only lies between the two clipped percents. Expected firings are
        // evaluated from the Forward rule (position >= from and to >= position) over the unclipped ends. CheckTriggers and
        // SplineTrigger.Invoke return while Application.isPlaying is false under UNITY_EDITOR (read directly), and the player text
        // after that block cannot run in EditMode; the test keeps the end event and the scenario check and asserts that no trigger
        // is reported in edit mode.
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
            Assert.That(fired, Is.EqualTo(new bool[positions.Length]), "no trigger is reported while not playing");
        }

        // CalculateProjection 0x4cbeb0 reports the triggers and nodes between the previous and the new percent (the tracer's own
        // CheckTriggers / CheckNodes), invokes the queued triggers (with the projector) and then the nodes. Those tracer methods return
        // while Application.isPlaying is false under UNITY_EDITOR (read directly), so in EditMode the projection is computed and
        // neither the trigger listener nor onNode is called (the player text after the #if block cannot run in EditMode).
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
            Assert.That(crossed, Is.Empty, "the trigger is not reported while not playing");
            Assert.That(points, Is.Empty, "the node is not reported while not playing");

            target.position = new Vector3(10f, 0f, 6f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.8).Within(1e-4));
            Assert.That(crossed, Is.Empty);
            target.position = new Vector3(4f, 0f, 2f);
            projector.CalculateProjection();
            Assert.That(projector.result.percent, Is.EqualTo(0.2).Within(1e-4));
            Assert.That(crossed, Is.Empty, "nor backwards");
            Assert.That(points, Is.Empty);
        }

        // LateRun 0x4cbc80: with autoProject the projection is redone only once the project target has moved (the compare is
        // Unity's Vector3 equality, whose folded epsilon is 0x2edbe6fe = fl(1e-5f * 1e-5f) at 0x29a6e8c); without autoProject
        // nothing happens.
        [Test]
        public void LateRunProjectsOnlyAfterTheTargetMoved()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(new Vector3(4f, 0f, 2f));
            SplineProjector projector = NewProjector(computer, target);

            target.position = new Vector3(6f, 0f, 1f);
            Invoke(projector, "LateRun");
            Assert.That(projector.result.percent, Is.EqualTo(0.3).Within(1e-4));
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(6f, 0f, 1f)));

            target.position = new Vector3(6f, 0f, 1.000001f);
            Invoke(projector, "LateRun");
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(6f, 0f, 1f)), "moved by less than 1e-5: not projected");

            target.position = new Vector3(8f, 0f, 1f);
            Invoke(projector, "LateRun");
            Assert.That(projector.result.percent, Is.EqualTo(0.4).Within(1e-4));

            projector.autoProject = false;
            target.position = new Vector3(2f, 0f, 1f);
            Invoke(projector, "LateRun");
            Assert.That(projector.result.percent, Is.EqualTo(0.4).Within(1e-4), "autoProject is off");
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(8f, 0f, 1f)));
        }

        // The native epsilon is 9.99999944E-11f, not 1E-10f: a move of exactly 1e-5f (0x3727c5ac, whose square rounds to
        // 0x2edbe6fe) is projected, where a 1E-10f compare would skip it.
        [Test]
        public void LateRunProjectsAMoveOfExactlyTheNativeEpsilon()
        {
            SplineComputer computer = NewLine();
            Transform target = NewTarget(Vector3.zero);
            SplineProjector projector = NewProjector(computer, target);
            Write(projector, "lastPosition", Vector3.zero);

            target.position = new Vector3(1E-5f, 0f, 0f);
            Invoke(projector, "LateRun");
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(1E-5f, 0f, 0f)), "projected, not skipped");
        }

        // LateRun 0x4cbc80 reads the project target through its getter (the own transform stands in) but CalculateProjection
        // 0x4cbeb0 returns at once for an empty field: such a projector only remembers where it was.
        [Test]
        public void LateRunWithoutAProjectTargetOnlyRemembersThePosition()
        {
            SplineProjector projector = NewObject("projector").AddComponent<SplineProjector>();
            projector.transform.position = new Vector3(3f, 0f, 1f);
            Invoke(projector, "LateRun");
            Assert.That(Read<Vector3>(projector, "lastPosition"), Is.EqualTo(new Vector3(3f, 0f, 1f)));
            Assert.That(projector.result.percent, Is.EqualTo(0.0));
            Assert.DoesNotThrow(() => projector.CalculateProjection());
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineProjector).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_mode", "_autoProject", "_subdivide", "_projectTarget", "applyTarget", "_targetObject", "_offset", "_rotationOffset",
                "onEndReached", "onBeginningReached", "lastPosition"
            }));
            Assert.That(typeof(SplineProjector).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "Mode" }));
            Assert.That(Enum.GetNames(typeof(SplineProjector.Mode)), Is.EqualTo(new[] { "Accurate", "Cached" }));
            Assert.That(typeof(SplineProjector).GetCustomAttributes(typeof(ExecuteInEditMode), false).Length, Is.EqualTo(1));
            AddComponentMenu menu = (AddComponentMenu)typeof(SplineProjector).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Spline Projector"));
            foreach (string name in new[] { "_mode", "_autoProject", "_subdivide", "_projectTarget", "applyTarget", "_targetObject", "lastPosition" })
            {
                FieldInfo field = typeof(SplineProjector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            foreach (string name in new[] { "_offset", "_rotationOffset" })
            {
                FieldInfo field = typeof(SplineProjector).GetField(name, BindingFlags.Instance | BindingFlags.Public);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            UnityEngine.RangeAttribute range = (UnityEngine.RangeAttribute)typeof(SplineProjector).GetField("_subdivide", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Single();
            Assert.That(range.min, Is.EqualTo(3f));
            Assert.That(range.max, Is.EqualTo(8f));
            Assert.That(typeof(SplineProjector).GetEvent("onEndReached").EventHandlerType, Is.EqualTo(typeof(SplineReachHandler)));
            Assert.That(typeof(SplineProjector).GetEvent("onBeginningReached").EventHandlerType, Is.EqualTo(typeof(SplineReachHandler)));
        }
    }
}
