using System;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterPhysicsUtilities.StickToCollider (0600390c @0x6A9E20). The character is a real
    // component on an owned inactive host, so its own collider is not in the physics scene; the ground is an active box
    // on the default layer below it. The reach and the pull-down are |velocity| * deltaTime * tan(angle), from the
    // _tanf call at 0x6a9f8f; a sine would fall short of both floors these cases place.
    public sealed class CharacterPhysicsUtilitiesTests
    {
        private static readonly Vector3 Origin = new Vector3(50f, 50f, 50f);
        private const float Tolerance = 1e-4f;

        // Gravity points down and the character faces up, moving at 10 along x, so one 0.1 s step covers 1 unit.
        private static void WithStick(float angle, float force, float floorDrop, Action<CharacterGravityVectorProbe, GameObject> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            character.Settings.Collider.StickToColliderMask = 1;
            character.Settings.Collider.StickToColliderAngleMax = angle;
            character.Settings.Collider.StickToColliderForce = force;
            SetActorProperty(character, "UpDirection", Vector3.up);
            character.SetWorldPosition(Origin);
            character.SetWorldVelocity(new Vector3(10f, 0f, 0f));

            GameObject floor = owned.Host("Lucid stick floor");
            floor.transform.position = Origin - new Vector3(0f, floorDrop + 0.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();
            body(character, floor);
        });

        // At the shipped 45 degrees the reach is a full step (tan 45 = 1): a floor 0.85 below is reached, where a sine
        // reach (0.707) would miss it, and the velocity loses one unit along the floor normal (force 1).
        [Test]
        public void ReachIsTheStepTimesTheTangentOfTheAngle()
        {
            WithStick(45f, 1f, 0.85f, (character, floor) =>
            {
                Assert.That(CharacterPhysicsUtilities.StickToCollider(character, 0.1f), Is.True);
                Assert.That(character.WorldVelocity.x, Is.EqualTo(10f).Within(Tolerance));
                Assert.That(character.WorldVelocity.y, Is.EqualTo(-1f).Within(Tolerance));
                Assert.That(character.WorldVelocity.z, Is.EqualTo(0f).Within(Tolerance));
            });
        }

        // The reach ends at the tangent: a floor 1.05 below a 45 degree, one-unit step is not touched and the velocity
        // is left as it was.
        [Test]
        public void FloorBeyondTheTangentReachIsNotTouched()
        {
            WithStick(45f, 1f, 1.05f, (character, floor) =>
            {
                Assert.That(CharacterPhysicsUtilities.StickToCollider(character, 0.1f), Is.False);
                Assert.That(character.WorldVelocity, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            });
        }

        // The pull-down is the same tangent reach scaled by the force (0x6aa000): at 30 degrees and force 2 it is
        // 2 * tan 30 = 1.1547, where a sine would give 1.
        [Test]
        public void PullDownIsTheTangentReachTimesTheForce()
        {
            WithStick(30f, 2f, 0.2f, (character, floor) =>
            {
                Assert.That(CharacterPhysicsUtilities.StickToCollider(character, 0.1f), Is.True);
                Assert.That(character.WorldVelocity.x, Is.EqualTo(10f).Within(Tolerance));
                Assert.That(character.WorldVelocity.y, Is.EqualTo(-2f * Mathf.Tan(30f * Mathf.Deg2Rad)).Within(Tolerance));
                Assert.That(character.WorldVelocity.y, Is.Not.EqualTo(-1f).Within(0.1f), "a sine reach would pull down by 1");
            });
        }
    }
}
