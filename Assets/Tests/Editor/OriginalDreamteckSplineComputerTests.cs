using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineComputer. The fixture spline is a Linear three-point polyline (0,0,0) -> (10,0,0) -> (10,0,10)
    // whose cached samples sit exactly on the points (percents 0, 0.5, 1), so every expected value is hand arithmetic. Native
    // addresses are cited per test. Each test owns its game objects and DestroyImmediate removes them in TearDown; the
    // computers are never multithreaded, so no SplineThreading thread is started.
    public sealed class OriginalDreamteckSplineComputerTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly BindingFlags Anything = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private SplineComputer NewComputer(string name, Vector3 position)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            owner.transform.position = position;
            return owner.AddComponent<SplineComputer>();
        }

        private static SplinePoint[] Corner(float x = 0f)
        {
            SplinePoint[] points = new SplinePoint[3];
            points[0] = new SplinePoint(new Vector3(x, 0f, 0f));
            points[1] = new SplinePoint(new Vector3(x + 10f, 0f, 0f));
            points[2] = new SplinePoint(new Vector3(x + 10f, 0f, 10f));
            return points;
        }

        private SplineComputer NewLine(string name, Vector3 position)
        {
            SplineComputer computer = NewComputer(name, position);
            computer.type = Spline.Type.Linear;
            computer.SetPoints(Corner());
            computer.RebuildImmediate();
            return computer;
        }

        private static void Call(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, null);
        }

        // Defaults come from the field initialisers of the ctor and the Spline(CatmullRom) member.
        [Test]
        public void FreshComputerKeepsTheShippedDefaults()
        {
            SplineComputer computer = NewComputer("defaults", Vector3.zero);
            Assert.That(computer.updateMode, Is.EqualTo(SplineComputer.UpdateMode.Update));
            Assert.That(computer.multithreaded, Is.False);
            Assert.That(computer.space, Is.EqualTo(SplineComputer.Space.Local));
            Assert.That(computer.sampleMode, Is.EqualTo(SplineComputer.SampleMode.Default));
            Assert.That(computer.type, Is.EqualTo(Spline.Type.CatmullRom));
            Assert.That(computer.optimizeAngleThreshold, Is.EqualTo(0.5f));
            Assert.That(computer.sampleRate, Is.EqualTo(10));
            Assert.That(computer.pointCount, Is.EqualTo(0));
            Assert.That(computer.sampleCount, Is.EqualTo(0));
            Assert.That(computer.subscriberCount, Is.EqualTo(0));
            Assert.That(computer.triggerGroups, Is.Empty);
            Assert.That(computer.isClosed, Is.False);
            Assert.That(computer.rawSamples, Is.Empty);
            Assert.That(computer.CalculateLength(), Is.EqualTo(0f), "no samples yet");
            Assert.That(computer.GetPoint(0).position, Is.EqualTo(Vector3.zero), "out of range answers a default point");
            Assert.That(computer.GetNode(0), Is.Null);
        }

        // OptimizeSamples 0x4b75c0: a middle raw sample is kept only when the direction changes by at least the threshold
        // (0.5 degrees) from the last kept one (angle test 0x4b7993..0x4b79b3); the first and last are always kept, and every raw
        // sample records the index of the last kept sample at or before it.
        [Test]
        public void OptimizedModeDropsCollinearSamplesAndKeepsCorners()
        {
            SplineComputer straight = NewComputer("straight", Vector3.zero);
            straight.type = Spline.Type.Linear;
            straight.sampleMode = SplineComputer.SampleMode.Optimized;
            straight.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(20f, 0f, 0f))
            });
            straight.RebuildImmediate();
            SampleCollection collection = new SampleCollection();
            straight.GetSamples(collection);
            Assert.That(straight.sampleCount, Is.EqualTo(2));
            Assert.That(collection.optimizedIndices, Is.EqualTo(new[] { 0, 0, 1 }));
            Near(collection.samples[0].position, new Vector3(0f, 0f, 0f), "first kept");
            Near(collection.samples[1].position, new Vector3(20f, 0f, 0f), "last kept");

            SplineComputer corner = NewComputer("corner", Vector3.zero);
            corner.type = Spline.Type.Linear;
            corner.sampleMode = SplineComputer.SampleMode.Optimized;
            corner.SetPoints(Corner());
            corner.RebuildImmediate();
            corner.GetSamples(collection);
            Assert.That(corner.sampleCount, Is.EqualTo(3));
            Assert.That(collection.optimizedIndices, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        // Raycast 0x4b7f20: the first step 0..0.5 is the segment (0,0,0)-(10,0,0) and a box at x 5 is hit at x 4.5. The percent
        // comes from the squared-distance ratio (0x4b817b..0x4b818e: mulps, mulps, divss, no sqrt): 0.5 * 20.25 / 100 = 0.10125.
        // A linear distance ratio would give 0.225.
        [Test]
        public void RaycastDerivesThePercentFromTheSquaredDistanceRatio()
        {
            SplineComputer computer = NewLine("line", Vector3.zero);
            GameObject wall = new GameObject("wall");
            _owned.Add(wall);
            wall.transform.position = new Vector3(5f, 0f, 0f);
            wall.AddComponent<BoxCollider>().size = Vector3.one;
            Physics.SyncTransforms();

            RaycastHit hit;
            double hitPercent;
            Assert.That(computer.Raycast(out hit, out hitPercent, ~0), Is.True);
            Near(hit.point, new Vector3(4.5f, 0f, 0f), "hit point");
            Assert.That(hitPercent, Is.EqualTo(0.10125).Within(1e-5));
        }

        // sampleRate setter 0x4b2f40: values below two are raised to two, and only a different value is applied.
        [Test]
        public void SampleRateIsClampedToTwo()
        {
            SplineComputer computer = NewComputer("rate", Vector3.zero);
            computer.sampleRate = 1;
            Assert.That(computer.sampleRate, Is.EqualTo(2));
            computer.sampleRate = 25;
            Assert.That(computer.sampleRate, Is.EqualTo(25));
        }

        // SetPoints 0x4b26a0 / GetPoints 0x4b23d0 / space setter 0x4b2380: a local computer stores points relative to its
        // transform and reports them in world space; switching the space re-stores the world points unchanged.
        [Test]
        public void SpaceSwitchKeepsTheWorldPositions()
        {
            SplineComputer computer = NewComputer("moved", new Vector3(5f, 0f, 0f));
            computer.type = Spline.Type.Linear;
            computer.SetPoints(Corner());
            Near(computer.GetPointPosition(0), Vector3.zero, "world");
            Near(computer.GetPointPosition(1), new Vector3(10f, 0f, 0f), "world 1");
            Near(computer.GetPointPosition(0, SplineComputer.Space.Local), new Vector3(-5f, 0f, 0f), "stored local");
            Near(computer.GetPoints()[2].position, new Vector3(10f, 0f, 10f), "world list");
            Near(computer.GetPoints(SplineComputer.Space.Local)[2].position, new Vector3(5f, 0f, 10f), "local list");

            computer.space = SplineComputer.Space.World;
            Near(computer.GetPointPosition(0), Vector3.zero, "world after switch");
            Near(computer.GetPointPosition(0, SplineComputer.Space.Local), Vector3.zero, "stored world after switch");
        }

        // Rebuild 0x4b2c30 / CalculateSamples 0x4b3d30 / the sample queries 0x4b6920..0x4b7150.
        [Test]
        public void SamplesFollowTheLinearPolyline()
        {
            SplineComputer computer = NewLine("line", Vector3.zero);
            Assert.That(computer.pointCount, Is.EqualTo(3));
            Assert.That(computer.sampleCount, Is.EqualTo(3));
            Assert.That(computer.iterations, Is.EqualTo(3));
            Assert.That(computer.moveStep, Is.EqualTo(0.5).Within(1e-6));
            Near(computer.EvaluatePosition(0.25), new Vector3(5f, 0f, 0f), "quarter");
            Near(computer.EvaluatePosition(0.75), new Vector3(10f, 0f, 5f), "three quarters");
            Near(computer.Evaluate(1).position, new Vector3(10f, 0f, 0f), "evaluate point 1");
            Near(computer[2].position, new Vector3(10f, 0f, 10f), "indexer");
            Assert.That(computer.rawSamples.Length, Is.EqualTo(3));
            Assert.That(computer.CalculateLength(), Is.EqualTo(20f).Within(1e-3f));
            Assert.That(computer.CalculateLength(0.25, 0.75), Is.EqualTo(10f).Within(1e-3f));

            float moved;
            Assert.That(computer.Travel(0.0, 5f, out moved), Is.EqualTo(0.25).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(computer.Travel(1.0, 15f, out moved, Spline.Direction.Backward), Is.EqualTo(0.25).Within(1e-6));

            SplineSample projected = computer.Project(new Vector3(10f, 0f, 2f));
            Near(projected.position, new Vector3(10f, 0f, 2f), "projected");
            Assert.That(projected.percent, Is.EqualTo(0.6).Within(1e-4));
        }

        // Evaluate/EvaluatePosition 0x4b6980 / 0x4b6b30: Calculate evaluates the spline and transforms the result, which matches
        // the cached world samples for a moved local computer.
        [Test]
        public void CalculateModeMatchesTheCachedWorldSamples()
        {
            SplineComputer computer = NewComputer("calc", new Vector3(5f, 0f, 0f));
            computer.type = Spline.Type.Linear;
            computer.SetPoints(Corner());
            computer.RebuildImmediate();
            Near(computer.EvaluatePosition(0.25, SplineComputer.EvaluateMode.Cached), new Vector3(5f, 0f, 0f), "cached");
            Near(computer.EvaluatePosition(0.25, SplineComputer.EvaluateMode.Calculate), new Vector3(5f, 0f, 0f), "calculated");
            Near(computer.Evaluate(0.75, SplineComputer.EvaluateMode.Calculate).position, new Vector3(10f, 0f, 5f), "calculated sample");
        }

        // Update/FixedUpdate/LateUpdate 0x4b3970/0x4b3760/0x4b3950: the callback that matches updateMode (or any of the three for
        // AllUpdate) runs the queued resample; None never queues one. Rows are callbacks, columns are modes.
        [Test]
        public void UpdateModeSelectsTheCallbacksThatResample()
        {
            string[] callbacks = { "Update", "FixedUpdate", "LateUpdate" };
            SplineComputer.UpdateMode[] modes =
            {
                SplineComputer.UpdateMode.Update, SplineComputer.UpdateMode.FixedUpdate, SplineComputer.UpdateMode.LateUpdate,
                SplineComputer.UpdateMode.AllUpdate, SplineComputer.UpdateMode.None
            };
            bool[][] runs =
            {
                new[] { true, false, false, true, false },
                new[] { false, true, false, true, false },
                new[] { false, false, true, true, false }
            };
            for (int callback = 0; callback < callbacks.Length; callback++)
            {
                for (int mode = 0; mode < modes.Length; mode++)
                {
                    // Play mode caches Application.isPlaying in Awake; the editor-only _editorIsPlaying=false would resample at once.
                    SplineComputer computer = DreamteckPlayState.Cache(NewComputer("mode" + callback + mode, Vector3.zero));
                    computer.updateMode = modes[mode];
                    computer.type = Spline.Type.Linear;
                    computer.SetPoints(Corner());
                    Call(computer, callbacks[callback]);
                    Assert.That(computer.sampleCount, Is.EqualTo(runs[callback][mode] ? 3 : 0), callbacks[callback] + " in " + modes[mode]);
                }
            }
        }

        // RunUpdate 0x4b3780 / RebuildUsers 0x4b3b30: after a resample the subscribers rebuild (last first), destroyed ones are
        // dropped and onRebuild is raised once.
        [Test]
        public void ResampleRebuildsUsersAndRaisesOnRebuild()
        {
            // The shipped queue path: the editor-only play-state caches are set to what play mode caches (the computer so that
            // SetPoints queues the resample, the users so that Rebuild queues and OnDestroy does not unsubscribe a destroyed one).
            SplineComputer computer = DreamteckPlayState.Cache(NewComputer("users", Vector3.zero));
            computer.type = Spline.Type.Linear;
            SplineUser alive = DreamteckPlayState.Cache(computer.gameObject.AddComponent<SplineUser>());
            GameObject doomedOwner = new GameObject("doomed");
            _owned.Add(doomedOwner);
            SplineUser doomed = DreamteckPlayState.Cache(doomedOwner.AddComponent<SplineUser>());
            alive.spline = computer;
            doomed.spline = computer;
            Assert.That(computer.subscriberCount, Is.EqualTo(2));

            int rebuilds = 0;
            int builds = 0;
            computer.onRebuild += delegate { rebuilds++; };
            alive.onPostBuild += delegate { builds++; };
            UnityEngine.Object.DestroyImmediate(doomedOwner);
            computer.SetPoints(Corner());
            Call(computer, "Update");
            Assert.That(rebuilds, Is.EqualTo(1));
            Assert.That(computer.subscriberCount, Is.EqualTo(1), "the destroyed user is dropped");
            Call(alive, "Update");
            Assert.That(builds, Is.EqualTo(1), "the rebuild reached the live user");
            Assert.That(alive.sampleCount, Is.EqualTo(3));
        }

        // Subscribe 0x4b45a0, Unsubscribe 0x4b4740, IsSubscribed 0x4b46a0, GetSubscribers 0x4b4820 (a copy).
        [Test]
        public void SubscriptionsIgnoreDuplicatesAndReturnACopy()
        {
            SplineComputer computer = NewComputer("subs", Vector3.zero);
            // On the computer's own object the editor-only SplineUser.Reset (run by AddComponent in the editor, never in a
            // player) would find it and subscribe, so the user lives on a separate object.
            GameObject userOwner = new GameObject("subs user");
            _owned.Add(userOwner);
            SplineUser user = userOwner.AddComponent<SplineUser>();
            Assert.That(computer.IsSubscribed(user), Is.False);
            computer.Subscribe(user);
            computer.Subscribe(user);
            Assert.That(computer.subscriberCount, Is.EqualTo(1));
            Assert.That(computer.IsSubscribed(user), Is.True);
            SplineUser[] copy = computer.GetSubscribers();
            Assert.That(copy, Is.EqualTo(new[] { user }));
            copy[0] = null;
            Assert.That(computer.IsSubscribed(user), Is.True, "editing the copy changes nothing");
            computer.Unsubscribe(user);
            computer.Unsubscribe(user);
            Assert.That(computer.subscriberCount, Is.EqualTo(0));
        }

        // SetPointPosition 0x4b5280 and its siblings: a negative index is ignored, an index past the end appends points.
        [Test]
        public void PointSettersAppendAndIgnoreNegativeIndices()
        {
            SplineComputer computer = NewComputer("setters", Vector3.zero);
            computer.SetPointPosition(-1, Vector3.one);
            Assert.That(computer.pointCount, Is.EqualTo(0));
            computer.SetPointPosition(2, new Vector3(1f, 2f, 3f));
            Assert.That(computer.pointCount, Is.EqualTo(3));
            Near(computer.GetPointPosition(2), new Vector3(1f, 2f, 3f), "appended position");
            computer.SetPointSize(1, 2.5f);
            Assert.That(computer.GetPointSize(1), Is.EqualTo(2.5f));
            computer.SetPointColor(1, Color.red);
            Assert.That(computer.GetPointColor(1), Is.EqualTo(Color.red));
            computer.SetPointNormal(1, Vector3.right);
            Near(computer.GetPointNormal(1), Vector3.right, "normal");
            computer.SetPointSize(-3, 9f);
            Assert.That(computer.pointCount, Is.EqualTo(3));
        }

        // GetPointPercent 0x4b6540 and PercentToPointIndex 0x4b66c0. Open: count = points - 1. Closed: count = points, the
        // percent divides by the point count, and both searches wrap an index past the end to 0 (0x4b66c0 jumps from the forward FloorInt at
        // 0x4b674c into the same wrap, 0x4b685a, as the backward search).
        [Test]
        public void PointPercentAndIndexTruthTable()
        {
            SplineComputer computer = NewLine("table", Vector3.zero);
            Assert.That(computer.GetPointPercent(0), Is.EqualTo(0.0));
            Assert.That(computer.GetPointPercent(1), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(computer.GetPointPercent(2), Is.EqualTo(1.0));
            Assert.That(computer.PercentToPointIndex(0.75), Is.EqualTo(1));
            Assert.That(computer.PercentToPointIndex(0.75, Spline.Direction.Backward), Is.EqualTo(2));
            Assert.That(computer.PercentToPointIndex(1.0), Is.EqualTo(2));
            Assert.That(computer.PercentToPointIndex(1.0, Spline.Direction.Backward), Is.EqualTo(2));
            Assert.That(computer.PercentToPointIndex(1.6, Spline.Direction.Backward), Is.EqualTo(0), "past the end wraps");
            Assert.That(computer.PercentToPointIndex(1.6), Is.EqualTo(0), "the forward search wraps the same way");

            computer.Close();
            Assert.That(computer.isClosed, Is.True);
            Assert.That(computer.GetPointPercent(1), Is.EqualTo(1.0 / 3.0).Within(1e-9));
            Assert.That(computer.PercentToPointIndex(0.5), Is.EqualTo(1));
            Assert.That(computer.PercentToPointIndex(1.0), Is.EqualTo(0), "the closing percent is the first point again");
            Assert.That(computer.PercentToPointIndex(1.0, Spline.Direction.Backward), Is.EqualTo(0));
        }

        // Close 0x4b7d20 needs three points and logs an error otherwise; Break 0x4b7c20 opens it again.
        [Test]
        public void CloseNeedsThreePointsAndBreakReopens()
        {
            SplineComputer computer = NewComputer("close", Vector3.zero);
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[] { new SplinePoint(Vector3.zero), new SplinePoint(Vector3.right) });
            LogAssert.Expect(LogType.Error, "Spline close needs at least 3 points before it can be closed. Current points: 2");
            computer.Close();
            Assert.That(computer.isClosed, Is.False);

            computer.SetPoints(Corner());
            computer.Close();
            Assert.That(computer.isClosed, Is.True);
            computer.Break(1);
            Assert.That(computer.isClosed, Is.False);
            Near(computer.GetPointPosition(0), new Vector3(10f, 0f, 0f), "break at 1 makes point 1 the first");
            computer.Break();
            Assert.That(computer.isClosed, Is.False, "an open spline ignores Break");
        }

        // AddTrigger 0x4b8990 creates the missing groups; RemoveTrigger 0x4b8a80, CheckTriggers 0x4b8b60 / 0x4b8bd0 and
        // ResetTriggers 0x4b8cc0 / 0x4b8d10 log an error for a missing group. The user argument of CheckTriggers is not forwarded.
        [Test]
        public void TriggerApisCreateGroupsLogMissingOnesAndDropTheUser()
        {
            SplineComputer computer = NewComputer("triggers", Vector3.zero);
            SplineTrigger trigger = computer.AddTrigger(2, 0.5, SplineTrigger.Type.Forward);
            Assert.That(computer.triggerGroups.Length, Is.EqualTo(3));
            Assert.That(trigger.name, Is.EqualTo("API Trigger"));
            Assert.That(trigger.color, Is.EqualTo(Color.white));
            Assert.That(computer.triggerGroups[2].triggers, Is.EqualTo(new[] { trigger }));
            Assert.That(computer.AddTriggerGroup(), Is.SameAs(computer.triggerGroups[3]));

            SplineUser user = computer.gameObject.AddComponent<SplineUser>();
            int calls = 0;
            trigger.AddListener(delegate (SplineUser sender) { calls++; });
            // SplineTrigger.Invoke reads Application.isPlaying directly and returns while not playing; the player branch is the
            // text after the #if UNITY_EDITOR block and cannot run in EditMode. Check marks the crossed trigger as worked before
            // Invoke, so the group routing is observed through that flag and the listener stays silent.
            Assert.That(Worked(trigger), Is.False);
            computer.CheckTriggers(0.4, 0.6, user);
            Assert.That(Worked(trigger), Is.True, "CheckTriggers(start, end, user) checks every group");
            Assert.That(calls, Is.EqualTo(0), "Invoke does not call the listener while not playing");
            trigger.Reset();
            computer.CheckTriggers(2, 0.4, 0.6);
            Assert.That(Worked(trigger), Is.True, "CheckTriggers(group, start, end) checks that group");
            Assert.That(calls, Is.EqualTo(0));
            trigger.Reset();
            computer.CheckTriggers(1, 0.4, 0.6);
            Assert.That(Worked(trigger), Is.False, "another group does not reach the trigger");

            trigger.workOnce = true;
            computer.ResetTriggers(2);
            Assert.That(trigger.Check(0.4, 0.6), Is.True);
            Assert.That(trigger.Check(0.4, 0.6), Is.False);
            computer.ResetTriggers();
            Assert.That(trigger.Check(0.4, 0.6), Is.True);

            LogAssert.Expect(LogType.Error, "Trigger group 9 does not exist");
            computer.CheckTriggers(9, 0.4, 0.6);
            LogAssert.Expect(LogType.Error, "Trigger group -1 does not exist");
            computer.ResetTriggers(-1);
            // RemoveTrigger 0x4b8a80 prints the trigger index (its second argument, edx) where the message says "trigger group",
            // so the numbers differ from the missing group here.
            LogAssert.Expect(LogType.Error, "Cannot delete trigger - trigger group 3 does not exist");
            computer.RemoveTrigger(7, 3);
            LogAssert.Expect(LogType.Error, "Cannot delete trigger - trigger group 5 does not exist");
            computer.RemoveTrigger(-1, 5);
            computer.RemoveTrigger(2, 0);
            Assert.That(computer.triggerGroups[2].triggers, Is.Empty);
        }

        private static bool Worked(SplineTrigger trigger)
        {
            return (bool)typeof(SplineTrigger).GetField("worked", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trigger);
        }

        // GetSamples 0x4b4510 shares the world samples and the sample mode with the collection.
        [Test]
        public void GetSamplesSharesTheCachedArrays()
        {
            SplineComputer computer = NewLine("share", Vector3.zero);
            SampleCollection collection = new SampleCollection();
            computer.GetSamples(collection);
            Assert.That(collection.length, Is.EqualTo(3));
            Near(collection.samples[2].position, new Vector3(10f, 0f, 10f), "shared sample");
            Assert.That(collection.sampleMode, Is.EqualTo(SplineComputer.SampleMode.Default));
            int index;
            double lerp;
            computer.GetSamplingValues(0.75, out index, out lerp);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(lerp, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void TransformHelpersUseTheCachedMatrices()
        {
            SplineComputer computer = NewComputer("matrix", new Vector3(5f, 0f, 0f));
            Near(computer.TransformPoint(Vector3.zero), new Vector3(5f, 0f, 0f), "point");
            Near(computer.InverseTransformPoint(new Vector3(5f, 0f, 0f)), Vector3.zero, "inverse point");
            Near(computer.TransformDirection(Vector3.up), Vector3.up, "direction ignores the translation");
            Near(computer.InverseTransformDirection(Vector3.up), Vector3.up, "inverse direction");
            Near(computer.position, new Vector3(5f, 0f, 0f), "position");
        }

        // The Roslyn names in the shipped assembly (DummyDll) place the RunUpdate local functions at ordinal 88
        // (<RunUpdate>g__CalculateWithTransform|88_0 and ...WithoutTransform|88_1; the player-define compile of the source
        // reproduces them). The editor compile this test runs declares the UNITY_EDITOR members ahead of RunUpdate (the editor
        // fields, the EditorUpdateMode enum, EditorAwake, EditorUpdateConnectedNodes, Reset), so its ordinal is 99, and it adds
        // the public nested enum EditorUpdateMode (editor-vs-ship declaration diff).
        [Test]
        public void CompilerGeneratedNamesAndNestedTypesMatchTheShippedAssembly()
        {
            Assert.That(typeof(SplineComputer).GetMethods(Anything).Select(m => m.Name).ToArray(),
                Does.Contain("<RunUpdate>g__CalculateWithTransform|99_0"));
            Assert.That(typeof(SplineComputer).GetMethods(Anything).Select(m => m.Name).ToArray(),
                Does.Contain("<RunUpdate>g__CalculateWithoutTransform|99_1"));
            Assert.That(typeof(SplineComputer).GetNestedTypes(Anything).Select(t => t.Name).ToArray(),
                Is.EquivalentTo(new[] { "EditorUpdateMode", "Space", "EvaluateMode", "SampleMode", "UpdateMode", "NodeLink" }));
            Type link = typeof(SplineComputer).GetNestedType("NodeLink", BindingFlags.NonPublic);
            Assert.That(link.GetFields(Anything).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "node", "pointIndex" }));
            Assert.That(link.GetMethods(Anything).Select(m => m.Name).ToArray(), Does.Contain("GetConnections"));
            Assert.That((int)SplineComputer.UpdateMode.None, Is.EqualTo(4));
            Assert.That((int)SplineComputer.SampleMode.Optimized, Is.EqualTo(2));
            Assert.That((int)SplineComputer.Space.Local, Is.EqualTo(1));
            Assert.That((int)SplineComputer.EvaluateMode.Calculate, Is.EqualTo(1));
        }

        // The editor compile declares eight editor-only fields (editorDrawPivot ... editorUpdateMode, incl. the private
        // _editorIsPlaying) ahead of the shipped list, as the original text does; the shipped build has none of them.
        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            string[] names = typeof(SplineComputer).GetFields(Anything).Select(f => f.Name).Where(n => n != "onRebuild").ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "editorDrawPivot", "editorPathColor", "editorAlwaysDraw", "editorDrawThickness", "editorBillboardThickness",
                "_editorIsPlaying", "isNewlyCreated", "editorUpdateMode",
                "multithreaded", "updateMode", "triggerGroups", "_spline", "_sampleCollection", "_originalSamplePercents", "_is2D",
                "hasSamples", "_optimizeAngleThreshold", "_space", "_sampleMode", "_subscribers", "_rawSamples", "_localToWorldMatrix",
                "_worldToLocalMatrix", "_nodes", "_rebuildPending", "_trsCached", "_trs", "_queueResample", "_queueRebuild"
            }));
            Assert.That(FormerName("_spline"), Is.EqualTo("spline"));
            Assert.That(FormerName("_originalSamplePercents"), Is.EqualTo("originalSamplePercents"));
            Assert.That(FormerName("_nodes"), Is.EqualTo("nodes"));
            FieldInfo threshold = typeof(SplineComputer).GetField("_optimizeAngleThreshold", Anything);
            UnityEngine.RangeAttribute range = (UnityEngine.RangeAttribute)Attribute.GetCustomAttribute(threshold, typeof(UnityEngine.RangeAttribute));
            Assert.That(range.min, Is.EqualTo(0.001f));
            Assert.That(range.max, Is.EqualTo(45f));
        }

        private static string FormerName(string field)
        {
            FieldInfo info = typeof(SplineComputer).GetField(field, Anything);
            return ((FormerlySerializedAsAttribute)Attribute.GetCustomAttribute(info, typeof(FormerlySerializedAsAttribute))).oldName;
        }
    }
}
