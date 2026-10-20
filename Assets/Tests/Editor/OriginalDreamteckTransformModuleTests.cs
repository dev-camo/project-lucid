using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.TransformModule. Expected values are hand arithmetic (or an independent matrix computation for the 2D
    // angle) from the native bodies cited per test. The private Get* methods are reached by reflection so their results can be
    // read without physics components; the Transform entry point is used wherever a plain Transform shows the same result.
    // The target users live on owned game objects, and TearDown destroys everything the test made.
    public sealed class OriginalDreamteckTransformModuleTests
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

        private static void Near(Quaternion actual, Quaternion expected, string message)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f), message + " (actual " + actual.eulerAngles.ToString("F4") + ")");
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        // A sample at the given position looking along forward with the given up; the size scales offsets and the scale.
        private static SplineSample Sample(Vector3 position, Vector3 forward, Vector3 up, float size = 1f)
        {
            return new SplineSample(position, up, forward, Color.white, size, 0.5);
        }

        private static T Call<T>(TransformModule module, string method, object argument)
        {
            MethodInfo info = typeof(TransformModule).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, method);
            return (T)info.Invoke(module, new[] { argument });
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

        // Field initialisers of the ctor 0x4e7660 (the apply flags are one dword and one qword store) and the cctor 0x4e7710.
        [Test]
        public void FreshModuleKeepsTheShippedDefaults()
        {
            TransformModule module = new TransformModule();
            Assert.That(module.applyPositionX && module.applyPositionY && module.applyPositionZ && module.applyPosition2D, Is.True);
            Assert.That(module.retainLocalPosition, Is.False);
            Assert.That(module.direction, Is.EqualTo(Spline.Direction.Forward));
            Assert.That(module.applyRotationX && module.applyRotationY && module.applyRotationZ && module.applyRotation2D, Is.True);
            Assert.That(module.retainLocalRotation, Is.False);
            Assert.That(module.applyScaleX || module.applyScaleY || module.applyScaleZ, Is.False);
            Assert.That(module.velocityHandleMode, Is.EqualTo(TransformModule.VelocityHandleMode.Zero));
            Assert.That(module.offset, Is.EqualTo(Vector2.zero));
            Assert.That(module.rotationOffset, Is.EqualTo(Vector3.zero));
            Assert.That(module.baseScale, Is.EqualTo(Vector3.one));
            Assert.That(module.hasOffset, Is.False);
            Assert.That(module.hasRotationOffset, Is.False);
            Assert.That(module.is2D, Is.False);
            Assert.That(module.targetUser == null, Is.True);
            Assert.That(module.applyPosition, Is.True);
            Assert.That(module.applyRotation, Is.True);
            Assert.That(module.applyScale, Is.False);
            Assert.That(module is ISerializationCallbackReceiver, Is.True);
        }

        // get_applyPosition 0x4e5f80 / set 0x4e5fc0 and the rotation pair 0x4e5fe0 / 0x4e6020: in 2D mode only the 2D flag
        // counts, otherwise any of X, Y, Z; the setters write all four flags. get_applyScale 0x4e6040 / set 0x4e6070 has no 2D
        // flag.
        [Test]
        public void ApplyFlagGroupsFollowTheNativeTruthTable()
        {
            TransformModule module = new TransformModule();
            module.applyPositionX = false;
            module.applyPositionY = false;
            module.applyPositionZ = false;
            Assert.That(module.applyPosition, Is.False, "2D flag does not count outside 2D mode");
            module.applyPositionZ = true;
            Assert.That(module.applyPosition, Is.True);
            module.is2D = true;
            Assert.That(module.applyPosition, Is.True, "2D flag is still set");
            module.applyPosition2D = false;
            Assert.That(module.applyPosition, Is.False, "in 2D mode X, Y and Z do not count");
            module.is2D = false;
            Assert.That(module.applyPosition, Is.True);

            module.applyPosition = false;
            Assert.That(module.applyPositionX || module.applyPositionY || module.applyPositionZ || module.applyPosition2D, Is.False);
            module.applyPosition = true;
            Assert.That(module.applyPositionX && module.applyPositionY && module.applyPositionZ && module.applyPosition2D, Is.True);

            module.applyRotationX = false;
            module.applyRotationY = false;
            module.applyRotationZ = false;
            Assert.That(module.applyRotation, Is.False);
            module.applyRotationY = true;
            Assert.That(module.applyRotation, Is.True);
            module.is2D = true;
            module.applyRotation2D = false;
            Assert.That(module.applyRotation, Is.False);
            module.applyRotation = true;
            Assert.That(module.applyRotationX && module.applyRotationY && module.applyRotationZ && module.applyRotation2D, Is.True);
            module.applyRotation = false;
            Assert.That(module.applyRotationX || module.applyRotationY || module.applyRotationZ || module.applyRotation2D, Is.False);

            Assert.That(module.applyScale, Is.False);
            module.applyScaleZ = true;
            Assert.That(module.applyScale, Is.True);
            module.applyScale = false;
            Assert.That(module.applyScaleX || module.applyScaleY || module.applyScaleZ, Is.False);
            module.applyScale = true;
            Assert.That(module.applyScaleX && module.applyScaleY && module.applyScaleZ, Is.True);
        }

        // set_offset 0x4e5b40 / set_rotationOffset 0x4e5c70 / set_baseScale 0x4e5e10: a value equal under Vector equality (squared
        // distance below 1e-10) changes nothing; a real change refreshes the has-flag and queues a rebuild of the target user.
        [Test]
        public void OffsetSettersSkipEqualValuesRefreshTheFlagsAndRebuildTheUser()
        {
            // The editor compile rebuilds at once while the cached play state is false, so the flag is set to the value play
            // mode caches (DreamteckPlayState) and the rebuild is queued by hand, as the spline setter queues it in the player.
            SplineUser user = DreamteckPlayState.Cache(NewUserWithLine());
            int builds = 0;
            user.onPostBuild += delegate { builds++; };
            user.Rebuild();
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

        // OnAfterDeserialize 0x4e7560 recomputes both has-flags from the serialized offsets, whatever the serialized flags say.
        [Test]
        public void DeserializationRecomputesTheOffsetFlags()
        {
            TransformModule stale = JsonUtility.FromJson<TransformModule>("{\"_hasOffset\":true,\"_hasRotationOffset\":true}");
            Assert.That(stale.hasOffset, Is.False);
            Assert.That(stale.hasRotationOffset, Is.False);

            TransformModule offsets = JsonUtility.FromJson<TransformModule>(
                "{\"_offset\":{\"x\":0.5,\"y\":0.0},\"_rotationOffset\":{\"x\":0.0,\"y\":0.0,\"z\":3.0}}");
            Assert.That(offsets.hasOffset, Is.True);
            Assert.That(offsets.hasRotationOffset, Is.True);
            Assert.That(offsets.offset, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(offsets.rotationOffset, Is.EqualTo(new Vector3(0f, 0f, 3f)));
        }

        // ApplyTransform 0x4e6090 with GetPosition 0x4e6150: the applied axes take the sample's position, the others keep the
        // transform's. Sample (1,2,3), forward +z, up +y, so the rotation is the identity.
        [Test]
        public void ApplyTransformMovesOnlyTheAppliedAxes()
        {
            Transform target = NewObject("target").transform;
            target.position = new Vector3(10f, 10f, 10f);
            TransformModule module = new TransformModule();
            module.splineResult = Sample(new Vector3(1f, 2f, 3f), Vector3.forward, Vector3.up);
            module.ApplyTransform(target);
            Near(target.position, new Vector3(1f, 2f, 3f), "all axes");
            Near(target.rotation, Quaternion.identity, "forward +z, up +y");
            Near(target.localScale, Vector3.one, "scale is not applied by default");

            target.position = new Vector3(10f, 10f, 10f);
            module.applyPositionY = false;
            module.ApplyTransform(target);
            Near(target.position, new Vector3(1f, 10f, 3f), "Y not applied");

            target.position = new Vector3(10f, 10f, 10f);
            module.applyPosition = false;
            module.ApplyTransform(target);
            Near(target.position, new Vector3(10f, 10f, 10f), "nothing applied");
        }

        // GetScale 0x4e6df0: the applied axes take baseScale times the sample size; ApplyTransform 0x4e6090 inlines the same.
        [Test]
        public void ScaleIsBaseScaleTimesSampleSizeOnTheAppliedAxes()
        {
            Transform target = NewObject("target").transform;
            target.localScale = new Vector3(1f, 1f, 1f);
            TransformModule module = new TransformModule();
            module.baseScale = new Vector3(1f, 2f, 3f);
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up, 2f);
            module.applyScale = true;
            module.ApplyTransform(target);
            Near(target.localScale, new Vector3(2f, 4f, 6f), "all axes");
            module.applyScaleY = false;
            target.localScale = new Vector3(5f, 5f, 5f);
            module.ApplyTransform(target);
            Near(target.localScale, new Vector3(2f, 5f, 6f), "Y not applied");
            Near(Call<Vector3>(module, "GetScale", new Vector3(7f, 7f, 7f)), new Vector3(2f, 7f, 6f), "GetScale");
        }

        // GetPosition 0x4e6150 (the 0x4e62a1..0x4e62e9 block): offset.x along the sample's right and offset.y along its up, both
        // times the size. Right = normalize(cross(up, forward)) = +x for forward +z, up +y. (1,2,3) + 2*(1,0,0) + 2*2*(0,1,0).
        [Test]
        public void OffsetMovesAlongTheSampleFrameScaledBySize()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(new Vector3(1f, 2f, 3f), Vector3.forward, Vector3.up, 2f);
            module.offset = new Vector2(1f, 2f);
            Near(Call<Vector3>(module, "GetPosition", new Vector3(9f, 9f, 9f)), new Vector3(3f, 6f, 3f), "offset");
        }

        // GetPosition 0x4e6150, the retain branch (0x4e6318..0x4e6678): the target user's position is taken into the sample's frame
        // (Matrix4x4.TRS of position and LookRotation, inverted), the applied local axes are zeroed and the point is taken back.
        // Sample at the origin looking along +x (yaw 90): local z is world +x and local x is world -z, so a user at (3,4,5) is
        // (-5,4,3) locally; with Z applied it becomes (-5,4,0), which is world (0,4,5).
        [Test]
        public void RetainLocalPositionKeepsTheUnappliedLocalAxes()
        {
            SplineUser user = NewObject("user").AddComponent<SplineUser>();
            user.transform.position = new Vector3(3f, 4f, 5f);
            TransformModule module = new TransformModule();
            module.targetUser = user;
            module.retainLocalPosition = true;
            module.applyPositionX = false;
            module.applyPositionY = false;
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            Near(Call<Vector3>(module, "GetPosition", Vector3.zero), new Vector3(3f, 4f, 0f), "aligned frame, only Z applied");
            module.splineResult = Sample(Vector3.zero, Vector3.right, Vector3.up);
            Near(Call<Vector3>(module, "GetPosition", Vector3.zero), new Vector3(0f, 4f, 5f), "yawed frame, only local Z applied");
            module.applyPositionZ = false;
            Near(Call<Vector3>(module, "GetPosition", Vector3.zero), new Vector3(3f, 4f, 5f), "nothing applied keeps the user position");
        }

        // GetRotation 0x4e6690: LookRotation(forward or -forward, up). Forward direction along +x looks down +x; Backward negates
        // the forward vector only.
        [Test]
        public void RotationLooksAlongTheSampleAndBackwardNegatesForward()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.right, Vector3.up);
            Quaternion forward = Call<Quaternion>(module, "GetRotation", Quaternion.identity);
            Near(forward * Vector3.forward, Vector3.right, "forward");
            Near(forward * Vector3.up, Vector3.up, "up");
            module.direction = Spline.Direction.Backward;
            Quaternion backward = Call<Quaternion>(module, "GetRotation", Quaternion.identity);
            Near(backward * Vector3.forward, Vector3.left, "backward");
            Near(backward * Vector3.up, Vector3.up, "backward keeps up");
        }

        // GetRotation 0x4e6690 (0x4e6988..0x4e6a66): a rotation offset is multiplied on after the look rotation. Euler(0,90,0) on
        // forward +z turns it to +x.
        [Test]
        public void RotationOffsetIsMultipliedOntoTheLookRotation()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            module.rotationOffset = new Vector3(0f, 90f, 0f);
            Quaternion rotation = Call<Quaternion>(module, "GetRotation", Quaternion.identity);
            Near(rotation * Vector3.forward, Vector3.right, "offset yaw");
        }

        // GetRotation 0x4e6690 (0x4e6d14..0x4e6dbe): unless all three axes are applied, the unapplied ones keep the input's Euler
        // angles. The spline looks along +x (Euler 0,90,0).
        [Test]
        public void UnappliedRotationAxesKeepTheInputEulerAngles()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.right, Vector3.up);
            Quaternion input = Quaternion.Euler(30f, 45f, 0f);
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(0f, 90f, 0f), "all axes applied ignores the input");
            module.applyRotationX = false;
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(30f, 90f, 0f), "X from the input");
            module.applyRotationX = true;
            module.applyRotationY = false;
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(0f, 45f, 0f), "Y from the input");
        }

        // GetRotation 0x4e6690 (0x4e6a6a..0x4e6c4b): retainLocalRotation takes the input relative to the sample rotation and zeroes
        // the applied local axes. The input is the spline's yaw 90 followed by a local roll of 30.
        [Test]
        public void RetainLocalRotationKeepsTheUnappliedLocalAxes()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.right, Vector3.up);
            module.retainLocalRotation = true;
            Quaternion spline = Quaternion.Euler(0f, 90f, 0f);
            Quaternion input = spline * Quaternion.Euler(0f, 0f, 30f);
            Near(Call<Quaternion>(module, "GetRotation", input), spline, "all axes applied zeroes the local roll");
            module.applyRotationZ = false;
            Near(Call<Quaternion>(module, "GetRotation", input), input, "unapplied Z keeps the local roll");
        }

        // GetRotation 0x4e6690 (0x4e673e..0x4e68cf): in 2D mode the look rotation is turned by Euler(90,-90,0) (the constant at
        // 0x29a7430 is both angles in radians), and its eulerAngles.z plus the Z rotation offset becomes a rotation about +z.
        // Independent matrix arithmetic gives the planar angle for forward +y, up -z as 90, for -x as 180 and for -y as 270.
        // Without applyRotation2D the input is returned unchanged.
        [Test]
        public void TwoDimensionalRotationIsAPlanarAngle()
        {
            TransformModule module = new TransformModule();
            module.is2D = true;
            Quaternion input = Quaternion.Euler(10f, 20f, 30f);
            module.splineResult = Sample(Vector3.zero, Vector3.up, Vector3.back);
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(0f, 0f, 90f), "forward +y");
            module.splineResult = Sample(Vector3.zero, Vector3.left, Vector3.back);
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(0f, 0f, 180f), "forward -x");
            module.splineResult = Sample(Vector3.zero, Vector3.down, Vector3.back);
            module.rotationOffset = new Vector3(40f, 50f, 15f);
            Near(Call<Quaternion>(module, "GetRotation", input), Quaternion.Euler(0f, 0f, 285f), "forward -y plus the Z offset only");
            module.applyRotation2D = false;
            Near(Call<Quaternion>(module, "GetRotation", input), input, "2D rotation off");
        }

        // HandleVelocity 0x4e6fa0 with the sample forward +z. Zero (and any unknown mode) gives zero, Preserve the velocity, Align
        // forward (negated when the velocity points against it) times the speed, AlignRealistic additionally times the cosine
        // between that direction and the velocity. Velocity (3,0,4) has speed 5 and cosine 0.8; (3,0,-4) is against forward.
        [Test]
        public void VelocityModesFollowTheNativeTruthTable()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            Vector3 along = new Vector3(3f, 0f, 4f);
            Vector3 against = new Vector3(3f, 0f, -4f);

            module.velocityHandleMode = TransformModule.VelocityHandleMode.Zero;
            Near(Call<Vector3>(module, "HandleVelocity", along), Vector3.zero, "Zero");
            module.velocityHandleMode = (TransformModule.VelocityHandleMode)7;
            Near(Call<Vector3>(module, "HandleVelocity", along), Vector3.zero, "unknown mode");
            module.velocityHandleMode = TransformModule.VelocityHandleMode.Preserve;
            Near(Call<Vector3>(module, "HandleVelocity", along), along, "Preserve");
            module.velocityHandleMode = TransformModule.VelocityHandleMode.Align;
            Near(Call<Vector3>(module, "HandleVelocity", along), new Vector3(0f, 0f, 5f), "Align");
            Near(Call<Vector3>(module, "HandleVelocity", against), new Vector3(0f, 0f, -5f), "Align against forward");
            module.velocityHandleMode = TransformModule.VelocityHandleMode.AlignRealistic;
            Near(Call<Vector3>(module, "HandleVelocity", along), new Vector3(0f, 0f, 4f), "AlignRealistic");
            Near(Call<Vector3>(module, "HandleVelocity", against), new Vector3(0f, 0f, -4f), "AlignRealistic against forward");
            Near(Call<Vector3>(module, "HandleVelocity", Vector3.zero), Vector3.zero, "no velocity stays zero");
        }

        // HandleVelocity 0x4e6fa0 (0x4e72a7..0x4e72fa): only the applied position axes take the new velocity.
        [Test]
        public void VelocityKeepsTheUnappliedAxes()
        {
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            module.velocityHandleMode = TransformModule.VelocityHandleMode.Zero;
            module.applyPositionY = false;
            Near(Call<Vector3>(module, "HandleVelocity", new Vector3(1f, 2f, 3f)), new Vector3(0f, 2f, 0f), "Y kept");
            module.applyPositionX = false;
            Near(Call<Vector3>(module, "HandleVelocity", new Vector3(1f, 2f, 3f)), new Vector3(1f, 2f, 0f), "X and Y kept");
            module.applyPositionZ = false;
            Near(Call<Vector3>(module, "HandleVelocity", new Vector3(1f, 2f, 3f)), new Vector3(1f, 2f, 3f), "nothing taken");
            module.applyPositionX = true;
            module.velocityHandleMode = TransformModule.VelocityHandleMode.Align;
            Near(Call<Vector3>(module, "HandleVelocity", new Vector3(3f, 0f, 4f)), new Vector3(0f, 0f, 4f), "Align takes X only");
        }

        // ApplyRigidbody 0x4e6e40 and ApplyRigidbody2D 0x4e73e0 set the body's transform scale (GetScale) before moving it, and a
        // null body faults at the first access.
        [Test]
        public void RigidbodyEntryPointsScaleTheBodyAndFaultOnNull()
        {
            TransformModule module = new TransformModule();
            module.baseScale = new Vector3(2f, 2f, 2f);
            module.applyScale = true;
            module.splineResult = Sample(new Vector3(1f, 2f, 3f), Vector3.forward, Vector3.up, 3f);

            GameObject body = NewObject("body");
            Rigidbody rigidbody = body.AddComponent<Rigidbody>();
            module.ApplyRigidbody(rigidbody);
            Near(body.transform.localScale, new Vector3(6f, 6f, 6f), "Rigidbody scale");

            GameObject flat = NewObject("flat");
            Rigidbody2D rigidbody2D = flat.AddComponent<Rigidbody2D>();
            module.ApplyRigidbody2D(rigidbody2D);
            Near(flat.transform.localScale, new Vector3(6f, 6f, 6f), "Rigidbody2D scale");

            Assert.Throws<NullReferenceException>(() => module.ApplyRigidbody(null));
            Assert.Throws<NullReferenceException>(() => module.ApplyRigidbody2D(null));
            Assert.Throws<NullReferenceException>(() => module.ApplyTransform(null));
        }

        // ApplyRigidbody 0x4e6e40 clears angular velocity X for applyRotationX, Y for applyRotationY and Z for applyRotationZ or
        // applyRotation2D; ApplyRigidbody2D 0x4e73e0 (0x4e7513 reads applyRotationX) clears it for applyRotationX alone. In the
        // original text that logic follows an `#if UNITY_EDITOR` block which, when Application.isPlaying is false, applies the
        // transform and returns; the player branch cannot run in EditMode, so this test asserts the edit-mode behaviour: the
        // angular velocity is left untouched for every flag combination and the transform entry point has run.
        [Test]
        public void RigidbodyAngularVelocityFollowsTheShippedRotationFlags()
        {
            Assert.That(Application.isPlaying, Is.False);
            TransformModule module = new TransformModule();
            module.splineResult = Sample(Vector3.zero, Vector3.forward, Vector3.up);
            module.applyRotationX = true;
            module.applyRotationY = true;
            module.applyRotationZ = true;
            module.applyRotation2D = true;
            module.applyRotation = false;

            Rigidbody body = NewObject("body").AddComponent<Rigidbody>();
            body.angularVelocity = new Vector3(1f, 2f, 3f);
            body.transform.localScale = new Vector3(5f, 5f, 5f);
            module.baseScale = new Vector3(2f, 2f, 2f);
            module.applyScale = true;
            foreach (bool flag in new[] { false, true })
            {
                module.applyRotationX = flag;
                module.applyRotationY = flag;
                module.applyRotationZ = flag;
                module.applyRotation2D = flag;
                module.ApplyRigidbody(body);
                Near(body.angularVelocity, new Vector3(1f, 2f, 3f), "the edit-mode branch never clears angular velocity (flags " + flag + ")");
            }

            Near(body.transform.localScale, new Vector3(2f, 2f, 2f), "the edit-mode branch applies the transform scale");

            Rigidbody2D flat = NewObject("flat").AddComponent<Rigidbody2D>();
            flat.angularVelocity = 7f;
            module.applyRotationX = true;
            module.applyRotation2D = false;
            module.ApplyRigidbody2D(flat);
            Assert.That(flat.angularVelocity, Is.EqualTo(7f), "applyRotationX alone does not clear it while the editor branch returns early");
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(TransformModule).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_hasOffset", "_hasRotationOffset", "_offset", "_rotationOffset", "_baseScale", "_2dMode", "velocityHandleMode",
                "_splineResult", "applyPositionX", "applyPositionY", "applyPositionZ", "applyPosition2D", "retainLocalPosition",
                "direction", "applyRotationX", "applyRotationY", "applyRotationZ", "applyRotation2D", "retainLocalRotation",
                "applyScaleX", "applyScaleY", "applyScaleZ", "targetUser", "position", "rotation"
            }));
            Assert.That(typeof(TransformModule).GetCustomAttributes(typeof(SerializableAttribute), false).Length, Is.EqualTo(1));
            Assert.That(typeof(TransformModule).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "VelocityHandleMode" }));
            Assert.That(Enum.GetNames(typeof(TransformModule.VelocityHandleMode)), Is.EqualTo(new[] { "Zero", "Preserve", "Align", "AlignRealistic" }));
            Assert.That(Enum.GetValues(typeof(TransformModule.VelocityHandleMode)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            FieldInfo hidden = typeof(TransformModule).GetField("targetUser");
            Assert.That(hidden.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            FieldInfo serialized = typeof(TransformModule).GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(serialized.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1));
            Assert.That(serialized.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
        }
    }
}
