using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss graph states that move or turn a Boss in Game.Runtime: BossState_ForcedRotation (02000209, 0x706C10), MoveToTarget
    // (0200020d, 0x707210), RetreatingJump (02000210, 0x7080F0), Scuttle (02000211, 0x7086A0), ShootLasers (02000223, 0x70A800) and the
    // four turning states TurnAwayFromTarget (0200022b, 0x70B8A0), TurnToBossManagerTarget (0200022c, 0x70BE30), TurnToCharacter
    // (0200022d, 0x70C570) and TurnToTarget (0200022e, 0x70CAF0). Each state is built from its JSON through the node factory and driven through its own
    // DoOnEnter/DoUpdate/DoOnLeave bodies against a real Boss on an owned inactive host, which gets the animation storage and the
    // time-manager reference a started Actor would have created. Where a state casts its ability to a type, the case supplies a real
    // ability (SpinOnSpot, RetreatingJump, Scuttle, LaserFence) driven through its own update; the turning states only read the
    // IAbilityBossMovement members, so they get a scripted implementation of that interface.
    // Expected angles are the signed angles between the boss's forward direction (+Z at the identity rotation) and the vector the state
    // measures, and the turn is the sign of that angle times the ability's turn speed times the frame time. A boss yawed by a degrees
    // faces (sin a, 0, cos a); the literals below are those values.
    // Not covered: the Actor animation callbacks of the effect base (they return early until the Actor is initialised, which needs
    // a started Actor).
    public sealed class BossMovementAndTurnStateTests
    {
        // Fixture values, not original constants: any key works for a boss that owns its ability dictionary.
        private const ActorAbilityType StateAbility = (ActorAbilityType)0x4C554348;

        private const string TurningBool = "Lucid turning";
        private const string SpeedFloat = "Lucid speed";
        private const string XFloat = "Lucid x direction";
        private const string ZFloat = "Lucid z direction";

        // CreateInstance sends Awake and OnEnable, and both reach ActorDefinition.UpdateCachedValues (0x7A8060), which calls
        // Settings.UpdateCachedValues on the unassigned Settings with no null test: two faults each.
        private static readonly Regex ActorDefinitionSettingsFault =
            new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        private static string Name() => "Lucid-boss-movement-state-" + Guid.NewGuid().ToString("N");

        private static string AbilityJson => "{\"AbilityType\":" + (int)StateAbility + "}";

        private static IFSMState NewState(string node, string json = null) =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json ?? AbilityJson);

        private static void Enter(IFSMState state, Boss boss) => Call(state, "DoOnEnter", boss, null);

        private static void Leave(IFSMState state, Boss boss) => Call(state, "DoOnLeave", boss, null);

        private static void UpdateState(IFSMState state, Boss boss, float deltaTime) =>
            Call(state, "DoUpdate", boss, new FSMUpdateContext(deltaTime, FSMUpdateType.FixedUpdate));

        // The two dictionaries the effect states read from the actor's storage and the time-manager reference the ability states read.
        private static Boss Prime(Boss boss)
        {
            boss.Storage.SetValue(ActorFSMKeys.ActiveAnimationLookup, new Dictionary<string, ActorAnimationDefinition>());
            boss.Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
            // Reflection: Actor.OnEnable (the only setter) needs a running FSM; this is the unresolved reference it leaves with no TimeManager row.
            Set(boss, "m_timeManagerRef", new SystemRef<TimeManager>(ProcessManager.GetDefaultName<TimeManager>(), null));
            return boss;
        }

        // Reflection: Actor builds its abilities from the FSM's ability definitions (CreateAbility); the test supplies its own.
        private static void AddAbility(Boss boss, ActorAbility ability) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(boss, "m_abilities")[StateAbility] = ability;

        private static void WithBoss(Action<Owned, Boss> body) => Owned.Run(owned => body(owned, Prime(NewBoss(owned, Vector3.zero))));

        private static BossDefinition NewBossDefinition(Owned owned, BossTraits traits)
        {
            for (int i = 0; i < 2; i++)
                LogAssert.Expect(LogType.Exception, ActorDefinitionSettingsFault);
            var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
            definition.Traits = traits;
            return definition;
        }

        // Reflection: the boss definition is a serialized field that the scene supplies (no App-free setter).
        private static void GiveDefinition(Owned owned, Boss boss, BossTraits traits) => Set(boss, "m_definition", NewBossDefinition(owned, traits));

        // A real Animator with the named bool and float parameters, installed as the boss's animator (the shared helper only builds bools).
        private static Animator AttachAnimator(Owned owned, Boss boss, string[] bools, string[] floats)
        {
            var animator = owned.Host("Lucid boss state animator", true).AddComponent<Animator>();
            var controller = owned.Track(new AnimatorController());
            controller.AddLayer("Base");
            foreach (string name in bools) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in floats) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetActorProperty(boss, "Animator", new ActorAnimator(animator));
            return animator;
        }

        // The state's own animation definition comes from the App's DataManager by name; the case supplies it instead.
        private static void GiveStateAnimation(IFSMState state, ActorAnimationDefinition definition) =>
            Set(state, "m_stateAnimationDefinition", definition);

        // Implements only what the turning and moving states read of an ability; it needs no definition.
        private sealed class ScriptedMovement : ActorAbility, IAbilityBossMovement
        {
            public Vector3 Target;
            public float Tolerance = 5f;
            public float Speed = 20f;
            public float AngleMultiplier = 1f;
            public bool Momentum;

            public override AbilityDefinition DefinitionBase => null;
            public override ActorFormType GetFormType() => ActorFormType.None;
            public override ActorAnimationDefinition GetAnimationDefinition() => null;

            public Vector3 CurrentTargetPosition => Target;
            public float MovementAnimationAngleMultiplier => AngleMultiplier;
            public float FaceAngleTolerance => Tolerance;
            public float TurnSpeed => Speed;
            public bool IsMomentum => Momentum;
            public bool TurnToCharacter => true;
        }

        private static ScriptedMovement NewMovement(Boss boss, Vector3 target, float tolerance = 5f, float speed = 20f)
        {
            var movement = new ScriptedMovement { Target = target, Tolerance = tolerance, Speed = speed };
            AddAbility(boss, movement);
            movement.Enter();
            return movement;
        }

        // The boss yawed by the angle faces (sin, 0, cos).
        private static Vector3 Facing(float yawDegrees) =>
            new Vector3(Mathf.Sin(yawDegrees * Mathf.Deg2Rad), 0f, Mathf.Cos(yawDegrees * Mathf.Deg2Rad));

        // ---- the turning states ----

        // BossState_TurnToTarget (0200022e, DoUpdate 0x70CC20): the angle runs from the boss's forward direction to the ability's target
        // offset about the boss's up axis. Outside the ability's tolerance (strict) the boss turns by speed * frame time towards the
        // target, applied in front of its rotation, and the animator's bool is raised; within it nothing turns and the bool is cleared.
        [Test]
        public void TurnToTargetTurnsTowardsTheAbilityOffsetUntilWithinTheTolerance()
        {
            foreach ((Vector3 target, float tolerance, float speed, float deltaTime, float yaw, bool turning) in new[]
            {
                (new Vector3(10f, 0f, 0f), 5f, 20f, 0.5f, 10f, true),
                (new Vector3(-10f, 0f, 0f), 5f, 20f, 0.5f, -10f, true),
                (new Vector3(10f, 0f, 0f), 5f, 30f, 0.25f, 7.5f, true),
                (new Vector3(10f, 0f, 0f), 120f, 20f, 0.5f, 0f, false),
                (new Vector3(0f, 0f, 10f), 5f, 20f, 0.5f, 0f, false),
            })
            {
                WithBoss((owned, boss) =>
                {
                    Animator animator = AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                    animator.SetBool(TurningBool, !turning);
                    NewMovement(boss, target, tolerance, speed);
                    IFSMState state = NewState(nameof(BossState_TurnToTarget));
                    GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.BoolSet, TurningBool)));

                    UpdateState(state, boss, deltaTime);
                    string label = $"target {target} tolerance {tolerance} speed {speed} dt {deltaTime}";
                    AssertNear(boss.ForwardDirection, Facing(yaw), label, 1e-3f);
                    Assert.That(animator.GetBool(TurningBool), Is.EqualTo(turning), label + " bool");
                });
            }
        }

        // The turn is applied in front of the rotation the boss already has, so it accumulates: a second tick of +10 degrees at a
        // forward direction already yawed by 10 degrees (target still 80 degrees off) makes 20.
        [Test]
        public void TurnToTargetAccumulatesOnTheCurrentRotation()
        {
            WithBoss((owned, boss) =>
            {
                AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                NewMovement(boss, new Vector3(10f, 0f, 0f));
                IFSMState state = NewState(nameof(BossState_TurnToTarget));
                UpdateState(state, boss, 0.5f);
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.ForwardDirection, Facing(20f), "two ticks", 1e-3f);
            });
        }

        // BossState_TurnAwayFromTarget (0200022b, DoUpdate 0x70B9D0): the angle runs from the reversed forward direction to the target offset,
        // so a target to the right (+X) is 90 degrees clockwise of the reversed forward direction and turns the boss the other way.
        [Test]
        public void TurnAwayFromTargetMeasuresFromTheReversedForwardDirection()
        {
            foreach ((Vector3 target, float yaw, bool turning) in new[]
            {
                (new Vector3(10f, 0f, 0f), -10f, true),
                (new Vector3(-10f, 0f, 0f), 10f, true),
                (new Vector3(0f, 0f, -10f), 0f, false),
            })
            {
                WithBoss((owned, boss) =>
                {
                    Animator animator = AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                    animator.SetBool(TurningBool, !turning);
                    NewMovement(boss, target);
                    IFSMState state = NewState(nameof(BossState_TurnAwayFromTarget));
                    GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.BoolSet, TurningBool)));

                    UpdateState(state, boss, 0.5f);
                    AssertNear(boss.ForwardDirection, Facing(yaw), $"target {target}", 1e-3f);
                    Assert.That(animator.GetBool(TurningBool), Is.EqualTo(turning), $"target {target} bool");
                });
            }
        }

        // BossState_TurnToCharacter (0200022d, DoUpdate 0x70C6A0): the target offset is the current character's position minus the
        // boss's, and nothing happens (no turn, no animator write) without a current character. The boss at (20, 0, 0) and the
        // character at (10, 0, 5) give the offset (-10, 0, 5), which is to the left (the character's own position would be to the right).
        [Test]
        public void TurnToCharacterTurnsTowardsTheCurrentCharacter()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(20f, 0f, 0f)));
                Animator animator = AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                NewMovement(boss, Vector3.zero);
                IFSMState state = NewState(nameof(BossState_TurnToCharacter));
                GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.BoolSet, TurningBool)));

                UpdateState(state, boss, 0.5f);
                AssertNear(boss.ForwardDirection, Facing(0f), "no current character", 1e-3f);
                Assert.That(animator.GetBool(TurningBool), Is.False, "the animator is not written");

                MakeCurrent(manager, characterObject, new Vector3(10f, 0f, 5f));
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.ForwardDirection, Facing(-10f), "towards the character", 1e-3f);
                Assert.That(animator.GetBool(TurningBool), Is.True, "outside the tolerance");

                // Straight ahead of the boss: within the tolerance, the turn stops and the bool is cleared.
                MakeCurrent(manager, characterObject, new Vector3(20f, 0f, 50f));
                boss.SetWorldRotation(Quaternion.identity);
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.ForwardDirection, Facing(0f), "no turn", 1e-3f);
                Assert.That(animator.GetBool(TurningBool), Is.False);
            });
        }

        // BossState_TurnToBossManagerTarget (0200022c, DoUpdate 0x70BF60): the angle runs to the boss's own position minus the Fourmen's average
        // position (not the reverse). The bosses at (0, 0, 0), (3, 0, 0) and (0, 0, 6) average (1, 0, 2); the first boss measures
        // (-1, 0, -2), which is -153 degrees, and the second (2, 0, -2), which is +135 degrees.
        [Test]
        public void TurnToBossManagerTargetMeasuresFromTheAveragePositionToTheBoss()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, 6f));
                foreach ((int index, float yaw) in new[] { (0, -10f), (1, 10f) })
                {
                    Boss boss = Prime(rig.Bosses[index]);
                    AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                    NewMovement(boss, Vector3.zero);
                    UpdateState(NewState(nameof(BossState_TurnToBossManagerTarget)), boss, 0.5f);
                    AssertNear(boss.ForwardDirection, Facing(yaw), "boss " + index, 1e-3f);
                }
            });
        }

        // ---- BossState_ForcedRotation ----

        // BossState_ForcedRotation (DoUpdate 0x706D40): the boss's rotation is replaced (not turned) by the spin ability's current angle
        // about the up axis. A quarter of the two-second spin is 90 degrees, negative when the spin is not clockwise.
        [Test]
        public void ForcedRotationReplacesTheRotationWithTheSpinAngle()
        {
            foreach ((bool clockwise, float angle, float yaw) in new[] { (true, 90f, 90f), (false, -90f, -90f) })
            {
                WithBoss((owned, boss) =>
                {
                    var spin = NewAbility<BossAbility_SpinOnSpot>(boss, NewDefinition<BossAbilityDefinition_SpinOnSpot>(owned, d =>
                    {
                        d.SpinRotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                        d.TimeToFullyRotateInSeconds = 2f;
                        d.Clockwise = clockwise;
                    }));
                    AddAbility(boss, spin);
                    spin.Enter();
                    spin.Update(0.5f);
                    Assert.That(spin.RotationInDegrees, Is.EqualTo(angle).Within(1e-3f), "the ability's angle after half a second");
                    boss.SetWorldRotation(Quaternion.Euler(0f, 30f, 0f));

                    UpdateState(NewState(nameof(BossState_ForcedRotation)), boss, 0.5f);
                    Assert.That(spin.RotationInDegrees, Is.EqualTo(angle).Within(1e-3f), "the state does not advance the ability");
                    AssertNear(boss.ForwardDirection, Facing(yaw), "clockwise " + clockwise, 1e-3f);
                });
            }
        }

        // ---- BossState_MoveToTarget ----

        private static ScriptedMovement NewMover(Boss boss, Vector3 target, bool momentum = false)
        {
            ScriptedMovement movement = NewMovement(boss, target);
            movement.Momentum = momentum;
            return movement;
        }

        // BossState_MoveToTarget (DoUpdate 0x707340): nothing happens while the ability is disabled, nor while the target is within 1E-10 (squared) of the
        // boss's world position.
        [Test]
        public void MoveToTargetDoesNothingWhileDisabledOrAlreadyAtTheTarget()
        {
            WithBoss((owned, boss) =>
            {
                GiveDefinition(owned, boss, new BossTraits());
                Animator animator = AttachAnimator(owned, boss, new string[0], new[] { SpeedFloat });
                animator.SetFloat(SpeedFloat, 99f);
                IFSMState state = NewState(nameof(BossState_MoveToTarget));
                GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.Boss_Speed, SpeedFloat)));

                ScriptedMovement movement = NewMover(boss, new Vector3(3f, 0f, 4f));
                movement.Leave();
                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(Vector3.zero), "disabled");

                movement.Enter();
                movement.Target = Vector3.zero;
                UpdateState(state, boss, 0.5f);
                Assert.That(animator.GetFloat(SpeedFloat), Is.EqualTo(99f), "at the target");
                Assert.That(Has(boss, BossFSMKeys.CurrentPosition), Is.False);
            });
        }

        // 0x707770 (not momentum): the boss is placed on the target and the stored position follows it. The animator speed is the squared
        // length of (previous - target) / frame time, where the previous position is the stored one and the target itself when none
        // is stored (or it is zero): 0 on the first tick; (3, 0, 4) - (6, 0, 8) over half a second is (-6, 0, -8): 100.
        [Test]
        public void MoveToTargetPlacesTheBossOnTheTargetAndFeedsTheSquaredSpeed()
        {
            WithBoss((owned, boss) =>
            {
                GiveDefinition(owned, boss, new BossTraits());
                Animator animator = AttachAnimator(owned, boss, new string[0], new[] { SpeedFloat });
                animator.SetFloat(SpeedFloat, 99f);
                IFSMState state = NewState(nameof(BossState_MoveToTarget));
                GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.Boss_Speed, SpeedFloat)));
                ScriptedMovement movement = NewMover(boss, new Vector3(3f, 0f, 4f));

                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(3f, 0f, 4f)));
                Assert.That(Stored<Vector3>(boss, BossFSMKeys.CurrentPosition), Is.EqualTo(new Vector3(3f, 0f, 4f)));
                Assert.That(animator.GetFloat(SpeedFloat), Is.EqualTo(0f), "the first tick has no previous position");

                movement.Target = new Vector3(6f, 0f, 8f);
                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(6f, 0f, 8f)));
                Assert.That(Stored<Vector3>(boss, BossFSMKeys.CurrentPosition), Is.EqualTo(new Vector3(6f, 0f, 8f)));
                Assert.That(animator.GetFloat(SpeedFloat), Is.EqualTo(100f).Within(1e-3f));
            });
        }

        // 0x7077D0 (momentum): the target vector is the horizontal velocity (y dropped); the boss is not moved, nothing is stored, and the
        // animator speed is x squared plus z squared.
        [Test]
        public void MoveToTargetTreatsAMomentumTargetAsTheHorizontalVelocity()
        {
            WithBoss((owned, boss) =>
            {
                GiveDefinition(owned, boss, new BossTraits());
                Animator animator = AttachAnimator(owned, boss, new string[0], new[] { SpeedFloat });
                IFSMState state = NewState(nameof(BossState_MoveToTarget));
                GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.Boss_Speed, SpeedFloat)));
                NewMover(boss, new Vector3(3f, 7f, 4f), momentum: true);

                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldVelocity, Is.EqualTo(new Vector3(3f, 0f, 4f)));
                Assert.That(boss.WorldPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Has(boss, BossFSMKeys.CurrentPosition), Is.False);
                Assert.That(animator.GetFloat(SpeedFloat), Is.EqualTo(25f));
            });
        }

        // 0x707509: with a smoothed rotator the boss's local direction to the target (normalised) drives the rotator's local Euler
        // angles: x is scaled by the ability's angle multiplier, y is zero and z is not scaled. The boss transform is at the origin, so
        // the direction to (3, 0, 4) is (0.6, 0, 0.8). Without the trait the rotator is left alone.
        [Test]
        public void MoveToTargetDrivesTheSmoothedRotatorFromTheNormalisedLocalDirection()
        {
            foreach ((bool smoothed, Vector3 expectedEuler) in new[] { (true, new Vector3(1.2f, 0f, 0.8f)), (false, Vector3.zero) })
            {
                WithBoss((owned, boss) =>
                {
                    GiveDefinition(owned, boss, new BossTraits { UsesSmoothedTransformRotator = smoothed });
                    AttachAnimator(owned, boss, new string[0], new[] { SpeedFloat });
                    SmoothedTransformRotator rotator = owned.Host("Lucid boss rotator", false).AddComponent<SmoothedTransformRotator>();
                    Call(rotator, "Initialise"); // Awake does not run in Edit Mode; this is its body (it caches the transform).
                    Set(boss, "m_smoothedTransformRotator", rotator); // serialized scene reference
                    ScriptedMovement movement = NewMover(boss, new Vector3(3f, 0f, 4f));
                    movement.AngleMultiplier = 2f;

                    UpdateState(NewState(nameof(BossState_MoveToTarget)), boss, 0.5f);
                    Assert.That(Quaternion.Angle(rotator.transform.localRotation, Quaternion.Euler(expectedEuler)), Is.LessThan(1e-2f),
                        "smoothed " + smoothed);
                });
            }
        }

        // DoOnLeave 0x707CE0: leaving stops the boss.
        [Test]
        public void MoveToTargetLeaveStopsTheBoss()
        {
            WithBoss((owned, boss) =>
            {
                boss.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                NewMovement(boss, Vector3.zero);
                Leave(NewState(nameof(BossState_MoveToTarget)), boss);
                Assert.That(boss.WorldVelocity, Is.EqualTo(Vector3.zero));
            });
        }

        // ---- BossState_RetreatingJump ----

        private static BossAbility_RetreatingJump NewJump(Owned owned, Boss boss)
        {
            var jump = NewAbility<BossAbility_RetreatingJump>(boss, NewDefinition<BossAbilityDefinition_MovementRetreatingJump>(owned, d =>
            {
                d.JumpHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.JumpSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.JumpMaxHeight = 10f;
                d.MiniumJumpRange = 35f;
                d.FaceAngleTolerance = 5f;
                d.TurnSpeed = 20f;
            }));
            AddAbility(boss, jump);
            jump.Enter();
            return jump;
        }

        // BossState_RetreatingJump (DoUpdate 0x708220): while the jump ability is enabled the boss is turned by the full turn speed
        // towards the character (the sign of the angle, outside the tolerance), moved to the jump's current target, and, from full jump
        // progress, the animator's bool is cleared. The ability's own update flies the jump: a far desired position triggers it, and
        // once the animation is triggered the progress runs by the frame time (0x6FEBFD).
        [Test]
        public void RetreatingJumpFollowsTheJumpTargetAndTurnsTowardsTheCharacter()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, Vector3.zero));
                Animator animator = AttachAnimator(owned, boss, new[] { TurningBool }, new string[0]);
                animator.SetBool(TurningBool, true);
                BossAbility_RetreatingJump jump = NewJump(owned, boss);
                IFSMState state = NewState(nameof(BossState_RetreatingJump));
                GiveStateAnimation(state, NewAnimationDefinition(owned, (ActorAnimationType.BoolSet, TurningBool)));

                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(0f, 0f, 50f));
                jump.Update(0.25f);
                jump.SetAnimationTriggered();
                jump.Update(0.25f);
                Assert.That(jump.JumpProgress, Is.EqualTo(0.25f));

                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, 0f)), "no current character: nothing moves");

                MakeCurrent(manager, characterObject, new Vector3(10f, 0f, 0f));
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.WorldPosition, new Vector3(0f, 2.5f, 12.5f), "the jump target at a quarter of the progress");
                AssertNear(boss.ForwardDirection, Facing(10f), "turned towards the character", 1e-3f);
                Assert.That(animator.GetBool(TurningBool), Is.True, "the jump is not complete");

                for (int i = 0; i < 3; i++)
                    jump.Update(0.25f);
                Assert.That(jump.JumpProgress, Is.EqualTo(1f));
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.WorldPosition, new Vector3(0f, 10f, 50f), "the top of the jump");
                Assert.That(animator.GetBool(TurningBool), Is.False, "the jump is complete");
            });
        }

        [Test]
        public void RetreatingJumpDoesNothingWhileTheAbilityIsDisabled()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(1f, 2f, 3f)));
                BossAbility_RetreatingJump jump = NewJump(owned, boss);
                jump.Leave();
                MakeCurrent(manager, characterObject, new Vector3(10f, 0f, 0f));
                UpdateState(NewState(nameof(BossState_RetreatingJump)), boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                AssertNear(boss.ForwardDirection, Facing(0f), "not turned", 1e-3f);
            });
        }

        // ---- BossState_Scuttle ----

        private static BossAbility_Scuttle NewScuttle(Owned owned, Boss boss)
        {
            var scuttle = NewAbility<BossAbility_Scuttle>(boss, NewDefinition<BossAbilityDefinition_MovementScuttle>(owned, d =>
            {
                d.ScuttleSpeed = 2f;
                d.ScuttleRange = 100f;
                d.ScuttleStartMinimumMovementDistance = 0.5f;
                d.ScuttleEndDistanceTolerance = 1f;
                d.ScuttleDestinationReachedSeconds = 0.2f;
                d.FaceAngleTolerance = 5f;
                d.TurnSpeed = 20f;
                d.FullSpeedCurve = AnimationCurve.Linear(0f, 0f, 10f, 10f);
                d.TargetDistanceForFullSpeed = 4f;
            }));
            AddAbility(boss, scuttle);
            scuttle.Enter();
            return scuttle;
        }

        // 0x7088C5..0x708BD0: the target is taken into the boss's local space (the boss transform is at the origin here). Each local axis
        // gives a normalised distance (axis squared over the squared distance of full speed) which the full-speed curve turns into a
        // speed; the animator's direction inputs and the local velocity take those with the sign of the local axis (zero counts as
        // negative); y of the local velocity is the target's local y. The boss turns towards the character by the full turn speed
        // (the sign of the angle) in front of its rotation, and the local velocity is applied after that turn, so the world velocity is
        // the local velocity rotated by the new rotation. Local target (4, 0, -9) over a full-speed distance of 4: 16/16 = 1 and
        // 81/16 = 5.0625; speeds 2 * 1 and 2 * 5.0625; rotated by 10 degrees, (2, 0, -10.125) is (0.211428, 0, -10.318475).
        [Test]
        public void ScuttleMovesBySignedCurveSpeedsAndTurnsTowardsTheCharacter()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(0f, 2f, 0f)));
                GiveDefinition(owned, boss, new BossTraits());
                Animator animator = AttachAnimator(owned, boss, new string[0], new[] { XFloat, ZFloat });
                BossAbility_Scuttle scuttle = NewScuttle(owned, boss);
                IFSMState state = NewState(nameof(BossState_Scuttle));
                GiveStateAnimation(state, NewAnimationDefinition(owned,
                    (ActorAnimationType.Boss_XDirection, XFloat), (ActorAnimationType.Boss_ZDirection, ZFloat)));
                MakeCurrent(manager, characterObject, new Vector3(10f, 0f, 0f));
                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(4f, 0f, -9f));
                scuttle.Update(0.1f);

                UpdateState(state, boss, 0.5f);
                Assert.That(animator.GetFloat(XFloat), Is.EqualTo(1f).Within(1e-4f), "x direction");
                Assert.That(animator.GetFloat(ZFloat), Is.EqualTo(-5.0625f).Within(1e-4f), "z direction");
                AssertNear(boss.ForwardDirection, Facing(10f), "turned towards the character", 1e-3f);
                AssertNear(boss.WorldVelocity, new Vector3(0.211428f, 0f, -10.318475f), "world velocity", 1e-3f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(0f, 2f, 0f)), "no ground: the height is kept");
            });
        }

        // 0x708BE0: the ray runs down the boss's gravity from the boss's position for (y + 20) * 2, and the boss is placed at the height
        // of the hit (kept when nothing is hit). From y = 2 that is 44: a track whose top is 30 below is found, one 50 below is not.
        [Test]
        public void ScuttleDropsTheBossOntoTheTrackWithinTheRayLength()
        {
            foreach ((float trackTop, float expectedY) in new[] { (-30f, -30f), (-50f, 2f) })
            {
                WithManager((owned, manager, characterObject) =>
                {
                    Boss boss = Prime(NewBoss(owned, new Vector3(0f, 2f, 0f)));
                    GiveDefinition(owned, boss, new BossTraits());
                    AttachAnimator(owned, boss, new string[0], new[] { XFloat, ZFloat });
                    BossAbility_Scuttle scuttle = NewScuttle(owned, boss);
                    MakeCurrent(manager, characterObject, new Vector3(0f, 0f, 10f));
                    boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(4f, 0f, 4f));
                    scuttle.Update(0.1f);
                    GameObject track = owned.Host("Lucid scuttle track", true);
                    track.transform.position = new Vector3(0f, trackTop - 1f, 0f);
                    track.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, 4f);
                    Physics.SyncTransforms();

                    UpdateState(NewState(nameof(BossState_Scuttle)), boss, 0.5f);
                    Assert.That(boss.WorldPosition.y, Is.EqualTo(expectedY).Within(1e-3f), "track top " + trackTop);
                });
            }
        }

        [Test]
        public void ScuttleLeaveStopsTheBoss()
        {
            WithBoss((owned, boss) =>
            {
                boss.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                NewScuttle(owned, boss);
                Leave(NewState(nameof(BossState_Scuttle)), boss);
                Assert.That(boss.WorldVelocity, Is.EqualTo(Vector3.zero));
            });
        }

        // ---- BossState_ShootLasers ----

        private const ActorAttachPointType LaserAttach = ActorAttachPointType.BossFourman_LaserEmitter_GreenHand;

        private sealed class LaserRig
        {
            public FourmenRig Rig;
            public GameObject[] Emitters;
        }

        // The Fourmen rig with a laser fence ability and a laser emitter (an owned host carrying a VariableLengthLasers) on every boss.
        private static LaserRig NewLaserRig(Owned owned, Action<BossAbilityDefinition_LaserFence> setup, params Vector3[] emitterPositions)
        {
            var laser = new LaserRig { Rig = NewFourmenRig(owned, emitterPositions.Select(position => Vector3.zero).ToArray()) };
            laser.Emitters = new GameObject[emitterPositions.Length];
            for (int i = 0; i < emitterPositions.Length; i++)
            {
                Boss boss = Prime(laser.Rig.Bosses[i]);
                GameObject emitter = owned.Host("Lucid laser emitter " + i, false);
                emitter.transform.position = emitterPositions[i];
                laser.Emitters[i] = emitter;
                object body = Activator.CreateInstance(typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic));
                Set(body, "LaserEmitters", emitter.AddComponent<VariableLengthLasers>());
                ((IDictionary)Get<object>(boss, "m_attachPointDictionary"))[LaserAttach] = body;
                var fence = NewAbility<BossAbility_LaserFence>(boss, NewDefinition<BossAbilityDefinition_LaserFence>(owned, d =>
                {
                    d.Attachment = LaserAttach;
                    setup?.Invoke(d);
                }));
                AddAbility(boss, fence);
                fence.Enter();
            }
            return laser;
        }

        // 0x70A940..0x70AAA1: the lasers are switched on for the fence's attach point, unless the ability is disabled or the Fourmen
        // have two bosses or fewer and this is not the first (a pair shares one fence). Leaving switches them off (0x70AFBA) with no
        // such condition.
        [Test]
        public void ShootLasersSwitchesTheEmitterOnAndOffExceptForTheSecondBossOfAPair()
        {
            Owned.Run(owned =>
            {
                LaserRig pair = NewLaserRig(owned, null, Vector3.zero, new Vector3(3f, 0f, 4f));
                Enter(NewState(nameof(BossState_ShootLasers)), pair.Rig.Bosses[1]);
                Assert.That(pair.Emitters[1].activeSelf, Is.False, "the second boss of a pair does not fire");
                Enter(NewState(nameof(BossState_ShootLasers)), pair.Rig.Bosses[0]);
                Assert.That(pair.Emitters[0].activeSelf, Is.True, "the first boss does");
                pair.Emitters[1].SetActive(true);
                Leave(NewState(nameof(BossState_ShootLasers)), pair.Rig.Bosses[1]);
                Assert.That(pair.Emitters[1].activeSelf, Is.False, "leaving switches the lasers off for any boss");
            });

            Owned.Run(owned =>
            {
                LaserRig trio = NewLaserRig(owned, null, Vector3.zero, new Vector3(3f, 0f, 4f), new Vector3(6f, 0f, 8f));
                Enter(NewState(nameof(BossState_ShootLasers)), trio.Rig.Bosses[1]);
                Assert.That(trio.Emitters[1].activeSelf, Is.True, "with three bosses every boss fires");
            });
        }

        [Test]
        public void ShootLasersDoesNothingWhileTheAbilityIsDisabled()
        {
            Owned.Run(owned =>
            {
                LaserRig trio = NewLaserRig(owned, null, Vector3.zero, new Vector3(3f, 0f, 4f), new Vector3(6f, 0f, 8f));
                Get<Dictionary<ActorAbilityType, ActorAbility>>(trio.Rig.Bosses[0], "m_abilities")[StateAbility].Leave();
                Enter(NewState(nameof(BossState_ShootLasers)), trio.Rig.Bosses[0]);
                Assert.That(trio.Emitters[0].activeSelf, Is.False);
            });
        }

        private static LineRenderer AddLine(GameObject emitter, Vector3 start, Vector3 end)
        {
            var line = emitter.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPositions(new[] { start, end });
            // Reflection: the renderer list is a serialized field that the prefab supplies.
            Get<List<LineRenderer>>(emitter.GetComponent<VariableLengthLasers>(), "m_lineRenderers").Add(line);
            return line;
        }

        private static Vector3[] Positions(LineRenderer line)
        {
            var positions = new Vector3[2];
            line.GetPositions(positions);
            return positions;
        }

        // 0x70AD58..0x70ADBA: with static targets the laser runs from the origin to the fence's target; only x and z of the
        // renderers' positions are replaced (SetLaserPositions keeps y), and nothing else of the boss is touched.
        [Test]
        public void ShootLasersAimsStaticTargetsFromTheOriginToTheFencesTarget()
        {
            Owned.Run(owned =>
            {
                LaserRig trio = NewLaserRig(owned, d =>
                {
                    d.HasStaticTargets = true;
                    d.LaserTarget = new Vector3(5f, 6f, 7f);
                }, Vector3.zero, new Vector3(3f, 0f, 4f), new Vector3(6f, 0f, 8f));
                LineRenderer line = AddLine(trio.Emitters[0], new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
                UpdateState(NewState(nameof(BossState_ShootLasers)), trio.Rig.Bosses[0], 0.5f);
                Assert.That(Positions(line), Is.EqualTo(new[] { new Vector3(0f, 2f, 0f), new Vector3(5f, 5f, 7f) }));
            });
        }

        // 0x70ADBC: otherwise the positions come from the Fourmen (this boss's emitter and the next boss's emitter), and the boss
        // shapes the hazard from the fence's depth, height and Y offset: the box centre is halfway along the laser in the emitter's
        // local space at the Y offset, and its size is (depth, height, laser length). The emitter at the origin looks at the next one
        // at (3, 0, 4): in its local space the laser runs 5 straight ahead, so the centre is (0, 6, 2.5) and the size (3, 4, 5).
        [Test]
        public void ShootLasersAimsAtTheNextBossAndShapesTheHazardFromTheFence()
        {
            Owned.Run(owned =>
            {
                LaserRig trio = NewLaserRig(owned, d =>
                {
                    d.HazardDepth = 3f;
                    d.HazardHeight = 4f;
                    d.HazardYOffset = 6f;
                }, Vector3.zero, new Vector3(3f, 0f, 4f), new Vector3(6f, 0f, 8f));
                Boss boss = trio.Rig.Bosses[0];
                VariableLengthLasers lasers = trio.Emitters[0].GetComponent<VariableLengthLasers>();
                LineRenderer line = AddLine(trio.Emitters[0], new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
                Transform pfxParent = owned.Host("Lucid laser pfx parent", false).transform;
                Transform near = owned.Host("Lucid laser near pfx", false).transform;
                Transform far = owned.Host("Lucid laser far pfx", false).transform;
                // Reflection: the effect transforms are serialized references that the prefab supplies.
                Set(lasers, "m_laserPfxParent", pfxParent);
                Set(lasers, "m_nearEdgePfx", near);
                Set(lasers, "m_farEdgePfx", far);
                BoxCollider hazard = owned.Host("Lucid laser hazard", false).AddComponent<BoxCollider>();
                object body = ((IDictionary)Get<object>(boss, "m_attachPointDictionary"))[LaserAttach];
                Set(body, "Collider", hazard);

                UpdateState(NewState(nameof(BossState_ShootLasers)), boss, 0.5f);
                Assert.That(Positions(line), Is.EqualTo(new[] { new Vector3(0f, 2f, 0f), new Vector3(3f, 5f, 4f) }));
                AssertNear(hazard.center, new Vector3(0f, 6f, 2.5f), "hazard centre", 1e-3f);
                AssertNear(hazard.size, new Vector3(3f, 4f, 5f), "hazard size", 1e-3f);
                AssertNear(pfxParent.localPosition, new Vector3(0f, 6f, 2.5f), "effect parent", 1e-3f);
                AssertNear(near.localPosition, Vector3.zero, "near edge", 1e-3f);
                AssertNear(far.localPosition, new Vector3(0f, 0f, 5f), "far edge", 1e-3f);
            });
        }
    }
}
