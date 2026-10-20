using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.ObjectController and its nested ObjectControl, driven through a SplineComputer that holds the Linear
    // polyline (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long, cached samples at percents 0, 0.5, 1, so the sample at percent 0
    // looks along +x and the one at percent 1 along +z, both with the world up and size 1. Expected values are hand arithmetic
    // or the System.Random sequences of the seeds the native Build uses, from the native bodies cited per test. The controllers
    // are plain components on owned game objects; edit mode refuses Destroy, so the copies are removed with DestroyImmediate
    // exactly where the native Remove does so. TearDown destroys everything the test made.
    // EditMode runs the editor compile of the original text, which adds editor-only code to the shipped methods. The copies
    // are instantiated through the player path (the editor branch uses PrefabUtility.InstantiatePrefab, which returns null for
    // a scene object, so retainPrefabInstancesInEditor is switched off), Clear destroys the copies at once while not playing,
    // and the tests of the queued rebuild cache the play state the way play mode does (DreamteckPlayState).
    public sealed class OriginalDreamteckObjectControllerTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (UnityEngine.Object owned in _owned)
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
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ", expected " + expected.ToString("F5") + ")");
        }

        private static void NearRotation(Quaternion actual, Quaternion expected, string message)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.1f), message + " (actual " + actual.eulerAngles.ToString("F3") + ", expected " + expected.eulerAngles.ToString("F3") + ")");
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private GameObject NewSource(string name, Vector3? scale = null)
        {
            GameObject source = NewObject(name);
            source.transform.localScale = scale ?? Vector3.one;
            return source;
        }

        private SplineComputer NewLine(bool closed = false, float pointSize = 1f)
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            SplinePoint[] points =
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            };
            for (int i = 0; i < points.Length; i++)
            {
                points[i].size = pointSize;
            }

            computer.SetPoints(points);
            if (closed)
            {
                computer.Close();
            }

            computer.RebuildImmediate();
            return computer;
        }

        private ObjectController NewBareController(SplineComputer line = null)
        {
            ObjectController controller = NewObject("controller").AddComponent<ObjectController>();
            // Editor-only option: the sources are scene objects, so the editor text must take the Instantiate branch that is the
            // whole body of the shipped InstantiateSingle. Nothing is spawned yet, so the setter's Clear/Spawn/Rebuild are no-ops.
            controller.retainPrefabInstancesInEditor = false;
            if (line != null)
            {
                controller.spline = line;
            }

            return controller;
        }

        // Spawns count copies of the sources in turn and builds once, so the copies stand on the spline.
        private ObjectController NewController(SplineComputer line, int count, params GameObject[] sources)
        {
            ObjectController controller = NewBareController(line);
            controller.objects = sources;
            controller.spawnCount = count;
            controller.RebuildImmediate();
            return controller;
        }

        private static Transform Copy(ObjectController controller, int index)
        {
            return controller.transform.GetChild(index);
        }

        private static object Call(object target, string name, params object[] arguments)
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

        private static FieldInfo Field(string name)
        {
            return typeof(ObjectController).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type ControlType()
        {
            return typeof(ObjectController).GetNestedType("ObjectControl", BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Array Spawned(ObjectController controller)
        {
            return (Array)Field("spawned").GetValue(controller);
        }

        private static double[] Doubles(int seed, int count)
        {
            System.Random random = new System.Random(seed);
            double[] values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = random.NextDouble();
            }

            return values;
        }

        private static float Lerp(float a, float b, double t)
        {
            return a + (b - a) * Mathf.Clamp01((float)t);
        }

        // The field initialisers of the ctor 0x4aa490 (empty objects and spawned, seed 1, zero offsets and rotations, unit scale
        // multipliers, uniform scale lerp, apply rotation, spawn delay 0.1) on top of the default members of the SplineUser.
        [Test]
        public void FreshControllerKeepsTheShippedDefaults()
        {
            Assert.That(NewObject("fresh").AddComponent<ObjectController>().retainPrefabInstancesInEditor, Is.True, "editor-only field initialiser (absent from the shipped layout)");
            ObjectController controller = NewBareController();
            Assert.That(controller.objects, Is.Not.Null);
            Assert.That(controller.objects.Length, Is.EqualTo(0));
            Assert.That(Spawned(controller).Length, Is.EqualTo(0));
            Assert.That(controller.objectMethod, Is.EqualTo(ObjectController.ObjectMethod.Instantiate));
            Assert.That(controller.spawnCount, Is.EqualTo(0));
            Assert.That(controller.objectPositioning, Is.EqualTo(ObjectController.Positioning.Stretch));
            Assert.That(controller.iteration, Is.EqualTo(ObjectController.Iteration.Ordered));
            Assert.That(controller.randomSeed, Is.EqualTo(1));
            Assert.That(controller.minOffset, Is.EqualTo(Vector3.zero));
            Assert.That(controller.maxOffset, Is.EqualTo(Vector3.zero));
            Assert.That(controller.offsetUseWorldCoords, Is.False);
            Assert.That(controller.minRotation, Is.EqualTo(Vector3.zero));
            Assert.That(controller.maxRotation, Is.EqualTo(Vector3.zero));
            Assert.That(controller.rotationOffset, Is.EqualTo(Vector3.zero));
            Assert.That(controller.uniformScaleLerp, Is.True);
            Assert.That(controller.minScaleMultiplier, Is.EqualTo(Vector3.one));
            Assert.That(controller.maxScaleMultiplier, Is.EqualTo(Vector3.one));
            Assert.That(controller.shellOffset, Is.False);
            Assert.That(controller.applyRotation, Is.True);
            Assert.That(controller.rotateByOffset, Is.False);
            Assert.That(controller.applyScale, Is.False);
            Assert.That(controller.delayedSpawn, Is.False);
            Assert.That(controller.spawnDelay, Is.EqualTo(0.1f));
            Assert.That(controller.evaluateOffset, Is.EqualTo(0f));
            Assert.That(controller.minObjectDistance, Is.EqualTo(0f));
            Assert.That(controller.maxObjectDistance, Is.EqualTo(0f));
            Assert.That((bool)Field("_useCustomObjectDistance").GetValue(controller), Is.False);
            Assert.That(controller.customOffsetRule == null, Is.True);
            Assert.That(controller.customRotationRule == null, Is.True);
            Assert.That(controller.customScaleRule == null, Is.True);
        }

        // Every setter 0x4a72c0..0x4a7c10 returns without a rebuild for an equal value and queues exactly one for a new one; the
        // vector setters compare with Vector3 equality, the rotationOffset setter with both limits. The play state is cached as
        // play mode does: with it unset the editor text rebuilds at once and the queued build never runs from Update.
        [Test]
        public void SettersQueueARebuildOnlyForChanges()
        {
            SplineComputer line = NewLine();
            ObjectController controller = DreamteckPlayState.Cache(NewBareController(line));
            controller.RebuildImmediate();
            int builds = 0;
            controller.onPostBuild += delegate { builds++; };
            ObjectControllerSineRule rule = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            _owned.Add(rule);

            Action[] same =
            {
                () => controller.rotationOffset = controller.rotationOffset,
                () => controller.objectPositioning = controller.objectPositioning,
                () => controller.iteration = controller.iteration,
                () => controller.randomSeed = controller.randomSeed,
                () => controller.minOffset = controller.minOffset,
                () => controller.maxOffset = controller.maxOffset,
                () => controller.offsetUseWorldCoords = controller.offsetUseWorldCoords,
                () => controller.minRotation = controller.minRotation,
                () => controller.maxRotation = controller.maxRotation,
                () => controller.minScaleMultiplier = controller.minScaleMultiplier,
                () => controller.maxScaleMultiplier = controller.maxScaleMultiplier,
                () => controller.uniformScaleLerp = controller.uniformScaleLerp,
                () => controller.shellOffset = controller.shellOffset,
                () => controller.applyRotation = controller.applyRotation,
                () => controller.rotateByOffset = controller.rotateByOffset,
                () => controller.applyScale = controller.applyScale,
                () => controller.evaluateOffset = controller.evaluateOffset,
                () => controller.minObjectDistance = controller.minObjectDistance,
                () => controller.maxObjectDistance = controller.maxObjectDistance,
                () => controller.customOffsetRule = controller.customOffsetRule,
                () => controller.customRotationRule = controller.customRotationRule,
                () => controller.customScaleRule = controller.customScaleRule
            };
            Action[] different =
            {
                () => controller.rotationOffset = new Vector3(5f, 5f, 5f),
                () => controller.objectPositioning = ObjectController.Positioning.Clip,
                () => controller.iteration = ObjectController.Iteration.Random,
                () => controller.randomSeed = 7,
                () => controller.minOffset = new Vector3(1f, 0f, 0f),
                () => controller.maxOffset = new Vector3(0f, 1f, 0f),
                () => controller.offsetUseWorldCoords = true,
                () => controller.minRotation = new Vector3(1f, 0f, 0f),
                () => controller.maxRotation = new Vector3(0f, 1f, 0f),
                () => controller.minScaleMultiplier = new Vector3(2f, 1f, 1f),
                () => controller.maxScaleMultiplier = new Vector3(1f, 2f, 1f),
                () => controller.uniformScaleLerp = false,
                () => controller.shellOffset = true,
                () => controller.applyRotation = false,
                () => controller.rotateByOffset = true,
                () => controller.applyScale = true,
                () => controller.evaluateOffset = 0.5f,
                () => controller.minObjectDistance = 1f,
                () => controller.maxObjectDistance = 2f,
                () => controller.customOffsetRule = rule,
                () => controller.customRotationRule = rule,
                () => controller.customScaleRule = rule
            };
            Assert.That(same.Length, Is.EqualTo(different.Length));
            for (int i = 0; i < same.Length; i++)
            {
                same[i]();
                Call(controller, "Update");
                Assert.That(builds, Is.EqualTo(i), "an equal value does not queue a rebuild (setter " + i + ")");
                different[i]();
                Call(controller, "Update");
                Assert.That(builds, Is.EqualTo(i + 1), "a new value queues one rebuild (setter " + i + ")");
            }
        }

        // The rotationOffset getter 0x4a7630 is the middle of the limits, and the setter 0x4a7670 pins both limits to the value.
        [Test]
        public void RotationOffsetIsTheMiddleOfTheLimitsAndPinsBoth()
        {
            ObjectController controller = NewBareController();
            controller.minRotation = new Vector3(0f, 0f, 10f);
            controller.maxRotation = new Vector3(20f, 90f, 30f);
            Near(controller.rotationOffset, new Vector3(10f, 45f, 20f), "middle of the limits");
            controller.rotationOffset = new Vector3(1f, 2f, 3f);
            Near(controller.minRotation, new Vector3(1f, 2f, 3f), "min pinned");
            Near(controller.maxRotation, new Vector3(1f, 2f, 3f), "max pinned");
            Near(controller.rotationOffset, new Vector3(1f, 2f, 3f), "middle of equal limits");
        }

        // set_spawnCount 0x4a6e60, InstantiateAll 0x4a8980 and InstantiateSingle 0x4a8ab0: the copies are the sources in turn
        // (index = copies so far - floor(copies / sources) * sources), appended as children of the controller.
        [Test]
        public void SpawnCountInstantiatesTheCopiesFromTheSourcesInTurn()
        {
            GameObject a = NewSource("A");
            GameObject b = NewSource("B");
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { a, b };
            controller.spawnCount = 3;
            Assert.That(controller.spawnCount, Is.EqualTo(3));
            Assert.That(controller.transform.childCount, Is.EqualTo(3));
            Assert.That(Copy(controller, 0).name, Is.EqualTo("A(Clone)"));
            Assert.That(Copy(controller, 1).name, Is.EqualTo("B(Clone)"));
            Assert.That(Copy(controller, 2).name, Is.EqualTo("A(Clone)"));
            Assert.That(Spawned(controller).Length, Is.EqualTo(3));
        }

        // InstantiateAll 0x4a8980 returns without a spline or without sources; Spawn 0x4a6d20 spawns once both exist.
        [Test]
        public void SpawningNeedsASplineAndSources()
        {
            GameObject a = NewSource("A");
            ObjectController withoutSpline = NewBareController();
            withoutSpline.objects = new[] { a };
            withoutSpline.spawnCount = 2;
            Assert.That(withoutSpline.transform.childCount, Is.EqualTo(0), "no spline");
            Assert.That(withoutSpline.spawnCount, Is.EqualTo(2));

            ObjectController withoutSources = NewBareController(NewLine());
            withoutSources.spawnCount = 2;
            Assert.That(withoutSources.transform.childCount, Is.EqualTo(0), "no sources");

            withoutSources.objects = new[] { a };
            withoutSources.Spawn();
            Assert.That(withoutSources.transform.childCount, Is.EqualTo(2), "Spawn tops up to the stored count");
        }

        // Raising the count appends only the missing copies (InstantiateAll starts at the copies so far).
        [Test]
        public void RaisingTheSpawnCountKeepsTheExistingCopies()
        {
            GameObject a = NewSource("A");
            GameObject b = NewSource("B");
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { a, b };
            controller.spawnCount = 2;
            Transform first = Copy(controller, 0);
            Transform second = Copy(controller, 1);
            controller.spawnCount = 4;
            Assert.That(controller.transform.childCount, Is.EqualTo(4));
            Assert.That(Copy(controller, 0), Is.SameAs(first));
            Assert.That(Copy(controller, 1), Is.SameAs(second));
            Assert.That(Copy(controller, 2).name, Is.EqualTo("A(Clone)"));
            Assert.That(Copy(controller, 3).name, Is.EqualTo("B(Clone)"));
        }

        // set_spawnCount clamps negative counts to 0 and Remove 0x4a6ea0 destroys the copies past the count at once in the editor.
        [Test]
        public void LoweringTheSpawnCountDestroysTheExtraCopies()
        {
            GameObject a = NewSource("A");
            GameObject b = NewSource("B");
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { a, b };
            controller.spawnCount = 3;
            Transform first = Copy(controller, 0);
            controller.spawnCount = 1;
            Assert.That(controller.spawnCount, Is.EqualTo(1));
            Assert.That(controller.transform.childCount, Is.EqualTo(1));
            Assert.That(Copy(controller, 0), Is.SameAs(first));
            Assert.That(Spawned(controller).Length, Is.EqualTo(1));
            controller.spawnCount = -4;
            Assert.That(controller.spawnCount, Is.EqualTo(0));
            Assert.That(controller.transform.childCount, Is.EqualTo(0));
            Assert.That(Spawned(controller).Length, Is.EqualTo(0));
        }

        // set_spawnCount only spawns for the instantiating method; the children method just stores the count.
        [Test]
        public void SpawnCountIsOnlyStoredForTheChildrenMethod()
        {
            GameObject a = NewSource("A");
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { a };
            controller.objectMethod = ObjectController.ObjectMethod.GetChildren;
            controller.spawnCount = 5;
            Assert.That(controller.spawnCount, Is.EqualTo(5));
            Assert.That(controller.transform.childCount, Is.EqualTo(0));
            controller.spawnCount = -1;
            Assert.That(controller.spawnCount, Is.EqualTo(0));
        }

        // InstantiateSingle 0x4a8ab0 asks UnityEngine.Random.Range(0, sources) for every copy in the random iteration.
        [Test]
        public void RandomIterationPicksWithTheEngineRandom()
        {
            GameObject a = NewSource("A");
            GameObject b = NewSource("B");
            UnityEngine.Random.State saved = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7);
                string[] expected = new string[8];
                string[] inTurn = new string[8];
                for (int i = 0; i < expected.Length; i++)
                {
                    expected[i] = UnityEngine.Random.Range(0, 2) == 0 ? "A(Clone)" : "B(Clone)";
                    inTurn[i] = i % 2 == 0 ? "A(Clone)" : "B(Clone)";
                }

                Assert.That(expected, Is.Not.EqualTo(inTurn), "the seed does not draw the sources in turn");
                ObjectController controller = NewBareController(NewLine());
                controller.objects = new[] { a, b };
                controller.iteration = ObjectController.Iteration.Random;
                UnityEngine.Random.InitState(7);
                controller.spawnCount = 8;
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.That(Copy(controller, i).name, Is.EqualTo(expected[i]), "copy " + i);
                }
            }
            finally
            {
                UnityEngine.Random.state = saved;
            }
        }

        // InstantiateSingle 0x4a8ab0 compares the iteration with zero (cmp [rbx+0x1c4], 0 at 0x4a8b21, je to the in-turn pick), so
        // only Ordered picks in turn; any other stored value, including one outside the enum, draws with Random.Range.
        [Test]
        public void AnIterationOutsideTheEnumDrawsWithTheEngineRandom()
        {
            GameObject a = NewSource("A");
            GameObject b = NewSource("B");
            UnityEngine.Random.State saved = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7);
                string[] expected = new string[8];
                string[] inTurn = new string[8];
                for (int i = 0; i < expected.Length; i++)
                {
                    expected[i] = UnityEngine.Random.Range(0, 2) == 0 ? "A(Clone)" : "B(Clone)";
                    inTurn[i] = i % 2 == 0 ? "A(Clone)" : "B(Clone)";
                }

                Assert.That(expected, Is.Not.EqualTo(inTurn), "the seed does not draw the sources in turn");
                ObjectController controller = NewBareController(NewLine());
                controller.objects = new[] { a, b };
                controller.iteration = (ObjectController.Iteration)2;
                UnityEngine.Random.InitState(7);
                controller.spawnCount = 8;
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.That(Copy(controller, i).name, Is.EqualTo(expected[i]), "copy " + i);
                }
            }
            finally
            {
                UnityEngine.Random.state = saved;
            }
        }

        // Build 0x4a8e80 spreads the copies over the spline with percent i / (copies - 1) and PostBuild 0x4aa260 applies them.
        [Test]
        public void BuildPlacesTheCopiesEvenlyAlongTheSpline()
        {
            SplineComputer line = NewLine();
            ObjectController controller = NewController(line, 3, NewSource("A"));
            Near(Copy(controller, 0).position, new Vector3(0f, 0f, 0f), "percent 0");
            Near(Copy(controller, 1).position, new Vector3(10f, 0f, 0f), "percent 0.5");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 10f), "percent 1");
            NearRotation(Copy(controller, 0).rotation, line.Evaluate(0.0).rotation, "the first copy takes the sample rotation");
            NearRotation(Copy(controller, 2).rotation, line.Evaluate(1.0).rotation, "the last copy takes the sample rotation");
            Assert.That(Quaternion.Angle(Copy(controller, 0).rotation, Copy(controller, 2).rotation), Is.GreaterThan(45f), "the spline turns");
        }

        // The percent is i / (copies - 1) + evaluateOffset, wrapped once by +-1: 0.25, 0.75, 1.25 -> 0.25.
        [Test]
        public void EvaluateOffsetShiftsAndWrapsThePercents()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.evaluateOffset = 0.25f;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(5f, 0f, 0f), "0.25");
            Near(Copy(controller, 1).position, new Vector3(10f, 0f, 5f), "0.75");
            Near(Copy(controller, 2).position, new Vector3(5f, 0f, 0f), "1.25 wraps to 0.25");
            controller.evaluateOffset = -0.25f;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(10f, 0f, 5f), "-0.25 wraps to 0.75");
            Near(Copy(controller, 1).position, new Vector3(5f, 0f, 0f), "0.25");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 5f), "0.75");
        }

        // With fewer than two copies the percent stays 0 plus the offset (the i / (copies - 1) branch is skipped).
        [Test]
        public void ASingleCopyStandsAtTheEvaluateOffset()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 1;
            controller.evaluateOffset = 0.5f;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(10f, 0f, 0f), "percent 0.5");
        }

        // On a closed spline the percent is i / copies, so the last copy does not sit on the first.
        [Test]
        public void AClosedSplineDividesByTheCopyCount()
        {
            SplineComputer line = NewLine(true);
            ObjectController controller = NewController(line, 2, NewSource("A"));
            Near(Copy(controller, 0).position, line.Evaluate(0.0).position, "percent 0");
            Near(Copy(controller, 1).position, line.Evaluate(0.5).position, "percent 0.5 of the closed spline");
            Assert.That(Vector3.Distance(Copy(controller, 1).position, line.Evaluate(1.0).position), Is.GreaterThan(1f));
        }

        // The clip positioning evaluates the whole spline and hides the copies whose percent falls outside the clip range.
        [Test]
        public void ClipPositioningEvaluatesTheWholeSplineAndHidesCopiesOutsideTheClip()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.objectPositioning = ObjectController.Positioning.Clip;
            controller.clipFrom = 0.25;
            controller.clipTo = 0.75;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(0f, 0f, 0f), "percent 0 of the whole spline");
            Near(Copy(controller, 1).position, new Vector3(10f, 0f, 0f), "percent 0.5");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 10f), "percent 1");
            Assert.That(Copy(controller, 0).gameObject.activeSelf, Is.False, "before the clip");
            Assert.That(Copy(controller, 1).gameObject.activeSelf, Is.True, "inside the clip");
            Assert.That(Copy(controller, 2).gameObject.activeSelf, Is.False, "after the clip");
        }

        // The stretch positioning evaluates in the clipped range: percents 0, 0.5, 1 of 0.5..1 are 0.5, 0.75, 1 of the spline.
        [Test]
        public void StretchPositioningEvaluatesInTheClippedRange()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.clipFrom = 0.5;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(10f, 0f, 0f), "0.5");
            Near(Copy(controller, 1).position, new Vector3(10f, 0f, 5f), "0.75");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 10f), "1");
            Assert.That(Copy(controller, 0).gameObject.activeSelf, Is.True);
            Assert.That(Copy(controller, 2).gameObject.activeSelf, Is.True);
        }

        // The offset is added as is in world coordinates; otherwise it is Cross(forward, up) * x + up * y, both times the sample
        // size, and the z of the offset is dropped.
        [Test]
        public void OffsetIsAppliedInTheSampleFrameOrTheWorldFrame()
        {
            SplineComputer line = NewLine();
            SplineSample start = line.Evaluate(0.0);
            SplineSample end = line.Evaluate(1.0);
            Near(start.forward, Vector3.right, "start forward");
            Near(start.up, Vector3.up, "start up");
            Near(end.forward, Vector3.forward, "end forward");
            Assert.That(start.size, Is.EqualTo(1f));
            ObjectController controller = NewBareController(line);
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.minOffset = new Vector3(2f, 3f, 4f);
            controller.maxOffset = new Vector3(2f, 3f, 4f);
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(0f, 3f, 2f), "start: Cross(right, up) = +z carries x, up carries y");
            Near(Copy(controller, 1).position, new Vector3(8f, 3f, 10f), "end: Cross(forward, up) = -x carries x");
            controller.offsetUseWorldCoords = true;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(2f, 3f, 4f), "world");
            Near(Copy(controller, 1).position, new Vector3(12f, 3f, 14f), "world end");
        }

        // The offset limits are drawn x, y, z in turn from System.Random(seed): Lerp(min, max, NextDouble()).
        [Test]
        public void RandomOffsetsFollowTheSeededGenerator()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.randomSeed = 3;
            controller.offsetUseWorldCoords = true;
            controller.minOffset = new Vector3(0f, 0f, 0f);
            controller.maxOffset = new Vector3(4f, 6f, 8f);
            controller.RebuildImmediate();
            double[] r = Doubles(3, 9);
            Vector3[] samples = { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = new Vector3(Lerp(0f, 4f, r[3 * i]), Lerp(0f, 6f, r[3 * i + 1]), Lerp(0f, 8f, r[3 * i + 2]));
                Near(Copy(controller, i).position, samples[i] + offset, "copy " + i);
            }

            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, samples[0] + new Vector3(Lerp(0f, 4f, r[0]), Lerp(0f, 6f, r[1]), Lerp(0f, 8f, r[2])), "a rebuild reseeds the generators");
        }

        // The shell offset draws one angle per copy from System.Random(seed + 1): (cos * 0.5 * dx, sin * 0.5 * dy, 0).
        [Test]
        public void ShellOffsetPlacesTheCopiesOnTheEllipse()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.randomSeed = 5;
            controller.offsetUseWorldCoords = true;
            controller.shellOffset = true;
            controller.minOffset = new Vector3(1f, 1f, 1f);
            controller.maxOffset = new Vector3(5f, 9f, 9f);
            controller.RebuildImmediate();
            double[] r = Doubles(6, 3);
            Vector3[] samples = { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) };
            for (int i = 0; i < 3; i++)
            {
                float angle = (float)r[i] * 360f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.5f * 4f, Mathf.Sin(angle) * 0.5f * 8f, 0f);
                Near(Copy(controller, i).position, samples[i] + offset, "copy " + i);
            }
        }

        // The rotation is the sample rotation times Euler(Lerp per axis) with the draws x, y, z in turn from System.Random(seed + 2),
        // and applyRotation off leaves the copy turned the way the last build left it (as spawned if it was never built). The
        // fresh controller caches the play state, so its setters queue instead of building at once with applyRotation still on.
        [Test]
        public void RotationAppliesTheSampleRotationAndTheRandomEuler()
        {
            SplineComputer line = NewLine();
            ObjectController controller = NewBareController(line);
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.rotationOffset = new Vector3(0f, 90f, 0f);
            controller.RebuildImmediate();
            NearRotation(Copy(controller, 0).rotation, line.Evaluate(0.0).rotation * Quaternion.Euler(0f, 90f, 0f), "fixed euler");

            controller.minRotation = Vector3.zero;
            controller.maxRotation = new Vector3(90f, 180f, 270f);
            controller.randomSeed = 4;
            controller.RebuildImmediate();
            double[] r = Doubles(6, 6);
            for (int i = 0; i < 2; i++)
            {
                Quaternion euler = Quaternion.Euler(Lerp(0f, 90f, r[3 * i]), Lerp(0f, 180f, r[3 * i + 1]), Lerp(0f, 270f, r[3 * i + 2]));
                NearRotation(Copy(controller, i).rotation, line.Evaluate((double)i).rotation * euler, "random euler of copy " + i);
            }

            // Build 0x4a99b0 skips the only write to ObjectControl.rotation (0x4aa063) and the controls survive the builds, so
            // the copy keeps the rotation of the previous build.
            controller.applyRotation = false;
            controller.RebuildImmediate();
            Quaternion lastEuler = Quaternion.Euler(Lerp(0f, 90f, r[0]), Lerp(0f, 180f, r[1]), Lerp(0f, 270f, r[2]));
            NearRotation(Copy(controller, 0).rotation, line.Evaluate(0.0).rotation * lastEuler, "applyRotation off keeps the last built rotation");

            ObjectController fresh = NewBareController(NewLine());
            DreamteckPlayState.Cache(fresh);
            fresh.objects = new[] { NewSource("A") };
            fresh.spawnCount = 2;
            fresh.applyRotation = false;
            fresh.RebuildImmediate();
            NearRotation(Copy(fresh, 0).rotation, Quaternion.identity, "a copy never rotated stays as spawned");
        }

        // rotateByOffset replaces the sample rotation by LookRotation(sample forward, copy position - sample position).
        [Test]
        public void RotateByOffsetLooksAlongTheSampleForwardWithTheOffsetAsUp()
        {
            SplineComputer line = NewLine();
            ObjectController controller = NewBareController(line);
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.offsetUseWorldCoords = true;
            controller.minOffset = new Vector3(0f, 0f, 3f);
            controller.maxOffset = new Vector3(0f, 0f, 3f);
            controller.rotateByOffset = true;
            controller.RebuildImmediate();
            NearRotation(Copy(controller, 0).rotation, Quaternion.LookRotation(Vector3.right, new Vector3(0f, 0f, 3f)), "rolled onto the offset");
            controller.rotateByOffset = false;
            controller.RebuildImmediate();
            NearRotation(Copy(controller, 0).rotation, line.Evaluate(0.0).rotation, "plain sample rotation");
            Assert.That(Quaternion.Angle(Quaternion.LookRotation(Vector3.right, new Vector3(0f, 0f, 3f)), line.Evaluate(0.0).rotation), Is.GreaterThan(45f));
        }

        // applyScale off sets the base scale back; on, the scale is Scale(baseScale * size, multiplier) with min == max skipping the draws.
        [Test]
        public void ScaleMultipliesTheBaseScaleBySizeAndTheMultiplier()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A", new Vector3(2f, 2f, 2f)) };
            controller.spawnCount = 2;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).localScale, new Vector3(2f, 2f, 2f), "applyScale off keeps the base scale");
            controller.applyScale = true;
            controller.minScaleMultiplier = new Vector3(1f, 3f, 1f);
            controller.maxScaleMultiplier = new Vector3(1f, 3f, 1f);
            controller.RebuildImmediate();
            Near(Copy(controller, 0).localScale, new Vector3(2f, 6f, 2f), "base * size * multiplier");
            Near(Copy(controller, 1).localScale, new Vector3(2f, 6f, 2f), "every copy");
        }

        // The scale multiplier is drawn from System.Random(seed + 3): one draw per copy for the uniform lerp, x, y, z in turn per
        // copy otherwise.
        [Test]
        public void RandomScalesFollowTheSeededGenerator()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A", new Vector3(2f, 2f, 2f)) };
            controller.spawnCount = 2;
            controller.applyScale = true;
            controller.randomSeed = 9;
            controller.minScaleMultiplier = new Vector3(1f, 1f, 1f);
            controller.maxScaleMultiplier = new Vector3(3f, 5f, 7f);
            controller.RebuildImmediate();
            double[] r = Doubles(12, 6);
            for (int i = 0; i < 2; i++)
            {
                Vector3 expected = new Vector3(2f * Lerp(1f, 3f, r[i]), 2f * Lerp(1f, 5f, r[i]), 2f * Lerp(1f, 7f, r[i]));
                Near(Copy(controller, i).localScale, expected, "uniform copy " + i);
            }

            controller.uniformScaleLerp = false;
            controller.RebuildImmediate();
            for (int i = 0; i < 2; i++)
            {
                Vector3 expected = new Vector3(2f * Lerp(1f, 3f, r[3 * i]), 2f * Lerp(1f, 5f, r[3 * i + 1]), 2f * Lerp(1f, 7f, r[3 * i + 2]));
                Near(Copy(controller, i).localScale, expected, "per axis copy " + i);
            }
        }

        // The custom rules answer the offset (SineRule: up * sine * amplitude) and the rotation (SpiralRule: sample rotation turned
        // back by revolve * percent) instead of the random ranges.
        [Test]
        public void CustomRulesDriveOffsetAndRotation()
        {
            SplineComputer line = NewLine();
            ObjectControllerSineRule sine = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            ObjectControllerSpiralRule spiral = ScriptableObject.CreateInstance<ObjectControllerSpiralRule>();
            _owned.Add(sine);
            _owned.Add(spiral);
            sine.amplitude = 2f;
            sine.offset = 0.5f;
            spiral.revolve = 90f;
            ObjectController controller = NewBareController(line);
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.customOffsetRule = sine;
            controller.customRotationRule = spiral;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(0f, 2f, 0f), "sine(0.5 pi) = 1 lifts the first copy by the amplitude");
            Near(Copy(controller, 1).position, new Vector3(10f, -2f, 10f), "sine(1.5 pi) = -1 lowers the last copy");
            NearRotation(Copy(controller, 0).rotation, line.Evaluate(0.0).rotation, "spiral at percent 0");
            NearRotation(Copy(controller, 1).rotation, line.Evaluate(1.0).rotation * Quaternion.AngleAxis(-90f, Vector3.forward), "spiral at percent 1");
        }

        // Build 0x4a8e80 sets the context on the scale rule but reads GetScale from the offset rule; with no offset rule the
        // build faults (RebuildImmediate logs the message). The play state is cached so that the setters queue: the editor
        // compile would build at once from the customOffsetRule setter and log the fault a second time.
        [Test]
        public void CustomScaleReadsTheOffsetRuleAsShipped()
        {
            ObjectControllerSineRule scaleRule = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            ObjectControllerSineRule offsetRule = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            _owned.Add(scaleRule);
            _owned.Add(offsetRule);
            scaleRule.minScale = 5f;
            scaleRule.maxScale = 5f;
            offsetRule.minScale = 3f;
            offsetRule.maxScale = 3f;
            ObjectController controller = DreamteckPlayState.Cache(NewBareController(NewLine()));
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.applyScale = true;
            controller.customScaleRule = scaleRule;
            controller.customOffsetRule = offsetRule;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).localScale, new Vector3(3f, 3f, 3f), "the offset rule's scale, not the scale rule's");

            controller.customOffsetRule = null;
            LogAssert.Expect(LogType.Error, new Regex("Object reference not set"));
            controller.RebuildImmediate();
        }

        // With the custom distance on, each copy is placed where the previous one travelled to: Lerp(min, max, draw) units further.
        [Test]
        public void CustomObjectDistanceTravelsAlongTheSpline()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            Field("_useCustomObjectDistance").SetValue(controller, true);
            controller.minObjectDistance = 5f;
            controller.maxObjectDistance = 5f;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(0f, 0f, 0f), "start");
            Near(Copy(controller, 1).position, new Vector3(5f, 0f, 0f), "5 units");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 0f), "10 units");

            controller.objectPositioning = ObjectController.Positioning.Clip;
            controller.RebuildImmediate();
            Near(Copy(controller, 1).position, new Vector3(5f, 0f, 0f), "clip positioning travels on the computer");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 0f), "clip 10 units");
        }

        // The local-frame offset and the scale both multiply by the sample size (0x4a9adb..0x4a9b07, 0x4a944c/0x4a96a6); the
        // world-frame offset does not. The line here has size 2 at every point.
        [Test]
        public void SampleSizeScalesTheLocalOffsetAndTheScaleButNotTheWorldOffset()
        {
            SplineComputer line = NewLine(false, 2f);
            Assert.That(line.Evaluate(0.0).size, Is.EqualTo(2f));
            ObjectController controller = NewBareController(line);
            controller.objects = new[] { NewSource("A", new Vector3(2f, 2f, 2f)) };
            controller.spawnCount = 2;
            controller.minOffset = new Vector3(2f, 3f, 4f);
            controller.maxOffset = new Vector3(2f, 3f, 4f);
            controller.applyScale = true;
            controller.minScaleMultiplier = new Vector3(1f, 3f, 1f);
            controller.maxScaleMultiplier = new Vector3(1f, 3f, 1f);
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(0f, 6f, 4f), "start: x 2 along +z and y 3 along up, both times size 2");
            Near(Copy(controller, 1).position, new Vector3(6f, 6f, 10f), "end: x 2 along -x times size 2");
            Near(Copy(controller, 0).localScale, new Vector3(4f, 12f, 4f), "base 2 * size 2 * multiplier");
            controller.offsetUseWorldCoords = true;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).position, new Vector3(2f, 3f, 4f), "world offset is not scaled");
        }

        // The rule's own context is set after the scale was read (0x4a98f3 against 0x4a93be), so the scale comes from the
        // offset rule's context of the previous copy, or of the previous build for the first one, and is stored as is: no base
        // scale, no sample size. The play state is cached so that the setters queue: immediate editor builds from the setters
        // would leave a different stale context before the first RebuildImmediate.
        [Test]
        public void CustomScaleUsesTheStaleContextOfTheOffsetRule()
        {
            ObjectControllerSineRule scaleRule = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            ObjectControllerSineRule offsetRule = ScriptableObject.CreateInstance<ObjectControllerSineRule>();
            _owned.Add(scaleRule);
            _owned.Add(offsetRule);
            offsetRule.minScale = 1f;
            offsetRule.maxScale = 3f;
            offsetRule.offset = 0.5f;
            ObjectController controller = DreamteckPlayState.Cache(NewBareController(NewLine()));
            controller.objects = new[] { NewSource("A", new Vector3(2f, 2f, 2f)) };
            controller.spawnCount = 2;
            controller.applyScale = true;
            controller.customScaleRule = scaleRule;
            controller.customOffsetRule = offsetRule;
            controller.RebuildImmediate();
            Near(Copy(controller, 0).localScale, new Vector3(3f, 3f, 3f), "a fresh context: percent -0, sin(0.5 pi) = 1");
            Near(Copy(controller, 1).localScale, new Vector3(3f, 3f, 3f), "copy 1 reads the context of copy 0, not its own (own would be sin(1.5 pi) = -1, 1)");
            controller.RebuildImmediate();
            Near(Copy(controller, 0).localScale, new Vector3(1f, 1f, 1f), "copy 0 reads the context copy 1 left in the last build");
            Near(Copy(controller, 1).localScale, new Vector3(3f, 3f, 3f), "copy 1 reads the context of copy 0");
        }

        // The distance draws come from System.Random(seed + 4), one per copy, and Lerp(min, max, draw) units are travelled from the
        // previous copy: seed 3 gives System.Random(7).
        [Test]
        public void RandomObjectDistancesFollowTheSeededGenerator()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.randomSeed = 3;
            Field("_useCustomObjectDistance").SetValue(controller, true);
            controller.minObjectDistance = 2f;
            controller.maxObjectDistance = 6f;
            controller.RebuildImmediate();
            double[] d = Doubles(7, 2);
            float first = Lerp(2f, 6f, d[0]);
            float second = first + Lerp(2f, 6f, d[1]);
            Near(Copy(controller, 0).position, Vector3.zero, "start");
            Near(Copy(controller, 1).position, OnLine(first), "first draw");
            Near(Copy(controller, 2).position, OnLine(second), "first and second draw");
            Assert.That(Vector3.Distance(OnLine(first), OnLine(5f)), Is.GreaterThan(Tolerance), "the draw is not the constant 5");
        }

        // The position at a distance along the polyline (0,0,0) -> (10,0,0) -> (10,0,10).
        private static Vector3 OnLine(float distance)
        {
            return distance <= 10f ? new Vector3(distance, 0f, 0f) : new Vector3(10f, 0f, distance - 10f);
        }

        // In the clip positioning the distance is travelled with the computer's own Travel (0x4a9124), which does not clip the
        // start: from 0 by 5 units it gives 0.25 where SplineUser.Travel (0x4aa100) would start at the clipped 0.5.
        [Test]
        public void ClipPositioningTravelsOnTheComputerNotTheClippedUser()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            Field("_useCustomObjectDistance").SetValue(controller, true);
            controller.minObjectDistance = 5f;
            controller.maxObjectDistance = 5f;
            controller.objectPositioning = ObjectController.Positioning.Clip;
            controller.clipFrom = 0.5;
            controller.RebuildImmediate();
            Near(Copy(controller, 1).position, new Vector3(5f, 0f, 0f), "percent 0.25");
            Assert.That(Copy(controller, 1).gameObject.activeSelf, Is.False, "0.25 is before the clip");
            Near(Copy(controller, 2).position, new Vector3(10f, 0f, 0f), "percent 0.5");
            Assert.That(Copy(controller, 2).gameObject.activeSelf, Is.True, "0.5 is inside the clip");
        }

        // ObjectControl.active is written only by the clip positioning (0x4a9f12): a copy hidden by the clip stays hidden after
        // the switch to the stretch positioning.
        [Test]
        public void StretchPositioningLeavesHiddenCopiesHidden()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 3;
            controller.objectPositioning = ObjectController.Positioning.Clip;
            controller.clipFrom = 0.25;
            controller.clipTo = 0.75;
            controller.RebuildImmediate();
            Assert.That(Copy(controller, 0).gameObject.activeSelf, Is.False);
            controller.objectPositioning = ObjectController.Positioning.Stretch;
            controller.RebuildImmediate();
            Assert.That(Copy(controller, 0).gameObject.activeSelf, Is.False, "stretch does not show it again");
            Assert.That(Copy(controller, 1).gameObject.activeSelf, Is.True);
            Assert.That(Copy(controller, 2).gameObject.activeSelf, Is.False, "stretch does not show it again");
        }

        // InstantiateAll 0x4a8980 counts its attempts from the initial length and InstantiateSingle 0x4a8ab0 returns on a null
        // source: {A, null} with a count of 3 makes one copy (the attempts for the null slot and the next one both return).
        [Test]
        public void ANullSourceEndsAnAttemptWithoutACopy()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A"), null };
            controller.spawnCount = 3;
            Assert.That(controller.transform.childCount, Is.EqualTo(1));
            Assert.That(Spawned(controller).Length, Is.EqualTo(1));
            Assert.That(Copy(controller, 0).name, Is.EqualTo("A(Clone)"));
        }

        // The rotate-by-offset rotation still multiplies by the Euler of the rotation limits (0x4a9e24).
        [Test]
        public void RotateByOffsetKeepsTheEulerFactor()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            controller.offsetUseWorldCoords = true;
            controller.minOffset = new Vector3(0f, 0f, 3f);
            controller.maxOffset = new Vector3(0f, 0f, 3f);
            controller.rotateByOffset = true;
            controller.rotationOffset = new Vector3(0f, 0f, 45f);
            controller.RebuildImmediate();
            NearRotation(Copy(controller, 0).rotation, Quaternion.LookRotation(Vector3.right, new Vector3(0f, 0f, 3f)) * Quaternion.Euler(0f, 0f, 45f), "look rotation times the euler");
        }

        // The vector setters compare with Vector3 inequality (squared distance against 1e-10, 0x4a73d2..0x4a73dd) and the
        // rotationOffset setter does so against both limits (0x4a76b5/0x4a76f6): a difference of 1e-6 queues no rebuild (play
        // state cached, see SettersQueueARebuildOnlyForChanges).
        [Test]
        public void NearlyEqualVectorsQueueNoRebuild()
        {
            ObjectController controller = DreamteckPlayState.Cache(NewBareController(NewLine()));
            controller.RebuildImmediate();
            int builds = 0;
            controller.onPostBuild += delegate { builds++; };
            Vector3 tiny = new Vector3(1e-6f, 0f, 0f);
            Action[] nearly =
            {
                () => controller.minOffset = tiny,
                () => controller.maxOffset = tiny,
                () => controller.minRotation = tiny,
                () => controller.minScaleMultiplier = Vector3.one + tiny,
                () => controller.rotationOffset = tiny
            };
            for (int i = 0; i < nearly.Length; i++)
            {
                nearly[i]();
                Call(controller, "Update");
                Assert.That(builds, Is.EqualTo(0), "setter " + i);
            }

            controller.minOffset = new Vector3(1e-3f, 0f, 0f);
            Call(controller, "Update");
            Assert.That(builds, Is.EqualTo(1), "a larger difference does");
        }

        // set_objectMethod 0x4a6cf0 and GetAll 0x4a8150: the children become the controlled objects, in order, remembering their
        // scale, and only the switch to the children method spawns.
        [Test]
        public void GetChildrenTakesTheChildrenOver()
        {
            ObjectController controller = NewBareController(NewLine());
            Transform[] children = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject child = NewObject("child" + i);
                child.transform.SetParent(controller.transform, false);
                child.transform.localScale = i == 1 ? new Vector3(2f, 2f, 2f) : Vector3.one;
                children[i] = child.transform;
            }

            controller.objectMethod = ObjectController.ObjectMethod.GetChildren;
            Array spawned = Spawned(controller);
            Assert.That(spawned.Length, Is.EqualTo(3));
            FieldInfo gameObjectField = ControlType().GetField("gameObject");
            FieldInfo baseScaleField = ControlType().GetField("baseScale");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(gameObjectField.GetValue(spawned.GetValue(i)), Is.SameAs(children[i].gameObject), "control " + i);
            }

            Assert.That((Vector3)baseScaleField.GetValue(spawned.GetValue(1)), Is.EqualTo(new Vector3(2f, 2f, 2f)));
            controller.RebuildImmediate();
            Near(children[0].position, new Vector3(0f, 0f, 0f), "child 0");
            Near(children[1].position, new Vector3(10f, 0f, 0f), "child 1");
            Near(children[2].position, new Vector3(10f, 0f, 10f), "child 2");
            controller.objectMethod = ObjectController.ObjectMethod.Instantiate;
            Assert.That(Spawned(controller), Is.SameAs(spawned), "switching away does not spawn");
        }

        // GetAll 0x4a8150 gates the search for an existing control on the new array's slot (0x4a83a7), which is always empty, so
        // every collect builds new controls and re-reads the scale a previous Apply already wrote.
        [Test]
        public void GetAllRecreatesTheControlsAndRereadsTheBaseScale()
        {
            ObjectController controller = NewBareController(NewLine());
            GameObject child = NewObject("child");
            child.transform.SetParent(controller.transform, false);
            controller.objectMethod = ObjectController.ObjectMethod.GetChildren;
            controller.applyScale = true;
            controller.minScaleMultiplier = new Vector3(3f, 3f, 3f);
            controller.maxScaleMultiplier = new Vector3(3f, 3f, 3f);
            controller.RebuildImmediate();
            Near(child.transform.localScale, new Vector3(3f, 3f, 3f), "scaled once");
            object first = Spawned(controller).GetValue(0);

            NewObject("second").transform.SetParent(controller.transform, false);
            Call(controller, "LateRun");
            object again = Spawned(controller).GetValue(0);
            Assert.That(again, Is.Not.SameAs(first), "known children get a new control");
            Assert.That((Vector3)ControlType().GetField("baseScale").GetValue(again), Is.EqualTo(new Vector3(3f, 3f, 3f)));
            controller.RebuildImmediate();
            Near(child.transform.localScale, new Vector3(9f, 9f, 9f), "the applied scale compounds");
        }

        // Clear 0x4a7cb0 restores the base scale of every controlled object, deactivates taken over children, and for copies
        // destroys the copies. The editor text does that with DestroyImmediate while not playing; the player text after the
        // #if UNITY_EDITOR block calls Destroy, which edit mode refuses, so that branch cannot run in EditMode (shipped
        // coverage of the deferred Destroy is not reachable here).
        [Test]
        public void ClearGivesTheBaseScaleBackAndHidesOrDestroys()
        {
            ObjectController children = NewBareController(NewLine());
            GameObject child = NewObject("child");
            child.transform.SetParent(children.transform, false);
            child.transform.localScale = new Vector3(2f, 2f, 2f);
            children.objectMethod = ObjectController.ObjectMethod.GetChildren;
            children.applyScale = true;
            children.minScaleMultiplier = new Vector3(3f, 3f, 3f);
            children.maxScaleMultiplier = new Vector3(3f, 3f, 3f);
            children.RebuildImmediate();
            Near(child.transform.localScale, new Vector3(6f, 6f, 6f), "scaled by the multiplier");
            children.Clear();
            Near(child.transform.localScale, new Vector3(2f, 2f, 2f), "base scale back");
            Assert.That(child.activeSelf, Is.False, "taken over children are hidden");
            Assert.That(Spawned(children).Length, Is.EqualTo(0));

            ObjectController copies = NewController(NewLine(), 2, NewSource("A", new Vector3(2f, 2f, 2f)));
            copies.applyScale = true;
            copies.minScaleMultiplier = new Vector3(3f, 3f, 3f);
            copies.maxScaleMultiplier = new Vector3(3f, 3f, 3f);
            copies.RebuildImmediate();
            Near(Copy(copies, 0).localScale, new Vector3(6f, 6f, 6f), "scaled copy");
            Transform firstCopy = Copy(copies, 0);
            copies.Clear();
            Assert.That(Spawned(copies).Length, Is.EqualTo(0));
            Assert.That(copies.transform.childCount, Is.EqualTo(0), "the editor text destroys the copies at once while not playing");
            Assert.That(firstCopy == null, Is.True);
        }

        // LateRun 0x4a8a20 takes the children over again whenever their number differs from the last count, and only for the
        // children method.
        [Test]
        public void LateRunCollectsChangedChildrenAgain()
        {
            ObjectController controller = NewBareController(NewLine());
            for (int i = 0; i < 2; i++)
            {
                NewObject("child" + i).transform.SetParent(controller.transform, false);
            }

            Call(controller, "LateRun");
            Assert.That((int)Field("lastChildCount").GetValue(controller), Is.EqualTo(0), "instantiate method ignores the children");

            controller.objectMethod = ObjectController.ObjectMethod.GetChildren;
            Call(controller, "LateRun");
            Assert.That((int)Field("lastChildCount").GetValue(controller), Is.EqualTo(2));
            Assert.That(Spawned(controller).Length, Is.EqualTo(2));
            Array before = Spawned(controller);
            Call(controller, "LateRun");
            Assert.That(Spawned(controller), Is.SameAs(before), "an unchanged count does not spawn");

            NewObject("child2").transform.SetParent(controller.transform, false);
            Call(controller, "LateRun");
            Assert.That(Spawned(controller).Length, Is.EqualTo(3));
            Assert.That((int)Field("lastChildCount").GetValue(controller), Is.EqualTo(3));
        }

        // Build 0x4a8e80 clears and spawns again when it meets an empty slot in the spawned array.
        [Test]
        public void ABuildWithAnEmptySlotRespawns()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.spawnCount = 2;
            Field("spawned").SetValue(controller, Array.CreateInstance(ControlType(), 2));
            controller.RebuildImmediate();
            Assert.That(Spawned(controller).Length, Is.EqualTo(2));
            Assert.That(Spawned(controller).GetValue(0), Is.Not.Null);
            Assert.That(controller.transform.childCount, Is.EqualTo(4), "two orphaned copies and two new ones");
        }

        // Spawn 0x4a6d20 only starts the coroutine while playing; in edit mode a delayed spawn instantiates at once.
        [Test]
        public void DelayedSpawnInstantiatesAtOnceOutsidePlayMode()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            controller.delayedSpawn = true;
            controller.spawnCount = 2;
            Assert.That(controller.transform.childCount, Is.EqualTo(2));
        }

        // MoveNext 0x4aa6c0: the loop runs from the copies so far up to and including the spawn count, one copy and one
        // WaitForSeconds per step, and Reset throws.
        [Test]
        public void DelayedSpawnCoroutineRunsUpToAndIncludingTheSpawnCount()
        {
            ObjectController controller = NewBareController(NewLine());
            controller.objects = new[] { NewSource("A") };
            Field("_spawnCount").SetValue(controller, 2);
            IEnumerator routine = (IEnumerator)Call(controller, "InstantiateAllWithDelay");
            int steps = 0;
            while (routine.MoveNext())
            {
                steps++;
                Assert.That(routine.Current, Is.TypeOf<WaitForSeconds>());
                Assert.That(controller.transform.childCount, Is.EqualTo(steps));
            }

            Assert.That(steps, Is.EqualTo(3));
            Assert.Throws<NotSupportedException>(() => routine.Reset());

            ObjectController withoutSpline = NewBareController();
            withoutSpline.objects = new[] { NewSource("B") };
            Assert.That(((IEnumerator)Call(withoutSpline, "InstantiateAllWithDelay")).MoveNext(), Is.False, "no spline");
            ObjectController withoutSources = NewBareController(NewLine());
            Assert.That(((IEnumerator)Call(withoutSources, "InstantiateAllWithDelay")).MoveNext(), Is.False, "no sources");
        }

        private static object NewControl(GameObject target)
        {
            return Activator.CreateInstance(ControlType(), new object[] { target });
        }

        private static T Get<T>(object control, string field)
        {
            return (T)ControlType().GetField(field).GetValue(control);
        }

        // ObjectControl ctor 0x4a87e0 and Apply 0x4aa2b0.
        [Test]
        public void ObjectControlKeepsItsDefaultsAndApplies()
        {
            GameObject target = NewObject("target");
            target.transform.localScale = new Vector3(2f, 3f, 4f);
            object control = NewControl(target);
            Assert.That(Get<GameObject>(control, "gameObject"), Is.SameAs(target));
            Assert.That(Get<Vector3>(control, "position"), Is.EqualTo(Vector3.zero));
            Assert.That(Get<Quaternion>(control, "rotation"), Is.EqualTo(Quaternion.identity));
            Assert.That(Get<Vector3>(control, "scale"), Is.EqualTo(Vector3.one));
            Assert.That(Get<bool>(control, "active"), Is.True);
            Assert.That(Get<Vector3>(control, "baseScale"), Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That((bool)ControlType().GetProperty("isNull").GetValue(control), Is.False);
            Assert.That(ControlType().GetProperty("transform").GetValue(control), Is.SameAs(target.transform));

            ControlType().GetField("position").SetValue(control, new Vector3(1f, 2f, 3f));
            ControlType().GetField("rotation").SetValue(control, Quaternion.Euler(0f, 90f, 0f));
            ControlType().GetField("scale").SetValue(control, new Vector3(5f, 5f, 5f));
            ControlType().GetField("active").SetValue(control, false);
            Call(control, "Apply");
            Near(target.transform.position, new Vector3(1f, 2f, 3f), "position");
            NearRotation(target.transform.rotation, Quaternion.Euler(0f, 90f, 0f), "rotation");
            Near(target.transform.localScale, new Vector3(5f, 5f, 5f), "scale");
            Assert.That(target.activeSelf, Is.False);
        }

        // The ObjectControl members check the game object first: a destroyed one reads as null, and Apply, Destroy and
        // DestroyImmediate quietly do nothing; DestroyImmediate 0x4a80c0 destroys a live one.
        [Test]
        public void ObjectControlSurvivesADestroyedObject()
        {
            GameObject target = NewObject("target");
            object control = NewControl(target);
            Call(control, "DestroyImmediate");
            Assert.That(target == null, Is.True, "destroyed");
            Assert.That((bool)ControlType().GetProperty("isNull").GetValue(control), Is.True);
            object transform = ControlType().GetProperty("transform").GetValue(control);
            Assert.That(transform == null, Is.True, "no transform for a destroyed object");
            Assert.DoesNotThrow(() => Call(control, "Apply"));
            Assert.DoesNotThrow(() => Call(control, "Destroy"));
            Assert.DoesNotThrow(() => Call(control, "DestroyImmediate"));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            // The editor compile adds _retainPrefabInstancesInEditor after _spawnCount (#if UNITY_EDITOR in the text; absent from
            // the shipped layout and the player-define compile) and shifts the iterator ordinal d__116 of the DummyDll to d__120.
            Assert.That(typeof(ObjectController).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "objects", "_evaluateOffset", "_spawnCount", "_retainPrefabInstancesInEditor", "_objectPositioning", "_iteration", "_randomSeed", "_minOffset",
                "_maxOffset", "_offsetUseWorldCoords", "_minRotation", "_maxRotation", "_uniformScaleLerp", "_minScaleMultiplier",
                "_maxScaleMultiplier", "_shellOffset", "_applyRotation", "_rotateByOffset", "_applyScale", "_objectMethod",
                "delayedSpawn", "spawnDelay", "lastChildCount", "spawned", "_useCustomObjectDistance", "_minObjectDistance",
                "_maxObjectDistance", "_customOffsetRule", "_customRotationRule", "_customScaleRule", "offsetRandomizer",
                "shellRandomizer", "rotationRandomizer", "scaleRandomizer", "distanceRandomizer"
            }));
            Assert.That(typeof(ObjectController).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "ObjectControl", "ObjectMethod", "Positioning", "Iteration", "<InstantiateAllWithDelay>d__120" }));
            Assert.That(typeof(ObjectController).BaseType, Is.EqualTo(typeof(SplineUser)));
            Assert.That(ControlType().IsNestedAssembly, Is.True);
            Assert.That(ControlType().IsSerializable, Is.True);
            Assert.That(ControlType().GetFields().Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "gameObject", "position", "rotation", "scale", "active", "baseScale" }));
            Assert.That((int)ObjectController.ObjectMethod.GetChildren, Is.EqualTo(1));
            Assert.That((int)ObjectController.Positioning.Clip, Is.EqualTo(1));
            Assert.That((int)ObjectController.Iteration.Random, Is.EqualTo(1));
            Assert.That(typeof(ObjectController).GetCustomAttribute<AddComponentMenu>().componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Object Controller"));
            Assert.That(Field("spawned").GetCustomAttribute<SerializeField>(), Is.Not.Null);
            Assert.That(Field("spawned").GetCustomAttribute<HideInInspector>(), Is.Not.Null);
            Assert.That(Field("delayedSpawn").GetCustomAttribute<HideInInspector>(), Is.Not.Null);
            Assert.That(Field("delayedSpawn").GetCustomAttribute<SerializeField>(), Is.Null);
        }
    }
}
