using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The climbing entry abilities: the shared EnterClimbing base and its air, ground and direction override leaves.
    // Expected values come from the 1.10.1 x86-64 bodies cited per test. A started Character (its collision
    // events, a capsule body, the ability UI) is not available in Edit Mode, so each case sets only the
    // members the body under test reads, through their real private setters, on a Character that is a
    // component of an owned inactive host.
    public sealed class CharacterClimbingAbilityTests
    {
        private const BindingFlags DeclaredOnly = Instance | BindingFlags.DeclaredOnly;

        private static CharacterClimbingCollisionTracker InstallTracker(object ability, Character character, bool colliding, Vector3 normal, Vector3 enterVelocity)
        {
            var tracker = new CharacterClimbingCollisionTracker(character, new LayerMask());
            SetProperty(tracker, "IsColliding", colliding);
            SetProperty(tracker, "CollisionNormal", normal);
            SetProperty(tracker, "WorldVelocityAtLastTerrainEnter", enterVelocity);
            SetProperty(ability, "ClimbingCollisionTracker", tracker);
            return tracker;
        }

        private static bool CanEnter(object ability) => (bool)Call(ability, "CanEnterClimb");

        // ---- CharacterAbility_ClimbingGround (020002c8) ----

        // 0x745580: threshold = Definition+0xDC; the result is ucomiss(threshold squared, character+0xCC) / seta, that
        // is threshold squared > WorldVelocityMagnitudeSqr. Equality fails, an unordered NaN fails, and the
        // squared threshold ignores the sign.
        [Test]
        public void GroundClimbEntersOnlyWhileTheSquaredSpeedIsBelowTheSquaredThreshold()
        {
            WithAbility<CharacterAbility_ClimbingGround, CharacterAbilityDefinition_ClimbingGround>((owned, character, ability, definition) =>
            {
                definition.VelocityThresholdGround = 10f;
                foreach ((float squaredSpeed, bool expected) in new[] { (0f, true), (99.99f, true), (100f, false), (100.01f, false), (float.NaN, false) })
                {
                    SetActorProperty(character, "WorldVelocityMagnitudeSqr", squaredSpeed);
                    Assert.That(CanEnter(ability), Is.EqualTo(expected), "squared speed " + squaredSpeed);
                }
                definition.VelocityThresholdGround = -10f;
                SetActorProperty(character, "WorldVelocityMagnitudeSqr", 99.99f);
                Assert.That(CanEnter(ability), Is.True, "a negative threshold is squared");
                definition.VelocityThresholdGround = 0f;
                SetActorProperty(character, "WorldVelocityMagnitudeSqr", 0f);
                Assert.That(CanEnter(ability), Is.False, "a zero threshold never enters");
            });
        }

        // 0x7455E0 jumps straight into ActorMovementUtilities.GetSlopeAngle with the character (a body that
        // needs the character's movement frame), replacing the shared surface angle of the base.
        [Test]
        public void GroundClimbMeasuresTheSlopeWithTheMovementUtilities()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_ClimbingGround), "GetSlopeAngle");
            Assert.That(calls.Select(call => call.DeclaringType.Name + "." + call.Name), Is.EqualTo(new[] { "ActorMovementUtilities.GetSlopeAngle" }));
            Assert.That(typeof(CharacterAbility_ClimbingGround).GetMethod("GetSlopeAngle", DeclaredOnly).GetBaseDefinition().DeclaringType.Name,
                Is.EqualTo("CharacterAbility_EnterClimbing`1"));
        }

        // ---- CharacterAbility_ClimbingAir (020002c6) ----

        private static bool AirClimbCanEnter(Action<CharacterGravityVectorProbe, CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir> arrange)
        {
            bool result = false;
            WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
            {
                definition.MinAngleOfAttack = 45f;
                arrange(character, ability, definition);
                result = CanEnter(ability);
            });
            return result;
        }

        // 0x744F10: angle of attack = Vector3.Angle(CollisionNormal, WorldVelocityAtLastTerrainEnter) - 90 (the
        // degenerate angle of a zero vector is 0, at 0x745029 the constant is -90 itself). A head-on approach
        // (velocity opposite the normal) gives 90 and enters without any input, whatever the controller does.
        [Test]
        public void AirClimbEntersWithoutInputWhenTheAngleOfAttackIsAtLeastTheMinimum()
        {
            foreach (Vector3 velocity in new[] { new Vector3(0f, 0f, -1f), new Vector3(0f, 0.5f, -1f), new Vector3(0f, 0f, -30f) })
                Assert.That(AirClimbCanEnter((character, ability, definition) =>
                    InstallTracker(ability, character, true, new Vector3(0f, 0f, 1f), velocity)), Is.True, "velocity " + velocity);
        }

        // A shallow approach (angle 0 and 116.57 degrees give -90 and 26.57, below 45) falls to the input test:
        // 0x745151 onwards builds the heading from the controller (x, 0, y) when there is controller movement
        // (HasControllerMovement, 0x7450E1) and from Vector3.forward otherwise, rotates it by the character's
        // WorldRotation (+0x54), normalises it and enters only when its dot with the collision normal is negative
        // (0x745208 ucomiss 0, dot / seta).
        [Test]
        public void AirClimbFallsBackToTheInputHeadingDotTheNormal()
        {
            Func<Vector3, Vector3, Quaternion, bool, Vector2, bool> run = (normal, velocity, rotation, hasInput, controller) =>
                AirClimbCanEnter((character, ability, definition) =>
                {
                    InstallTracker(ability, character, true, normal, velocity);
                    SetActorProperty(character, "WorldRotation", rotation);
                    SetProperty(character, "RawControllerMovementMagnitude", hasInput ? 1f : 0f);
                    SetProperty(character, "ControllerMovement", controller);
                });
            Vector3 wall = new Vector3(0f, 0f, 1f);
            Quaternion identity = Quaternion.identity;

            // The approach velocity follows the normal, so the angle of attack is 0 - 90 = -90 and the input decides.
            Assert.That(run(wall, wall, identity, false, Vector2.zero), Is.False, "forward points at the normal: dot +1");
            Assert.That(run(-wall, -wall, identity, false, Vector2.zero), Is.True, "forward points against the normal: dot -1");
            Assert.That(run(wall, wall, Quaternion.Euler(0f, 180f, 0f), false, Vector2.zero), Is.True, "the rotation turns forward round");
            Assert.That(run(wall, wall, identity, true, new Vector2(1f, 0f)), Is.False, "sideways input is perpendicular: dot exactly 0 is not negative");
            Assert.That(run(wall, wall, identity, true, new Vector2(0f, -1f)), Is.True, "backwards input is (0, 0, -1)");
            Assert.That(run(wall, wall, identity, false, new Vector2(0f, -1f)), Is.False, "the controller is ignored without controller movement");
            // 116.57 degrees between normal and velocity is an angle of attack of 26.57, below the minimum of 45:
            // the forward heading decides and refuses, where a head-on approach enters.
            Assert.That(run(wall, new Vector3(1f, 0f, -0.5f), identity, false, Vector2.zero), Is.False);
        }

        // 0x7450CE ucomiss(angle, +0xDC) / jae: the minimum is inclusive, and an unordered minimum does not
        // enter. A zero velocity has the degenerate angle, so the angle of attack is exactly -90.
        [Test]
        public void AirClimbMinimumAngleOfAttackIsInclusiveAndNaNDoesNotEnter()
        {
            bool Enters(float minimum)
            {
                bool result = false;
                WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
                {
                    definition.MinAngleOfAttack = minimum;
                    InstallTracker(ability, character, true, new Vector3(0f, 0f, 1f), Vector3.zero);
                    SetProperty(character, "RawControllerMovementMagnitude", 0f);
                    result = CanEnter(ability);
                });
                return result;
            }
            Assert.That(Enters(-90f), Is.True, "equal to the minimum");
            Assert.That(Enters(-90.01f), Is.True);
            Assert.That(Enters(-89.99f), Is.False, "forward (0, 0, 1) is not against the normal");
            Assert.That(Enters(float.NaN), Is.False);
        }

        // ---- CharacterAbility_ClimbingDirectionOverride (020002c7) ----

        // 0x745320: nothing is read unless the previous update saw an active direction override. Then the
        // request key is looked up with TryGetValue; a stored value (true or false) is removed and its value
        // returned, and an absent key gives false.
        [Test]
        public void DirectionOverrideClimbConsumesTheRequestOnlyAfterAnOverrideWasActive()
        {
            WithAbility<CharacterAbility_ClimbingDirectionOverride, CharacterAbilityDefinition_ClimbingDirectionOverride>((owned, character, ability, definition) =>
            {
                GraphStorageKey key = ActorFSMKeys.ClimbingEnterWhenDirectionOverrideEnds;
                Set(ability, "m_directionOverrideActivePrevious", false);
                character.Storage.SetValue(key, true);
                Assert.That(CanEnter(ability), Is.False, "no override was active");
                Assert.That(character.Storage.TryGetValue(key, out bool untouched) && untouched, Is.True, "the request is left alone");

                Set(ability, "m_directionOverrideActivePrevious", true);
                Assert.That(CanEnter(ability), Is.True, "the request is returned");
                Assert.That(character.Storage.TryGetValue(key, out bool _), Is.False, "and consumed");

                Assert.That(CanEnter(ability), Is.False, "no request");
                Assert.That(character.Storage.TryGetValue(key, out bool _), Is.False);

                character.Storage.SetValue(key, false);
                Assert.That(CanEnter(ability), Is.False, "a false request enters nothing");
                Assert.That(character.Storage.TryGetValue(key, out bool _), Is.False, "but is still consumed");
            });
        }

        // 0x7452B0: the base update, then the direction override flag of the character is remembered.
        [Test]
        public void DirectionOverrideClimbRemembersTheOverrideAfterTheBaseUpdate()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_ClimbingDirectionOverride), "DoOnUpdate");
            int baseUpdate = IndexOf(calls, "CharacterAbility_EnterClimbing", "DoOnUpdate");
            int active = IndexOf(calls, "Character", "IsDirectionOverrideActive");
            Assert.That(baseUpdate, Is.LessThan(active));
            Assert.That(calls.Count, Is.EqualTo(2));
        }

        // 0x745480: the base text, then ", direction modifer active = " (sic) and the bool, appended once.
        [Test]
        public void DirectionOverrideClimbReportsTheOverrideInTheDebugText()
        {
            WithAbility<CharacterAbility_ClimbingDirectionOverride, CharacterAbilityDefinition_ClimbingDirectionOverride>((owned, character, ability, definition) =>
            {
                var text = new StringBuilder();
                Call(ability, "GetDebugExtraInfo", text);
                Assert.That(text.ToString(), Is.EqualTo("CanClimb = Off, direction modifer active = False"));

                character.AddModifierOverride((int)DirectionModifierType.Override, new Vector3(0f, 0f, 1f));
                SetProperty(ability, "IsTriggered", true);
                text.Clear();
                Call(ability, "GetDebugExtraInfo", text);
                Assert.That(text.ToString(), Is.EqualTo("CanClimb = On, direction modifer active = True"));
            });
        }

        // ---- CharacterAbility_EnterClimbing (020002c9) ----

        // 0xE60410: 180 minus Vector3.Angle(character gravity, collision normal). The floor normal opposes
        // gravity (180 degrees, so 0), the ceiling normal follows it (180), a wall is 90, and a zero normal has the
        // degenerate angle 0 (so 180). The plain angle would swap the floor and ceiling results.
        [Test]
        public void SlopeAngleIsOneEightyMinusTheAngleBetweenGravityAndTheNormal()
        {
            WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
            {
                character.GravityVector = new Vector3(0f, -20f, 0f);
                CharacterClimbingCollisionTracker tracker = InstallTracker(ability, character, true, Vector3.zero, Vector3.zero);
                foreach ((Vector3 normal, float slope) in new[]
                {
                    (new Vector3(0f, 1f, 0f), 0f), (new Vector3(0f, -1f, 0f), 180f), (new Vector3(0f, 0f, 1f), 90f),
                    (new Vector3(1f, 0f, 0f), 90f), (Vector3.zero, 180f),
                })
                {
                    SetProperty(tracker, "CollisionNormal", normal);
                    Assert.That((float)Call(ability, "GetSlopeAngle"), Is.EqualTo(slope).Within(1e-3f), "normal " + normal);
                }
            });
        }

        // 0xE60370: not colliding is false before the angle is read; otherwise the definition's surface angle
        // limits (60 to 120 by default) judge the slope.
        [Test]
        public void ClimbableSurfaceNeedsACollisionAndASlopeInsideTheSurfaceLimits()
        {
            WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
            {
                CharacterClimbingCollisionTracker tracker = InstallTracker(ability, character, false, new Vector3(0f, 0f, 1f), Vector3.zero);
                Assert.That((bool)Call(ability, "CollidingWithClimbableSurface"), Is.False, "wall slope 90 but not colliding");
                SetProperty(tracker, "IsColliding", true);
                Assert.That((bool)Call(ability, "CollidingWithClimbableSurface"), Is.True, "wall slope 90");
                SetProperty(tracker, "CollisionNormal", new Vector3(0f, 1f, 0f));
                Assert.That((bool)Call(ability, "CollidingWithClimbableSurface"), Is.False, "floor slope 0");
                SetProperty(tracker, "CollisionNormal", new Vector3(0f, -1f, 0f));
                Assert.That((bool)Call(ability, "CollidingWithClimbableSurface"), Is.False, "ceiling slope 180");
                definition.MinVerticalAngleSurface = 150f;
                definition.MaxVerticalAngleSurface = 200f;
                Assert.That((bool)Call(ability, "CollidingWithClimbableSurface"), Is.True, "ceiling slope inside the authored limits");
            });
        }

        // 0xE605A0 and 0xE605D0: entering starts the collision tracker after the character base update, leaving
        // clears the trigger and stops the tracker (which zeroes what it tracked).
        [Test]
        public void EnteringStartsTheTrackerAndLeavingClearsTheTriggerAndStopsIt()
        {
            WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
            {
                definition.GravityMultiplier = 2.5f;
                CharacterClimbingCollisionTracker tracker = InstallTracker(ability, character, true, new Vector3(0f, 0f, 1f), new Vector3(1f, 2f, 3f));
                Assert.That(Get<bool>(tracker, "m_started"), Is.False);
                Call(ability, "DoOnEnter");
                Assert.That(Get<bool>(tracker, "m_started"), Is.True);
                Assert.That(ability.GravityMultiplier, Is.EqualTo(2.5f), "the character base entry ran");

                SetProperty(ability, "IsTriggered", true);
                Call(ability, "DoOnLeave");
                Assert.That(((IAbilityTrigger)ability).IsTriggered, Is.False);
                Assert.That(Get<bool>(tracker, "m_started"), Is.False);
                Assert.That(tracker.IsColliding, Is.False);
                Assert.That(tracker.CollisionNormal, Is.EqualTo(Vector3.zero));
            });
        }

        // 0xE60A40: the base text, then "CanClimb = " with On or Off from the trigger flag.
        [Test]
        public void DebugTextReportsWhetherClimbingCanStart()
        {
            WithAbility<CharacterAbility_ClimbingGround, CharacterAbilityDefinition_ClimbingGround>((owned, character, ability, definition) =>
            {
                var text = new StringBuilder("> ");
                Call(ability, "GetDebugExtraInfo", text);
                SetProperty(ability, "IsTriggered", true);
                Call(ability, "GetDebugExtraInfo", text);
                Assert.That(text.ToString(), Is.EqualTo("> CanClimb = OffCanClimb = On"));
            });
        }

        // A TimeManager whose PlayerMovement total is set; the category object is the one TimeExtensions resolves.
        private static void SetTotalTime(TimeManager manager, float total)
        {
            object category = typeof(TimeExtensions).GetMethod("GetScriptableObject", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(null, new object[] { TimeCategory.PlayerMovement });
            ((System.Collections.IDictionary)Get<object>(manager, "m_totalTimeLookup"))[category] = total;
        }

        private static void WithUpdatingAirClimb(float total, float cooldown, float lastClimb, Vector3 accruedNormal,
            Action<Owned, CharacterGravityVectorProbe, CharacterAbility_ClimbingAir, CharacterClimbingCollisionTracker> body) =>
            WithAbility<CharacterAbility_ClimbingAir, CharacterAbilityDefinition_ClimbingAir>((owned, character, ability, definition) =>
            {
                owned.LeaseRow<TimeManager>();
                TimeManager manager = NewTimeManager(owned, TimeCategory.PlayerMovement, 1f);
                SetTotalTime(manager, total);
                Set(ability, "m_timeManager", manager);
                definition.ReAttachCooldownSeconds = cooldown;
                // Heading forward (0, 0, 1) against a minimum angle of attack no zero velocity reaches.
                definition.MinAngleOfAttack = 90f;
                SetActorProperty(character, "WorldRotation", Quaternion.identity);
                character.Storage.SetValue(ActorFSMKeys.ClimbingLastTimestamp, lastClimb);
                CharacterClimbingCollisionTracker tracker = InstallTracker(ability, character, false, Vector3.zero, new Vector3(1f, 2f, 3f));
                // One counted contact, so the tracker update never reaches its ground raycast.
                Set(tracker, "m_collisionCount", 1);
                Set(tracker, "m_accruedCollisionNormals", accruedNormal);
                body(owned, character, ability, tracker);
            });

        private static bool Triggered(CharacterAbility_ClimbingAir ability) => ((IAbilityTrigger)ability).IsTriggered;

        private static void Update(CharacterAbility_ClimbingAir ability) => Call(ability, "DoOnUpdate", (CharacterBrain)null, 0.02f);

        // 0xE60600: a climb enters when the time since the last climb (PlayerMovement total time minus the stored
        // timestamp) has reached the cooldown (0xE607B4 ucomiss / jb: equal passes), the tracker reports a
        // climbable surface, no direction override is active and CanEnterClimb agrees. Then the world velocity at
        // terrain enter and the collision normal are published for the climbing state.
        [Test]
        public void UpdateTriggersAndPublishesTheEntryWhenEveryConditionHolds()
        {
            WithUpdatingAirClimb(10f, 0.5f, 9f, new Vector3(0f, 0f, -3f), (owned, character, ability, tracker) =>
            {
                Update(ability);
                Assert.That(Triggered(ability), Is.True);
                Assert.That(tracker.IsColliding, Is.True);
                Assert.That(tracker.CollisionNormal, Is.EqualTo(new Vector3(0f, 0f, -1f)), "the accrued normals are normalised");
                Assert.That(character.Storage.GetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, Vector3.zero, false), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(character.Storage.GetValue(ActorFSMKeys.ClimbingEnterCollisionNormal, Vector3.zero, false), Is.EqualTo(new Vector3(0f, 0f, -1f)));
            });
        }

        // The cooldown is inclusive (elapsed exactly equals it) and exclusive below; the trigger from a previous
        // update is cleared and nothing is published when the cooldown has not elapsed (0xE60849).
        [Test]
        public void UpdateWaitsForTheReAttachCooldown()
        {
            WithUpdatingAirClimb(10f, 0.5f, 9.5f, new Vector3(0f, 0f, -1f), (owned, character, ability, tracker) =>
            {
                Update(ability);
                Assert.That(Triggered(ability), Is.True, "10 - 9.5 is exactly the cooldown");
            });
            WithUpdatingAirClimb(10f, 0.5f, 9.75f, new Vector3(0f, 0f, -1f), (owned, character, ability, tracker) =>
            {
                SetProperty(ability, "IsTriggered", true);
                Update(ability);
                Assert.That(Triggered(ability), Is.False);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, out Vector3 _), Is.False);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.ClimbingEnterCollisionNormal, out Vector3 _), Is.False);
            });
        }

        // A surface slope outside the limits (a floor, 0 degrees), an active direction override and a refusing
        // CanEnterClimb (heading forward with the normal facing it) each stop the climb without publishing.
        [Test]
        public void UpdateStopsForAnUnclimbableSurfaceADirectionOverrideOrARefusal()
        {
            WithUpdatingAirClimb(10f, 0.5f, 0f, new Vector3(0f, 1f, 0f), (owned, character, ability, tracker) =>
            {
                Update(ability);
                Assert.That(Triggered(ability), Is.False, "floor");
            });
            WithUpdatingAirClimb(10f, 0.5f, 0f, new Vector3(0f, 0f, -1f), (owned, character, ability, tracker) =>
            {
                character.AddModifierOverride((int)DirectionModifierType.Override, new Vector3(0f, 0f, 1f));
                Update(ability);
                Assert.That(Triggered(ability), Is.False, "direction override");
            });
            WithUpdatingAirClimb(10f, 0.5f, 0f, new Vector3(0f, 0f, 1f), (owned, character, ability, tracker) =>
            {
                Update(ability);
                Assert.That(Triggered(ability), Is.False, "CanEnterClimb refuses");
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, out Vector3 _), Is.False);
            });
        }

        // ---- IL witnesses for bodies that need a started Character ----

        // 0xE60220: the character base initialisation, then the tracker built from the character and the
        // definition's track mask (the constructor argument is the layer mask read at +0xCC), then the
        // TimeManager lookup. The ability UI instantiation of the base needs a started Character.
        [Test]
        public void InitialiseBuildsTheTrackerAfterTheBaseAndLooksUpTheTimeManagerLast()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_EnterClimbing<>), "Initialise");
            int baseInitialise = IndexOf(calls, "CharacterAbility", "Initialise");
            int tracker = IndexOf(calls, "CharacterClimbingCollisionTracker", ".ctor");
            int time = IndexOf(calls, "ProcessManager", "GetSystem");
            Assert.That(new[] { baseInitialise, tracker, time }, Is.Ordered.Ascending.And.Unique);
            Assert.That(calls.Count(m => m.Name == ".ctor"), Is.EqualTo(1));
        }

        // 0xE60600: the base update, the tracker update, the stored timestamp read, the PlayerMovement total
        // time, the climbable surface test (inlined natively, a call here), the direction override, then CanEnterClimb.
        [Test]
        public void UpdateReadsTheTimestampAndTimeBeforeTheDirectionOverrideAndCanEnter()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_EnterClimbing<>), "DoOnUpdate");
            int baseUpdate = IndexOf(calls, "CharacterAbility", "DoOnUpdate");
            int tracker = IndexOf(calls, "CharacterClimbingCollisionTracker", "Update");
            int timestamp = IndexOf(calls, "IGraphStorage", "GetValue");
            int total = IndexOf(calls, "TimeExtensions", "GetTotalTime");
            int surface = IndexOf(calls, "CharacterAbility_EnterClimbing", "CollidingWithClimbableSurface");
            int overridden = IndexOf(calls, "Character", "IsDirectionOverrideActive");
            int canEnter = IndexOf(calls, "CharacterAbility_EnterClimbing", "CanEnterClimb");
            Assert.That(new[] { baseUpdate, tracker, timestamp, total, surface, overridden, canEnter }, Is.Ordered.Ascending.And.Unique);
        }

        // 0x544F10 reads the tracker's two vectors, measures one angle, and builds the input heading through
        // the character rotation and a normalise; ground climbing never normalises.
        [Test]
        public void AirClimbMeasuresOneAngleAndNormalisesTheInputHeading()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_ClimbingAir), "CanEnterClimb");
            Assert.That(calls.Count(m => m.DeclaringType.Name == "Vector3" && m.Name == "Angle"), Is.EqualTo(1));
            Assert.That(calls.Count(m => m.DeclaringType.Name == "Vector3" && m.Name == "get_normalized"), Is.EqualTo(1));
            Assert.That(calls.Count(m => m.DeclaringType.Name == "Vector3" && m.Name == "Dot"), Is.EqualTo(1));
            Assert.That(calls.Count(m => m.DeclaringType.Name == "Quaternion" && m.Name == "op_Multiply"), Is.EqualTo(1));
            Assert.That(Calls(typeof(CharacterAbility_ClimbingGround), "CanEnterClimb").Where(m => m.DeclaringType.Name == "Vector3"), Is.Empty);
        }

        // Declaration shape (not behaviour): the generic base keeps its constraint and interface.
        [Test]
        public void DeclarationShapeEnterClimbingKeepsItsConstraintInterfaceAndLeaves()
        {
            Type open = typeof(CharacterAbility_EnterClimbing<>);
            Assert.That(open.IsAbstract, Is.True);
            Assert.That(open.GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(CharacterAbilityDefinition_EnterClimbing) }));
            Assert.That(open.GetInterfaces(), Does.Contain(typeof(IAbilityTrigger)));
            Assert.That(open.GetFields(DeclaredOnly).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "<IsTriggered>k__BackingField", "<ClimbingCollisionTracker>k__BackingField", "m_timeManager" }));
            Assert.That(typeof(CharacterAbility_ClimbingAir).BaseType, Is.EqualTo(typeof(CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingAir>)));
            Assert.That(typeof(CharacterAbility_ClimbingGround).BaseType, Is.EqualTo(typeof(CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingGround>)));
            Assert.That(typeof(CharacterAbility_ClimbingDirectionOverride).BaseType,
                Is.EqualTo(typeof(CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingDirectionOverride>)));
            Assert.That(typeof(CharacterAbility_ClimbingDirectionOverride).GetFields(DeclaredOnly).Select(f => f.Name), Is.EqualTo(new[] { "m_directionOverrideActivePrevious" }));
            Assert.That(open.GetProperty("IsTriggered").GetGetMethod().IsFinal, Is.True, "interface implementation");
        }
    }
}
