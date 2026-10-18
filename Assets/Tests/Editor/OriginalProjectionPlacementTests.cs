using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.ProjectionPlacement (TypeDef 0x02000765, x86-64 0x5CF860..0x5D0552). Expected
    // values follow the native arithmetic with inputs chosen so the float results are exact or
    // nearly so. Raycasts use either an empty mask, which never hits, or layer 30 alone, where the
    // case places one owned BoxCollider before syncing transforms.
    public sealed class OriginalProjectionPlacementTests
    {
        private const int ProbeLayer = 30;
        private static readonly LayerMask Nothing = 0;
        private static readonly LayerMask Probe = 1 << ProbeLayer;

        private static ProjectionPlacement Placement(bool fromStart, int distanceOffset = 0)
        {
            var placement = new ProjectionPlacement();
            Set(placement, "m_fromStart", fromStart);
            Set(placement, "m_distanceOffset", distanceOffset);
            return placement;
        }

        private static BoxCollider Box(Owned owned, Vector3 centre, Vector3 size, bool isTrigger = false)
        {
            GameObject host = owned.Host("Lucid projection obstacle");
            host.layer = ProbeLayer;
            host.transform.position = centre;
            host.transform.localScale = size;
            BoxCollider box = host.AddComponent<BoxCollider>();
            box.isTrigger = isTrigger;
            Physics.SyncTransforms();
            return box;
        }

        private static Vector3[] Positions(LineRendererContainer renderer)
        {
            var positions = Get<Vector3[]>(renderer, "m_positions");
            int count = Get<int>(renderer, "m_positionsCount");
            var used = new Vector3[count];
            System.Array.Copy(positions, used, count);
            return used;
        }

        [Test]
        public void DeclarationsKeepTheOriginalFieldsMethodsAndAttributes()
        {
            AssertMembers(typeof(ProjectionPlacement), 0x00102001,
                new[] { ("m_fromStart", 0x0001), ("m_distanceOffset", 0x0001), ("m_raycastInterval", 0x0001), ("m_raycastSnapTime", 0x0001),
                        ("m_raycastStepTime", 0x0001), ("m_velocityToForward", 0x0001), ("m_gravityMultiplier", 0x0001), ("m_speed", 0x0001),
                        ("m_timeTotal", 0x0001), ("m_distance", 0x0001), ("m_positionOffset", 0x0001), ("<RaycastHit>k__BackingField", 0x0001),
                        ("m_raycastHitTimer", 0x0001), ("<Position>k__BackingField", 0x0001) },
                new[] { ("get_RaycastHit", 0x0886), ("set_RaycastHit", 0x0881), ("get_Position", 0x0886), ("set_Position", 0x0881),
                        ("Initialise", 0x0086), ("Reset", 0x0086), ("Update", 0x0086), ("CheckForRaycastHit", 0x0081),
                        ("ProjectForRaycastHit", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ProjectionPlacement), 1, 2);
            Assert.That(WorldMemberAccess.AttributeRow(typeof(ProjectionPlacement).GetField("m_fromStart", Declared)),
                Is.EqualTo("TooltipAttribute(Whether the projection is from the start location else use the end.) SerializeField()"));
            Assert.That(WorldMemberAccess.AttributeRow(typeof(ProjectionPlacement).GetField("m_velocityToForward", Declared)),
                Is.EqualTo("TooltipAttribute(Maximum velocity to forward deviation to trigger a raycast check.) SerializeField()"));
            foreach (var (field, tooltip) in new[]
            {
                ("m_distanceOffset", "Initial distance offset to start projection."),
                ("m_raycastInterval", "Distance interval to perform raycast for collision hit."),
                ("m_raycastSnapTime", "Time to leave raycast hit snapped to location."),
                ("m_raycastStepTime", "Time to step raycast along projection."),
            })
                Assert.That(WorldMemberAccess.AttributeRow(typeof(ProjectionPlacement).GetField(field, Declared)),
                    Is.EqualTo("SerializeField() TooltipAttribute(" + tooltip + ")"), field);
        }

        // .ctor 0x5D0540 stores the four floats at 0x29a7a30 (10, 1, 0.1, 0.1) from +0x18. Initialise
        // 0x5CF8A0 takes the start pair when m_fromStart is set and the end pair otherwise. Reset
        // 0x5CF8D0 zeroes the time, distance, offset, hit flag and timer and leaves Position.
        [Test]
        public void ConstructorInitialiseAndResetFollowTheNativeStores()
        {
            var placement = Placement(true);
            Assert.That((Get<float>(placement, "m_raycastInterval"), Get<float>(placement, "m_raycastSnapTime"),
                Get<float>(placement, "m_raycastStepTime"), Get<float>(placement, "m_velocityToForward")), Is.EqualTo((10f, 1f, 0.1f, 0.1f)));
            placement.Initialise(1f, 2f, 3f, 4f);
            Assert.That((Get<float>(placement, "m_speed"), Get<float>(placement, "m_gravityMultiplier")), Is.EqualTo((1f, 3f)));
            Set(placement, "m_fromStart", false);
            placement.Initialise(1f, 2f, 3f, 4f);
            Assert.That((Get<float>(placement, "m_speed"), Get<float>(placement, "m_gravityMultiplier")), Is.EqualTo((2f, 4f)));

            Set(placement, "m_timeTotal", 5f);
            Set(placement, "m_distance", 6f);
            Set(placement, "m_positionOffset", Vector3.one);
            Set(placement, "<RaycastHit>k__BackingField", true);
            Set(placement, "m_raycastHitTimer", 7f);
            Set(placement, "<Position>k__BackingField", new Vector3(1f, 2f, 3f));
            placement.Reset();
            Assert.That((Get<float>(placement, "m_timeTotal"), Get<float>(placement, "m_distance"), Get<float>(placement, "m_raycastHitTimer")), Is.EqualTo((0f, 0f, 0f)));
            Assert.That(Get<Vector3>(placement, "m_positionOffset"), Is.EqualTo(Vector3.zero));
            Assert.That(placement.RaycastHit, Is.False);
            Assert.That(placement.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Get<float>(placement, "m_speed"), Is.EqualTo(2f), "Reset leaves the speed");
        }

        // Update 0x5CF940: timeStep = projectionSpeed / m_speed * deltaTime is added to m_timeTotal t;
        // with d = (end - start) / |end - start| the offset is d * m_distanceOffset +
        // (d * m_speed + (0.5 * t) * (gravity * m_gravityMultiplier)) * t; Position is the start
        // (m_fromStart) or the end plus the offset; m_distance adds the step from the last offset.
        // Without a hit it returns |end - start| > m_distance.
        // Start 0, end 10x, speed 2, multiplier 2, gravity -5y, offset 1, dt 0.5:
        // t = 0.25 gives (1.5, -0.3125, 0); t = 0.5 gives (2, -1.25, 0), a step of length 1.0625.
        [Test]
        public void UpdateFollowsTheBallisticOffsetFromTheChosenEnd()
        {
            var gravity = new Vector3(0f, -5f, 0f);
            var start = Vector3.zero;
            var end = new Vector3(10f, 0f, 0f);

            ProjectionPlacement placement = Placement(true, 1);
            placement.Initialise(2f, 99f, 2f, 99f);
            Assert.That(placement.Update(start, end, 1f, gravity, Nothing, 0.5f), Is.True);
            Assert.That(placement.Position, Is.EqualTo(new Vector3(1.5f, -0.3125f, 0f)));
            Assert.That(Get<float>(placement, "m_timeTotal"), Is.EqualTo(0.25f));
            float firstStep = Mathf.Sqrt(1.5f * 1.5f + 0.3125f * 0.3125f);
            Assert.That(Get<float>(placement, "m_distance"), Is.EqualTo(firstStep).Within(1e-6f));
            Assert.That(placement.Update(start, end, 1f, gravity, Nothing, 0.5f), Is.True);
            Assert.That(placement.Position, Is.EqualTo(new Vector3(2f, -1.25f, 0f)));
            Assert.That(Get<Vector3>(placement, "m_positionOffset"), Is.EqualTo(new Vector3(2f, -1.25f, 0f)));
            Assert.That(Get<float>(placement, "m_distance"), Is.EqualTo(firstStep + 1.0625f).Within(1e-6f));
            Assert.That(placement.RaycastHit, Is.False);

            ProjectionPlacement fromEnd = Placement(false, 1);
            fromEnd.Initialise(99f, 2f, 99f, 2f);
            fromEnd.Update(start, end, 1f, gravity, Nothing, 0.5f);
            Assert.That(fromEnd.Position, Is.EqualTo(new Vector3(11.5f, -0.3125f, 0f)), "the end is the origin; the direction still runs from the start");

            ProjectionPlacement shortLine = Placement(true, 1);
            shortLine.Initialise(2f, 99f, 2f, 99f);
            Assert.That(shortLine.Update(start, new Vector3(1.5f, 0f, 0f), 1f, gravity, Nothing, 0.5f), Is.False, "1.5 is not beyond the 1.53 travelled");
        }

        // CheckForRaycastHit 0x5CFCF0 (inlined in Update) raycasts from the previous Position
        // (+0x4C/+0x54 read at 0x5CFBF8) along the step (new offset - m_positionOffset) for
        // m_raycastInterval with QueryTriggerInteraction.Ignore (esi 1). A hit sets RaycastHit and
        // the timer to m_raycastSnapTime; while RaycastHit is set the timer only falls by the time
        // step, clamped at 0 (maxss), and Update returns timer > 0 instead of the distance test.
        // The origin row starts the ray at a stored (0, 5, 0): along (1.5, -0.3125) it passes about
        // y = 3.96 at the box, while a ray from the new Position or the start would hit it. The
        // direction row's second step (0.5, -0.9375) from (1.5, -0.3125) reaches the box centred
        // at (2.5, -2.1875); a ray along the whole offset (2, -1.25) passes y = -0.9375 there.
        [Test]
        public void ARaycastHitSnapsThePointUntilTheTimerRunsOut()
        {
            Owned.Run(owned =>
            {
                var gravity = new Vector3(0f, -5f, 0f);
                var start = Vector3.zero;
                var end = new Vector3(10f, 0f, 0f);
                // The first ray runs from the origin towards (1.5, -0.3125, 0).
                BoxCollider box = Box(owned, new Vector3(5f, -5f * 0.3125f / 1.5f, 0f), Vector3.one, true);

                ProjectionPlacement placement = Placement(true, 1);
                placement.Initialise(2f, 99f, 2f, 99f);
                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.True);
                Assert.That(placement.RaycastHit, Is.False, "a trigger collider is ignored");

                box.isTrigger = false;
                placement.Reset();
                Set(placement, "<Position>k__BackingField", new Vector3(0f, 5f, 0f));
                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.True);
                Assert.That(placement.RaycastHit, Is.False, "the ray starts at the stored Position");
                Assert.That(Get<float>(placement, "m_raycastHitTimer"), Is.EqualTo(0f));
                Assert.That(placement.Position, Is.EqualTo(new Vector3(1.5f, -0.3125f, 0f)));

                placement.Reset();
                Set(placement, "<Position>k__BackingField", Vector3.zero);
                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.True);
                Assert.That(placement.RaycastHit, Is.True);
                Assert.That(Get<float>(placement, "m_raycastHitTimer"), Is.EqualTo(1f));
                Assert.That(placement.Position, Is.EqualTo(new Vector3(1.5f, -0.3125f, 0f)), "the point still moves");

                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.True);
                Assert.That(Get<float>(placement, "m_raycastHitTimer"), Is.EqualTo(0.75f));
                Set(placement, "m_raycastHitTimer", 0.1f);
                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.False, "the timer, not the distance, decides");
                Assert.That(Get<float>(placement, "m_raycastHitTimer"), Is.EqualTo(0f));
                Assert.That(placement.RaycastHit, Is.True, "RaycastHit stays set");
                // maxss keeps its second operand, 0, for an unordered first operand.
                Set(placement, "m_raycastHitTimer", float.NaN);
                Assert.That(placement.Update(start, end, 1f, gravity, Probe, 0.5f), Is.False);
                Assert.That(Get<float>(placement, "m_raycastHitTimer"), Is.EqualTo(0f), "Mathf.Max(timer - step, 0), not Max(0, ...)");

                ProjectionPlacement stepped = Placement(true, 1);
                stepped.Initialise(2f, 99f, 2f, 99f);
                Assert.That(stepped.Update(start, end, 1f, gravity, Nothing, 0.5f), Is.True);
                Assert.That(stepped.Position, Is.EqualTo(new Vector3(1.5f, -0.3125f, 0f)));
                Box(owned, new Vector3(2.5f, -2.1875f, 0f), Vector3.one);
                Assert.That(stepped.Update(start, end, 1f, gravity, Probe, 0.5f), Is.True);
                Assert.That(stepped.RaycastHit, Is.True, "the ray runs along the step, not the whole offset");
                Assert.That(Get<float>(stepped, "m_raycastHitTimer"), Is.EqualTo(1f));
            });
        }

        // ProjectForRaycastHit 0x5CFDD0 with a zero gravity multiplier (ucomiss/jne/jp at 0x5CFECE)
        // adds the start position, adds |end - start| - m_distanceOffset to distance and fails.
        [Test]
        public void ProjectionWithoutGravityAddsOnlyTheStart()
        {
            ProjectionPlacement placement = Placement(false, 1);
            placement.Initialise(99f, 10f, 99f, 0f);
            var renderer = new LineRendererContainer();
            RaycastHit hit = default;
            float distance = 0.5f;
            Assert.That(placement.ProjectForRaycastHit(new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 7f), Vector3.down, Probe, renderer, ref hit, ref distance), Is.False);
            Assert.That(Positions(renderer), Is.EqualTo(new[] { new Vector3(1f, 2f, 3f) }));
            Assert.That(distance, Is.EqualTo(3.5f), "0.5 + (4 - 1)");
        }

        // With gravity the trace starts at the end with velocity d * m_speed, and each step adds
        // gravity * (m_gravityMultiplier * m_raycastStepTime) to the velocity, moves by
        // velocity * m_raycastStepTime, adds the step length to distance, and adds the previous
        // point (position - stepDirection * stepLength) while distance < |end - start|. A last
        // raycast from the end along the summed steps then adds the hit point, or the final position.
        // Speed 10 over a unit line gives one unit step: points (1,0,0) then (2,0,0), distance 1.
        // The loop is guarded by the caller's distance against |end - start| (ucomiss/jbe at
        // 0x5CFF6B, again at 0x5D0075); m_distanceOffset is read only on the zero-gravity path
        // (0x5CFEEC). A passed distance of 5 skips the loop: the final raycast has a zero length,
        // so only the end is added and distance stays 5. An offset of 1 changes nothing here.
        [Test]
        public void ProjectionTracesFromTheEndAndAddsTheFinalPositionWithoutAHit()
        {
            foreach (int distanceOffset in new[] { 0, 1 })
            {
                ProjectionPlacement placement = Placement(false, distanceOffset);
                placement.Initialise(99f, 10f, 99f, 1f);
                var renderer = new LineRendererContainer();
                RaycastHit hit = default;
                float distance = 0f;
                string label = "offset " + distanceOffset;
                Assert.That(placement.ProjectForRaycastHit(Vector3.zero, Vector3.right, Vector3.zero, Nothing, renderer, ref hit, ref distance), Is.False, label);
                Assert.That(Positions(renderer), Is.EqualTo(new[] { new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f) }), label);
                Assert.That(distance, Is.EqualTo(1f), label);
            }

            ProjectionPlacement travelled = Placement(false, 1);
            travelled.Initialise(99f, 10f, 99f, 1f);
            var travelledRenderer = new LineRendererContainer();
            RaycastHit travelledHit = default;
            float travelledDistance = 5f;
            Assert.That(travelled.ProjectForRaycastHit(Vector3.zero, Vector3.right, Vector3.zero, Nothing, travelledRenderer, ref travelledHit, ref travelledDistance), Is.False);
            Assert.That(Positions(travelledRenderer), Is.EqualTo(new[] { new Vector3(1f, 0f, 0f) }), "the loop never runs");
            Assert.That(travelledDistance, Is.EqualTo(5f));
        }

        // From (-1,0,0) to the origin at speed 10 under gravity -10y: one step of (1, -0.1, 0), length
        // sqrt(1.01). Its turn 1 - cos is about 0.005, below the default 0.1, so the trace adds the
        // previous point (the origin) and the last raycast from the origin hits the box face at
        // x = 0.5: distance becomes sqrt(1.01) + (hit.distance - sqrt(1.01)). With
        // m_velocityToForward at 0.001 the in-loop raycast (0x5D025E) hits instead and returns at
        // once, so only the hit point is added. From (-3,0,0) with the limit at 0.007 the first
        // step's turn (about 0.00496) stays below it and the second, (1, -0.2, 0), takes the sum to
        // about 0.0097: the in-loop ray from the origin along (2, -0.3, 0) hits the face at
        // (0.5, -0.075, 0). The in-loop hit subtracts the last step's length (0x5D0510), so distance
        // is sqrt(1.01) + hit.distance; subtracting the summed ray length instead would give about
        // hit.distance + 0.0024.
        [Test]
        public void ProjectionRaycastsTheSummedStepsWhenTheTurnExceedsTheLimitOrAtTheEnd()
        {
            Owned.Run(owned =>
            {
                Box(owned, new Vector3(0.75f, -0.075f, 0f), new Vector3(0.5f, 1f, 1f));
                var expectedPoint = new Vector3(0.5f, -0.05f, 0f);
                float hitDistance = expectedPoint.magnitude;

                foreach (bool inLoop in new[] { false, true })
                {
                    ProjectionPlacement placement = Placement(false);
                    placement.Initialise(99f, 10f, 99f, 1f);
                    if (inLoop) Set(placement, "m_velocityToForward", 0.001f);
                    var renderer = new LineRendererContainer();
                    RaycastHit hit = default;
                    float distance = 0f;
                    string label = inLoop ? "in-loop raycast" : "final raycast";
                    Assert.That(placement.ProjectForRaycastHit(new Vector3(-1f, 0f, 0f), Vector3.zero, new Vector3(0f, -10f, 0f), Probe, renderer, ref hit, ref distance), Is.True, label);
                    Vector3[] positions = Positions(renderer);
                    Assert.That(positions, Has.Length.EqualTo(inLoop ? 1 : 2), label);
                    if (!inLoop) WorldMemberAccess.AreClose(Vector3.zero, positions[0], label + " previous point");
                    WorldMemberAccess.AreClose(expectedPoint, positions[positions.Length - 1], label + " hit point");
                    WorldMemberAccess.AreClose(expectedPoint, hit.point, label + " hitInfo");
                    Assert.That(distance, Is.EqualTo(hitDistance).Within(1e-4f), label);
                }

                ProjectionPlacement twoStep = Placement(false);
                twoStep.Initialise(99f, 10f, 99f, 1f);
                Set(twoStep, "m_velocityToForward", 0.007f);
                var twoStepRenderer = new LineRendererContainer();
                RaycastHit twoStepHit = default;
                float twoStepDistance = 0f;
                var twoStepPoint = new Vector3(0.5f, -0.075f, 0f);
                Assert.That(twoStep.ProjectForRaycastHit(new Vector3(-3f, 0f, 0f), Vector3.zero, new Vector3(0f, -10f, 0f), Probe, twoStepRenderer, ref twoStepHit, ref twoStepDistance), Is.True, "two-step in-loop raycast");
                Vector3[] twoStepPositions = Positions(twoStepRenderer);
                Assert.That(twoStepPositions, Has.Length.EqualTo(2), "two-step in-loop raycast");
                WorldMemberAccess.AreClose(Vector3.zero, twoStepPositions[0], "two-step previous point");
                WorldMemberAccess.AreClose(twoStepPoint, twoStepPositions[1], "two-step hit point");
                WorldMemberAccess.AreClose(twoStepPoint, twoStepHit.point, "two-step hitInfo");
                Assert.That(twoStepDistance, Is.EqualTo(Mathf.Sqrt(1.01f) + twoStepPoint.magnitude).Within(1e-4f), "two-step distance");
            });
        }
    }
}
