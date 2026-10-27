using System;
using HardlightProject;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.EnemyAbilityTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime Enemy.DoOnFixedUpdatePreFSM (0600213e @0x56C200). All three gravity sites call the virtual
    // GravityNormalised (vcall slot 17 at 0x56c286, 0x56c315 and 0x56c39e) and use the result unnegated: the realignment
    // skip adds it to UpDirection (addps 0x56c293), it is the RotateTowards target (0x56c31b/0x56c31e), and it is the
    // stick ray direction while the gravity force is on. Only the -UpDirection operands are negated (0x56c2e0, 0x56c3b9).
    // The enemy is the shared real-component scene; its body keeps the default downward gravity.
    public sealed class EnemyGravityAlignmentTests
    {
        private const float DeltaTime = 0.02f;
        private const float Tolerance = 1e-4f;

        // The members the fixed update reads after the gravity block: the buff handler, and an animator with the three
        // bools UpdateBrain reads. The definition's collider settings come from the case.
        private static void Prepare(Scene scene, CharacterSettings.ColliderSettings collider)
        {
            SetActorProperty(scene.Enemy, "BuffHandler", new ActorBuffHandler(scene.Enemy));
            var animator = scene.Owned.Host(Name(), true).AddComponent<Animator>();
            var controller = scene.Owned.Track(new AnimatorController());
            controller.AddLayer("Base");
            scene.Owned.Track(controller.layers[0].stateMachine);
            controller.AddParameter("AttackAnimComplete[FSM]", AnimatorControllerParameterType.Bool);
            controller.AddParameter("WindUpAnimStarted[FSM]", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Alert[Bool]", AnimatorControllerParameterType.Bool);
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetActorProperty(scene.Enemy, "Animator", new ActorAnimator(animator));
            scene.Definition.Settings = new CharacterSettings { Collider = collider };
            Assert.That(scene.Body.GravityForceEnabled, Is.True, "the body starts with the gravity force on");
            Assert.That(scene.Enemy.GravityNormalised, Is.EqualTo(Vector3.down));
        }

        private static void FixedUpdate(Enemy enemy) => Call(enemy, "DoOnFixedUpdatePreFSM", DeltaTime);

        // An upright enemy under downward gravity has |Up + GravityNormalised|^2 = 0, below 9.9999994e-11 (0x29a6e8c), so
        // the original skips the realignment and the enemy stays upright.
        [Test]
        public void UprightEnemyUnderGravityIsNotRealigned()
        {
            WithScene(scene =>
            {
                Prepare(scene, new CharacterSettings.ColliderSettings());
                FixedUpdate(scene.Enemy);
                Assert.That(Vector3.Angle(scene.Enemy.UpDirection, Vector3.up), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(scene.Enemy.ForwardDirection, Vector3.forward), Is.LessThan(0.01f));
            });
        }

        // A tilted enemy turns its down direction towards gravity by RotateToGravityRadiansPerSecond * deltaTime
        // (180 degrees per second, so 3.6 degrees in one step) and orientates to the negated result.
        [Test]
        public void TiltedEnemyTurnsItsDownDirectionTowardsGravity()
        {
            WithScene(scene =>
            {
                Prepare(scene, new CharacterSettings.ColliderSettings());
                scene.Enemy.SetWorldRotation(Quaternion.Euler(0f, 0f, 30f));
                Vector3 up = scene.Enemy.UpDirection;
                Vector3 down = Vector3.RotateTowards(-up, Vector3.down, scene.Traits.RotateToGravityRadiansPerSecond * DeltaTime, 0f);

                FixedUpdate(scene.Enemy);

                Assert.That(Vector3.Angle(scene.Enemy.UpDirection, -down), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(scene.Enemy.UpDirection, Vector3.up), Is.EqualTo(26.4f).Within(0.01f));
            });
        }

        // With the gravity force on, the stick ray is cast along GravityNormalised for GroundCheckMinDistance and the enemy
        // moves to the hit point: the floor below is hit, the ceiling above is not.
        [Test]
        public void StickRayIsCastAlongGravity()
        {
            WithScene(scene =>
            {
                Prepare(scene, new CharacterSettings.ColliderSettings { StickToColliderMask = 1, GroundCheckMinDistance = 1f });
                Box(scene.Owned, new Vector3(0f, -1f, 0f));
                Box(scene.Owned, new Vector3(0f, 1.25f, 0f));
                Physics.SyncTransforms();

                FixedUpdate(scene.Enemy);

                Assert.That(scene.Enemy.WorldPosition.y, Is.EqualTo(-0.5f).Within(Tolerance), "moved onto the floor's top face");
                Assert.That(scene.Enemy.WorldPosition.x, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(scene.Enemy.WorldPosition.z, Is.EqualTo(0f).Within(Tolerance));
            });
        }

        // A unit-high, wide box on the default layer centred at the given point.
        private static void Box(Owned owned, Vector3 centre)
        {
            GameObject host = owned.Host(Name());
            host.transform.position = centre;
            host.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 4f);
        }
    }
}
