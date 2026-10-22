using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Motion = HardlightProject.CharacterAbilityDefinition_HalfPipe.HalfPipeMotion;

namespace ProjectLucid.Tests
{
    // The half pipe air and transfer abilities and the half pipe air state. Expected values come from the 1.10.1
    // x86-64 bodies cited per test. Bodies that need a started Character (the state updates and entries, whose
    // bases reach the camera, form and stamina graphs) are checked as call sequences read from the compiled IL.
    public sealed class CharacterHalfPipeAbilityTests : OwnedGraphNamesFixture
    {
        public CharacterHalfPipeAbilityTests() : base("Lucid-halfpipe-", "Lucid-state-") { }

        private static Motion MotionWith(float ascending, float descending) => new Motion
        {
            Gravity = new CharacterAbilityDefinition_HalfPipe.HalfPipeGravity
            {
                GravityMultiplierAscending = ascending, GravityMultiplierDescending = descending, TerminalSpeed = 100f,
            },
            GravityOverrides = new CharacterAbilityDefinition_HalfPipe.HalfPipeGravityOverride[0],
        };

        private static CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride Override(float speed, float time) =>
            new CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride
            {
                ActivationSpeed = speed, ActivationTime = time, Gravity = new CharacterAbilityDefinition_HalfPipe.HalfPipeGravity(),
                GravityOverrides = new CharacterAbilityDefinition_HalfPipe.HalfPipeGravityOverride[0],
            };

        // ---- CharacterAbility_HalfPipeTransfer (020002cd) ----

        private static ColliderTrackingUtilities.RaycastTransform Lip(int layers, Vector3 position, Vector3 direction, Vector3 up) =>
            new ColliderTrackingUtilities.RaycastTransform { LayerMask = layers, Position = position, Direction = direction, Up = up };

        // 0x746A00: the transform, direction, speed and the gravity are stored, the air motion is taken, and the
        // stored gravity is the supplied one divided by the air motion's multiplier for the character's current
        // velocity (0x746AFF GetGravityMultiplier on the virtual WorldVelocity): 2 climbing, 4 falling.
        [Test]
        public void CachingStoresTheLipAndDividesTheGravityByTheCurrentMotionMultiplier()
        {
            WithAbility<CharacterAbility_HalfPipeTransfer, CharacterAbilityDefinition_HalfPipeTransfer>((owned, character, ability, definition) =>
            {
                character.GravityVector = new Vector3(0f, -20f, 0f);
                Motion motion = MotionWith(2f, 4f);
                ColliderTrackingUtilities.RaycastTransform lip = Lip(5, new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, -1f), new Vector3(0f, 1f, 0f));
                foreach ((Vector3 velocity, Vector3 expected) in new[] { (new Vector3(0f, 5f, 0f), new Vector3(0f, -15f, 0f)), (new Vector3(0f, -5f, 0f), new Vector3(0f, -7.5f, 0f)) })
                {
                    SetActorProperty(character, "WorldVelocity", velocity);
                    ability.CacheTransform(motion, lip, new Vector3(1f, 0f, 0f), 7f, new Vector3(0f, -30f, 0f));
                    Assert.That(Get<Vector3>(ability, "m_gravity"), Is.EqualTo(expected), "velocity " + velocity);
                }
                Assert.That(Get<Vector3>(ability, "m_direction"), Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(Get<float>(ability, "m_speed"), Is.EqualTo(7f));
                ColliderTrackingUtilities.RaycastTransform stored = Get<ColliderTrackingUtilities.RaycastTransform>(ability, "m_raycastTransform");
                Assert.That((stored.LayerMask, stored.Position, stored.Direction, stored.Up), Is.EqualTo((5, lip.Position, lip.Direction, lip.Up)));
                Assert.That(ability.AirMotion, Is.SameAs(motion));
            });
        }

        // 0x7472B0: the cached gravity scaled by the multiplier for the velocity passed in (not the character's).
        // Without an air motion the multiplier is 1.
        [Test]
        public void GravityIsTheCachedGravityScaledByTheMotionMultiplierOfTheVelocity()
        {
            WithAbility<CharacterAbility_HalfPipeTransfer, CharacterAbilityDefinition_HalfPipeTransfer>((owned, character, ability, definition) =>
            {
                Set(ability, "m_gravity", new Vector3(1f, -10f, 2f));
                Assert.That(Call(ability, "GetGravity", new Vector3(0f, 5f, 0f)), Is.EqualTo(new Vector3(1f, -10f, 2f)), "no air motion");
                SetProperty(ability, "AirMotion", MotionWith(2f, 4f));
                Assert.That(Call(ability, "GetGravity", new Vector3(0f, 5f, 0f)), Is.EqualTo(new Vector3(2f, -20f, 4f)));
                Assert.That(Call(ability, "GetGravity", new Vector3(0f, -5f, 0f)), Is.EqualTo(new Vector3(4f, -40f, 8f)));
            });
        }

