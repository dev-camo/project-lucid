using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplinePositioner, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long, so percent p is (20p,0,0) up to 0.5 and (10,0,20p-10) after, and the
    // point at 0.5 is the corner. Expected values are hand arithmetic from the native bodies cited per test. The positioners are
    // plain components on owned game objects (Transform physics mode unless a test says otherwise) and TearDown destroys
    // everything the test made.
    public sealed class OriginalDreamteckSplinePositionerTests
    {
        private const float Tolerance = 1e-3f;

        private const string SelfReferenceMessage = "You should not be assigning a self-reference to the followTarget field.";

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

        private SplinePositioner NewPositioner(SplineComputer computer = null)
        {
            SplinePositioner positioner = NewObject("positioner").AddComponent<SplinePositioner>();
            if (computer != null)
            {
                positioner.spline = computer;
                positioner.RebuildImmediate();
            }

            return positioner;
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

        private static int Subscriptions(SplineTracer tracer, SplineUser subscriber)
        {
            EmptySplineHandler handler = Read<EmptySplineHandler>(tracer, "onMotionApplied");
            return handler == null ? 0 : handler.GetInvocationList().Count(d => ReferenceEquals(d.Target, subscriber));
        }

        // Constructor 0x4cb350 sets the follow direction to Backward (-1) before the tracer constructor; everything else keeps
        // its default. The getters 0x4c8f80 (target object: the owner stands in) and 0x4c98b0 (position: the result percent).
        [Test]
        public void FreshPositionerKeepsTheShippedDefaults()
        {
            SplinePositioner positioner = NewPositioner();
            Assert.That(positioner.mode, Is.EqualTo(SplinePositioner.Mode.Percent));
            Assert.That(positioner.followTargetDirection, Is.EqualTo(Spline.Direction.Backward));
            Assert.That((int)positioner.followTargetDirection, Is.EqualTo(-1));
            Assert.That(positioner.followTarget == null, Is.True);
            Assert.That(positioner.followTargetDistance, Is.EqualTo(0f));
            Assert.That(positioner.followLoop, Is.False);
            Assert.That(positioner.position, Is.EqualTo(0.0));
            Assert.That(positioner.targetObject, Is.SameAs(positioner.gameObject), "the owner stands in for a missing target");
            Assert.That(positioner.physicsMode, Is.EqualTo(SplineTracer.PhysicsMode.Transform));
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0f));
            Assert.That(Read<float>(positioner, "_lastPosition"), Is.EqualTo(0f));
            Assert.That(positioner is SplineTracer, Is.True);
            Assert.That(Invoke(positioner, "GetTransform"), Is.SameAs(positioner.transform));
        }

        // SetPercent 0x4ca1a0: the base moves the target, then the position field takes (float)percent as given (not the clamped
        // result). With handleJunctions it ends in InvokeNodes, which is harmless with nothing queued.
        [Test]
        public void SetPercentMovesTheTargetAndStoresThePosition()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.SetPercent(0.25);
            Near(positioner.transform.position, new Vector3(5f, 0f, 0f), "quarter");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.25f));
            positioner.SetPercent(0.75, false, false);
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "three quarters");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.75f));
            Assert.That(() => positioner.SetPercent(0.5, true, true), Throws.Nothing);
            Near(positioner.transform.position, new Vector3(10f, 0f, 0f), "the corner");
            positioner.SetPercent(1.5);
            Near(positioner.transform.position, new Vector3(10f, 0f, 10f), "the sample is clamped to the end");
            Assert.That(positioner.position, Is.EqualTo(1.0), "the result percent is clamped");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(1.5f), "the field keeps the argument");
        }

        // set_position 0x4c98c0: the float field is compared as a double, so a value a float cannot hold (0.3) counts as changed
        // every time, while 0.5 does not. The percent mode passes the double itself on (0x4c9914: xmm0 untouched), so the result
        // percent is exactly 0.3 rather than the float 0.3 widened.
        [Test]
        public void PositionSetterComparesTheFloatFieldAsADouble()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.position = 0.5;
            Near(positioner.transform.position, new Vector3(10f, 0f, 0f), "0.5");
            positioner.transform.position = new Vector3(1f, 1f, 1f);
            positioner.position = 0.5;
            Near(positioner.transform.position, new Vector3(1f, 1f, 1f), "an equal position does nothing");

            positioner.position = 0.3;
            Near(positioner.transform.position, new Vector3(6f, 0f, 0f), "0.3 of 20");
            Assert.That(positioner.position, Is.EqualTo(0.3), "the double goes through");
            Assert.That(positioner.position, Is.Not.EqualTo((double)0.3f));
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.3f));
            positioner.transform.position = new Vector3(1f, 1f, 1f);
            positioner.position = 0.3;
            Near(positioner.transform.position, new Vector3(6f, 0f, 0f), "(double)0.3f differs from 0.3, so it moves again");
        }

        // set_mode 0x4c9940 rebuilds only on a change. In the distance mode set_position 0x4c98c0 goes through SetDistance with the
        // float field (0x4c9906) and SetDistance 0x4ca3c0 leaves the distance in the field.
        [Test]
        public void DistanceModeMovesBySetDistanceAndStoresTheDistance()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.mode = SplinePositioner.Mode.Distance;
            Assert.That(positioner.mode, Is.EqualTo(SplinePositioner.Mode.Distance));
            positioner.position = 5.0;
            Near(positioner.transform.position, new Vector3(5f, 0f, 0f), "5 units");
            Assert.That(positioner.position, Is.EqualTo(0.25).Within(1e-4), "position still reads the percent");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(5f));
            positioner.position = 15.0;
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "15 units");
            Assert.That(positioner.position, Is.EqualTo(0.75).Within(1e-4));
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(15f));
        }

        // SetDistance 0x4ca3c0 in the percent mode (0x4ca53d): the field takes the travelled percent as a float.
        [Test]
        public void SetDistanceInPercentModeStoresTheTravelledPercent()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.SetDistance(5f);
            Near(positioner.transform.position, new Vector3(5f, 0f, 0f), "5 units");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.25f).Within(1e-4f));
            positioner.SetDistance(100f);
            Near(positioner.transform.position, new Vector3(10f, 0f, 10f), "the end of the spline");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(positioner.position, Is.EqualTo(1.0));
        }

        // PostBuild 0x4ca130 applies the stored position again with triggers (SetDistance in the distance mode, SetPercent
        // otherwise); it does not call the base.
        [Test]
        public void PostBuildAppliesTheStoredPositionAgain()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.position = 0.25;
            positioner.transform.position = new Vector3(100f, 0f, 0f);
            positioner.RebuildImmediate();
            Near(positioner.transform.position, new Vector3(5f, 0f, 0f), "percent mode");

            positioner.mode = SplinePositioner.Mode.Distance;
            positioner.position = 15.0;
            positioner.transform.position = new Vector3(100f, 0f, 0f);
            positioner.RebuildImmediate();
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "distance mode");
        }

        // set_followTarget 0x4c91c0 and OnFollowTargetMotionApplied 0x4c95f0: the positioner sits followTargetDistance away from
        // the followed tracer's percent, in followTargetDirection (Backward by default), and follows when that tracer moves.
        [Test]
        public void FollowTargetPlacesTheDistanceFromTheFollowedTracer()
        {
            SplineComputer computer = NewLine();
            SplinePositioner leader = NewPositioner(computer);
            SplinePositioner follower = NewPositioner(computer);
            follower.followTargetDistance = 5f;
            follower.followTarget = leader;
            Assert.That(follower.followTarget, Is.SameAs(leader));
            Assert.That(Subscriptions(leader, follower), Is.EqualTo(1));
            Near(follower.transform.position, Vector3.zero, "backward from the start stays at the start");

            leader.SetPercent(0.5);
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "5 units behind the corner");
            Assert.That(follower.position, Is.EqualTo(0.25).Within(1e-4));

            follower.followTargetDirection = Spline.Direction.Forward;
            Assert.That(follower.followTargetDirection, Is.EqualTo(Spline.Direction.Forward));
            Near(follower.transform.position, new Vector3(10f, 0f, 5f), "5 units ahead of the corner");
            follower.followTargetDistance = 2f;
            Near(follower.transform.position, new Vector3(10f, 0f, 2f), "2 units ahead");
            leader.SetPercent(0.25);
            Near(follower.transform.position, new Vector3(7f, 0f, 0f), "follows the leader");

            SplinePositioner replacement = NewPositioner(computer);
            follower.followTarget = replacement;
            Assert.That(Subscriptions(leader, follower), Is.EqualTo(0), "the old leader is released");
            Assert.That(Subscriptions(replacement, follower), Is.EqualTo(1));
            follower.followTarget = null;
            Assert.That(follower.followTarget == null, Is.True);
            Assert.That(Subscriptions(replacement, follower), Is.EqualTo(0));
        }

        // The distance, loop and direction setters 0x4c96d0/0x4c9780/0x4c9820 apply the follow again only with a target, and only
        // for a changed value (0x4c9750 and following op_Inequality checks).
        [Test]
        public void FollowSettersReapplyTheFollowOnlyWithATarget()
        {
            SplineComputer computer = NewLine();
            SplinePositioner positioner = NewPositioner(computer);
            positioner.transform.position = new Vector3(3f, 3f, 3f);
            Assert.That(() => { positioner.followTargetDistance = 4f; }, Throws.Nothing);
            Assert.That(() => { positioner.followLoop = true; }, Throws.Nothing);
            Assert.That(() => { positioner.followTargetDirection = Spline.Direction.Forward; }, Throws.Nothing);
            Assert.That(positioner.followTargetDistance, Is.EqualTo(4f));
            Assert.That(positioner.followLoop, Is.True);
            Assert.That(positioner.followTargetDirection, Is.EqualTo(Spline.Direction.Forward));
            Near(positioner.transform.position, new Vector3(3f, 3f, 3f), "nothing was followed");

            SplinePositioner leader = NewPositioner(computer);
            leader.SetPercent(0.5);
            positioner.followTarget = leader;
            Near(positioner.transform.position, new Vector3(10f, 0f, 4f), "4 units ahead of the corner");
            positioner.transform.position = new Vector3(3f, 3f, 3f);
            positioner.followLoop = true;
            Near(positioner.transform.position, new Vector3(3f, 3f, 3f), "an unchanged value does nothing");
            positioner.followLoop = false;
            Near(positioner.transform.position, new Vector3(10f, 0f, 4f), "a changed loop flag follows again");
        }

        // OnFollowTargetMotionApplied 0x4c95f0 with followLoop: a distance that ran off one end (the rest above 1e-6) continues from
        // the other end when the first result is at most 1e-6 (travel from 1) or at least 0.999999 (travel from 0).
        [Test]
        public void FollowLoopContinuesFromTheOtherEnd()
        {
            SplineComputer computer = NewLine();
            SplinePositioner leader = NewPositioner(computer);
            SplinePositioner follower = NewPositioner(computer);
            follower.followTargetDistance = 5f;
            follower.followTargetDirection = Spline.Direction.Forward;
            follower.followTarget = leader;

            leader.SetPercent(0.95);
            Near(follower.transform.position, new Vector3(10f, 0f, 10f), "without the loop the follower stops at the end");

            follower.followLoop = true;
            Near(follower.transform.position, new Vector3(4f, 0f, 0f), "19 + 5 = 24 units wraps to 4");

            follower.followTargetDirection = Spline.Direction.Backward;
            leader.SetPercent(0.05);
            Near(follower.transform.position, new Vector3(10f, 0f, 6f), "1 - 5 = -4 units wraps to 16");

            follower.followLoop = false;
            Near(follower.transform.position, Vector3.zero, "without the loop it stops at the start");

            follower.followLoop = true;
            leader.SetPercent(0.5);
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "inside the spline nothing wraps");
        }

        // set_followTarget 0x4c91c0: the old target is released before the self-reference is refused, so a refused assignment leaves
        // the positioner holding its old target, deaf to it.
        [Test]
        public void SelfReferenceIsRefusedAfterTheOldTargetWasReleased()
        {
            SplineComputer computer = NewLine();
            SplinePositioner leader = NewPositioner(computer);
            SplinePositioner follower = NewPositioner(computer);
            follower.followTargetDistance = 5f;
            follower.followTargetDirection = Spline.Direction.Forward;
            follower.followTarget = leader;
            Assert.That(Subscriptions(leader, follower), Is.EqualTo(1));

            LogAssert.Expect(LogType.Log, SelfReferenceMessage);
            follower.followTarget = follower;
            Assert.That(follower.followTarget, Is.SameAs(leader), "the field was not replaced");
            Assert.That(Subscriptions(leader, follower), Is.EqualTo(0), "but the subscription is gone");
            Vector3 before = follower.transform.position;
            leader.SetPercent(0.5);
            Near(follower.transform.position, before, "the leader no longer moves the follower");

            follower.followTarget = null;
            Assert.That(follower.followTarget == null, Is.True);
        }

        // Awake 0x4c9a70 subscribes to a serialized follow target and OnDestroy 0x4c9c50 releases it. A component added to an
        // inactive object only wakes when the object is activated.
        [Test]
        public void AwakeAndOnDestroyTieTheSerializedFollowTarget()
        {
            SplineComputer computer = NewLine();
            SplinePositioner leader = NewPositioner(computer);
            GameObject host = NewObject("late");
            host.SetActive(false);
            SplinePositioner late = host.AddComponent<SplinePositioner>();
            Write(late, "_followTarget", leader);
            Assert.That(Subscriptions(leader, late), Is.EqualTo(0), "not awake yet");
            host.SetActive(true);
            Assert.That(Subscriptions(leader, late), Is.EqualTo(1), "Awake subscribed");

            UnityEngine.Object.DestroyImmediate(late);
            Assert.That(Subscriptions(leader, late), Is.EqualTo(0), "OnDestroy released it");
        }

        // SetPercent 0x4ca1a0 and SetDistance 0x4ca3c0 report the crossed triggers when asked (SetDistance from the result's own
        // percent, with its own copy of the queue loop) and the passed nodes when junctions are asked (SetDistance reports them
        // after storing the position, but still once). Editor branch: CheckTriggers, InvokeTriggers and CheckNodes (SplineTracer)
        // return while Application.isPlaying is false, read directly, so the motion and the stored position happen but neither the
        // trigger listeners nor onNode are called, with or without the request. The reports of the shipped branch are asserted in
        // OriginalDreamteckSplinePositionerPlayModeTests.SetPercentAndSetDistanceReportTriggersAndNodes.
        [Test]
        public void SetPercentAndSetDistanceReportNothingInEditMode()
        {
            SplineComputer computer = NewLine();
            SplineTrigger trigger = computer.AddTrigger(0, 0.5, SplineTrigger.Type.Double);
            List<SplineUser> crossed = new List<SplineUser>();
            trigger.AddListener(delegate (SplineUser user) { crossed.Add(user); });
            GameObject nodeOwner = NewObject("node");
            nodeOwner.transform.position = new Vector3(10f, 0f, 0f);
            Node node = nodeOwner.AddComponent<Node>();
            computer.ConnectNode(node, 1);

            SplinePositioner positioner = NewPositioner(computer);
            positioner.useTriggers = true;
            List<int> points = new List<int>();
            positioner.onNode += delegate (List<SplineTracer.NodeConnection> passed)
            {
                foreach (SplineTracer.NodeConnection connection in passed)
                {
                    Assert.That(connection.node, Is.SameAs(node));
                    points.Add(connection.point);
                }
            };

            positioner.SetPercent(0.25);
            positioner.SetPercent(0.75);
            Assert.That(crossed, Is.Empty, "triggers not requested");
            Assert.That(points, Is.Empty, "junctions not requested");
            positioner.SetPercent(0.25);
            positioner.SetPercent(0.75, true, true);
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "SetPercent moved the target");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.75f));
            Assert.That(crossed, Is.Empty, "the trigger is not reported while not playing");
            Assert.That(points, Is.Empty, "the node is not reported while not playing");

            positioner.SetPercent(0.1);
            positioner.SetDistance(15f);
            Assert.That(crossed, Is.Empty);
            Assert.That(points, Is.Empty);
            positioner.SetPercent(0.1);
            positioner.SetDistance(15f, true, true);
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "SetDistance moved the target");
            Assert.That(crossed, Is.Empty, "SetDistance reports no trigger while not playing");
            Assert.That(points, Is.Empty, "and no node");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplinePositioner).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_targetObject", "_followTarget", "_followTargetDistance", "_followLoop", "_followTargetDirection", "_position", "_mode",
                "_lastPosition"
            }));
            Assert.That(typeof(SplinePositioner).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "Mode" }));
            Assert.That(Enum.GetNames(typeof(SplinePositioner.Mode)), Is.EqualTo(new[] { "Percent", "Distance" }));
            Assert.That((int)SplinePositioner.Mode.Distance, Is.EqualTo(1));
            Assert.That(typeof(SplinePositioner).GetCustomAttributes(typeof(ExecuteInEditMode), false).Length, Is.EqualTo(1));
            AddComponentMenu menu = (AddComponentMenu)typeof(SplinePositioner).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Spline Positioner"));
            foreach (string name in new[] { "_targetObject", "_followTarget", "_followTargetDistance", "_followLoop", "_followTargetDirection", "_position", "_mode" })
            {
                FieldInfo field = typeof(SplinePositioner).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            Assert.That(typeof(SplinePositioner).GetField("_lastPosition", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(0));
            Assert.That(typeof(SplinePositioner).GetMethod("SetPercent").GetParameters()[2].DefaultValue, Is.EqualTo(false));
            Assert.That(typeof(SplinePositioner).GetMethod("SetDistance").GetParameters()[1].DefaultValue, Is.EqualTo(false));
        }
    }
}
