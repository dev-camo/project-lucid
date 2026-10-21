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
    // The shipped (player) branches of Dreamteck.Splines.ObjectController, run in play mode where the engine itself caches
    // the play state: a setter queues one rebuild that Update runs and reports through onPostBuild, and Clear destroys the
    // copies with the deferred Destroy. The same SplineComputer polyline as OriginalDreamteckObjectControllerTests, whose
    // EditMode tests cover the editor branches of the same methods (the editor text rebuilds at once from every setter).
    // The sources are scene objects; the editor-only retainPrefabInstancesInEditor branch of InstantiateSingle is skipped
    // while playing. No frame passes inside a synchronous test, so the engine does not run Update in between.
    public sealed class OriginalDreamteckObjectControllerPlayModeTests
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

        // The Linear polyline (0,0,0) -> (10,0,0) -> (10,0,10), built once.
        private SplineComputer NewLine()
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
                points[i].size = 1f;
            }

            computer.SetPoints(points);
            computer.RebuildImmediate();
            return computer;
        }

        private ObjectController NewBareController(SplineComputer line = null)
        {
            ObjectController controller = NewObject("controller").AddComponent<ObjectController>();
            if (line != null)
            {
                controller.spline = line;
            }

            return controller;
        }

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

        private static Array Spawned(ObjectController controller)
        {
            return (Array)typeof(ObjectController).GetField("spawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(controller);
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

        // Every setter 0x4a72c0..0x4a7c10 returns without a rebuild for an equal value and queues exactly one for a new one; the
        // vector setters compare with Vector3 equality, the rotationOffset setter with both limits. Covers the shipped queue; the
        // EditMode test of the same name is not kept because the editor text rebuilds at once instead.
        [Test]
        public void SettersQueueARebuildOnlyForChanges()
        {
            SplineComputer line = NewLine();
            ObjectController controller = NewBareController(line);
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

        // The rotation is the sample rotation times Euler(Lerp per axis) with the draws x, y, z in turn from System.Random(seed + 2),
        // and applyRotation off leaves the copy turned the way the last build left it (as spawned if it was never built).
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
            fresh.objects = new[] { NewSource("A") };
            fresh.spawnCount = 2;
            fresh.applyRotation = false;
            fresh.RebuildImmediate();
            NearRotation(Copy(fresh, 0).rotation, Quaternion.identity, "a copy never rotated stays as spawned");
        }

        // Build 0x4a8e80 sets the context on the scale rule but reads GetScale from the offset rule; with no offset rule the
        // build faults (RebuildImmediate logs the message).
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
            ObjectController controller = NewBareController(NewLine());
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

        // The rule's own context is set after the scale was read (0x4a98f3 against 0x4a93be), so the scale comes from the
        // offset rule's context of the previous copy, or of the previous build for the first one, and is stored as is: no base
        // scale, no sample size.
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
            ObjectController controller = NewBareController(NewLine());
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

        // The vector setters compare with Vector3 inequality (squared distance against 1e-10, 0x4a73d2..0x4a73dd) and the
        // rotationOffset setter does so against both limits (0x4a76b5/0x4a76f6): a difference of 1e-6 queues no rebuild.
        [Test]
        public void NearlyEqualVectorsQueueNoRebuild()
        {
            ObjectController controller = NewBareController(NewLine());
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

        // Clear 0x4a7cb0 restores the base scale of every controlled object, deactivates taken over children, and for copies
        // calls the deferred Destroy: the copies stand until the end of the frame (the EditMode test of the same name covers the
        // editor branch, which destroys them at once while not playing).
        [UnityTest]
        public IEnumerator ClearGivesTheBaseScaleBackAndHidesOrDestroys()
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
            Transform secondCopy = Copy(copies, 1);
            copies.Clear();
            Assert.That(Spawned(copies).Length, Is.EqualTo(0));
            Assert.That(copies.transform.childCount, Is.EqualTo(2), "Destroy is deferred: the copies stand until the end of the frame");
            Near(firstCopy.localScale, new Vector3(2f, 2f, 2f), "base scale back");
            yield return null;
            Assert.That(firstCopy == null, Is.True, "the first copy is gone after the frame");
            Assert.That(secondCopy == null, Is.True, "the second copy is gone after the frame");
            Assert.That(copies.transform.childCount, Is.EqualTo(0));
        }
    }
}