        // 0x7469D0: leaving clears the speed and the hit time (+0x108, +0x118) and nothing else.
        [Test]
        public void LeavingClearsTheSpeedAndTheHitTimeOnly()
        {
            WithAbility<CharacterAbility_HalfPipeTransfer, CharacterAbilityDefinition_HalfPipeTransfer>((owned, character, ability, definition) =>
            {
                Set(ability, "m_speed", 5f);
                Set(ability, "m_hitTime", 2f);
                Set(ability, "m_direction", new Vector3(1f, 2f, 3f));
                Set(ability, "m_hitNormal", new Vector3(0f, 1f, 0f));
                Call(ability, "DoOnLeave");
                Assert.That(Get<float>(ability, "m_speed"), Is.EqualTo(0f));
                Assert.That(ability.HitTime, Is.EqualTo(0f));
                Assert.That(Get<Vector3>(ability, "m_direction"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(ability.HitNormal, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            });
        }

        // 0x746B40 with no surface to meet (the track mask selects nothing): the projection reports failure, the
        // hit normal keeps the lip direction it started from, the character's velocity is left alone, and the
        // location tracing state set around the projection (DebugBeginLocationInstantiation 0x746BC2, End 0x746CA5)
        // is released.
        [Test]
        public void ProjectingWithNothingToHitFailsAndReleasesTheTracingState()
        {
            WithAbility<CharacterAbility_HalfPipeTransfer, CharacterAbilityDefinition_HalfPipeTransfer>((owned, character, ability, definition) =>
            {
                character.GravityVector = new Vector3(0f, -10f, 0f);
                SetActorProperty(character, "WorldVelocity", new Vector3(3f, 0f, 0f));
                ability.CacheTransform(MotionWith(1f, 1f), Lip(0, Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.up), new Vector3(1f, 0f, 0f), 5f, new Vector3(0f, -10f, 0f));

                Assert.That(ability.ProjectToHalfPipe(0.02f), Is.False);
                Assert.That(ability.HitNormal, Is.EqualTo(new Vector3(0f, 0f, -1f)));
                Assert.That(character.WorldVelocity, Is.EqualTo(new Vector3(3f, 0f, 0f)));
                Assert.That(typeof(ColliderTrackingUtilities).GetField("m_character", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
            });
        }

        // 0x7470E0: the base result is the one returned, and the projection only runs for an enabled ability
        // (gizmo mode, fixed delta time step). With a track mask that selects nothing it neither hits nor throws.
        [Test]
        public void GizmoDrawingFollowsTheBaseResult()
        {
            WithAbility<CharacterAbility_HalfPipeTransfer, CharacterAbilityDefinition_HalfPipeTransfer>((owned, character, ability, definition) =>
            {
                ability.CacheTransform(MotionWith(1f, 1f), Lip(0, Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.up), new Vector3(1f, 0f, 0f), 5f, new Vector3(0f, -10f, 0f));
                Assert.That(ability.OnDrawGizmosSelected(), Is.False);
                SetProperty(ability, "Enabled", true);
                Assert.That(ability.OnDrawGizmosSelected(), Is.True);
            });
        }

        // 0x746B40, call order only (no hit case runs, so the speed and plane values are not witnessed): after the
        // projection, the heading across the lip is crossed with the up direction; the cached direction and the
        // character's velocity are projected onto the plane that heading is normal to, and the look rotation the
        // body builds is discarded.
        // SetWorldVelocity and SetWorldRotation are Actor virtuals, so the IL call targets are declared by Actor even though
        // the receiver is a Character.
        [Test]
        public void ProjectToHalfPipeCallOrderMatchesTheNative()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_HalfPipeTransfer), "ProjectToHalfPipe");
            int begin = IndexOf(calls, "ColliderTrackingUtilities", "DebugBeginLocationInstantiation");
            int project = IndexOf(calls, "ColliderTrackingUtilities", "ProjectToNeighbourSlope");
            int end = IndexOf(calls, "ColliderTrackingUtilities", "DebugEndLocationInstantiation");
            int cross = IndexOf(calls, "Vector3", "Cross");
            int firstProject = IndexOf(calls, "Vector3", "ProjectOnPlane");
            int secondProject = IndexOf(calls, "Vector3", "ProjectOnPlane", firstProject + 1);
            int velocity = IndexOf(calls, "Actor", "SetWorldVelocity");
            int look = IndexOf(calls, "Quaternion", "LookRotation");
            Assert.That(new[] { begin, project, end, cross, firstProject, secondProject, velocity, look }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Vector3", "get_normalized"), Is.EqualTo(2));
            Assert.That(Count(calls, "Vector3", "Dot"), Is.EqualTo(1));
            Assert.That(Count(calls, "Actor", "SetWorldRotation"), Is.EqualTo(0), "the look rotation is not applied");
        }

        // ---- CharacterAbility_HalfPipeAir (020002cc) ----

        // 0x745CE0: the half pipe base update first, then the air time grows by the step.
        [Test]
        public void AirTimeGrowsAfterTheBaseUpdate()
        {
            WithAbility<CharacterAbility_HalfPipeAir, CharacterAbilityDefinition_HalfPipeAir>((owned, character, ability, definition) =>
            {
                var brain = new CharacterBrain_Replay();
                Call(ability, "DoOnUpdate", brain, 0.25f);
                Call(ability, "DoOnUpdate", brain, 0.25f);
                Assert.That(Get<float>(ability, "m_airTime"), Is.EqualTo(0.5f));
                Assert.That(Get<float>(ability, "m_activeTimer"), Is.EqualTo(0.5f), "the half pipe base ran");
            });
        }

        // A definition with three overrides sorted by speed, an exclusion cosine of 0.5, the default motion and a
        // character facing up with the given fixed time total.
        private static void WithAir(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_HalfPipeAir, CharacterAbilityDefinition_HalfPipeAir, Motion[]> body) =>
            WithAbility<CharacterAbility_HalfPipeAir, CharacterAbilityDefinition_HalfPipeAir>((owned, character, ability, definition) =>
            {
                var overrides = new[] { Override(5f, 2f), Override(10f, 1f), Override(20f, 0.5f) };
                definition.AirMotion = MotionWith(1f, 1f);
                definition.AirMotionOverrides = overrides;
                definition.AirMotionExclude = new CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionExclude { ExcludeAngle = 60f, ExcludeAngleCosine = 0.5f };
                SetActorProperty(character, "UpDirection", Vector3.up);
                Set(character, "m_totalFixedTime", 10f);
                body(owned, character, ability, definition, new[] { definition.AirMotion, overrides[0], overrides[1], overrides[2] });
            });

        // The coroutine host is refused (an inactive host), so the deferred snapshot never runs in the case.
        private static void WithRefusedFixedUpdate(Owned owned)
        {
            FieldInfo instance = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = instance.GetValue(null);
            owned.Lease(new ReleaseAction(() => instance.SetValue(null, prior)));
            instance.SetValue(null, owned.Host("Lucid refusing coroutine host", false).AddComponent<CoroutineUtils>());
        }

        private static (int index, Motion motion) EnterAir(CharacterGravityVectorProbe character, CharacterAbility_HalfPipeAir ability,
            float speed, int storedIndex, float leaveTime, Vector3 storedNormal)
        {
            SetActorProperty(character, "WorldVelocityMagnitude", speed);
            if (storedIndex != -1) character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionIndex, storedIndex);
            if (leaveTime != 0f) character.Storage.SetValue(ActorFSMKeys.HalfPipeAirLeaveTime, leaveTime);
            if (storedNormal != Vector3.zero) character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionNormal, storedNormal);
            LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"));
            Call(ability, "DoOnEnter");
            return (character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -99, false), ability.AirMotion);
        }

