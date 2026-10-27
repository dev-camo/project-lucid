using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime ColliderTrackingUtilities.ProjectToNeighbourSlope (0600391b @0x6ABB80). Each query step
    // calls the gravity callback for its five frames, then casts the recontact ray from the position the step started
    // at: the position locals are written only by the integration block after the cast (0x6ac274/0x6ac288) and the ray
    // origin reads them unchanged (0x6acb96). Here a character leaves the top of a wall straight up under a constant
    // gravity stub and falls back past the lip; the recontact point is the pre-step position projected on the wall.
    public sealed class ColliderTrackingNeighbourSlopeTests
    {
        private static readonly Vector3 Base = new Vector3(200f, 30f, 200f);
        private const float LipHeight = 1.2f;
        private const float Speed = 8f;
        private const float DeltaTime = 1f / 60f;
        private static readonly Vector3 GravityAcceleration = new Vector3(0f, -20f, 0f);

        [Test]
        public void RecontactRayStartsFromThePositionBeforeTheStep()
        {
            Owned.Run(owned =>
            {
                // A wide wall whose face is the plane z = Base.z, facing -z, with its top LipHeight above Base.
                GameObject wall = owned.Host("Lucid neighbour slope wall");
                wall.transform.position = Base + new Vector3(0f, LipHeight - 10f, 1f);
                wall.AddComponent<BoxCollider>().size = new Vector3(20f, 20f, 2f);
                Physics.SyncTransforms();

                int gravityCalls = 0;
                var start = new ColliderTrackingUtilities.RaycastTransform
                {
                    LayerMask = 1,
                    Position = Base,
                    Direction = Vector3.forward,
                    Up = Vector3.up
                };
                bool found = ColliderTrackingUtilities.ProjectToNeighbourSlope(start, Vector3.up, Vector3.up, Speed,
                    velocity => { gravityCalls++; return GravityAcceleration; }, false, DeltaTime,
                    out float hitTime, out Vector3 hitPosition, out Vector3 hitNormal);

                // The original's order: the step's ray starts where the step began; integration follows the cast.
                float queryInterval = DeltaTime * 5f;
                Vector3 position = Base;
                Vector3 velocity = Vector3.up * Speed;
                bool airborne = false;
                float expectedTime = 0f;
                Vector3 expectedOrigin = Vector3.zero;
                Vector3 nextPosition = Vector3.zero;
                int steps = 0;
                for (int step = 0; step < 32; step++)
                {
                    expectedTime += queryInterval;
                    Vector3 origin = position;
                    for (int frame = 0; frame < 5; frame++)
                    {
                        velocity += GravityAcceleration * DeltaTime;
                        position += velocity * DeltaTime;
                    }
                    steps++;
                    bool hit = origin.y < Base.y + LipHeight;
                    if (hit && airborne)
                    {
                        expectedOrigin = origin;
                        nextPosition = position;
                        break;
                    }
                    airborne |= !hit;
                }

                Assert.That(airborne, Is.True, "the path leaves the wall before it comes back");
                Assert.That(Base.y + LipHeight - expectedOrigin.y, Is.GreaterThan(0.01f), "the recontact origin is clearly below the lip");
                Assert.That(found, Is.True);
                Assert.That(hitTime, Is.EqualTo(expectedTime).Within(1e-5f));
                Assert.That(gravityCalls, Is.EqualTo(steps * 5), "five gravity frames per step, the last step's before its cast");
                Assert.That(hitPosition.x, Is.EqualTo(Base.x).Within(1e-4f));
                Assert.That(hitPosition.y, Is.EqualTo(expectedOrigin.y).Within(1e-4f));
                Assert.That(hitPosition.z, Is.EqualTo(Base.z).Within(1e-4f));
                Assert.That(Mathf.Abs(hitPosition.y - nextPosition.y), Is.GreaterThan(0.05f), "not the position after the step");
                Assert.That(hitNormal, Is.EqualTo(Vector3.back));
            });
        }
    }
}
