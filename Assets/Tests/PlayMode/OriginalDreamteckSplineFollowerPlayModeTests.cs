using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped (player) branches of Dreamteck.Splines.SplineTracer and SplineFollower, run where Application.isPlaying is
    // really true. The editor-branch counterparts are in OriginalDreamteckSplineFollowerTests. The line is the same Linear
    // polyline (0,0,0) -> (10,0,0) -> (10,0,10) as there. The tests are synchronous: no frame passes, so the engine's own
    // Update and LateUpdate do not run in between. TearDown destroys everything the test made.
    public sealed class OriginalDreamteckSplineFollowerPlayModeTests
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

        private SplineFollower NewFollower(SplineComputer computer = null)
        {
            SplineFollower follower = NewObject("follower").AddComponent<SplineFollower>();
            if (computer != null)
            {
                follower.spline = computer;
                follower.RebuildImmediate();
            }

            return follower;
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

        // LateRun 0x4bb6f0 follows only while follow is on. Follow 0x4bb720: Uniform moves by speed times the frame time, so a
        // zero speed re-places the follower on its sample; Time mode with a zero duration does nothing.
        // Follow reads Time.deltaTime, which is non-zero in play mode; both arms are independent of it (speed 0 and duration 0).
        [Test]
        public void LateRunFollowsOnlyWhileFollowIsOn()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineFollower follower = NewFollower(NewLine());
            follower.SetPercent(0.25);
            follower.followSpeed = 0f;
            follower.transform.position = new Vector3(99f, 99f, 99f);
            follower.follow = false;
            Invoke(follower, "LateRun");
            Near(follower.transform.position, new Vector3(99f, 99f, 99f), "follow off");

            follower.follow = true;
            Invoke(follower, "LateRun");
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "Uniform with zero speed re-applies the sample");

            follower.transform.position = new Vector3(99f, 99f, 99f);
            follower.followMode = SplineFollower.FollowMode.Time;
            follower.followDuration = 0f;
            Invoke(follower, "LateRun");
            Near(follower.transform.position, new Vector3(99f, 99f, 99f), "Time mode with zero duration moves nothing");
        }

        // CheckTriggers 0x4cae40 / AddTriggerToQueue 0x4cdc50 / InvokeTriggers 0x4cafb0: with useTriggers the triggers of the
        // follower's group crossed between the previous and the new percent are queued and invoked with the follower once
        // SetPercent asked for them. Nothing fires without useTriggers, without the request, or for a missing group.
        [Test]
        public void SetPercentInvokesTheCrossedTriggersOfItsGroup()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineComputer computer = NewLine();
            SplineTrigger trigger = computer.AddTrigger(0, 0.5, SplineTrigger.Type.Double);
            List<SplineUser> crossed = new List<SplineUser>();
            trigger.AddListener(delegate (SplineUser user) { crossed.Add(user); });
            SplineFollower follower = NewFollower(computer);
            follower.SetPercent(0.25);

            follower.SetPercent(0.75, true);
            Assert.That(crossed, Is.Empty, "useTriggers is off");
            follower.SetPercent(0.25);
            follower.useTriggers = true;
            follower.SetPercent(0.75);
            Assert.That(crossed, Is.Empty, "not requested");
            follower.SetPercent(0.25);
            follower.SetPercent(0.75, true);
            Assert.That(crossed.Count, Is.EqualTo(1));
            Assert.That(crossed[0], Is.SameAs(follower));
            follower.SetPercent(0.9, true);
            Assert.That(crossed.Count, Is.EqualTo(1), "0.75 to 0.9 crosses nothing");
            follower.SetPercent(0.25, true);
            Assert.That(crossed.Count, Is.EqualTo(2), "Double triggers fire backwards too");

            follower.triggerGroup = 3;
            follower.SetPercent(0.75, true);
            Assert.That(crossed.Count, Is.EqualTo(2), "a missing group index is ignored");
            follower.triggerGroup = -1;
            follower.SetPercent(0.25, true);
            Assert.That(crossed.Count, Is.EqualTo(2), "so is a negative one");

            follower.triggerGroup = 0;
            follower.SetPercent(0.25);
            follower.SetDistance(15f, true);
            Assert.That(crossed.Count, Is.EqualTo(3), "SetDistance reports from the result's own percent");
        }

        // CheckNodes 0x4cb030 / GetJunction 0x4cdb90 / InvokeNodes 0x4ca320: SetPercent with handleJunctions queues the nodes of

        // Without a subscriber CheckNodes queues nothing. A node queued before the subscriber left faults InvokeNodes.
        [Test]
        public void SetPercentReportsTheNodesItPassed()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineComputer computer = NewLine();
            GameObject nodeOwner = NewObject("node");
            nodeOwner.transform.position = new Vector3(10f, 0f, 0f);
            Node node = nodeOwner.AddComponent<Node>();
            computer.ConnectNode(node, 1);

            SplineFollower follower = NewFollower(computer);

            List<Node> nodes = new List<Node>();
            List<int> points = new List<int>();
            SplineTracer.JunctionHandler handler = delegate (List<SplineTracer.NodeConnection> passed)
            {
                foreach (SplineTracer.NodeConnection connection in passed)
                {
                    nodes.Add(connection.node);
                    points.Add(connection.point);
                }
            };
            follower.onNode += handler;
            follower.SetPercent(0.25);
            follower.SetPercent(0.75, false, false);
            Assert.That(nodes, Is.Empty, "junctions not requested");
            follower.SetPercent(0.25);
            follower.SetPercent(0.75, false, true);
            Assert.That(nodes.Count, Is.EqualTo(1));
            Assert.That(nodes[0], Is.SameAs(node));
            Assert.That(points, Is.EqualTo(new[] { 1 }));
            follower.SetPercent(0.75, false, true);
            Assert.That(nodes.Count, Is.EqualTo(1), "the queue was emptied and equal percents report nothing");
            follower.SetPercent(0.1);
            follower.SetPercent(0.4, false, true);
            Assert.That(nodes.Count, Is.EqualTo(1), "no point passed");

            follower.SetPercent(0.25);
            Invoke(follower, "CheckNodes", 0.25, 0.75);
            follower.onNode -= handler;
            TargetInvocationException fault = Assert.Throws<TargetInvocationException>(() => Invoke(follower, "InvokeNodes"));
            Assert.That(fault.InnerException, Is.TypeOf<NullReferenceException>(), "the queued node has nobody to tell");

            SplineFollower silent = NewFollower(computer);
            silent.SetPercent(0.25);
            silent.SetPercent(0.75, false, true);
            Assert.That(nodes.Count, Is.EqualTo(1));
        }

        // Move(double) Loop: CheckTriggers(start, 1) at 0x4bbe0a, then after the wrap CheckTriggers(0, end) at 0x4bbe70, and the queue
        // is fired once at the end (0x4bbf1a). From 0.9 by 0.25 the first range fires 0.95 and the second 0.1, in that order.
        [Test]
        public void LoopWrapReportsTheTriggersOfBothStretchesInOrder()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineComputer computer = NewLine();
            List<double> fired = new List<double>();
            SplineTrigger late = computer.AddTrigger(0, 0.95, SplineTrigger.Type.Forward);
            late.AddListener(delegate (SplineUser user) { fired.Add(late.position); });
            SplineTrigger early = computer.AddTrigger(0, 0.1, SplineTrigger.Type.Forward);
            early.AddListener(delegate (SplineUser user) { fired.Add(early.position); });
            SplineFollower follower = NewFollower(computer);
            follower.useTriggers = true;
            follower.wrapMode = SplineFollower.Wrap.Loop;
            follower.SetPercent(0.9);
            follower.Move(0.25);
            Assert.That(fired, Is.EqualTo(new[] { 0.95, 0.1 }));
        }

        // ApplyMotion 0x4ca5f0: onMotionApplied (0x4cad7e) is raised after the move was applied; the default arm of the physics

        // CheckNodes 0x4cb030: the Backward loop (0x4cb274) walks toPoint-1 down to fromPoint, and a range inside point 0 that
        // starts at the beginning reports point 0 (0x4cb12c..0x4cb165).
        [Test]
        public void CheckNodesCoversTheBackwardLoopAndTheBeginningEdge()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineComputer computer = NewLine();
            SplineFollower follower = NewFollower(computer);
            List<int> points = new List<int>();
            follower.onNode += delegate (List<SplineTracer.NodeConnection> passed)
            {
                foreach (SplineTracer.NodeConnection connection in passed)
                {
                    points.Add(connection.point);
                }
            };

            GameObject middleOwner = NewObject("middle");
            middleOwner.transform.position = new Vector3(10f, 0f, 0f);
            Node middle = middleOwner.AddComponent<Node>();
            computer.ConnectNode(middle, 1);
            follower.direction = Spline.Direction.Backward;
            follower.SetPercent(0.75);
            follower.SetPercent(0.25, false, true);
            Assert.That(points, Is.EqualTo(new[] { 1 }), "indices 1 (0.25) and 2 (0.75) leave point 1");

            computer.DisconnectNode(1);
            Node first = NewObject("first").AddComponent<Node>();
            computer.ConnectNode(first, 0);
            points.Clear();
            follower.direction = Spline.Direction.Forward;
            follower.SetPercent(0.0);
            follower.SetPercent(0.25, false, true);
            Assert.That(points, Is.EqualTo(new[] { 0 }), "both indices are point 0 and the range starts at 0");
        }
    }
}
