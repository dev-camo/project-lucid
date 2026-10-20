using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineUser, driven through a SplineComputer that holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10) (cached samples at percents 0, 0.5, 1). Expected values are hand arithmetic from the
    // native bodies cited per test. The users are plain SplineUser components on owned game objects, never multithreaded, and
    // TearDown destroys everything the test made.
    public sealed class OriginalDreamteckSplineUserTests
    {
        private const float Tolerance = 1e-4f;

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

        private SplineComputer NewLine(bool closed = false)
        {
            GameObject owner = new GameObject("line");
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            if (closed)
            {
                computer.Close();
            }

            computer.RebuildImmediate();
            return computer;
        }

        private SplineUser NewUser(SplineComputer computer = null)
        {
            GameObject owner = new GameObject("user");
            _owned.Add(owner);
            SplineUser user = owner.AddComponent<SplineUser>();
            if (computer != null)
            {
                user.spline = computer;
            }

            return user;
        }

        private static void Call(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, null);
        }

        // Field initialisers of the ctor 0x4cbb40 and the Awake 0x4cd790 that fetches the (absent) samples in edit mode.
        [Test]
        public void FreshUserKeepsTheShippedDefaults()
        {
            SplineUser user = NewUser();
            Assert.That(user.spline == null, Is.True);
            Assert.That(user.autoUpdate, Is.True);
            Assert.That(user.updateMethod, Is.EqualTo(SplineUser.UpdateMethod.Update));
            Assert.That(user.clipFrom, Is.EqualTo(0.0));
            Assert.That(user.clipTo, Is.EqualTo(1.0));
            Assert.That(user.loopSamples, Is.False);
            Assert.That(user.span, Is.EqualTo(1.0));
            Assert.That(user.samplesAreLooped, Is.False);
            Assert.That(user.sampleCount, Is.EqualTo(0));
            Assert.That(user.buildOnAwake, Is.True);
            Assert.That(user.buildOnEnable, Is.False);
            Assert.That(user.multithreaded, Is.False);
            Assert.That(user.rotationModifier, Is.Not.Null);
            Assert.That(user.offsetModifier, Is.Not.Null);
            Assert.That(user.colorModifier, Is.Not.Null);
            Assert.That(user.sizeModifier, Is.Not.Null);
            Assert.That(user is ISerializationCallbackReceiver, Is.True);
            Assert.That(user.ClipPercent(0.5), Is.EqualTo(0.0), "without samples the clipped percent is 0");
        }

        // spline setter 0x4ce0f0: the subscription moves with the computer, an equal value does nothing.
        [Test]
        public void ChangingTheSplineMovesTheSubscription()
        {
            SplineComputer first = NewLine();
            SplineComputer second = NewLine();
            SplineUser user = NewUser(first);
            Assert.That(first.IsSubscribed(user), Is.True);
            user.spline = first;
            Assert.That(first.subscriberCount, Is.EqualTo(1));
            user.spline = second;
            Assert.That(first.IsSubscribed(user), Is.False);
            Assert.That(second.IsSubscribed(user), Is.True);
            user.spline = null;
            Assert.That(second.IsSubscribed(user), Is.False);
            Assert.That(user.spline == null, Is.True);
        }

        // clipFrom/clipTo setters 0x4ce240 / 0x4ce2f0: clamped to 0..1, and on an open computer the other end is pushed along.
        // SetClipRange 0x4cf5e0 raises the end to the start instead.
        [Test]
        public void ClipSettersClampAndPushTheOtherEnd()
        {
            SplineUser user = NewUser(NewLine());
            user.clipFrom = 0.4;
            Assert.That(user.clipFrom, Is.EqualTo(0.4));
            user.clipTo = 0.3;
            Assert.That(user.clipTo, Is.EqualTo(0.3));
            Assert.That(user.clipFrom, Is.EqualTo(0.3), "clipFrom follows clipTo down on an open computer");
            user.clipFrom = 0.5;
            Assert.That(user.clipTo, Is.EqualTo(0.5), "clipTo follows clipFrom up");
            user.clipFrom = 2.0;
            Assert.That(user.clipFrom, Is.EqualTo(1.0));
            user.clipTo = -1.0;
            Assert.That(user.clipTo, Is.EqualTo(0.0));
            Assert.That(user.clipFrom, Is.EqualTo(0.0));

            user.SetClipRange(0.6, 0.2);
            Assert.That(user.clipFrom, Is.EqualTo(0.6));
            Assert.That(user.clipTo, Is.EqualTo(0.6), "to is raised to from");
            user.SetClipRange(0.1, 0.9);
            Assert.That(user.clipFrom, Is.EqualTo(0.1));
            Assert.That(user.clipTo, Is.EqualTo(0.9));
            Assert.That(user.span, Is.EqualTo(0.8).Within(1e-9));
        }

        // SetClipRange 0x4cf5e0 reads _spline.isClosed (null check at 0x4cf5f9, call at 0x4cf604) before it compares the ends
        // (cmpnltsd at 0x4cf617), so a user without a computer faults even when to >= from, and keeps its range.
        [Test]
        public void SetClipRangeAsksTheComputerBeforeComparingTheEnds()
        {
            SplineUser user = NewUser();
            Assert.Throws<NullReferenceException>(() => user.SetClipRange(0.2, 0.8));
            Assert.Throws<NullReferenceException>(() => user.SetClipRange(0.8, 0.2));
            Assert.That(user.clipFrom, Is.EqualTo(0.0));
            Assert.That(user.clipTo, Is.EqualTo(1.0));
        }

        // ClipPercent 0x4cf500 / UnclipPercent 0x4cdaa0 on an open range 0.25..0.75: linear both ways, 0 and 1 map exactly.
        [Test]
        public void ClipAndUnclipMapTheOpenRange()
        {
            SplineUser user = NewUser(NewLine());
            user.clipFrom = 0.25;
            user.clipTo = 0.75;
            user.RebuildImmediate();
            Assert.That(user.ClipPercent(0.5), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(user.ClipPercent(0.25), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(user.ClipPercent(0.75), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(user.UnclipPercent(0.0), Is.EqualTo(0.25));
            Assert.That(user.UnclipPercent(1.0), Is.EqualTo(0.75));
            Assert.That(user.UnclipPercent(0.25), Is.EqualTo(0.375).Within(1e-9));
            Assert.That(user.UnclipPercent(0.5), Is.EqualTo(0.5).Within(1e-9));
        }

        // The looped branches of ClipPercent 0x4cf500 / UnclipPercent 0x4cdaa0. A closed computer lets clipFrom pass clipTo:
        // 0.75..0.25 spans 0.5 over the end. Switching loopSamples off orders the range again (0x4ce3e0).
        [Test]
        public void LoopedRangeWrapsOverTheEnd()
        {
            SplineUser user = NewUser(NewLine(true));
            user.loopSamples = true;
            user.clipFrom = 0.75;
            user.clipTo = 0.25;
            user.RebuildImmediate();
            Assert.That(user.clipFrom, Is.EqualTo(0.75));
            Assert.That(user.clipTo, Is.EqualTo(0.25));
            Assert.That(user.samplesAreLooped, Is.True);
            Assert.That(user.span, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(user.ClipPercent(0.875), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(user.ClipPercent(0.125), Is.EqualTo(0.75).Within(1e-9));
            Assert.That(user.ClipPercent(0.4), Is.EqualTo(1.0), "in the gap, nearer the end of the range");
            Assert.That(user.ClipPercent(0.6), Is.EqualTo(0.0), "in the gap, nearer the start of the range");
            Assert.That(user.UnclipPercent(0.25), Is.EqualTo(0.875).Within(1e-9));
            Assert.That(user.UnclipPercent(0.75), Is.EqualTo(0.125).Within(1e-9));

            user.loopSamples = false;
            Assert.That(user.clipFrom, Is.EqualTo(0.25));
            Assert.That(user.clipTo, Is.EqualTo(0.75));
            Assert.That(user.samplesAreLooped, Is.False);
        }

        // GetSamples 0x4ce680 / GetSampleRaw 0x4ce910 / GetSamplePercent 0x4cea40: the first and last clipped samples are the
        // exact clip ends, the ones between come from the computer's samples.
        [Test]
        public void ClippedSamplesStartAndEndExactlyOnTheClipRange()
        {
            SplineUser user = NewUser(NewLine());
            user.clipFrom = 0.25;
            user.clipTo = 0.75;
            user.RebuildImmediate();
            Assert.That(user.sampleCount, Is.EqualTo(3));
            SplineSample sample = new SplineSample();
            user.GetSampleRaw(0, ref sample);
            Near(sample.position, new Vector3(5f, 0f, 0f), "first");
            user.GetSampleRaw(1, ref sample);
            Near(sample.position, new Vector3(10f, 0f, 0f), "middle");
            user.GetSampleRaw(2, ref sample);
            Near(sample.position, new Vector3(10f, 0f, 5f), "last");
            Assert.That(user.GetSamplePercent(0), Is.EqualTo(0.25).Within(1e-6));
            Assert.That(user.GetSamplePercent(1), Is.EqualTo(0.5).Within(1e-6));
            Assert.That(user.GetSamplePercent(2), Is.EqualTo(0.75).Within(1e-6));
            user.GetSample(1, ref sample);
            Near(sample.position, new Vector3(10f, 0f, 0f), "modified middle");
        }

        // GetSampleWithAngleCompensation 0x4cec50: the inner size is scaled by 1/sqrt(dot(in, out) * 0.5 + 0.5) (0x4ceef0..0x4cef19).
        // The corner turns from (1,0,0) to (0,0,1), so dot is 0 and the size 1 becomes 1/sqrt(0.5); the end samples stay at 1.
        [Test]
        public void AngleCompensationWidensTheCornerAndLeavesTheEnds()
        {
            SplineUser user = NewUser(NewLine());
            user.RebuildImmediate();
            Assert.That(user.sampleCount, Is.EqualTo(3));
            SplineSample sample = new SplineSample();
            user.GetSampleWithAngleCompensation(0, ref sample);
            Assert.That(sample.size, Is.EqualTo(1f).Within(1e-5f), "first");
            user.GetSampleWithAngleCompensation(1, ref sample);
            Assert.That(sample.size, Is.EqualTo(1.4142135f).Within(1e-5f), "corner");
            user.GetSampleWithAngleCompensation(2, ref sample);
            Assert.That(sample.size, Is.EqualTo(1f).Within(1e-5f), "last");
        }

        // ModifySample 0x4cf340 with the empty default modifiers leaves the sample alone; the two-argument form copies first.
        [Test]
        public void ModifySampleWithEmptyModifiersKeepsTheSample()
        {
            SplineUser user = NewUser();
            SplineSample source = new SplineSample(new Vector3(1f, 2f, 3f), Vector3.up, Vector3.forward, Color.red, 2f, 0.3);
            SplineSample destination = new SplineSample();
            user.ModifySample(ref source, ref destination);
            Near(destination.position, new Vector3(1f, 2f, 3f), "position");
            Assert.That(destination.size, Is.EqualTo(2f).Within(1e-6f));
            Assert.That(destination.percent, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(destination.color, Is.EqualTo(Color.red));
        }

        // Evaluate/Travel/CalculateLength 0x4cf8f0..0x4d2280 take clipped percents. With the range 0.5..1 the user's 0 is the
        // spline's 0.5. Travel reduces moved by the clip difference (0.75 - 0.5 = 0.25), as shipped.
        [Test]
        public void QueriesWorkInClippedPercents()
        {
            SplineUser user = NewUser(NewLine());
            user.clipFrom = 0.5;
            user.RebuildImmediate();
            Near(user.EvaluatePosition(0.0), new Vector3(10f, 0f, 0f), "start of the clip");
            Near(user.EvaluatePosition(0.5), new Vector3(10f, 0f, 5f), "middle of the clip");
            Near(user.Evaluate(1.0).position, new Vector3(10f, 0f, 10f), "end of the clip");
            Assert.That(user.CalculateLength(), Is.EqualTo(10f).Within(1e-3f));

            float moved;
            Assert.That(user.Travel(0.0, 5f, Spline.Direction.Forward, out moved), Is.EqualTo(0.5).Within(1e-6));
            Assert.That(moved, Is.EqualTo(4.75f).Within(1e-4f));
            Assert.That(user.Travel(1.0, 5f, Spline.Direction.Forward, out moved), Is.EqualTo(1.0));
            Assert.That(user.Travel(0.0, 5f, Spline.Direction.Backward, out moved), Is.EqualTo(0.0));
            Assert.That(user.Travel(0.3, 0f, Spline.Direction.Forward), Is.EqualTo(0.3));

            SplineSample projected = new SplineSample();
            user.Project(new Vector3(10f, 0f, 5f), ref projected);
            Near(projected.position, new Vector3(10f, 0f, 5f), "projection");
            Assert.That(projected.percent, Is.EqualTo(0.5).Within(1e-3));
        }

        // Update/LateUpdate/FixedUpdate 0x4cf060/0x4cf210/0x4cf260 only run when updateMethod matches; RunUpdate 0x4cf0b0 builds
        // and then raises onPostBuild in the same call.
        [Test]
        public void UpdateMethodSelectsTheCallbackThatBuilds()
        {
            string[] callbacks = { "Update", "FixedUpdate", "LateUpdate" };
            SplineUser.UpdateMethod[] methods = { SplineUser.UpdateMethod.Update, SplineUser.UpdateMethod.FixedUpdate, SplineUser.UpdateMethod.LateUpdate };
            SplineComputer computer = NewLine();
            for (int callback = 0; callback < callbacks.Length; callback++)
            {
                for (int method = 0; method < methods.Length; method++)
                {
                    SplineUser user = NewUser(computer);
                    user.updateMethod = methods[method];
                    int builds = 0;
                    user.onPostBuild += delegate { builds++; };
                    Call(user, callbacks[callback]);
                    Assert.That(builds, Is.EqualTo(callback == method ? 1 : 0), callbacks[callback] + " for " + methods[method]);
                    Assert.That(user.sampleCount, Is.EqualTo(callback == method ? 3 : 0));
                }
            }
        }

        // Rebuild 0x4cef30 and autoUpdate 0x4ce3a0: Rebuild only queues while autoUpdate is on; switching it on queues one.
        [Test]
        public void AutoUpdateGatesTheQueuedRebuild()
        {
            SplineUser user = NewUser(NewLine());
            int builds = 0;
            user.onPostBuild += delegate { builds++; };
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(1));
            user.autoUpdate = false;
            user.Rebuild();
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(1), "Rebuild is ignored without autoUpdate");
            user.autoUpdate = true;
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(2));
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(2), "nothing is queued any more");
        }

        // OnDidApplyAnimationProperties 0x4c9ea0 replaces the clip range with the animation mirrors. The setters fill the mirror
        // from the previous value (as shipped), so after 0 -> 0.3 -> 0.6 the animated value is 0.3.
        [Test]
        public void AnimationPropertiesReplaceTheClipRangeWithTheMirrors()
        {
            SplineUser user = NewUser(NewLine());
            user.clipFrom = 0.3;
            user.clipFrom = 0.6;
            MethodInfo apply = typeof(SplineUser).GetMethod("OnDidApplyAnimationProperties", BindingFlags.Instance | BindingFlags.NonPublic);
            apply.Invoke(user, null);
            Assert.That(user.clipFrom, Is.EqualTo(0.3).Within(1e-6));
            Assert.That(user.clipTo, Is.EqualTo(1.0));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineUser).GetFields(flags).Select(f => f.Name).Where(n => n != "onPostBuild").ToArray(), Is.EqualTo(new[]
            {
                "updateMethod", "_spline", "_autoUpdate", "_rotationModifier", "_offsetModifier", "_colorModifier", "_sizeModifier",
                "_clipFromSample", "_clipToSample", "_loopSamples", "_clipFrom", "_clipTo", "animClipFrom", "animClipTo",
                "_sampleCollection", "rebuild", "getSamples", "postBuild", "_trs", "_hasTransform", "_workSample", "_sampleCount",
                "_startSampleIndex", "evalResult", "multithreaded", "buildOnAwake", "buildOnEnable"
            }));
            Assert.That(typeof(SplineUser).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "UpdateMethod" }));
            Assert.That((int)SplineUser.UpdateMethod.LateUpdate, Is.EqualTo(2));
        }
    }
}
