using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterState_Ground<TAbilityDef, TAbility> (TypeDef 0x02000367, shared x86-64 bodies
    // 0xE6FA00..0xE73190), exercised through a concrete leaf closed over the ground boost ability. The character is a
    // real component on an owned inactive host; the ability and the state are built by their real constructors. Each
    // case cites the native comparison it pins and carries a negative control that flips the predicate. Methods that
    // need a started Character (camera proxy, boost stamina) are out of reach in Edit Mode and are not covered here.
    public sealed class CharMoves05GroundStateTests
    {
        private static readonly Type GroundType = typeof(CharacterState_Ground<CharacterAbilityDefinition_MovementGroundBoost, CharacterAbility_MovementGroundBoost>);
        private static readonly Vector3 Origin = new Vector3(50f, 50f, 50f);
        private const float Tolerance = 1e-3f;
        private static readonly Vector3EqualityComparer Vec = new Vector3EqualityComparer(Tolerance);

        // The state is abstract; this leaf adds nothing but the ability type its JSON would carry.
        private sealed class GroundProbeState : CharacterState_Ground<CharacterAbilityDefinition_MovementGroundBoost, CharacterAbility_MovementGroundBoost>
        {
            public GroundProbeState(FiniteStateMachine fsm, FSMIdentifier stateId)
                : base(fsm, stateId, new AbilityJSONCtorArgs { AbilityType = ActorAbilityType.Character_MovementGroundBoost })
            {
            }
        }

        private sealed class Scene
        {
            public CharacterGravityVectorProbe Character;
            public CharacterAbilityDefinition_MovementGroundBoost Definition;
            public CharacterAbility_MovementGroundBoost Ability;
            public GroundProbeState State;
        }

        private static string Name() => "Lucid-ground-state-" + Guid.NewGuid().ToString("N");

        // Gravity points down, so the world up is +y; the fixed pass is active and the clock is cached at ten seconds.
        private static void WithGround(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            CharacterGravityVectorProbe character = NewCharacter(owned);
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_MovementGroundBoost>());
            var ability = new CharacterAbility_MovementGroundBoost();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities")[ActorAbilityType.Character_MovementGroundBoost] = ability;
            Set(character, "m_totalFixedTime", 10f);
            SetProperty(character, "QueuedActions", new CharacterQueuedActions(character));
            var state = new GroundProbeState(new FiniteStateMachine(Name(), skipAddToManager: true), new FSMIdentifier(Name()));
            body(owned, new Scene { Character = character, Definition = definition, Ability = ability, State = state });
        });

        private static object Invoke(object state, string name, params object[] arguments)
        {
            MethodInfo method = GroundType.GetMethod(name, DeclaredMembers);
            try
            {
                return method.Invoke(state, arguments);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static float Stored(Character character, GraphStorageKey key) => character.Storage.TryGetValue(key, out float value) ? value : float.NaN;

        private static bool IsStored(Character character, GraphStorageKey key) => character.Storage.TryGetValue(key, out float _);

        // ---- UpdateForLanding (0600155d @0xE6FF90) ----

        private static void SetLanding(Character character, float time, float speed)
        {
            character.Storage.SetValue(ActorFSMKeys.GroundLandingTime, time);
            character.Storage.SetValue(ActorFSMKeys.GroundLandingSpeed, speed);
        }

        // 0xE7009d..0xE700a6: the elapsed time since landing is compared with the window using ja, so exactly the window
        // is still inside it; 0xE70177 jbe keeps a velocity at or under the recorded speed (the hold applies), a faster
        // one is left alone.
        [Test]
        public void LandingHoldRaisesASlowerVelocityToTheRecordedSpeedAlongItsDirection()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldVelocity(new Vector3(0f, 0f, 2f));
                SetLanding(scene.Character, 10f, 3f);
                Invoke(scene.State, "UpdateForLanding", scene.Character, 0.5f);
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(new Vector3(0f, 0f, 3f)).Using(Vec));
            });
        }

        [Test]
        public void LandingHoldLeavesAFasterVelocityAlone()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldVelocity(new Vector3(0f, 0f, 5f));
                SetLanding(scene.Character, 10f, 3f);
                Invoke(scene.State, "UpdateForLanding", scene.Character, 0.5f);
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(new Vector3(0f, 0f, 5f)).Using(Vec));
            });
        }

        [Test]
        public void LandingHoldEndsOnceTheElapsedTimeExceedsTheWindowButNotAtItsEdge()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldVelocity(new Vector3(0f, 0f, 2f));
                SetLanding(scene.Character, 8f, 3f);
                Invoke(scene.State, "UpdateForLanding", scene.Character, 2f);
                Assert.That(scene.Character.WorldVelocity.z, Is.EqualTo(3f).Within(Tolerance), "elapsed equals the window");
                scene.Character.SetWorldVelocity(new Vector3(0f, 0f, 2f));
                Invoke(scene.State, "UpdateForLanding", scene.Character, 1.99f);
                Assert.That(scene.Character.WorldVelocity.z, Is.EqualTo(2f).Within(Tolerance), "elapsed is past the window");
            });
        }

        [Test]
        public void LandingHoldIgnoresAZeroRecordedSpeed()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldVelocity(new Vector3(0f, 0f, 2f));
                SetLanding(scene.Character, 10f, 0f);
                Invoke(scene.State, "UpdateForLanding", scene.Character, 0.5f);
                Assert.That(scene.Character.WorldVelocity.z, Is.EqualTo(2f).Within(Tolerance));
            });
        }

        // ---- UpdateForSlope (0600155e @0xE701D0) ----

        private static void ConfigureSlope(Scene scene, float slopeAngle, float speed, float gripRestoreTimer)
        {
            // A tilted character, so the slope look direction is not degenerate.
            scene.Character.SetWorldRotation(Quaternion.Euler(30f, 0f, 0f));
            scene.Character.SetWorldVelocity(scene.Character.WorldRotation * (Vector3.forward * speed));
            SetProperty(scene.Ability, "SlopeAngle", slopeAngle);
            scene.Character.Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, gripRestoreTimer);
        }

        private static GlobalConstantDefinition_Character SlopeConstants(Owned owned, Scene scene)
        {
            var constants = owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Character>());
            constants.MovementInputSpeedMin = 2f;
            constants.MovementStationarySlopeAngleMax = 45f;
            constants.MovementOrientateToSlopeAngleMax = 90f;
            constants.GripRestoreTime = 0.75f;
            SetProperty(scene.Character, "Constants", constants);
            return constants;
        }

        // 0xE70389..0xE703b6 and the angle range before it: a slope strictly between the stationary and orientation
        // limits, a speed under the input minimum, no pending grip restore and no constant direction modifier. Then the
        // character is turned along the slope, given the minimum speed and the restore time (0xE705d0 re-reads the
        // constants object).
        [Test]
        public void SlopeSettlingAppliesOnlyInsideTheAngleRangeUnderTheMinimumSpeed()
        {
            WithGround((owned, scene) =>
            {
                SlopeConstants(owned, scene);
                (float angle, float speed, float timer, bool applies, string why)[] cases =
                {
                    (60f, 1f, 0f, true, "inside the range, slow, no restore"),
                    (45f, 1f, 0f, false, "the stationary limit itself is not inside"),
                    (90f, 1f, 0f, false, "the orientation limit itself is not inside"),
                    (30f, 1f, 0f, false, "below the range"),
                    (95f, 1f, 0f, false, "above the range"),
                    (60f, 2f, 0f, false, "at the input minimum speed"),
                    (60f, 3f, 0f, false, "faster than the input minimum"),
                    (60f, 1f, 0.5f, false, "a grip restore is pending"),
                };
                foreach (var test in cases)
                {
                    ConfigureSlope(scene, test.angle, test.speed, test.timer);
                    Vector3 before = scene.Character.WorldVelocity;
                    Invoke(scene.State, "UpdateForSlope", scene.Character, scene.Ability);
                    if (test.applies)
                    {
                        Assert.That(scene.Character.WorldVelocityMagnitude, Is.EqualTo(2f).Within(Tolerance), test.why);
                        Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripRestoreTimer), Is.EqualTo(0.75f).Within(Tolerance), test.why);
                    }
                    else
                    {
                        Assert.That(scene.Character.WorldVelocity, Is.EqualTo(before).Using(Vec), test.why);
                        Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripRestoreTimer), Is.EqualTo(test.timer).Within(Tolerance), test.why);
                    }
                }
            });
        }

        // 0xE7023c..0xE70292: the definition's own limits replace the constants when it overrides them.
        [Test]
        public void SlopeSettlingUsesTheDefinitionsLimitsWhenItOverridesTheConstants()
        {
            WithGround((owned, scene) =>
            {
                SlopeConstants(owned, scene);
                scene.Definition.OverrideSlopeAngleMaxConstants = true;
                scene.Definition.MovementStationarySlopeAngleMaxOverride = 10f;
                scene.Definition.MovementOrientateToSlopeAngleMaxOverride = 20f;
                ConfigureSlope(scene, 15f, 1f, 0f);
                Invoke(scene.State, "UpdateForSlope", scene.Character, scene.Ability);
                Assert.That(scene.Character.WorldVelocityMagnitude, Is.EqualTo(2f).Within(Tolerance), "inside the override range");
                ConfigureSlope(scene, 60f, 1f, 0f);
                Vector3 before = scene.Character.WorldVelocity;
                Invoke(scene.State, "UpdateForSlope", scene.Character, scene.Ability);
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(before).Using(Vec), "the constants' range no longer applies");
            });
        }

        // ---- UpdateRemainTimer (06001560 @0xE70680) ----

        private static CharacterQueuedActions Queue(Character character) => character.QueuedActions;

        // The queued-actions flags are set the way CharacterQueuedActions would after a brain press; the public setters
        // need a started brain.
        private static void QueueBoost(Character character) => Set(Queue(character), "m_boost", true);

        private static void QueueJump(Character character) => Set(Queue(character), "m_jump", true);

        // 0xE70b9f..0xE70bec: with none of the four jump and boost abilities the countdown is not run at all.
        [Test]
        public void RemainTimerIsNotCountedWithoutAnyJumpOrBoostAbility()
        {
            WithGround((owned, scene) =>
            {
                Get<Dictionary<ActorAbilityType, ActorAbility>>(scene.Character, "m_abilities").Remove(ActorAbilityType.Character_MovementGroundBoost);
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Stored(scene.Character, ActorFSMKeys.StateRemainTimer), Is.EqualTo(1f));
            });
        }

        [Test]
        public void RemainTimerCountsDownAndStopsAtZero()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Stored(scene.Character, ActorFSMKeys.StateRemainTimer), Is.EqualTo(0.75f).Within(Tolerance));
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Stored(scene.Character, ActorFSMKeys.StateRemainTimer), Is.EqualTo(0f), "clamped by maxss, not negative");
            });
        }

        // 0xE70670: a stored zero is the same as no timer, so nothing is handed over and the queue is kept.
        [Test]
        public void RemainTimerOfZeroLeavesTheQueuedActionsAlone()
        {
            WithGround((owned, scene) =>
            {
                QueueBoost(scene.Character);
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Queue(scene.Character).Boost, Is.True);
                Assert.That(Get<bool>(scene.Ability, "m_queuedBoost"), Is.False);
            });
        }

        // 0xE70bec..: once the time has run out the queued boost is handed to the boost ability (QueueBoost(true)) and
        // only then are the queued buffers reset.
        [Test]
        public void ExpiredRemainTimerHandsOverTheQueuedBoostAndResetsTheQueue()
        {
            WithGround((owned, scene) =>
            {
                QueueBoost(scene.Character);
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Get<bool>(scene.Ability, "m_queuedBoost"), Is.True, "handed over");
                Assert.That(Queue(scene.Character).Boost, Is.False, "queue reset after the hand over");
            });
        }

        [Test]
        public void RemainTimerStillRunningDoesNotHandOverTheQueue()
        {
            WithGround((owned, scene) =>
            {
                QueueBoost(scene.Character);
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Get<bool>(scene.Ability, "m_queuedBoost"), Is.False);
                Assert.That(Queue(scene.Character).Boost, Is.True);
            });
        }

        // A queued jump with no jump ability to take it is not handed over, so the buffers are not reset
        // (the reset runs only when one of the two hand overs succeeded).
        [Test]
        public void ExpiredRemainTimerKeepsTheQueueWhenNoAbilityTakesIt()
        {
            WithGround((owned, scene) =>
            {
                QueueJump(scene.Character);
                scene.Character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0.1f);
                Invoke(scene.State, "UpdateRemainTimer", scene.Character, 0.25f);
                Assert.That(Queue(scene.Character).Jump, Is.True);
                Assert.That(Get<bool>(scene.Ability, "m_queuedBoost"), Is.False);
            });
        }

        // ---- UpdateTurnGrip (06001565 @0xE71590) ----

        private static bool TurnGrip(Scene scene, float deltaTime)
        {
            object[] arguments = { scene.Character, scene.Ability, deltaTime, Vector3.forward, 0f };
            return (bool)Invoke(scene.State, "UpdateTurnGrip", arguments);
        }

        private static AnimationCurve TwoKeyCurve() => AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // 0xE716b4 jbe: the boost restore timer counts down only while positive, clamped at zero by maxss.
        [Test]
        public void TurnGripCountsDownTheBoostRestoreTimer()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.Storage.SetValue(ActorFSMKeys.GroundBoostRestoreTimer, 0.5f);
                TurnGrip(scene, 0.2f);
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundBoostRestoreTimer), Is.EqualTo(0.3f).Within(Tolerance));
                TurnGrip(scene, 0.5f);
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundBoostRestoreTimer), Is.EqualTo(0f), "clamped at zero");
            });
        }

        // While the grip restore timer runs the grip is zero, whatever curves exist (a negative control: a two-key curve
        // is present), and the timer is counted down; a timer that expires this step still gives zero grip.
        [Test]
        public void TurnGripIsZeroWhileTheGripRestoreTimerRuns()
        {
            WithGround((owned, scene) =>
            {
                SetProperty(scene.Ability, "GripAgainstGravityMultiplierCurve", TwoKeyCurve());
                scene.Character.Storage.SetValue(ActorFSMKeys.GroundGripRestoreTimer, 0.5f);
                scene.Character.Storage.SetValue(ActorFSMKeys.GroundGripTime, 3f);
                Assert.That(TurnGrip(scene, 0.2f), Is.False);
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripRestoreTimer), Is.EqualTo(0.3f).Within(Tolerance));
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripValue), Is.EqualTo(0f));
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripTime), Is.EqualTo(3f), "the curve branch is not taken");
                Assert.That(TurnGrip(scene, 0.5f), Is.False);
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripRestoreTimer), Is.EqualTo(0f));
            });
        }

        // 0xE719bb..0xE719ca: a missing curve or one under two keys gives full grip and clears the grip time.
        [Test]
        public void TurnGripIsFullWithoutAUsableAgainstGravityCurve()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.Storage.SetValue(ActorFSMKeys.GroundGripTime, 3f);
                Assert.That(TurnGrip(scene, 0.02f), Is.True, "no curve");
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripValue), Is.EqualTo(1f));
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripTime), Is.EqualTo(0f));
                scene.Character.Storage.SetValue(ActorFSMKeys.GroundGripTime, 3f);
                SetProperty(scene.Ability, "GripAgainstGravityMultiplierCurve", new AnimationCurve(new Keyframe(0f, 1f)));
                Assert.That(TurnGrip(scene, 0.02f), Is.True, "a one-key curve");
                Assert.That(Stored(scene.Character, ActorFSMKeys.GroundGripTime), Is.EqualTo(0f));
            });
        }

        // ---- DetectLedgeAhead (0600156a @0xE72AE0) and UpdateLedgeBraking (06001569 @0xE725E0) ----

        // The cosine caches CalculateCachedValues derives from the authored angles, supplied directly.
        private static LedgeBrakeParameters Ledge(float maximumCosAngleToPlane, float ledgeCosAngleLimit)
        {
            var parameters = new LedgeBrakeParameters();
            Set(parameters, "m_maximumCosAngleToPlane", maximumCosAngleToPlane);
            Set(parameters, "m_ledgeCosAngleLimit", ledgeCosAngleLimit);
            return parameters;
        }

        // Facing +x with world up +y, a capsule on the Ignore Raycast layer at the character's position (so its own
        // bounds are real and its collider is not hit), and a track mask of the default layer only.
        private static void WithLedgeScene(Action<Owned, Scene> body) => WithGround((owned, scene) =>
        {
            scene.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
            scene.Character.SetWorldPosition(Origin);
            GameObject capsuleHost = owned.Host("Lucid ledge capsule");
            capsuleHost.layer = 2;
            capsuleHost.transform.position = Origin;
            Set(scene.Character, "m_capsuleColliderCollision", capsuleHost.AddComponent<CapsuleCollider>());
            scene.Character.Settings.Collider.TrackMask = 1;
            Physics.SyncTransforms();
            body(owned, scene);
        });

        private static void Block(Owned owned, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            GameObject host = owned.Host("Lucid ledge block");
            host.transform.SetPositionAndRotation(centre, rotation);
            host.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
        }

        private static bool Detect(Scene scene, LedgeBrakeParameters parameters) => (bool)Invoke(scene.State, "DetectLedgeAhead", scene.Character, parameters);

        private static Vector3? Recorded(Character character) =>
            character.Storage.TryGetValue(ActorFSMKeys.GroundLedgePosition, out Vector3 position) ? position : (Vector3?)null;

        // 0xE72b66..0xE72b7a: the negated gravity direction is dotted with the up direction, and a surface flatter than
        // the maximum plane angle is rejected before any ray is cast (no bounds are read: the capsule is not set).
        [Test]
        public void LedgeDetectionRejectsAnUpDirectionOutsideThePlaneLimitBeforeCasting()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                Assert.That(Detect(scene, Ledge(1.5f, 0.5f)), Is.False);
                Assert.That(Recorded(scene.Character), Is.Null);
            });
        }

        // 0xE72d26..0xE72d29: a walkable surface ahead records its hit point and is not a ledge.
        [Test]
        public void LedgeDetectionRecordsAWalkableHitAheadAndIsNotALedge()
        {
            WithLedgeScene((owned, scene) =>
            {
                Block(owned, Origin + new Vector3(2f, -0.5f, 0f), new Vector3(4f, 1f, 4f), Quaternion.identity);
                Assert.That(Detect(scene, Ledge(0.5f, 0.7f)), Is.False);
                Assert.That(Recorded(scene.Character).Value, Is.EqualTo(Origin + new Vector3(2f, 0f, 0f)).Using(Vec));
            });
        }

        // A steep surface ahead (normal dot up under the ledge limit) is a ledge, and nothing is recorded.
        [Test]
        public void LedgeDetectionTreatsASteepHitAheadAsALedgeWithoutRecording()
        {
            WithLedgeScene((owned, scene) =>
            {
                Block(owned, Origin + new Vector3(2f, 0f, 0f), new Vector3(6f, 0.2f, 6f), Quaternion.Euler(0f, 0f, 60f));
                Assert.That(Detect(scene, Ledge(0.5f, 0.7f)), Is.True);
                Assert.That(Recorded(scene.Character), Is.Null);
            });
        }

        // 0xE7305E: with nothing ahead, the down-forward ray compares the height of the character above its hit point with
        // the ledge limit using ja (strictly greater), and records nothing either way.
        [Test]
        public void LedgeDetectionComparesTheStopRayHeightWithTheLedgeLimit()
        {
            WithLedgeScene((owned, scene) =>
            {
                Block(owned, Origin + new Vector3(0.25f, -0.6f, 0f), new Vector3(2.5f, 1f, 4f), Quaternion.identity);
                Assert.That(Detect(scene, Ledge(0.5f, 0.05f)), Is.True, "the floor is 0.1 below the character, over the limit");
                Assert.That(Detect(scene, Ledge(0.5f, 0.2f)), Is.False, "the same floor under a larger limit");
                Assert.That(Recorded(scene.Character), Is.Null);
            });
        }

        // 0xE72e..: no hit on either ray records the character position as the ledge.
        [Test]
        public void LedgeDetectionRecordsTheCharacterPositionWhenBothRaysMiss()
        {
            WithLedgeScene((owned, scene) =>
            {
                Assert.That(Detect(scene, Ledge(0.5f, 0.7f)), Is.True);
                Assert.That(Recorded(scene.Character).Value, Is.EqualTo(Origin).Using(Vec));
            });
        }

        // 0xE725e0: a character at or under 0.0001 speed does not even look for a ledge.
        [Test]
        public void LedgeBrakingDoesNothingForAStationaryCharacter()
        {
            WithLedgeScene((owned, scene) =>
            {
                scene.Character.SetWorldVelocity(Vector3.zero);
                Invoke(scene.State, "UpdateLedgeBraking", scene.Character, Ledge(0.5f, 0.7f));
                Assert.That(Recorded(scene.Character), Is.Null, "detection would have recorded the position");
            });
        }

        // A moving character whose up direction is outside the plane limit gets no braking: the velocity is unchanged.
        [Test]
        public void LedgeBrakingLeavesAMovingCharacterAloneOutsideThePlaneLimit()
        {
            WithGround((owned, scene) =>
            {
                scene.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                scene.Character.SetWorldVelocity(new Vector3(3f, 0f, 0f));
                Invoke(scene.State, "UpdateLedgeBraking", scene.Character, Ledge(1.5f, 0.7f));
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(new Vector3(3f, 0f, 0f)).Using(Vec));
            });
        }

        // The ground states keep their declared shape.
        [Test]
        public void DeclarationShapeGroundStateKeepsItsBaseAndConstraints()
        {
            Assert.That(GroundType.IsAbstract, Is.True);
            Assert.That(GroundType.BaseType, Is.EqualTo(typeof(CharacterState_Ability<CharacterAbility_MovementGroundBoost>)));
            Type[] constraints = GroundType.GetGenericTypeDefinition().GetGenericArguments()[1].GetGenericParameterConstraints();
            Assert.That(constraints, Has.Length.EqualTo(1));
        }
    }
}
