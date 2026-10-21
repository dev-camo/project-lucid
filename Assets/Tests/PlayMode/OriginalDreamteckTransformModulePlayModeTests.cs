using System.Collections.Generic;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped (player) branches of Dreamteck.Splines.TransformModule, run where Application.isPlaying is really true. The
    // editor-branch counterparts are in OriginalDreamteckTransformModuleTests. The tests are synchronous: no frame passes, so
    // the engine's own Update calls do not interleave with the manual ones. TearDown destroys everything the test made.
    public sealed class OriginalDreamteckTransformModulePlayModeTests
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
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private static SplineSample Sample(Vector3 position, Vector3 forward, Vector3 up, float size = 1f)
        {
            return new SplineSample(position, up, forward, Color.white, size, 0.5);
        }

        private SplineUser NewUserWithLine()
        {
            GameObject lineOwner = NewObject("line");
            SplineComputer computer = lineOwner.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            computer.RebuildImmediate();
            SplineUser user = NewObject("user").AddComponent<SplineUser>();
            user.spline = computer;
            return user;
        }

        private static void Update(SplineUser user)
        {
            MethodInfo info = typeof(SplineUser).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null);
            info.Invoke(user, null);
        }

        // set_offset 0x4e5b40 / set_rotationOffset 0x4e5c70 / set_baseScale 0x4e5e10: a value equal under Vector equality (squared
        // distance below 1e-10) changes nothing; a real change refreshes the has-flag and queues a rebuild of the target user.
        // In play mode the spline setter itself queues the first build (Rebuild sets rebuild and getSamples while the cached
        // play state is true), so the first Update runs it without a manual Rebuild.
        [Test]
        public void OffsetSettersSkipEqualValuesRefreshTheFlagsAndRebuildTheUser()
        {
            Assert.That(Application.isPlaying, Is.True);
            SplineUser user = NewUserWithLine();
            int builds = 0;
            user.onPostBuild += delegate { builds++; };
            Update(user);
            Assert.That(builds, Is.EqualTo(1), "initial queued build");

            TransformModule module = new TransformModule();
            module.targetUser = user;
            module.offset = new Vector2(1e-6f, 0f);
            module.rotationOffset = new Vector3(0f, 1e-6f, 0f);
            module.baseScale = new Vector3(1f, 1f, 1f + 1e-6f);
            Update(user);
            Assert.That(builds, Is.EqualTo(1), "equal values queue nothing");
            Assert.That(module.offset, Is.EqualTo(Vector2.zero), "the stored value is untouched");
            Assert.That(module.hasOffset, Is.False);
            Assert.That(module.hasRotationOffset, Is.False);
            Assert.That(module.baseScale, Is.EqualTo(Vector3.one));

            module.offset = new Vector2(1f, 0f);
            Assert.That(module.hasOffset, Is.True);
            Update(user);
            Assert.That(builds, Is.EqualTo(2));
            module.rotationOffset = new Vector3(0f, 10f, 0f);
            Assert.That(module.hasRotationOffset, Is.True);
            Update(user);
            Assert.That(builds, Is.EqualTo(3));
            module.baseScale = new Vector3(2f, 2f, 2f);
            Assert.That(module.baseScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Update(user);
            Assert.That(builds, Is.EqualTo(4));

            module.offset = Vector2.zero;
            Assert.That(module.hasOffset, Is.False, "back to zero clears the flag");
            module.rotationOffset = Vector3.zero;
            Assert.That(module.hasRotationOffset, Is.False);

            TransformModule free = new TransformModule();
            free.offset = new Vector2(0f, 3f);
            Assert.That(free.hasOffset, Is.True, "without a target user nothing is rebuilt");
        }

        // ApplyRigidbody 0x4e6e40 clears angular velocity X for applyRotationX, Y for applyRotationY and Z for applyRotationZ or
        // applyRotation2D; ApplyRigidbody2D 0x4e73e0 (0x4e7513 reads applyRotationX) clears it for applyRotationX alone. The
        // calls are synchronous, so the engine's physics step does not change the angular velocity between the assertions.
        [Test]
        public void RigidbodyAngularVelocityFollowsTheShippedRotationFlags()
        {
            Assert.That(Application.isPlaying, Is.True);
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            module.applyRotationX = false;
            module.applyRotationY = false;
            module.applyRotationZ = false;
            module.applyRotation2D = false;

            Rigidbody body = NewObject("body").AddComponent<Rigidbody>();
            body.angularVelocity = new Vector3(1f, 2f, 3f);
            module.ApplyRigidbody(body);
            Near(body.angularVelocity, new Vector3(1f, 2f, 3f), "no flag clears nothing");
            module.applyRotationX = true;
            module.ApplyRigidbody(body);
            Near(body.angularVelocity, new Vector3(0f, 2f, 3f), "X flag clears X only");
            module.applyRotationX = false;
            module.applyRotationY = true;
            body.angularVelocity = new Vector3(1f, 2f, 3f);
            module.ApplyRigidbody(body);
            Near(body.angularVelocity, new Vector3(1f, 0f, 3f), "Y flag clears Y only");
            module.applyRotationY = false;
            module.applyRotation2D = true;
            body.angularVelocity = new Vector3(1f, 2f, 3f);
            module.ApplyRigidbody(body);
            Near(body.angularVelocity, new Vector3(1f, 2f, 0f), "2D flag clears Z without applyRotationZ");
            module.applyRotation2D = false;
            module.applyRotationZ = true;
            body.angularVelocity = new Vector3(1f, 2f, 3f);
            module.ApplyRigidbody(body);
            Near(body.angularVelocity, new Vector3(1f, 2f, 0f), "Z flag clears Z");

            module.applyRotationZ = false;
            Rigidbody2D flat = NewObject("flat").AddComponent<Rigidbody2D>();
            flat.angularVelocity = 7f;
            module.applyRotation2D = true;
            module.ApplyRigidbody2D(flat);
            Assert.That(flat.angularVelocity, Is.EqualTo(7f), "applyRotation2D alone keeps the angular velocity");
            module.applyRotationX = true;
            module.applyRotation2D = false;
            module.ApplyRigidbody2D(flat);
            Assert.That(flat.angularVelocity, Is.EqualTo(0f), "applyRotationX alone clears it");
        }
    }
}
