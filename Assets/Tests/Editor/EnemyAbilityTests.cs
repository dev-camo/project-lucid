using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.EnemyAbilityTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy abilities (EnemyAbility 02000631, EnemyAbility_Movement`1 02000639 and its AirMovement 02000632,
    // FireProjectile (see EnemyFireProjectileAbilityTests), GroundMovement 02000636 and GroundMovement_WithTarget
    // 02000637; Dead 02000633, GroundAttack 02000635, Idle 02000638 and Taunt 0200063a) against the 1.10.1 x86-64
    // bodies at 0x56E4F0..0x570D30 and the shared generic bodies at 0x117F560..0x117FB00. Each ability is built by its
    // shipped constructor and initialised with an authored definition on a real Enemy (see EnemyAbilityTestSupport).
    // Not covered: the abilities driven by a started Actor (Actor.InitialiseAbilities needs the actor traits and the
    // data manager's definitions) and the animator parameters through a started ActorAnimator update.
    public sealed class EnemyAbilityTests
    {
        private static void Curves(EnemyAbilityDefinition_Movement definition)
        {
            definition.TurnSameDirectionCurve = AnimationCurve.Constant(0f, 1f, 1f);
            definition.AccelerationCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }

        // The curve value is the elapsed time, so the held duration is visible in the result.
        private static void TimeCurves(EnemyAbilityDefinition_Movement definition)
        {
            definition.TurnSameDirectionCurve = AnimationCurve.Linear(0f, 0f, 10f, 10f);
            definition.AccelerationCurve = AnimationCurve.Linear(0f, 0f, 10f, 10f);
        }

        // ---- declarations ----

        // Flags, member order and Il2CppSetOption order from the DummyDll tables (1 = NullChecks, 2 = ArrayBoundsChecks).
        // An emission self-check of the restored declarations, not a native behaviour oracle.
        [Test]
        public void EmissionCheck_AbilitiesKeepTheirFlagsFieldsAndMethods()
        {
            var none = new (string, int)[0];
            var ctor = new[] { (".ctor", 0x1886) };
            var definitionBase = ("get_DefinitionBase", 0x08c6);
            var initialise = ("Initialise", 0x00c6);
            var doOnUpdate = ("DoOnUpdate", 0x00c4);
            var updateMovement = ("UpdateMovement", 0x00c6);
            var updateAnimation = ("UpdateMovementAnimation", 0x00c6);
            var proxyMovement = ("UpdateVisualProxyMovement", 0x00c6);

            AssertMembers(typeof(EnemyAbility), 0x00100081,
                new[] { ("m_enemy", 0x0004), ("<IsTriggered>k__BackingField", 0x0001) },
                new[] { initialise, ("get_IsTriggered", 0x09e6), ("set_IsTriggered", 0x0884), ("UpdateMovement", 0x05c6),
                    ("UpdateMovementAnimation", 0x05c6), ("UpdateVisualProxyMovement", 0x01c6), (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(EnemyAbility), 2, 1);
            Assert.That(typeof(EnemyAbility).GetInterfaces(), Does.Contain(typeof(IAbilityTrigger)));

            Type movement = typeof(EnemyAbility_Movement<>);
            AssertMembers(movement, 0x00100081,
                new[] { ("<Definition>k__BackingField", 0x0001), ("m_previousPosition", 0x0001), ("m_thisFrameMovement", 0x0004),
                    ("m_previousFrameMovement", 0x0001), ("m_timeSpentAccelerating", 0x0001), ("m_timeSpentTurningDirection", 0x0001) },
                new[] { ("get_Definition", 0x0886), ("set_Definition", 0x0881), definitionBase, initialise, doOnUpdate, updateMovement,
                    ("CompareMovement", 0x0081), updateAnimation, (".ctor", 0x1884) });
            AssertIl2CppOptions(movement, 1, 2);
            Assert.That(movement.GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(EnemyAbilityDefinition_Movement) }));

            AssertMembers(typeof(EnemyAbility_AirMovement), 0x00100001, none, new[] { updateMovement, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_AirMovement), 1, 2);
            AssertMembers(typeof(EnemyAbility_Dead), 0x00100001, new[] { ("m_definition", 0x0001) },
                new[] { definitionBase, initialise, doOnUpdate, updateMovement, updateAnimation, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_Dead), 2, 1);
            AssertMembers(typeof(EnemyAbility_GroundAttack), 0x00100001, new[] { ("m_definition", 0x0001) },
                new[] { definitionBase, initialise, doOnUpdate, updateMovement, updateAnimation, proxyMovement, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_GroundAttack), 1, 2);
            AssertMembers(typeof(EnemyAbility_GroundMovement), 0x00100001, none, ctor);
            AssertIl2CppOptions(typeof(EnemyAbility_GroundMovement), 2, 1);
            AssertMembers(typeof(EnemyAbility_GroundMovement_WithTarget), 0x00100001, none,
                new[] { doOnUpdate, updateAnimation, proxyMovement, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_GroundMovement_WithTarget), 2, 1);
            AssertMembers(typeof(EnemyAbility_Idle), 0x00100001, new[] { ("m_definition", 0x0001) },
                new[] { definitionBase, initialise, doOnUpdate, updateMovement, updateAnimation, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_Idle), 1, 2);
            AssertMembers(typeof(EnemyAbility_Taunt), 0x00100001, new[] { ("m_definition", 0x0001), ("m_tauntedForSeconds", 0x0001) },
                new[] { definitionBase, initialise, ("DoOnEnter", 0x00c4), doOnUpdate, updateMovement, updateAnimation, (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbility_Taunt), 2, 1);
            Assert.That(new[] { typeof(EnemyAbility_AirMovement), typeof(EnemyAbility_GroundMovement) }
                .Select(type => type.BaseType.GetGenericTypeDefinition()), Is.All.EqualTo(movement));
        }

        // ---- the base ability ----

        // 0x56E4F0 runs the actor ability initialisation (0x5197c0) and then keeps the actor only when it is an Enemy
        // (the class test against the Enemy type); any other actor, or none, leaves the field null.
        [Test]
        public void InitialiseKeepsTheActorOnlyWhenItIsAnEnemy()
        {
            WithScene(scene =>
            {
                var definition = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_Idle>());
                var ability = new EnemyAbility_Idle();
                ability.Initialise(scene.Enemy, definition);
                Assert.That(Get<Enemy>(ability, "m_enemy"), Is.SameAs(scene.Enemy));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));
                Assert.That(ability.Enabled, Is.False);
                Assert.That(ability.UpdateEnabled, Is.True);

                var other = new EnemyAbility_Idle();
                other.Initialise(scene.Target(Vector3.zero), definition);
                Assert.That(Get<Enemy>(other, "m_enemy"), Is.Null, "an actor that is not an enemy");
                var none = new EnemyAbility_Idle();
                none.Initialise(null, definition);
                Assert.That(Get<Enemy>(none, "m_enemy"), Is.Null, "no actor");
            });
        }

        // 0x117F5A0: the definition is kept only when it is the ability's own definition class; another definition
        // class leaves it null (the cast of a failed class test passes through null).
        [Test]
        public void MovementKeepsOnlyItsOwnDefinitionClass()
        {
            WithScene(scene =>
            {
                var own = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_GroundMovement>());
                var ability = new EnemyAbility_GroundMovement();
                ability.Initialise(scene.Enemy, own);
                Assert.That(ability.Definition, Is.SameAs(own));
                Assert.That(ability.DefinitionBase, Is.SameAs(own));

                var wrong = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_Idle>());
                var rejected = new EnemyAbility_GroundMovement();
                rejected.Initialise(scene.Enemy, wrong);
                Assert.That(rejected.Definition, Is.Null);
                Assert.That(Get<Enemy>(rejected, "m_enemy"), Is.SameAs(scene.Enemy), "the enemy is kept regardless");
                // The ground movement definition is also the base of the with-target one.
                var derived = scene.Owned.Track(ScriptableObject.CreateInstance<EnemyAbilityDefinition_GroundMovement_WithTarget>());
                var withTarget = new EnemyAbility_GroundMovement_WithTarget();
                withTarget.Initialise(scene.Enemy, derived);
                Assert.That(withTarget.Definition, Is.SameAs(derived));
            });
        }

        // 0x56E5E0: a proxy that is not detached is left alone. A detached proxy faces the enemy's rotation, except on
        // a spline (the navigation's location rotation; an unstarted spline reports identity) or a static-and-return
        // navigation, where it faces along the right axis of the tracked rotation with the enemy's up direction. The
        // enemy stands at 30 degrees yaw, which tells the three results apart: 30 (as is), 90 (spline), 120 (static).
        [Test]
        public void TheVisualProxyIsSteeredOnlyWhenDetachedAndFollowsTheNavigationRotation()
        {
            WithScene(scene =>
            {
                EnemyAbility ability = scene.Ability<EnemyAbility_Idle, EnemyAbilityDefinition_Idle>();
                EnemyVisualProxyProbe proxy = scene.AttachProxy();
                scene.Enemy.SetWorldRotation(Quaternion.Euler(0f, 30f, 0f));

                ability.UpdateVisualProxyMovement(0.1f);
                Assert.That(proxy.RotationWrites, Is.EqualTo(0), "the proxy is not detached");

                scene.Traits.ShouldDetachVisualProxy = true;
                ability.UpdateVisualProxyMovement(0.1f);
                Assert.That(proxy.RotationWrites, Is.EqualTo(1));
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 30f, 0f), "no navigation");

                Set(scene.Enemy, "m_navigation", scene.Owned.Host(Name(), false).AddComponent<EnemyNavigationProbe>());
                ability.UpdateVisualProxyMovement(0.1f);
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 30f, 0f), "another navigation");

                Set(scene.Enemy, "m_navigation", scene.Owned.Host(Name(), false).AddComponent<EnemyNavigationSpline>());
                ability.UpdateVisualProxyMovement(0.1f);
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 90f, 0f), "spline navigation");

                Set(scene.Enemy, "m_navigation", scene.Owned.Host(Name(), false).AddComponent<EnemyNavigationStaticAndReturnAfterChase>());
                ability.UpdateVisualProxyMovement(0.1f);
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 120f, 0f), "static navigation");
                Assert.That(proxy.RotationWrites, Is.EqualTo(4));
            });
        }

        // 0x570500 and 0x5706F0 override the proxy movement: a detached proxy always faces the enemy's rotation,
        // whatever the navigation, and an attached one is left alone.
        [Test]
        public void GroundAttackAndGroundMovementWithTargetKeepTheProxyOnTheEnemyRotation()
        {
            WithScene(scene =>
            {
                EnemyAbility attack = scene.Ability<EnemyAbility_GroundAttack, EnemyAbilityDefinition_GroundAttack>();
                EnemyAbility withTarget = scene.Ability<EnemyAbility_GroundMovement_WithTarget, EnemyAbilityDefinition_GroundMovement_WithTarget>();
                EnemyVisualProxyProbe proxy = scene.AttachProxy();
                scene.Enemy.SetWorldRotation(Quaternion.Euler(0f, 30f, 0f));
                Set(scene.Enemy, "m_navigation", scene.Owned.Host(Name(), false).AddComponent<EnemyNavigationSpline>());

                attack.UpdateVisualProxyMovement(0.1f);
                withTarget.UpdateVisualProxyMovement(0.1f);
                Assert.That(proxy.RotationWrites, Is.EqualTo(0), "not detached");

                scene.Traits.ShouldDetachVisualProxy = true;
                attack.UpdateVisualProxyMovement(0.1f);
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 30f, 0f), "ground attack on a spline");
                proxy.LastRotation = Quaternion.identity;
                withTarget.UpdateVisualProxyMovement(0.1f);
                AssertRotation(proxy.LastRotation, Quaternion.Euler(0f, 30f, 0f), "ground movement with target on a spline");
                Assert.That(proxy.RotationWrites, Is.EqualTo(2));
            });
        }

        // ---- movement ----

        // 0x117F650: triggered when the brain's impulse differs from zero (the vector comparison tolerates less than
        // 1e-5 in length) and the target is not locked; 0x570590 (with target) needs the lock instead.
        [Test]
        public void MovementTriggersFromTheImpulseAndTheTargetLock()
        {
            WithScene(scene =>
            {
                EnemyAbility plain = scene.Ability<EnemyAbility_GroundMovement, EnemyAbilityDefinition_GroundMovement>();
                EnemyAbility withTarget = scene.Ability<EnemyAbility_GroundMovement_WithTarget, EnemyAbilityDefinition_GroundMovement_WithTarget>();
                plain.Enter();
                withTarget.Enter();
                foreach ((float x, float y, bool locked, bool plainTriggered, bool withTargetTriggered) in new[]
                {
                    (0f, 0f, false, false, false), (0f, 0f, true, false, false), (0.5f, 0f, false, true, false),
                    (0.5f, 0f, true, false, true), (0f, -1f, false, true, false), (0f, -1f, true, false, true),
                    (1e-6f, 0f, false, false, false), (1e-6f, 0f, true, false, false), (1e-3f, 0f, true, false, true),
                })
                {
                    scene.Impulse(x, y);
                    scene.Flag("BrainTargetLocked", locked);
                    plain.Update(0.02f);
                    withTarget.Update(0.02f);
                    string why = "impulse (" + x + ", " + y + "), locked " + locked;
                    Assert.That(plain.IsTriggered, Is.EqualTo(plainTriggered), "ground movement, " + why);
                    Assert.That(withTarget.IsTriggered, Is.EqualTo(withTargetTriggered), "with target, " + why);
                }
            });
        }

        // 0x117F6E0: the impulse is shifted into the previous-frame slot and each axis counts how long it has held the
        // exact same value (0x117F8F0); the turn is the x curve at that time times x times the turn speed times the
        // frame time, a rotation about the enemy's up applied in world space, and the forward speed is y times the
        // forward speed times the y curve at its held time, set on the local z axis while the local y velocity stays.
        [Test]
        public void MovementTurnsAndAcceleratesByTheCurvesOverHowLongTheImpulseHasBeenHeld()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_GroundMovement, EnemyAbilityDefinition_GroundMovement>(definition =>
                {
                    definition.TurnSpeed = 100f;
                    definition.ForwardSpeed = 4f;
                    TimeCurves(definition);
                });
                scene.Enemy.SetWorldVelocity(new Vector3(0f, -2f, 0f));
                scene.Impulse(1f, 1f);

                ability.UpdateMovement(0.5f);
                AssertRotation(scene.Enemy.WorldRotation, Quaternion.identity, "both axes just changed: the curves read zero");
                AssertNear(scene.Enemy.WorldVelocity, new Vector3(0f, -2f, 0f), "no forward speed yet");
                Assert.That(Get<float>(ability, "m_timeSpentTurningDirection"), Is.EqualTo(0f));
                Assert.That(Get<float>(ability, "m_timeSpentAccelerating"), Is.EqualTo(0f));

                ability.UpdateMovement(0.5f);
                AssertRotation(scene.Enemy.WorldRotation, Quaternion.Euler(0f, 25f, 0f), "held 0.5 s: 0.5 * 1 * 100 * 0.5 degrees");
                AssertNear(scene.Enemy.WorldVelocity, new Vector3(0.84523652f, -2f, 1.81261558f), "forward 1 * 4 * 0.5 along the turned z axis");

                ability.UpdateMovement(0.5f);
                AssertRotation(scene.Enemy.WorldRotation, Quaternion.Euler(0f, 75f, 0f), "held 1 s: another 50 degrees");
                AssertNear(scene.Enemy.WorldVelocity, new Vector3(3.86370331f, -2f, 1.0352762f), "forward 1 * 4 * 1");

                // Only x changes: its hold time restarts while y keeps counting.
                scene.Impulse(0.5f, 1f);
                ability.UpdateMovement(0.5f);
                AssertRotation(scene.Enemy.WorldRotation, Quaternion.Euler(0f, 75f, 0f), "x just changed");
                AssertNear(scene.Enemy.WorldVelocity, new Vector3(5.79555496f, -2f, 1.5529143f), "forward 1 * 4 * 1.5");
                Assert.That(Get<float>(ability, "m_timeSpentTurningDirection"), Is.EqualTo(0f));
                Assert.That(Get<float>(ability, "m_timeSpentAccelerating"), Is.EqualTo(1.5f));
            });
        }

        // 0x117F7A2 onward: while the alert animation plays, the turn still applies but the local velocity is zero.
        [Test]
        public void MovementStopsForwardMotionWhileTheAlertAnimationPlays()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_GroundMovement, EnemyAbilityDefinition_GroundMovement>(definition =>
                {
                    definition.TurnSpeed = 90f;
                    definition.ForwardSpeed = 4f;
                    Curves(definition);
                });
                scene.Enemy.SetWorldVelocity(new Vector3(1f, -2f, 3f));
                scene.Impulse(1f, 1f);
                scene.Flag("AlertAnimation", true);
                ability.UpdateMovement(0.5f);
                AssertRotation(scene.Enemy.WorldRotation, Quaternion.Euler(0f, 45f, 0f), "1 * 1 * 90 * 0.5 degrees");
                AssertNear(scene.Enemy.WorldVelocity, Vector3.zero, "alert");
            });
        }

        // 0x117F8F0: each axis is compared exactly and independently; an axis that held its value adds the frame time
        // to its counter, one that changed resets it to zero.
        [Test]
        public void EachMovementAxisCountsItsOwnHoldTime()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_GroundMovement, EnemyAbilityDefinition_GroundMovement>(Curves);
                scene.Impulse(0.5f, 0.25f);
                ability.UpdateMovement(0.25f);
                Assert.That((Get<float>(ability, "m_timeSpentTurningDirection"), Get<float>(ability, "m_timeSpentAccelerating")), Is.EqualTo((0f, 0f)));
                ability.UpdateMovement(0.25f);
                Assert.That((Get<float>(ability, "m_timeSpentTurningDirection"), Get<float>(ability, "m_timeSpentAccelerating")), Is.EqualTo((0.25f, 0.25f)));
                scene.Impulse(0.5f, 0.75f);
                ability.UpdateMovement(0.5f);
                Assert.That((Get<float>(ability, "m_timeSpentTurningDirection"), Get<float>(ability, "m_timeSpentAccelerating")), Is.EqualTo((0.75f, 0f)));
                scene.Impulse(0f, 0.75f);
                ability.UpdateMovement(0.5f);
                Assert.That((Get<float>(ability, "m_timeSpentTurningDirection"), Get<float>(ability, "m_timeSpentAccelerating")), Is.EqualTo((0f, 0.5f)));
            });
        }

        // 0x117F940: the animation takes the definition's forward speed (not the measured one) and whether the x
        // impulse is non-zero; only a detached proxy also reports moving (local x or z beyond 1e-4) and lateral
        // travel (-1, or +1 while a spline navigation is reversing, and 0 when not moving). A spotted target sets the
        // bool animation and is then consumed through the enemy.
        [Test]
        public void MovementAnimationReportsSpeedTurningMovingAndLateral()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                var ability = scene.Ability<EnemyAbility_GroundMovement, EnemyAbilityDefinition_GroundMovement>(definition =>
                {
                    definition.ForwardSpeed = 4f;
                    Curves(definition);
                });
                scene.Impulse(0.5f, 0f);
                ability.UpdateMovement(0.1f);
                scene.Enemy.SetWorldVelocity(new Vector3(0.5f, 0f, 0f));
                scene.Animator.SetBool("Moving", true);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(4f));
                Assert.That(scene.Animator.GetBool("Turning"), Is.True);
                Assert.That(scene.Animator.GetBool("Moving"), Is.True, "an attached proxy leaves the moving flag alone");
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(Sentinel), "and the lateral value");

                scene.Impulse(0f, 0f);
                ability.UpdateMovement(0.1f);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetBool("Turning"), Is.False);

                scene.Traits.ShouldDetachVisualProxy = true;
                scene.Enemy.SetWorldRotation(Quaternion.identity);
                foreach ((Vector3 local, bool moving) in new[]
                {
                    (Vector3.zero, false), (new Vector3(0.5f, 0f, 0f), true), (new Vector3(0f, 7f, 0.5f), true),
                    (new Vector3(5e-5f, 0f, 5e-5f), false), (new Vector3(2e-4f, 0f, 0f), true),
                })
                {
                    scene.Animator.SetBool("Moving", !moving);
                    scene.Animator.SetFloat("Lateral", Sentinel);
                    scene.Enemy.SetWorldVelocity(local);
                    ability.UpdateMovementAnimation(scene.Animation);
                    Assert.That(scene.Animator.GetBool("Moving"), Is.EqualTo(moving), "local velocity " + local);
                    Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(moving ? -1f : 0f), "local velocity " + local);
                }

                var spline = scene.Owned.Host(Name(), false).AddComponent<EnemyNavigationSpline>();
                Set(scene.Enemy, "m_navigation", spline);
                scene.Enemy.SetWorldVelocity(new Vector3(0.5f, 0f, 0f));
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(-1f), "a spline that is not reversing");
                Set(spline, "m_reversing", true);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(1f), "a reversing spline");
                scene.Enemy.SetWorldVelocity(Vector3.zero);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(0f), "not moving");

                scene.Flag("BrainTargetSpotted", true);
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetBool("Spotted"), Is.True);
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true), Is.False, "consumed");
                Assert.That(scene.Enemy.BrainTargetSpotted, Is.False);
            });
        }

        // 0x570620: the with-target animation sets the speed and the turning flag only (no moving or lateral value,
        // even for a detached proxy), and consumes a spotted target.
        [Test]
        public void GroundMovementWithTargetAnimatesOnlySpeedAndTurning()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                scene.Traits.ShouldDetachVisualProxy = true;
                var ability = scene.Ability<EnemyAbility_GroundMovement_WithTarget, EnemyAbilityDefinition_GroundMovement_WithTarget>(definition =>
                {
                    definition.ForwardSpeed = 6f;
                    Curves(definition);
                });
                scene.Impulse(-0.5f, 0f);
                ability.UpdateMovement(0.1f);
                scene.Enemy.SetWorldVelocity(new Vector3(0.5f, 0f, 0f));
                scene.Flag("BrainTargetSpotted", true);
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true);
                scene.Animator.SetBool("Moving", true);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(6f));
                Assert.That(scene.Animator.GetBool("Turning"), Is.True);
                Assert.That(scene.Animator.GetBool("Spotted"), Is.True);
                Assert.That(scene.Animator.GetBool("Moving"), Is.True, "untouched");
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(Sentinel), "untouched");
                Assert.That(scene.Enemy.BrainTargetSpotted, Is.False, "consumed");
            });
        }

        // ---- air movement ----

        // 0x56E770: the base movement runs first; the world velocity then loses its component along gravity (the
        // simplified body's gravity points down).
        [Test]
        public void AirMovementRemovesTheVelocityAlongGravity()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_AirMovement, EnemyAbilityDefinition_AirMovement>(definition =>
                {
                    definition.ForwardSpeed = 4f;
                    Curves(definition);
                });
                scene.Enemy.SetWorldVelocity(new Vector3(0f, -5f, 0f));
                scene.Impulse(0f, 1f);
                ability.UpdateMovement(0.02f);
                AssertNear(scene.Enemy.WorldVelocity, new Vector3(0f, 0f, 4f), "forward 4 with the fall removed");
                AssertNear(scene.Body.WorldVelocity, new Vector3(0f, 0f, 4f), "stored on the body");
            });
        }

        // 0x56E83C..0x56E99B: without the height restriction nothing is cast. With it, a ray along gravity (triggers
        // ignored, the authored mask and length) measures the ground distance; a distance outside [min, max] moves the
        // enemy along the ray from the hit point by clamped/distance of the way, unclamped (a distance of 1 with a
        // minimum of 2 puts it at 2, beyond the original position); within 1e-4 of the ground it lands on the hit point.
        [Test]
        public void AirMovementClampsTheHeightAboveTheGround()
        {
            WithScene(scene =>
            {
                var floor = scene.Owned.Host("Lucid enemy floor", true);
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
                Physics.SyncTransforms();
                var ability = scene.Ability<EnemyAbility_AirMovement, EnemyAbilityDefinition_AirMovement>(definition =>
                {
                    definition.ForwardSpeed = 0f;
                    Curves(definition);
                    definition.RaycastLength = 10f;
                    definition.RaycastTarget = ~0;
                    definition.MinHeight = 2f;
                    definition.MaxHeight = 3f;
                });
                Func<float, Vector3> heightAt = height => new Vector3(0f, height, 0f);

                scene.Enemy.SetWorldPosition(heightAt(5f));
                ability.UpdateMovement(0.02f);
                AssertNear(scene.Enemy.WorldPosition, heightAt(5f), "no height restriction");

                ability.Definition.RestrictHeight = true;
                foreach ((float height, float expected, string why) in new[]
                {
                    (5f, 3f, "above the maximum: 3 of 5 of the way down"),
                    (1f, 2f, "below the minimum: the unclamped ratio 2 goes beyond the start"),
                    (2.5f, 2.5f, "inside the band"),
                    (15f, 15f, "ground beyond the ray length"),
                    (0.00005f, 0f, "within 1e-4 of the ground the ratio is zero: the hit point"),
                })
                {
                    scene.Enemy.SetWorldPosition(heightAt(height));
                    ability.UpdateMovement(0.02f);
                    AssertNear(scene.Enemy.WorldPosition, heightAt(expected), why);
                }
            });
        }

        // ---- dead ----

        // 0x56EB70: triggered while the enemy is dead. A revive time of zero keeps it dead; otherwise the dead timer in
        // the actor's storage counts up by the frame time and the enemy is revived (OnRevive removes the timer and the
        // dead flag) once the timer reaches the revive time.
        [Test]
        public void DeadRevivesTheEnemyWhenTheTimerReachesTheReviveTime()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_Dead, EnemyAbilityDefinition_Dead>(definition => Set(definition, "m_reviveTime", 2f));
                ability.Enter();
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.False, "alive");
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyDeadTimer), Is.False);

                scene.Flag("IsDead", true);
                foreach (float expectedTimer in new[] { 0.5f, 1f, 1.5f })
                {
                    ability.Update(0.5f);
                    Assert.That(ability.IsTriggered, Is.True, "still dead at " + expectedTimer);
                    Assert.That(scene.ActorStore(ActorFSMKeys.EnemyDeadTimer, -1f), Is.EqualTo(expectedTimer));
                }
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.False, "revived once the timer reaches 2");
                Assert.That(scene.Enemy.IsDead, Is.False);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyDeadTimer), Is.False, "the timer is removed");
            });
        }

        [Test]
        public void DeadWithAZeroReviveTimeStaysDeadAndLeavesTheTimerAlone()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_Dead, EnemyAbilityDefinition_Dead>(definition => Set(definition, "m_reviveTime", 0f));
                scene.Flag("IsDead", true);
                ability.Enter();
                ability.Update(100f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(scene.Enemy.IsDead, Is.True);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyDeadTimer), Is.False);
            });
        }

        // 0x56EC50, 0x56ECA0: no movement, and the movement and lateral animation values are set to zero.
        [Test]
        public void DeadStandsStillAndZeroesTheMovementAnimation()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                var ability = scene.Ability<EnemyAbility_Dead, EnemyAbilityDefinition_Dead>();
                scene.Enemy.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                ability.UpdateMovement(0.1f);
                AssertNear(scene.Enemy.WorldVelocity, Vector3.zero, "dead");
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(0f));
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(0f));
            });
        }

        // ---- idle ----

        // 0x5708F0: idle when the impulse is zero, no wind-up or attack is on, and the enemy stands on the ground.
        [Test]
        public void IdleNeedsNoImpulseNoAttackAndGround()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_Idle, EnemyAbilityDefinition_Idle>();
                ability.Enter();
                foreach ((float x, bool windUp, bool attack, bool ground, bool expected) in new[]
                {
                    (0f, false, false, true, true), (0.5f, false, false, true, false), (0f, true, false, true, false),
                    (0f, false, true, true, false), (0f, false, false, false, false), (1e-6f, false, false, true, true),
                })
                {
                    scene.Impulse(x, 0f);
                    scene.Flag("BrainWindUpAttack", windUp);
                    scene.Flag("BrainAttackTarget", attack);
                    SetProperty(scene.Body, "CollidingWithGround", ground);
                    ability.Update(0.02f);
                    Assert.That(ability.IsTriggered, Is.EqualTo(expected),
                        "x " + x + ", wind-up " + windUp + ", attack " + attack + ", ground " + ground);
                }
            });
        }

        // 0x570990, 0x5709E0: no movement; a spotted target sets the bool animation and is consumed; the movement and
        // lateral animation values are zero.
        [Test]
        public void IdleStandsStillConsumesASightingAndZeroesTheAnimation()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                var ability = scene.Ability<EnemyAbility_Idle, EnemyAbilityDefinition_Idle>();
                scene.Enemy.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                ability.UpdateMovement(0.1f);
                AssertNear(scene.Enemy.WorldVelocity, Vector3.zero, "idle");
                scene.Flag("BrainTargetSpotted", true);
                scene.Brain.Storage.SetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true);
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetBool("Spotted"), Is.True);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(0f));
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(0f));
                Assert.That(scene.Enemy.BrainTargetSpotted, Is.False, "consumed");
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true), Is.False);
            });
        }

        // ---- taunt ----

        // 0x570C10, 0x570C40: triggered while the enemy has hit the player; entering restarts the taunt clock. A taunt
        // time of zero never ends; otherwise the player hit is cleared (and the ability ends) once the taunt has run
        // for the taunt time, with the end at exactly that time included.
        [Test]
        public void TauntEndsTheHitOnceItHasRunForTheTauntTime()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_Taunt, EnemyAbilityDefinition_Taunt>(definition => Set(definition, "m_tauntingAnimationLength", 1f));
                scene.Flag("HitPlayer", true);
                ability.Enter();
                Assert.That(ability.IsTriggered, Is.True, "entering while the player was hit");
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<float>(ability, "m_tauntedForSeconds"), Is.EqualTo(0.5f));
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.False, "the taunt time is reached");
                Assert.That(scene.Enemy.HitPlayer, Is.False, "the hit is cleared");

                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_tauntedForSeconds"), Is.EqualTo(1f), "no hit: the clock does not run");
                scene.Flag("HitPlayer", true);
                ability.Leave();
                ability.Enter();
                Assert.That(Get<float>(ability, "m_tauntedForSeconds"), Is.EqualTo(0f), "entering restarts the clock");
            });
        }

        [Test]
        public void TauntWithAZeroTauntTimeNeverEnds()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_Taunt, EnemyAbilityDefinition_Taunt>(definition => Set(definition, "m_tauntingAnimationLength", 0f));
                scene.Flag("HitPlayer", true);
                ability.Enter();
                ability.Update(100f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(scene.Enemy.HitPlayer, Is.True);
                Assert.That(Get<float>(ability, "m_tauntedForSeconds"), Is.EqualTo(0f));
                scene.Flag("HitPlayer", false);
                ability.Update(0.02f);
                Assert.That(ability.IsTriggered, Is.False, "the hit ended elsewhere");
            });
        }

        // 0x570C90, 0x570CE0: no movement; the movement and lateral animation values are zero.
        [Test]
        public void TauntStandsStillAndZeroesTheMovementAnimation()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                var ability = scene.Ability<EnemyAbility_Taunt, EnemyAbilityDefinition_Taunt>();
                scene.Enemy.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                ability.UpdateMovement(0.1f);
                AssertNear(scene.Enemy.WorldVelocity, Vector3.zero, "taunt");
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(0f));
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(0f));
            });
        }

        // ---- ground attack ----

        // 0x570160: triggered while the brain stands still, locked on and winding up. The wind-up timer in the actor's
        // storage advances by the frame time only while the animator is ready, and once it exceeds the wind-up time
        // (strictly) the brain is told the wind-up is complete and the timer is reset. The attack is finished when the
        // animator reports the attack complete.
        [Test]
        public void GroundAttackCompletesTheWindUpOnceTheTimerExceedsTheWindUpTime()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_GroundAttack, EnemyAbilityDefinition_GroundAttack>(definition => definition.WindUpAttackTime = 0.5f);
                ability.Enter();
                foreach ((float x, bool locked, bool windUp, bool expected) in new[]
                {
                    (0f, true, true, true), (0.5f, true, true, false), (0f, false, true, false), (0f, true, false, false),
                })
                {
                    scene.Impulse(x, 0f);
                    scene.Flag("BrainTargetLocked", locked);
                    scene.Flag("BrainWindUpAttack", windUp);
                    ability.Update(0.02f);
                    Assert.That(ability.IsTriggered, Is.EqualTo(expected), "x " + x + ", locked " + locked + ", wind-up " + windUp);
                }

                // The animator is ready: the timer counts 0.25, 0.5 (not beyond), then 0.75 and completes.
                scene.Enemy.Storage.Clear();
                scene.Flag("AnimatorReadyWindUpAttack", true);
                ability.Update(0.25f);
                ability.Update(0.25f);
                Assert.That(scene.ActorStore(EnemyBrainFSMKeys.AttackWindUpTimer, -1f), Is.EqualTo(0.5f));
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.AttackWindUpComplete, false), Is.False, "exactly the wind-up time");
                ability.Update(0.25f);
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.AttackWindUpComplete, false), Is.True);
                Assert.That(scene.ActorStore(EnemyBrainFSMKeys.AttackWindUpTimer, -1f), Is.EqualTo(0f), "reset");

                Assert.That(ability.AbilityFinished, Is.False);
                scene.Flag("AnimatorAttackComplete", true);
                ability.Update(0.02f);
                Assert.That(ability.AbilityFinished, Is.True);
                scene.Flag("AnimatorAttackComplete", false);
                ability.Update(0.02f);
                Assert.That(ability.AbilityFinished, Is.False);
            });
        }

        // 0x570287..0x570340: the timer is read with the frame time as its stored default, so a missing timer reads (and
        // is stored as) the frame time even when the animator is not ready.
        [Test]
        public void GroundAttackStoresTheFrameTimeAsTheDefaultOfAMissingTimer()
        {
            WithScene(scene =>
            {
                var ability = scene.Ability<EnemyAbility_GroundAttack, EnemyAbilityDefinition_GroundAttack>(definition => definition.WindUpAttackTime = 0.5f);
                ability.Enter();
                ability.Update(0.125f);
                Assert.That(scene.ActorStore(EnemyBrainFSMKeys.AttackWindUpTimer, -1f), Is.EqualTo(0.125f));
                ability.Update(0.125f);
                Assert.That(scene.ActorStore(EnemyBrainFSMKeys.AttackWindUpTimer, -1f), Is.EqualTo(0.125f), "not ready: unchanged");
                ability.Update(1f);
                Assert.That(scene.ActorStore(EnemyBrainFSMKeys.AttackWindUpTimer, -1f), Is.EqualTo(0.125f));
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.AttackWindUpComplete, false), Is.False);
            });
        }

        // 0x570440: the attack stands still, keeping only a downward local y velocity; 0x5704B0 zeroes the movement
        // and lateral animation values.
        [Test]
        public void GroundAttackKeepsOnlyADownwardVelocityAndZeroesTheAnimation()
        {
            WithScene(scene =>
            {
                scene.AttachAnimator();
                var ability = scene.Ability<EnemyAbility_GroundAttack, EnemyAbilityDefinition_GroundAttack>();
                foreach ((float y, float expected) in new[] { (-2f, -2f), (2f, 0f), (0f, 0f) })
                {
                    scene.Enemy.SetWorldVelocity(new Vector3(3f, y, 5f));
                    ability.UpdateMovement(0.1f);
                    AssertNear(scene.Enemy.WorldVelocity, new Vector3(0f, expected, 0f), "vertical velocity " + y);
                }
                ability.UpdateMovementAnimation(scene.Animation);
                Assert.That(scene.Animator.GetFloat("Speed"), Is.EqualTo(0f));
                Assert.That(scene.Animator.GetFloat("Lateral"), Is.EqualTo(0f));
            });
        }
    }
}
