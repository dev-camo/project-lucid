using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineTracer and SplineFollower, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long, cached samples at percents 0, 0.5, 1, so percent p is (20p,0,0) up to 0.5
    // and (10,0,20p-10) after. Expected values are hand arithmetic from the native bodies cited per test. The followers are
    // plain components on owned game objects (Transform physics mode unless a test says otherwise), never multithreaded, and
    // TearDown destroys everything the test made.
    public sealed class OriginalDreamteckSplineFollowerTests
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

        private static void Near(Vector3 actual, Vector3 expected, string message, float tolerance)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance), message + " (actual " + actual.ToString("F5") + ")");
        }

        private static bool IsMissing(object value)
        {
            // Editor GetComponent returns fake-null objects, which compare equal to null only through UnityEngine.Object.
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

        // Field initialisers of the SplineTracer ctor 0x4cb370 and the SplineFollower ctor 0x4bc130 (follow on, speed and duration
        // 1, a fresh speed modifier), and the Awake 0x4c9ba0 that fetches the Transform target.
        [Test]
        public void FreshFollowerKeepsTheShippedDefaults()
        {
            SplineFollower follower = NewFollower();
            Assert.That(follower.follow, Is.True);
            Assert.That(follower.followSpeed, Is.EqualTo(1f));
            Assert.That(follower.followDuration, Is.EqualTo(1f));
            Assert.That(follower.wrapMode, Is.EqualTo(SplineFollower.Wrap.Default));
            Assert.That(follower.followMode, Is.EqualTo(SplineFollower.FollowMode.Uniform));
            Assert.That(follower.autoStartPosition, Is.False);
            Assert.That(follower.preserveUniformSpeedWithOffset, Is.False);
            Assert.That(follower.speedModifier, Is.Not.Null);
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward));
            Assert.That(follower.applyDirectionRotation, Is.True);
            Assert.That(follower.useTriggers, Is.False);
            Assert.That(follower.triggerGroup, Is.EqualTo(0));
            Assert.That(follower.physicsMode, Is.EqualTo(SplineTracer.PhysicsMode.Transform));
            Assert.That(follower.dontLerpDirection, Is.False);
            Assert.That(follower.motion, Is.Not.Null);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.0));
            Assert.That(follower.result.percent, Is.EqualTo(0.0));
            Assert.That(follower is SplineTracer, Is.True);
            Assert.That(Invoke(follower, "GetTransform"), Is.SameAs(follower.transform), "GetTransform returns the own transform");
        }

        // get_motion 0x4cd4f0 creates the module once; ApplyMotion 0x4ca5f0 points it back at the tracer.
        [Test]
        public void MotionIsCreatedOnceAndPointsBackAtTheTracer()
        {
            SplineFollower follower = NewFollower(NewLine());
            Assert.That(follower.motion, Is.SameAs(follower.motion));
            follower.SetPercent(0.25);
            Assert.That(follower.motion.targetUser == follower, Is.True);
        }

        // SetPercent 0x4ca1f0: the sample at the clipped percent becomes the result (its percent is the clamped argument),
        // ApplyMotion moves the Transform. Without samples nothing happens. The follower override 0x4bbfd0 only adds bookkeeping.
        [Test]
        public void SetPercentPlacesTheTransformOnTheSample()
        {
            SplineFollower empty = NewFollower();
            empty.transform.position = new Vector3(7f, 7f, 7f);
            empty.SetPercent(0.5);
            Near(empty.transform.position, new Vector3(7f, 7f, 7f), "no samples, no move");
            Assert.That(empty.GetPercent(), Is.EqualTo(0.0));

            SplineFollower follower = NewFollower(NewLine());
            follower.SetPercent(0.25);
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "quarter");
            Assert.That(follower.GetPercent(), Is.EqualTo(0.25).Within(1e-9));
            Near(follower.result.position, new Vector3(5f, 0f, 0f), "result");
            follower.SetPercent(0.75);
            Near(follower.transform.position, new Vector3(10f, 0f, 5f), "three quarters");
            follower.SetPercent(1.5);
            Near(follower.transform.position, new Vector3(10f, 0f, 10f), "clamped past the end");
            Assert.That(follower.GetPercent(), Is.EqualTo(1.0));
            follower.Restart(0.4);
            Near(follower.transform.position, new Vector3(8f, 0f, 0f), "restart at 0.4");
            follower.Restart();
            Near(follower.transform.position, Vector3.zero, "restart at the start");
        }

        // SetDistance 0x4cd860: the percent reached by travelling the distance from the start, with no sample count check.
        [Test]
        public void SetDistanceTravelsFromTheStart()
        {
            SplineFollower follower = NewFollower(NewLine());
            follower.SetDistance(5f);
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "5 units");
            Assert.That(follower.GetPercent(), Is.EqualTo(0.25).Within(1e-4));
            follower.SetDistance(15f);
            Near(follower.transform.position, new Vector3(10f, 0f, 5f), "15 units");
            follower.SetDistance(100f);
            Assert.That(follower.GetPercent(), Is.EqualTo(1.0).Within(1e-6), "the end of the spline");
        }

        // set_followSpeed 0x4bb110: a changed, non-zero speed points the follower the matching way. set_direction 0x4bb220 makes the
        // speed's sign agree with the direction. set_followDuration 0x4bb280 clamps at zero.
        [Test]
        public void SpeedDirectionAndDurationStayConsistent()
        {
            SplineFollower follower = NewFollower();
            follower.followSpeed = -2f;
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Backward));
            follower.followSpeed = 3f;
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward));
            follower.followSpeed = 0f;
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward), "zero speed keeps the direction");
            follower.followSpeed = -1f;
            follower.followSpeed = 0f;
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Backward));
            Assert.That(follower.followSpeed, Is.EqualTo(0f));

            // 0x4bb15b..0x4bb1a6: the guard is Mathf.Approximately(value, 0), so a speed under 8 * Mathf.Epsilon also counts as zero.
            follower.direction = Spline.Direction.Forward;
            follower.followSpeed = 1f;
            follower.followSpeed = -Mathf.Epsilon;
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward), "a nearly zero speed keeps the direction");
            Assert.That(follower.followSpeed, Is.EqualTo(-Mathf.Epsilon));

            follower.followSpeed = 4f;
            follower.direction = Spline.Direction.Backward;
            Assert.That(follower.followSpeed, Is.EqualTo(-4f), "backward flips a positive speed");
            follower.direction = Spline.Direction.Backward;
            Assert.That(follower.followSpeed, Is.EqualTo(-4f));
            follower.direction = Spline.Direction.Forward;
            Assert.That(follower.followSpeed, Is.EqualTo(4f), "forward flips a negative speed");

            follower.followDuration = -3f;
            Assert.That(follower.followDuration, Is.EqualTo(0f));
            follower.followDuration = 2.5f;
            Assert.That(follower.followDuration, Is.EqualTo(2.5f));
        }

        // set_follow 0x4bb2c0: with autoStartPosition a change first projects the follower's position on the spline and sets that
        // percent. The follower stands at (11,0,5), next to the point (10,0,5) at percent 0.75.
        [Test]
        public void FollowToggleProjectsOnlyWithAutoStartPosition()
        {
            SplineComputer computer = NewLine();
            SplineFollower plain = NewFollower(computer);
            plain.transform.position = new Vector3(11f, 0f, 5f);
            plain.follow = false;
            Assert.That(plain.follow, Is.False);
            Near(plain.transform.position, new Vector3(11f, 0f, 5f), "no auto start position");

            SplineFollower automatic = NewFollower(computer);
            automatic.autoStartPosition = true;
            automatic.transform.position = new Vector3(11f, 0f, 5f);
            automatic.follow = true;
            Near(automatic.transform.position, new Vector3(11f, 0f, 5f), "unchanged value does nothing");
            automatic.follow = false;
            Near(automatic.transform.position, new Vector3(10f, 0f, 5f), "projected and placed", 0.01f);
            Assert.That(automatic.GetPercent(), Is.EqualTo(0.75).Within(1e-3));
            Assert.That(automatic.follow, Is.False);
        }

        // Start 0x4bb630 projects a following, auto-starting follower; PostBuild 0x4bb820 applies the motion again after a rebuild
        // only when samples exist, the follower follows and autoStartPosition is off.
        [Test]
        public void StartAndPostBuildFollowTheNativeConditions()
        {
            SplineComputer computer = NewLine();
            SplineFollower automatic = NewFollower(computer);
            automatic.autoStartPosition = true;
            automatic.transform.position = new Vector3(11f, 0f, 5f);
            Invoke(automatic, "Start");
            Near(automatic.transform.position, new Vector3(10f, 0f, 5f), "Start projects", 0.01f);

            SplineFollower idle = NewFollower(computer);
            idle.transform.position = new Vector3(11f, 0f, 5f);
            Invoke(idle, "Start");
            Near(idle.transform.position, new Vector3(11f, 0f, 5f), "Start without autoStartPosition");

            SplineFollower follower = NewFollower(computer);
            follower.SetPercent(0.5);
            follower.transform.position = new Vector3(99f, 99f, 99f);
            follower.RebuildImmediate();
            Near(follower.transform.position, new Vector3(10f, 0f, 0f), "following, no auto start: applied again");

            follower.transform.position = new Vector3(99f, 99f, 99f);
            follower.autoStartPosition = true;
            follower.RebuildImmediate();
            Near(follower.transform.position, new Vector3(99f, 99f, 99f), "auto start position skips it");

            follower.autoStartPosition = false;
            follower.follow = false;
            follower.RebuildImmediate();
            Near(follower.transform.position, new Vector3(99f, 99f, 99f), "not following skips it");
        }

        // LateRun 0x4bb6f0 follows only while follow is on. Follow 0x4bb720: Uniform moves by speed times the frame time, so a
        // zero speed re-places the follower on its sample; Time mode with a zero duration does nothing.
        [Test]
        public void LateRunFollowsOnlyWhileFollowIsOn()
        {
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

        // Move(double) 0x4bbbb0, Default wrap. From 0.9 a move of 0.2 stops at the end (percent 1.0) and raises onEndReached once
        // with the percent it started from (0.9); the C# event and the UnityEvent both fire. Moving again at the end raises
        // nothing. Zero moves are ignored.
        [Test]
        public void MoveByPercentStopsAtTheEndInDefaultWrap()
        {
            SplineFollower follower = NewFollower(NewLine());
            List<double> ends = new List<double>();
            List<float> unityEnds = new List<float>();
            follower.onEndReached += delegate (double percent) { ends.Add(percent); };
            SetUnityEvent(follower, "_unityOnEndReached", unityEnds);
            follower.SetPercent(0.9);
            follower.Move(0.0);
            Assert.That(ends, Is.Empty);
            follower.Move(0.2);
            Assert.That(follower.GetPercent(), Is.EqualTo(1.0));
            Near(follower.transform.position, new Vector3(10f, 0f, 10f), "at the end");
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(ends[0], Is.EqualTo(0.9).Within(1e-9));
            Assert.That(unityEnds, Is.EqualTo(new[] { 0.9f }));
            follower.Move(0.2);
            Assert.That(ends.Count, Is.EqualTo(1), "already at the end");
            Assert.That(follower.GetPercent(), Is.EqualTo(1.0));
        }

        // Move(double) 0x4bbbb0, Loop: the overflow continues from the start. The end event carries the wrapped start percent (0),
        // not the percent the move began at.
        [Test]
        public void MoveByPercentLoopsAroundBothEnds()
        {
            SplineFollower follower = NewFollower(NewLine());
            follower.wrapMode = SplineFollower.Wrap.Loop;
            List<double> ends = new List<double>();
            List<double> beginnings = new List<double>();
            follower.onEndReached += delegate (double percent) { ends.Add(percent); };
            follower.onBeginningReached += delegate (double percent) { beginnings.Add(percent); };
            follower.SetPercent(0.9);
            follower.Move(0.25);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.15).Within(1e-9));
            Near(follower.transform.position, new Vector3(3f, 0f, 0f), "wrapped to 0.15");
            Assert.That(ends, Is.EqualTo(new[] { 0.0 }));
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward));

            follower.direction = Spline.Direction.Backward;
            follower.SetPercent(0.1);
            follower.Move(0.3);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.8).Within(1e-9));
            Near(follower.transform.position, new Vector3(10f, 0f, 6f), "wrapped to 0.8");
            Assert.That(beginnings, Is.EqualTo(new[] { 1.0 }));
        }

        // Move(double) 0x4bbbb0, PingPong: the overflow is reflected and the direction flips (through the virtual setter, which also
        // flips the speed's sign). The end event carries the reflection point (1 going up, 0 going down).
        [Test]
        public void MoveByPercentReflectsInPingPong()
        {
            SplineFollower follower = NewFollower(NewLine());
            follower.wrapMode = SplineFollower.Wrap.PingPong;
            List<double> ends = new List<double>();
            List<double> beginnings = new List<double>();
            follower.onEndReached += delegate (double percent) { ends.Add(percent); };
            follower.onBeginningReached += delegate (double percent) { beginnings.Add(percent); };
            follower.SetPercent(0.9);
            follower.Move(0.25);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.85).Within(1e-9));
            Near(follower.transform.position, new Vector3(10f, 0f, 7f), "reflected to 0.85");
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Backward));
            Assert.That(follower.followSpeed, Is.EqualTo(-1f));
            Assert.That(ends, Is.EqualTo(new[] { 1.0 }));
            follower.Move(0.25);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.6).Within(1e-9), "keeps going backwards");
            Assert.That(ends.Count, Is.EqualTo(1));

            follower.SetPercent(0.1);
            follower.Move(0.3);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(follower.direction, Is.EqualTo(Spline.Direction.Forward));
            Assert.That(follower.followSpeed, Is.EqualTo(1f));
            Assert.That(beginnings, Is.EqualTo(new[] { 0.0 }));
        }

        // Move(double) 0x4bbbb0, Default wrap going backwards stops at the beginning (percent 0) and raises onBeginningReached with
        // the start percent.
        [Test]
        public void MoveByPercentStopsAtTheBeginningInDefaultWrap()
        {
            SplineFollower follower = NewFollower(NewLine());
            List<double> beginnings = new List<double>();
            follower.onBeginningReached += delegate (double percent) { beginnings.Add(percent); };
            follower.direction = Spline.Direction.Backward;
            follower.SetPercent(0.1);
            follower.Move(0.2);
            Assert.That(follower.GetPercent(), Is.EqualTo(0.0));
            Near(follower.transform.position, Vector3.zero, "at the beginning");
            Assert.That(beginnings.Count, Is.EqualTo(1));
            Assert.That(beginnings[0], Is.EqualTo(0.1).Within(1e-9));
        }

        // Move(float) 0x4bb880 with a distance of 5 from percent 0.9 (2 units left). Default stops at the end and the end event
        // carries the start percent. Loop continues from 0 with the remaining 3 units (percent 0.15); PingPong turns around and
        // travels the 3 units back from 1 (percent 0.85).
        [Test]
        public void MoveByDistanceWrapsWithTheRemainingDistance()
        {
            SplineComputer computer = NewLine();
            List<double> ends = new List<double>();

            SplineFollower stopping = NewFollower(computer);
            stopping.onEndReached += delegate (double percent) { ends.Add(percent); };
            stopping.SetPercent(0.9);
            stopping.Move(5f);
            Assert.That(stopping.GetPercent(), Is.EqualTo(1.0));
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(ends[0], Is.EqualTo(0.9).Within(1e-9));

            SplineFollower looping = NewFollower(computer);
            looping.wrapMode = SplineFollower.Wrap.Loop;
            looping.onEndReached += delegate (double percent) { ends.Add(percent); };
            looping.SetPercent(0.9);
            looping.Move(5f);
            Assert.That(looping.GetPercent(), Is.EqualTo(0.15).Within(1e-3));
            Assert.That(ends.Count, Is.EqualTo(2));
            Assert.That(ends[1], Is.EqualTo(0.9).Within(1e-9), "the start percent of the move, unlike Move(double)");

            SplineFollower bouncing = NewFollower(computer);
            bouncing.wrapMode = SplineFollower.Wrap.PingPong;
            bouncing.SetPercent(0.9);
            bouncing.Move(5f);
            Assert.That(bouncing.GetPercent(), Is.EqualTo(0.85).Within(1e-3));
            Assert.That(bouncing.direction, Is.EqualTo(Spline.Direction.Backward));
        }

        // Move(float) 0x4bb880 going backwards from 0.1 (2 units from the beginning) with 5 units. Default stops at 0 and raises
        // onBeginningReached; Loop continues from the end with the remaining 3 units (percent 0.85, direction unchanged);
        // PingPong turns forward and travels 3 units from 0 (percent 0.15).
        [Test]
        public void MoveByDistanceWrapsBackwards()
        {
            SplineComputer computer = NewLine();
            List<double> beginnings = new List<double>();

            SplineFollower stopping = NewFollower(computer);
            stopping.onBeginningReached += delegate (double percent) { beginnings.Add(percent); };
            stopping.direction = Spline.Direction.Backward;
            stopping.SetPercent(0.1);
            stopping.Move(5f);
            Assert.That(stopping.GetPercent(), Is.EqualTo(0.0).Within(1e-6));
            Assert.That(beginnings.Count, Is.EqualTo(1));
            Assert.That(beginnings[0], Is.EqualTo(0.1).Within(1e-9));

            SplineFollower looping = NewFollower(computer);
            looping.wrapMode = SplineFollower.Wrap.Loop;
            looping.direction = Spline.Direction.Backward;
            looping.SetPercent(0.1);
            looping.Move(5f);
            Assert.That(looping.GetPercent(), Is.EqualTo(0.85).Within(1e-3));
            Assert.That(looping.direction, Is.EqualTo(Spline.Direction.Backward));

            SplineFollower bouncing = NewFollower(computer);
            bouncing.wrapMode = SplineFollower.Wrap.PingPong;
            bouncing.direction = Spline.Direction.Backward;
            bouncing.SetPercent(0.1);
            bouncing.Move(5f);
            Assert.That(bouncing.GetPercent(), Is.EqualTo(0.15).Within(1e-3));
            Assert.That(bouncing.direction, Is.EqualTo(Spline.Direction.Forward));
        }

        // DoTravel 0x4bc0d0 travels along the spline, or along the offset path when preserveUniformSpeedWithOffset is set and the
        // motion module has an offset. 15 units along the plain line reach 0.75; along a path shifted 2 units to the side the
        // corner is cut differently and the same distance ends elsewhere. The check reads the motion module field, so it faults
        // while no module was created (a follower that never applied a motion).
        [Test]
        public void DoTravelUsesTheOffsetPathOnlyWhenPreservingUniformSpeed()
        {
            SplineComputer computer = NewLine();
            SplineFollower follower = NewFollower(computer);
            double plain = (double)Invoke(follower, "DoTravel", 0.0, 15f, 0f);
            Assert.That(plain, Is.EqualTo(0.75).Within(1e-4));

            follower.motion.offset = new Vector2(2f, 0f);
            Assert.That((double)Invoke(follower, "DoTravel", 0.0, 15f, 0f), Is.EqualTo(0.75).Within(1e-4), "preserveUniformSpeedWithOffset is off");
            follower.preserveUniformSpeedWithOffset = true;
            double shifted = (double)Invoke(follower, "DoTravel", 0.0, 15f, 0f);
            Assert.That(Math.Abs(shifted - plain), Is.GreaterThan(0.01), "the offset path is measured");
            follower.motion.offset = Vector2.zero;
            Assert.That((double)Invoke(follower, "DoTravel", 0.0, 15f, 0f), Is.EqualTo(plain).Within(1e-9), "no offset, plain travel");

            SplineFollower bare = NewFollower();
            bare.preserveUniformSpeedWithOffset = true;
            // The editor serializer may have created the module already; the fault needs it absent.
            FieldInfo motionField = bare.GetType().GetField("_motion", BindingFlags.Instance | BindingFlags.NonPublic);
            motionField.SetValue(bare, null);
            Assert.That(motionField.GetValue(bare), Is.Null);
            TargetInvocationException fault = Assert.Throws<TargetInvocationException>(() => Invoke(bare, "DoTravel", 0.0, 5f, 0f));
            Assert.That(fault.InnerException, Is.TypeOf<NullReferenceException>());
        }

        // CheckTriggers 0x4cae40 / AddTriggerToQueue 0x4cdc50 / InvokeTriggers 0x4cafb0: with useTriggers the triggers of the
        // follower's group crossed between the previous and the new percent are queued and invoked with the follower once
        // SetPercent asked for them. Nothing fires without useTriggers, without the request, or for a missing group.
        [Test]
        public void SetPercentInvokesTheCrossedTriggersOfItsGroup()
        {
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
        // the points passed (here point 1 between 0.25 and 0.75) and raises onNode once with them; the queue is then empty.
        // Without a subscriber CheckNodes queues nothing. A node queued before the subscriber left faults InvokeNodes.
        [Test]
        public void SetPercentReportsTheNodesItPassed()
        {
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

        // ApplyMotion 0x4ca5f0: a Rigidbody or Rigidbody2D physics mode without that component throws MissingComponentException with
        // the shipped text; the Transform mode is unaffected, and nothing happens before samples exist.
        [Test]
        public void MissingPhysicsComponentsThrowTheShippedMessage()
        {
            SplineFollower follower = NewFollower(NewLine());
            follower.physicsMode = SplineTracer.PhysicsMode.Rigidbody;
            Assert.That(follower.physicsMode, Is.EqualTo(SplineTracer.PhysicsMode.Rigidbody));
            MissingComponentException body = Assert.Throws<MissingComponentException>(() => follower.SetPercent(0.25));
            Assert.That(body.Message, Is.EqualTo("There is no Rigidbody attached to follower but the Physics mode is set to use one."));
            follower.physicsMode = SplineTracer.PhysicsMode.Rigidbody2D;
            MissingComponentException flat = Assert.Throws<MissingComponentException>(() => follower.SetPercent(0.25));
            Assert.That(flat.Message, Is.EqualTo("There is no Rigidbody2D attached to follower but the Physics mode is set to use one."));
            follower.physicsMode = SplineTracer.PhysicsMode.Transform;
            follower.SetPercent(0.25);
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "Transform mode again");

            SplineFollower sampleless = NewFollower();
            sampleless.physicsMode = SplineTracer.PhysicsMode.Rigidbody;
            Vector3 before = sampleless.transform.position;
            Assert.DoesNotThrow(() => sampleless.SetPercent(0.5));
            Assert.DoesNotThrow(() => Invoke(sampleless, "ApplyMotion"));
            Near(sampleless.transform.position, before, "nothing moves before samples exist", 0.0001f);
        }

        // physicsMode setter 0x4cd450 and RefreshTargets 0x4c9110 fetch the matching component of the tracer's own object.
        [Test]
        public void PhysicsModeFetchesTheMatchingComponent()
        {
            SplineFollower follower = NewFollower(NewLine());
            Rigidbody body = follower.gameObject.AddComponent<Rigidbody>();
            follower.physicsMode = SplineTracer.PhysicsMode.Rigidbody;
            Assert.That(follower.GetType().GetField("targetRigidbody", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follower), Is.SameAs(body));
            follower.physicsMode = SplineTracer.PhysicsMode.Rigidbody2D;
            Assert.That(IsMissing(follower.GetType().GetField("targetRigidbody2D", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follower)), Is.True);
            Assert.That(Invoke(follower, "GetRigidbody"), Is.SameAs(body));
            Assert.That(IsMissing(Invoke(follower, "GetRigidbody2D")), Is.True);
        }

        // ApplyMotion 0x4ca5f0: the applied direction rotation uses the follower's direction (Forward when applyDirectionRotation is
        // off); dontLerpDirection replaces the interpolated forward and up with those of the sample the percent falls in. Setting
        // direction or dontLerpDirection re-applies the motion (0x4cd600, 0x4cd630).
        [Test]
        public void DirectionAndDontLerpDirectionShapeTheAppliedRotation()
        {
            SplineComputer computer = NewLine();
            SplineFollower follower = NewFollower(computer);
            follower.SetPercent(0.25);
            Vector3 interpolated = follower.result.forward;
            Vector3 firstSample = computer[0].forward;
            Assert.That(Vector3.Angle(interpolated, firstSample), Is.GreaterThan(1f), "the corner sample bends the interpolated forward");
            Near(follower.modifiedResult.forward, interpolated, "modified result starts as the result");
            Near(follower.transform.forward, interpolated, "looks along the interpolated forward");

            follower.dontLerpDirection = true;
            Assert.That(follower.dontLerpDirection, Is.True);
            Near(follower.modifiedResult.forward, firstSample, "sample forward");
            Near(follower.modifiedResult.up, computer[0].up, "sample up");
            Near(follower.transform.forward, firstSample, "looks along the sample forward");
            Near(follower.result.forward, interpolated, "the raw result is untouched");
            // 0x4ca6cb GetSamplingValues gives the floor index: at 0.4 that is still sample 0 (a rounded index would pick sample 1).
            follower.SetPercent(0.4);
            Near(follower.modifiedResult.forward, computer[0].forward, "floor sample forward at 0.4");
            Near(follower.modifiedResult.up, computer[0].up, "floor sample up at 0.4");
            follower.SetPercent(0.25);
            follower.dontLerpDirection = false;
            Near(follower.transform.forward, interpolated, "back to interpolated");

            follower.direction = Spline.Direction.Backward;
            Near(follower.transform.forward, -interpolated, "backward looks the other way");
            follower.applyDirectionRotation = false;
            follower.SetPercent(0.25);
            Near(follower.transform.forward, interpolated, "no direction rotation looks forward");
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineTracer).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "applyDirectionRotation", "useTriggers", "triggerGroup", "_direction", "_dontLerpDirection", "_physicsMode", "_motion",
                "targetRigidbody", "targetRigidbody2D", "targetTransform", "_result", "_finalResult", "onNode", "onMotionApplied",
                "triggerInvokeQueue", "nodeConnectionQueue", "addTriggerIndex", "MIN_DELTA"
            }));
            Assert.That(typeof(SplineTracer).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "NodeConnection", "PhysicsMode", "JunctionHandler" }));
            Assert.That(Enum.GetNames(typeof(SplineTracer.PhysicsMode)), Is.EqualTo(new[] { "Transform", "Rigidbody", "Rigidbody2D" }));
            Assert.That(typeof(SplineTracer.JunctionHandler).BaseType, Is.EqualTo(typeof(MulticastDelegate)));
            Assert.That(typeof(SplineTracer.NodeConnection).GetFields().Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "node", "point" }));
            Assert.That(typeof(SplineTracer).GetMethod("SetPercent").GetParameters()[1].DefaultValue, Is.EqualTo(false));

            Assert.That(typeof(SplineFollower).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "wrapMode", "followMode", "autoStartPosition", "_follow", "_startPosition", "preserveUniformSpeedWithOffset",
                "onEndReached", "onBeginningReached", "_followSpeed", "_followDuration", "_speedModifier", "_unityOnEndReached",
                "_unityOnBeginningReached", "lastClippedPercent"
            }));
            Assert.That(typeof(SplineFollower).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "FollowMode", "Wrap", "FloatEvent" }));
            Assert.That(Enum.GetNames(typeof(SplineFollower.FollowMode)), Is.EqualTo(new[] { "Uniform", "Time" }));
            Assert.That(Enum.GetNames(typeof(SplineFollower.Wrap)), Is.EqualTo(new[] { "Default", "Loop", "PingPong" }));
            Assert.That(typeof(SplineFollower.FloatEvent).BaseType, Is.EqualTo(typeof(UnityEvent<float>)));
            Assert.That(typeof(SplineFollower.FloatEvent).GetCustomAttributes(typeof(SerializableAttribute), false).Length, Is.EqualTo(1));
            AddComponentMenu menu = (AddComponentMenu)typeof(SplineFollower).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Spline Follower"));
            FieldInfo serialized = typeof(SplineFollower).GetField("_follow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(serialized.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false).Length, Is.EqualTo(1));
        }

        // The serialized FloatEvent fields have no public API; they are filled the way deserialization would.
        // Move(double) 0x4bbbb0, Default wrap at 0x4bbc9a..0x4bbccd: a follower at percent 0 whose last clipped percent reached 1
        // (it ended the spline and the loop of a closed spline put it back at 0) starts the move from 1, so it neither moves back
        // to 0.1 nor raises onEndReached (1.0 < 1.0 is false), and it stores the unclamped end percent 1.1 (0x4bbce9).
        [Test]
        public void DefaultWrapTreatsAZeroPercentAfterTheEndAsStillAtTheEnd()
        {
            SplineFollower follower = NewFollower(NewLine());
            List<double> ends = new List<double>();
            follower.onEndReached += delegate (double percent) { ends.Add(percent); };
            follower.SetPercent(0.0);
            SetLastClippedPercent(follower, 1.0);
            follower.Move(0.1);
            Assert.That(follower.GetPercent(), Is.EqualTo(1.0));
            Near(follower.transform.position, new Vector3(10f, 0f, 10f), "stays at the end");
            Assert.That(ends, Is.Empty);
            Assert.That(GetLastClippedPercent(follower), Is.EqualTo(1.1).Within(1e-9));
        }

        // Move(double) Loop: CheckTriggers(start, 1) at 0x4bbe0a, then after the wrap CheckTriggers(0, end) at 0x4bbe70, and the queue
        // is fired once at the end (0x4bbf1a). From 0.9 by 0.25 the first range fires 0.95 and the second 0.1, in that order.
        [Test]
        public void LoopWrapReportsTheTriggersOfBothStretchesInOrder()
        {
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
        // switch (0x4ca95e jne 0x4cad95) returns without applying anything or raising it.
        [Test]
        public void MotionAppliedIsRaisedAfterApplyingAndNotForAnUnknownPhysicsMode()
        {
            SplineFollower follower = NewFollower(NewLine());
            List<Vector3> seen = new List<Vector3>();
            follower.onMotionApplied += delegate { seen.Add(follower.transform.position); };
            follower.SetPercent(0.25);
            Assert.That(seen.Count, Is.EqualTo(1));
            Near(seen[0], new Vector3(5f, 0f, 0f), "the event sees the applied position");
            follower.physicsMode = (SplineTracer.PhysicsMode)3;
            follower.SetPercent(0.75);
            Assert.That(seen.Count, Is.EqualTo(1), "an unknown physics mode raises nothing");
            Near(follower.transform.position, new Vector3(5f, 0f, 0f), "and applies nothing");
        }

        // CheckNodes 0x4cb030: the Backward loop (0x4cb274) walks toPoint-1 down to fromPoint, and a range inside point 0 that
        // starts at the beginning reports point 0 (0x4cb12c..0x4cb165).
        [Test]
        public void CheckNodesCoversTheBackwardLoopAndTheBeginningEdge()
        {
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

        // SetPercent 0x4bbfed..0x4bbff2 stores the unclamped argument; SetDistance 0x4bc04a stores ClipPercent of the result.
        [Test]
        public void FollowerOverridesRememberTheLastClippedPercent()
        {
            SplineFollower follower = NewFollower(NewLine());
            follower.SetPercent(1.5);
            Assert.That(GetLastClippedPercent(follower), Is.EqualTo(1.5));
            follower.SetPercent(0.9);
            follower.SetDistance(5f);
            Assert.That(GetLastClippedPercent(follower), Is.EqualTo(0.25).Within(1e-6));
        }

        private static double GetLastClippedPercent(SplineFollower follower)
        {
            return (double)typeof(SplineFollower).GetField("lastClippedPercent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follower);
        }

        private static void SetLastClippedPercent(SplineFollower follower, double value)
        {
            typeof(SplineFollower).GetField("lastClippedPercent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(follower, value);
        }

        private static void SetUnityEvent(SplineFollower follower, string field, List<float> received)
        {
            SplineFollower.FloatEvent unityEvent = new SplineFollower.FloatEvent();
            unityEvent.AddListener(delegate (float percent) { received.Add(percent); });
            typeof(SplineFollower).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(follower, unityEvent);
        }
    }
}