        // 0x745630 with a chain-less entry (no stored index, normal or leave time): the default normal is the
        // character's up direction, whose dot with itself (1) reaches the exclusion cosine (0.5, 0x745A50
        // ucomiss / jae), so the default motion is kept with index -1 and the speed table is not consulted.
        [Test]
        public void AnEntryFacingTheDefaultNormalKeepsTheDefaultMotion()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                WithRefusedFixedUpdate(owned);
                var entered = EnterAir(character, ability, 50f, -1, 0f, Vector3.zero);
                Assert.That(entered.index, Is.EqualTo(-1));
                Assert.That(entered.motion, Is.SameAs(motions[0]));
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 99f, false), Is.EqualTo(0f));
            });
        }

        // Past the exclusion the motion comes from the speed table: the highest override whose activation speed
        // the character has reached (0x745AC8 ja: an equal speed qualifies), the default when none. A NaN speed is
        // unordered, ja is not taken and 0x745B18 jbe keeps looping, so every override qualifies (a negated
        // "speed >= ActivationSpeed" test would break out at the first).
        [Test]
        public void AFreshEntryPicksTheHighestOverrideTheSpeedHasReached()
        {
            foreach ((float speed, int index) in new[] { (3f, -1), (5f, 0), (7f, 0), (10f, 1), (19.9f, 1), (20f, 2), (50f, 2), (float.NaN, 2) })
                WithAir((owned, character, ability, definition, motions) =>
                {
                    WithRefusedFixedUpdate(owned);
                    var entered = EnterAir(character, ability, speed, -1, 0f, new Vector3(1f, 0f, 0f));
                    Assert.That(entered.index, Is.EqualTo(index), "speed " + speed);
                    Assert.That(entered.motion, Is.SameAs(motions[index + 1]), "speed " + speed);
                });
        }

        // A stored index from a previous half pipe (0x745A00 clamps it to the last override) reuses that override
        // while the time since leaving is below its activation time (0x745A2B ucomiss / jbe: equal fails),
        // otherwise the speed table decides again.
        [Test]
        public void AChainedEntryReusesTheNextOverrideWithinItsActivationTime()
        {
            // Overrides activate within 2, 1 and 0.5 seconds; the total is 10.
            foreach ((int stored, float leftAt, float speed, int expected) in new[]
            {
                (1, 9.5f, 0f, 1),    // 0.5 elapsed < 1
                (1, 9f, 0f, -1),     // exactly 1 elapsed: not below, and speed 0 reaches nothing
                (1, 8f, 12f, 1),     // 2 elapsed: speed table, 12 reaches the second override
                (7, 9.75f, 0f, 2),   // index clamped to 2: 0.25 elapsed < 0.5
                (0, 8.5f, 0f, 0),    // 1.5 elapsed < 2
                (2, 9f, 25f, 2),     // 1 elapsed >= 0.5: speed table gives 2 again
                (1, float.NaN, 0f, -1), // NaN elapsed: 0x745A2B ucomiss / jbe is taken, so the speed table decides (0 reaches nothing)
            })
                WithAir((owned, character, ability, definition, motions) =>
                {
                    WithRefusedFixedUpdate(owned);
                    var entered = EnterAir(character, ability, speed, stored, leftAt, new Vector3(1f, 0f, 0f));
                    Assert.That(entered.index, Is.EqualTo(expected), "stored " + stored + ", left at " + leftAt + ", speed " + speed);
                    Assert.That(entered.motion, Is.SameAs(motions[expected + 1]));
                });
        }

        // The exclusion is inclusive: a stored normal whose dot with up equals the cosine ends the chain at once,
        // whatever the stored index and speed (0x7459BE cmovae sets -1 when dot >= cosine).
        [Test]
        public void AnEntryOnTheExclusionAngleEndsTheChain()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                WithRefusedFixedUpdate(owned);
                var entered = EnterAir(character, ability, 50f, 1, 9.75f, new Vector3(0.8660254f, 0.5f, 0f));
                Assert.That(entered.index, Is.EqualTo(-1));
                Assert.That(entered.motion, Is.SameAs(motions[0]));
            });
        }

        // A NaN dot is unordered: 0x7459B6 ucomiss / cmovae and the re-test at 0x745A50 jae are not taken, so the
        // entry is not excluded and the speed table picks the override (a negated "dot < cosine" would exclude it).
        [Test]
        public void AnEntryWithANaNDotIsNotExcluded()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                WithRefusedFixedUpdate(owned);
                var entered = EnterAir(character, ability, 50f, -1, 0f, new Vector3(float.NaN, 0f, 0f));
                Assert.That(entered.index, Is.EqualTo(2));
                Assert.That(entered.motion, Is.SameAs(motions[3]));
            });
        }

        // The entry resets the air time and stores the leave time as zero beside the chosen index.
        [Test]
        public void EntryResetsTheAirTimeAndTheLeaveTime()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                WithRefusedFixedUpdate(owned);
                Set(ability, "m_airTime", 3f);
                EnterAir(character, ability, 12f, -1, 9f, new Vector3(1f, 0f, 0f));
                Assert.That(Get<float>(ability, "m_airTime"), Is.EqualTo(0f));
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 99f, false), Is.EqualTo(0f));
            });
        }

        // 0x745D50: leaving zeroes the air time, stores the fixed time total as the leave time, advances the stored index
        // while overrides remain after it (0x745F78 jge: the last index stays) and stores the character's up direction
        // as the chain normal.
        [Test]
        public void LeavingRecordsTheLeaveTimeAdvancesTheIndexAndStoresTheNormal()
        {
            foreach ((int stored, int expected) in new[] { (-1, 0), (0, 1), (1, 2), (2, 2), (9, 9) })
                WithAir((owned, character, ability, definition, motions) =>
                {
                    Set(ability, "m_airTime", 3f);
                    SetActorProperty(character, "UpDirection", new Vector3(0f, 0f, 1f));
                    if (stored != -1) character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionIndex, stored);
                    Call(ability, "DoOnLeave");
                    Assert.That(Get<float>(ability, "m_airTime"), Is.EqualTo(0f));
                    Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 99f, false), Is.EqualTo(10f));
                    Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -99, false), Is.EqualTo(expected), "stored " + stored);
                    Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionNormal, Vector3.zero, false), Is.EqualTo(new Vector3(0f, 0f, 1f)));
                });
        }

        // Without overrides the index never advances: -1 is not below -1 (0x745F78).
        [Test]
        public void LeavingWithoutOverridesLeavesTheIndex()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                definition.AirMotionOverrides = new CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride[0];
                Call(ability, "DoOnLeave");
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -99, false), Is.EqualTo(-1));
            });
        }

        // 0x746870, run on the next fixed update: the character's velocity, gravity, the track mask, the position, the
        // negated collision contact normal and the world up direction are snapshotted for the gizmo.
        [Test]
        public void TheFixedUpdateSnapshotCapturesTheLipTheCharacterIsOn()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                character.GravityVector = new Vector3(0f, -20f, 0f);
                SetActorProperty(character, "WorldVelocity", new Vector3(1f, 2f, 3f));
                SetActorProperty(character, "WorldPosition", new Vector3(4f, 5f, 6f));
                character.Settings.Collider.TrackMask = 5;
                Set(character.Collider, "m_collisionContactCount", 1);
                Set(character.Collider, "m_collisionContactNormal", new Vector3(0f, 0f, 1f));

                Call(ability, "<DoOnEnter>b__4_0");

                Assert.That(Get<Vector3>(ability, "m_projectedVelocity"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get<Vector3>(ability, "m_projectedGravity"), Is.EqualTo(new Vector3(0f, -20f, 0f)));
                ColliderTrackingUtilities.RaycastTransform lip = Get<ColliderTrackingUtilities.RaycastTransform>(ability, "m_projectedRaycastTransform");
                Assert.That(lip.LayerMask, Is.EqualTo(5));
                Assert.That(lip.Position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
                Assert.That(lip.Direction, Is.EqualTo(new Vector3(0f, 0f, -1f)));
                Assert.That(lip.Up, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            });
        }

        // The snapshot callback of the gizmo returns the cached gravity whatever velocity it is asked about, and the
        // gizmo draws while the projected velocity is not (nearly) zero: squared 1e-4 or more (0x7461E2 ucomiss / ja
        // skips when 1e-4 is greater than the square, an ordered comparison, so a NaN velocity still projects and
        // returns true at 0x74641D setbe), whatever the base result.
        [Test]
        public void TheGizmoDrawsOnlyWhileThereIsAProjectedVelocity()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                Set(ability, "m_projectedGravity", new Vector3(0f, -9f, 0f));
                Assert.That(Call(ability, "<OnDrawGizmosSelected>b__7_0", Vector3.one), Is.EqualTo(new Vector3(0f, -9f, 0f)));
                Set(ability, "m_projectedRaycastTransform", Lip(0, Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.up));
                foreach ((Vector3 velocity, bool drawn) in new[] { (Vector3.zero, false), (new Vector3(0f, 0f, 0.5f), true), (new Vector3(0.001f, 0f, 0f), false), (new Vector3(0f, 0f, 30f), true), (new Vector3(float.NaN, 0f, 0f), true) })
                {
                    Set(ability, "m_projectedVelocity", velocity);
                    Assert.That(ability.OnDrawGizmosSelected(), Is.EqualTo(drawn), "velocity " + velocity);
                }
            });
        }

        // 0x746490 and 0x746550: the labels, with the air time shown only while it is not zero, and the leave time
        // (while positive) replacing the air time in the debug text.
        [Test]
        public void TextsReportTheAirTimeAndTheLeaveTime()
        {
            WithAir((owned, character, ability, definition, motions) =>
            {
                var text = new System.Text.StringBuilder();
                Assert.That(ability.GetUIText(text), Is.False);
                Assert.That(text.ToString(), Is.Empty);
                Set(ability, "m_airTime", 1.5f);
                Assert.That(ability.GetUIText(text), Is.True);
                StringAssert.IsMatch("^Air Time = 1[.,]50\r?\n$", text.ToString());

                text.Clear();
                character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionIndex, 2);
                Call(ability, "GetDebugExtraInfo", text);
                StringAssert.IsMatch("AirTime = 1[.,]50 MotionIndex = 2 $", text.ToString());

                text.Clear();
                character.Storage.SetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 9f);
                Call(ability, "GetDebugExtraInfo", text);
                StringAssert.IsMatch("LeaveTime = 1[.,]00 MotionIndex = 2 $", text.ToString());
            });
        }

        // ---- CharacterState_HalfPipeAir (02000370) ----

        private static FiniteStateMachine Machine() => new FiniteStateMachine("Lucid-halfpipe-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);

        private static IFSMState NewState(string json) =>
            FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_HalfPipeAir), new FSMIdentifier("Lucid-state-" + Guid.NewGuid().ToString("N")), json);

        // 0x777210: the factory builds the state from the JSON arguments; the base keeps the ability type and the
        // state keeps the transfer ability type it will hand over to (+0x3C).
        [Test]
        public void StatesReadTheirAbilityTypesFromTheirJson()
        {
            IFSMState state = NewState("{\"AbilityType\":" + (int)ActorAbilityType.Character_HalfPipeAirRunning + ",\"HalfPipeTransferAbilityType\":"
                + (int)ActorAbilityType.Character_HalfPipeTransferRunning + "}");
            Assert.That(state.GetType(), Is.EqualTo(typeof(CharacterState_HalfPipeAir)));
            Assert.That(Get<ActorAbilityType>(state, "m_halfPipeTransferAbilityType"), Is.EqualTo(ActorAbilityType.Character_HalfPipeTransferRunning));
            Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_HalfPipeAirRunning));
            Assert.That(Get<ActorAbilityType>(NewState("{}"), "m_halfPipeTransferAbilityType"), Is.EqualTo((ActorAbilityType)0), "an omitted field is zero");
        }

        private static IFSMState NewStateWithTransfer() =>
            NewState("{\"AbilityType\":" + (int)ActorAbilityType.Character_HalfPipeAirRunning + ",\"HalfPipeTransferAbilityType\":"
                + (int)ActorAbilityType.Character_HalfPipeTransferRunning + "}");

        // 0x778650: the transfer ability of the configured type, if the character has one, takes the lip the
        // character is on: the track mask, the position, the reverse of the collider normal and the world's up
        // (Vector3.up, not the gravity-relative up); the direction is the velocity over its magnitude, and the
        // gravity is the character's. The result is the transfer's own projection (false here: nothing to hit).
        [Test]
        public void TheTransferAbilityTakesTheLipOfTheCharacter()
        {
            WithAir((owned, character, air, definition, motions) =>
            {
                var transfer = new CharacterAbility_HalfPipeTransfer();
                Set(transfer, "m_character", character);
                Set(transfer, "m_definition", owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_HalfPipeTransfer>()));
                ((Dictionary<ActorAbilityType, ActorAbility>)Get<object>(character, "m_abilities"))[ActorAbilityType.Character_HalfPipeTransferRunning] = transfer;
                SetProperty(air, "AirMotion", MotionWith(2f, 2f));
                character.GravityVector = new Vector3(20f, 0f, 0f);
                character.Settings.Collider.TrackMask = 0;
                SetActorProperty(character, "WorldPosition", new Vector3(1f, 2f, 3f));
                SetActorProperty(character, "WorldVelocity", new Vector3(0f, 6f, 8f));
                SetActorProperty(character, "WorldVelocityMagnitude", 10f);
                IFSMState state = NewStateWithTransfer();

                object result = Call(state, "ActivateAbilityTransfer", character, air, new Vector3(9f, 9f, 9f), new Vector3(0f, 0f, 1f), 0.02f);

                Assert.That(result, Is.EqualTo(false));
                var lip = Get<ColliderTrackingUtilities.RaycastTransform>(transfer, "m_raycastTransform");
                Assert.That((lip.LayerMask, lip.Position, lip.Direction, lip.Up), Is.EqualTo((0, new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, -1f), Vector3.up)));
                Assert.That(Get<Vector3>(transfer, "m_direction"), Is.EqualTo(new Vector3(0f, 0.6f, 0.8f)));
                Assert.That(Get<float>(transfer, "m_speed"), Is.EqualTo(10f));
                Assert.That(Get<Vector3>(transfer, "m_gravity"), Is.EqualTo(new Vector3(10f, 0f, 0f)), "the gravity 20 over the motion multiplier 2");
                Assert.That(transfer.AirMotion, Is.SameAs(air.AirMotion));
            });
        }

        [Test]
        public void ATransferAbilityMissingFromTheCharacterGivesFalse()
        {
            WithAir((owned, character, air, definition, motions) =>
            {
                IFSMState state = NewStateWithTransfer();
                Assert.That(Call(state, "ActivateAbilityTransfer", character, air, Vector3.zero, Vector3.up, 0.02f), Is.EqualTo(false));
            });
        }

        // ---- IL witnesses ----

        // 0x777810: the half pipe update first, then (only while the collider is active) the raycast location step and its
        // result flag, the transfer hand-over on a miss, the ground check, and finally the plane projection of the position
        // and velocity before the rotation, in the native order.
        // SetWorldPosition, SetWorldVelocity and SetWorldRotation are Actor virtuals, so their IL call targets are Actor's.
        [Test]
        public void StateUpdateFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_HalfPipeAir), "DoUpdate");
            int baseUpdate = IndexOf(calls, "CharacterState_HalfPipe", "DoUpdate");
            int projectRaycast = IndexOf(calls, "ColliderTrackingUtilities", "ProjectColliderRaycastLocation");
            int transfer = IndexOf(calls, "CharacterState_HalfPipeAir", "ActivateAbilityTransfer");
            int normalised = IndexOf(calls, "Vector3", "get_normalized");
            int project = IndexOf(calls, "Vector3", "ProjectOnPlane");
            int position = IndexOf(calls, "Actor", "SetWorldPosition");
            int velocity = IndexOf(calls, "Actor", "SetWorldVelocity");
            int look = IndexOf(calls, "Quaternion", "LookRotation");
            int rotation = IndexOf(calls, "Actor", "SetWorldRotation");
            Assert.That(new[] { baseUpdate, projectRaycast, transfer, normalised, project, position, velocity, look, rotation }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Vector3", "Dot"), Is.EqualTo(2), "one dot per plane distance");
            Assert.That(Count(calls, "ColliderTrackingUtilities", "ProjectColliderRaycastLocation"), Is.EqualTo(1));
        }

        // 0x777D1E mulss scales the forward speed by the step before the raycast location step; 0x777E4A xor r14b,1
        // negates the ground hit for the stored update flag; 0x778633 mov ecx,1 stores the buffer-disabled flag true.
        // Without a started Character these are read from the compiled opcodes.
        [Test]
        public void StateUpdateScalesTheSpeedByTheStepAndStoresTheNativeFlagValues()
        {
            MethodBase update = typeof(CharacterState_HalfPipeAir).GetMethods(DeclaredMembers).Single(m => m.Name == "DoUpdate");
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(update);
            List<(int Offset, System.Reflection.Emit.OpCode Code)> il = OriginalILCallSequence.Instructions(update);
            bool Targets(OriginalILCallSequence.Call call, string type, string name) => call.Target.DeclaringType.Name == type && call.Target.Name == name;

            OriginalILCallSequence.Call project = calls.Single(c => Targets(c, "ColliderTrackingUtilities", "ProjectColliderRaycastLocation"));
            int lastReadBefore = calls.Last(c => c.Offset < project.Offset && Targets(c, "IGraphStorage", "GetValue")).Offset;
            Assert.That(il.Count(i => i.Offset > lastReadBefore && i.Offset < project.Offset && i.Code == System.Reflection.Emit.OpCodes.Mul), Is.EqualTo(1),
                "forward speed times the step");

            List<OriginalILCallSequence.Call> hits = calls.Where(c => Targets(c, "RaycastColliderHit", "get_Hit")).ToList();
            Assert.That(hits.Count, Is.EqualTo(2));
            int afterElseHit = il.FindIndex(i => i.Offset == hits[1].Offset);
            Assert.That(new[] { il[afterElseHit + 1].Code, il[afterElseHit + 2].Code },
                Is.EqualTo(new[] { System.Reflection.Emit.OpCodes.Ldc_I4_0, System.Reflection.Emit.OpCodes.Ceq }), "the update flag is the negated hit");

            List<OriginalILCallSequence.Call> stores = calls.Where(c => Targets(c, "IGraphStorage", "SetValue")).ToList();
            Assert.That(stores.Count, Is.EqualTo(7));
            // Stores 2 and 3 are the collider-active clear and the buffer-disabled flag of the ground-hit exit.
            Assert.That(il[il.FindIndex(i => i.Offset == stores[2].Offset) - 1].Code, Is.EqualTo(System.Reflection.Emit.OpCodes.Ldc_I4_0));
            Assert.That(il[il.FindIndex(i => i.Offset == stores[3].Offset) - 1].Code, Is.EqualTo(System.Reflection.Emit.OpCodes.Ldc_I4_1));
        }

        // 0x7772D0: the half pipe entry, then the trajectory is processed, the speed along the collider plane and the
        // character's projection onto the collider are computed in that order, and the collider flags are stored.
        [Test]
        public void StateEntryProcessesTheTrajectoryBeforeProjectingTheCharacter()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_HalfPipeAir), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_HalfPipe", "DoOnEnter");
            int trajectory = IndexOf(calls, "ColliderTrackingUtilities", "ProcessHalfPipeTrajectory");
            int speed = IndexOf(calls, "ColliderTrackingUtilities", "CalculateCharacterSpeedAlongPlane");
            int project = IndexOf(calls, "ColliderTrackingUtilities", "ProjectCharacterToCollider");
            int store = IndexOf(calls, "IGraphStorage", "SetValue");
            Assert.That(new[] { baseEnter, trajectory, speed, project, store }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "IGraphStorage", "SetValue"), Is.EqualTo(4));
        }

        // 0x778850: the half pipe leave first, then the three collider keys are removed and the landing speed, landing
        // time and boost restore timer stored; the landing speed is stored (0x778b8b) before the time is read
        // (GetTotalFixedTime, 0x778bb6) for the second store.
        [Test]
        public void StateLeaveRemovesTheColliderKeysThenRecordsTheLanding()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_HalfPipeAir), "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_HalfPipe", "DoOnLeave");
            int remove = IndexOf(calls, "IGraphStorage", "RemoveValue");
            int speedStore = IndexOf(calls, "IGraphStorage", "SetValue");
            int time = IndexOf(calls, "Actor", "GetTotalFixedTime");
            int timeStore = IndexOf(calls, "IGraphStorage", "SetValue", speedStore + 1);
            Assert.That(new[] { baseLeave, remove, speedStore, time, timeStore }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "IGraphStorage", "RemoveValue"), Is.EqualTo(3));
            Assert.That(Count(calls, "IGraphStorage", "SetValue"), Is.EqualTo(3));
        }

        // 0xE731C0, 0xE73340 and 0xE73700 on the generic half pipe state: the entry stores the gravity direction, the update either
        // applies the air motion's gravity multiplier and velocity clamp (adjust, then set) or clears both half pipe flags,
        // and the leave removes the stored gravity direction after the ability base.
        // GravityNormalised and SetWorldVelocity are Actor virtuals (Character overrides the getter), so their IL call
        // targets are Actor's.
        [Test]
        public void GenericHalfPipeStateStoresAndClearsItsGravityDirection()
        {
            List<MethodBase> enter = Calls(typeof(CharacterState_HalfPipe<,>), "DoOnEnter");
            Assert.That(IndexOf(enter, "CharacterState_Ability", "DoOnEnter"), Is.LessThan(IndexOf(enter, "IGraphStorage", "SetValue")));
            Assert.That(IndexOf(enter, "Actor", "get_GravityNormalised"), Is.LessThan(IndexOf(enter, "IGraphStorage", "SetValue")));

            List<MethodBase> update = Calls(typeof(CharacterState_HalfPipe<,>), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_Ability", "DoUpdate");
            int airActive = IndexOf(update, "CharacterAbility_HalfPipe", "get_AirActive");
            int gravity = IndexOf(update, "CharacterAbility_HalfPipe", "GetGravityMultiplier");
            int adjust = IndexOf(update, "ActorState_Ability", "AdjustGravityMultiplier");
            int velocity = IndexOf(update, "Actor", "SetWorldVelocity");
            Assert.That(new[] { baseUpdate, airActive, gravity, adjust, velocity }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(update, "IGraphStorage", "SetValue"), Is.EqualTo(2), "colliderActive and transferActive");
            // The gravity direction comparison is Vector3 == (0xE734EE-0xE73529 inlines its squared-distance test), decided before the multiplier is read.
            Assert.That(Count(update, "Vector3", "op_Equality"), Is.EqualTo(1));
            Assert.That(Count(update, "Vector3", "Equals"), Is.EqualTo(0));
            Assert.That(IndexOf(update, "Vector3", "op_Equality"), Is.LessThan(gravity));

            List<MethodBase> leave = Calls(typeof(CharacterState_HalfPipe<,>), "DoOnLeave");
            Assert.That(IndexOf(leave, "CharacterState_Ability", "DoOnLeave"), Is.LessThan(IndexOf(leave, "IGraphStorage", "RemoveValue")));
        }

        // Declaration shape (not behaviour): base types, node attributes and the argument type of the state.
        [Test]
        public void DeclarationShapeHalfPipeTypesKeepTheirBasesAttributesAndMembers()
        {
            Assert.That(typeof(CharacterAbility_HalfPipeAir).BaseType, Is.EqualTo(typeof(CharacterAbility_HalfPipe<CharacterAbilityDefinition_HalfPipeAir>)));
            Assert.That(typeof(CharacterAbility_HalfPipeTransfer).BaseType, Is.EqualTo(typeof(CharacterAbility_HalfPipe<CharacterAbilityDefinition_HalfPipeTransfer>)));
            Assert.That(typeof(CharacterAbility_HalfPipeAir).GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "m_airTime", "m_projectedVelocity", "m_projectedGravity", "m_projectedRaycastTransform" }));
            Assert.That(typeof(CharacterAbility_HalfPipeTransfer).GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "m_raycastTransform", "m_direction", "m_speed", "m_gravity", "m_hitTime", "m_hitPosition", "m_hitNormal" }));
            Type generic = typeof(CharacterState_HalfPipe<,>);
            Assert.That(generic.IsAbstract, Is.True);
            Assert.That(generic.GetGenericArguments().Select(a => a.Name), Is.EqualTo(new[] { "TAbilityDefType", "TAbilityType" }));
            Assert.That(generic.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"));
            Type state = typeof(CharacterState_HalfPipeAir);
            Assert.That(state.BaseType, Is.EqualTo(typeof(CharacterState_HalfPipe<CharacterAbilityDefinition_HalfPipeAir, CharacterAbility_HalfPipeAir>)));
            Assert.That(state.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo("HalfPipeAir"));
            Assert.That(state.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"));
            Type args = state.GetNestedType("HalfPipeAirJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(args.IsNestedFamily && args.IsSerializable, Is.True);
            Assert.That(args.BaseType, Is.EqualTo(typeof(ActorState_Ability).GetNestedType("AbilityJSONCtorArgs", BindingFlags.NonPublic)));
            Assert.That(args.GetField("HalfPipeTransferAbilityType").FieldType, Is.EqualTo(typeof(ActorAbilityType)));
            Assert.That(args.IsDefined(typeof(FiniteStateMachineJsonCtorArgsAttribute), false), Is.True);
            Assert.That(FSMClassFactory.States.TryGetValue(nameof(CharacterState_HalfPipeAir), out JSONFactoryClass factory), Is.True);
            Assert.That(factory.JSONCtorArgsType, Is.EqualTo(args));
            Assert.That(FSMClassFactory.States.ContainsKey(generic.Name), Is.False, "the generic state is not a factory node");
        }
    }
}
