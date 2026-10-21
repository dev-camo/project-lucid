using System;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The crab boss abilities of Game.Runtime: BossAbility_BodySlam (020001db, 0x6F7270..0x6F7F80), BossAbility_BossCrabClawAttack
    // (020001dc, 0x6F8020..0x6F8FD0), BossAbility_DeathFlight (020001de, 0x6F99B0..0x6F9DD0), BossAbility_LaserFence (020001df,
    // 0x6F9DD0..0x6F9EE0) and BossAbility_RegrowClaw (020001ed, 0x6FE4D0..0x6FE9B0). Expected values come from the x86-64 bodies cited
    // per case. Every ability is built by its real constructor and initialised against a real Boss on an owned inactive host.
    // Not covered, because they need the BossManager's prefab pools or a hazard object: the projectile spawn that ends a body slam
    // (FireProjectileFromBoss 0x6F7B40) and a claw strike (FireProjectileFromBoss 0x6F8790), and so the strike branch of the claw
    // attack's Attacking (0x6F8527) and the wave branch of the body slam's (0x6F7931).
    public sealed class BossCrabAbilityTests
    {
        private static readonly GraphStorageKey BodySlamCount = new GraphStorageKey("bodySlamCount", 0, 0);
        private static readonly GraphStorageKey ClawAttackCount = new GraphStorageKey("clawAttackCount", 0, 0);

        // ---- BossAbility_LaserFence ----

        // 0x6F9EB0/0x6F9EC0/0x6F9ED0 return the definition's hazard depth (+0x50), height (+0x54) and Y offset (+0x58); Initialise
        // (0x6F9DE0) stores the definition through an `as` cast, so a definition of another type leaves null.
        [Test]
        public void LaserFenceReportsTheDefinitionsHazardShape()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                var definition = NewDefinition<BossAbilityDefinition_LaserFence>(owned, d =>
                {
                    d.HazardDepth = 2.5f;
                    d.HazardHeight = 7.5f;
                    d.HazardYOffset = 3.25f;
                });
                BossAbility_LaserFence ability = NewAbility<BossAbility_LaserFence>(boss, definition);
                Assert.That(ability.Definition, Is.SameAs(definition));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));
                Assert.That((ability.GetHazardDepth(), ability.GetHazardHeight(), ability.GetHazardYOffset()), Is.EqualTo((2.5f, 7.5f, 3.25f)));

                ability.Initialise(boss, NewDefinition<BossAbilityDefinition_SpinOnSpot>(owned));
                Assert.That(ability.Definition, Is.Null, "a definition of another type is dropped by the as cast");
                Assert.That(ability.DefinitionBase, Is.Null);
            });
        }

        // ---- BossAbility_RegrowClaw ----

        // 0x6FE640 adds one to the left claw's deflated count when the claw is the left one (0xBBCA983D) and to the right claw's
        // for every other attack type; the count starts from zero (AdjustValue reads without storing a default).
        [Test]
        public void RegrowClawCountsTheDeflatedAttacksPerClaw()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_RegrowClaw ability = NewAbility<BossAbility_RegrowClaw>(boss, NewDefinition<BossAbilityDefinition_RegrowClaw>(owned));
                ability.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_LeftClaw);
                ability.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_LeftClaw);
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.EqualTo(2));
                Assert.That(Has(boss, BossFSMKeys.BossCrabDeflatedRightCount), Is.False);

                ability.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_RightClaw);
                ability.AttackedWithDeflatedClaw(BossAttackTypes.None);
                ability.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_Body);
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedRightCount), Is.EqualTo(3), "anything but the left claw counts as the right one");
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.EqualTo(2));
            });
        }

        // 0x6FE780: the stored count (one when nothing was stored, and that default is stored) is compared with the definition's
        // uses to trigger regrowth with a greater-or-equal test; reaching it resets the stored count to zero.
        [Test]
        public void RegrowClawRegrowsOnceTheCountReachesTheConfiguredUsesAndRestartsTheCount()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                var definition = NewDefinition<BossAbilityDefinition_RegrowClaw>(owned, d => d.UsesToTriggerRegrowth = 2);
                BossAbility_RegrowClaw ability = NewAbility<BossAbility_RegrowClaw>(boss, definition);

                Assert.That(ability.ShouldRegrowClaw(BossAttackTypes.BossCrab_LeftClaw), Is.False, "the missing count reads as one, below two");
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.EqualTo(1), "and the default one is stored");
                ability.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_LeftClaw);
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.EqualTo(2));
                Assert.That(ability.ShouldRegrowClaw(BossAttackTypes.BossCrab_LeftClaw), Is.True, "two uses reach a threshold of two");
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.Zero, "regrowing restarts the count");
                Assert.That(Has(boss, BossFSMKeys.BossCrabDeflatedRightCount), Is.False, "the other claw is untouched");

                boss.Storage.SetValue(BossFSMKeys.BossCrabDeflatedRightCount, 5);
                Assert.That(ability.ShouldRegrowClaw(BossAttackTypes.BossCrab_RightClaw), Is.True, "above the threshold regrows too");
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedRightCount), Is.Zero);

                definition.UsesToTriggerRegrowth = 1;
                boss.Storage.RemoveValue<int>(BossFSMKeys.BossCrabDeflatedLeftCount);
                Assert.That(ability.ShouldRegrowClaw(BossAttackTypes.BossCrab_LeftClaw), Is.True, "the default count of one reaches a threshold of one");
                Assert.That(Stored<int>(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.Zero);
            });
        }

        // ---- BossAbility_DeathFlight ----

        // 0x6F9B30 flies along the current character's normalised forward direction (0x6F9BE2..0x6F9C4B), from the boss's position
        // with the height taken from the curve at the flight time; the flight starts from the boss's position (0x6F9B72).
        [Test]
        public void DeathFlightStartsAlongTheCharactersForwardDirectionAtTheCurveHeight()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, new Vector3(1f, 2f, 3f));
                MakeCurrent(manager, character, Vector3.zero);
                SetActorProperty(character, "ForwardDirection", new Vector3(0f, 0f, 2f));
                var definition = NewDefinition<BossAbilityDefinition_DeathFlight>(owned, d => d.HeightCurve = AnimationCurve.Linear(0f, 4f, 10f, 4f));
                BossAbility_DeathFlight ability = NewAbility<BossAbility_DeathFlight>(boss, definition);

                ability.Enter();
                AssertNear(ability.CurrentTargetPosition, new Vector3(1f, 4f, 4f), "the direction (0,0,1) added to (1,_,3) at curve height 4");
                Assert.That(Get<Vector3>(ability, "m_bossPosition"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Get<Vector3>(ability, "m_relativeDirection"), Is.EqualTo(Vector3.forward));

                SetActorProperty(character, "ForwardDirection", Vector3.zero);
                Vector3 position = boss.WorldPosition;
                ability.Leave();
                ability.Enter();
                Assert.That(Get<Vector3>(ability, "m_relativeDirection"), Is.EqualTo(Vector3.zero), "a zero direction stays zero (normalised below 1e-5)");
                AssertNear(ability.CurrentTargetPosition, new Vector3(position.x, 4f, position.z), "no horizontal movement");
            });
        }

        // 0x6F9D30: the flight time grows by the delta; the distance factor is delta*MovementRate + Acceleration*flightTime and
        // the target is direction*factor + the boss's current position, at the curve height of the new flight time. Leaving
        // (0x6F9D00) restarts the flight time.
        [Test]
        public void DeathFlightAdvancesByTheSpeedAndAcceleration()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, new Vector3(1f, 2f, 3f));
                MakeCurrent(manager, character, Vector3.zero);
                SetActorProperty(character, "ForwardDirection", new Vector3(0f, 0f, 2f));
                var definition = NewDefinition<BossAbilityDefinition_DeathFlight>(owned, d =>
                {
                    d.HeightCurve = AnimationCurve.Linear(0f, 0f, 10f, 10f);
                    d.MovementRateMetersPerSecond = 5f;
                    d.AccelerationMetersPerSecond = 2f;
                });
                BossAbility_DeathFlight ability = NewAbility<BossAbility_DeathFlight>(boss, definition);
                ability.Enter();

                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(1f, 0.5f, 3f + 0.5f * 5f + 2f * 0.5f), "flight time 0.5, factor 3.5");
                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(1f, 1f, 3f + 0.5f * 5f + 2f * 1f), "flight time 1, factor 4.5");

                boss.SetWorldPosition(new Vector3(-4f, 0f, 10f));
                ability.Leave();
                Assert.That(Get<float>(ability, "m_flightTime"), Is.Zero);
                ability.Enter();
                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(-4f, 0.5f, 10f + 3.5f), "restarted flight, from the new position");
            });
        }

        // ---- BossAbility_BodySlam ----

        private static BossAbility_BodySlam NewBodySlam(Owned owned, Boss boss, Action<BossAbilityDefinition_BodySlam> setup = null) =>
            NewAbility<BossAbility_BodySlam>(boss, NewDefinition<BossAbilityDefinition_BodySlam>(owned, setup));

        // 0x6F7F20..0x6F7F70: the setters store single flags, SkipAttackPreparation stores both wind-up flags through one 16-bit
        // write at +0x21, the property getters read +0x20 (start), +0x22 (wound up) and +0x23 (trigger); the group counter reset
        // is an empty body.
        [Test]
        public void BodySlamFlagsFollowTheNativeStores()
        {
            var ability = new BossAbility_BodySlam();
            Assert.That((ability.StartWindUp, ability.WoundUp, ability.IsTriggered), Is.EqualTo((false, false, false)));
            ability.SetAttackFlow(true);
            Assert.That(ability.StartWindUp, Is.True);
            Assert.That(Get<bool>(ability, "m_abilityActivated"), Is.True);
            ability.SetAttackFlow(false);
            Assert.That(Get<bool>(ability, "m_abilityActivated"), Is.False);
            Assert.That(ability.StartWindUp, Is.True, "the start request is raised either way");

            ability.SetAttackWindingUp();
            Assert.That(Get<bool>(ability, "m_isWindingUp"), Is.True);
            Assert.That(ability.WoundUp, Is.False);
            ability.SetAttackWoundUp();
            Assert.That(ability.WoundUp, Is.True);
            ability.SetAttackAnimStarted();
            Assert.That(Get<bool>(ability, "m_attackAnimStarted"), Is.True);
            Assert.That(ability.IsTriggered, Is.False, "triggering is the first Attacking call, not a setter");

            var skipped = new BossAbility_BodySlam();
            skipped.SkipAttackPreparation(null);
            Assert.That((Get<bool>(skipped, "m_isWindingUp"), skipped.WoundUp), Is.EqualTo((true, true)));
            skipped.ResetProjectileGroupCounter();
            Assert.That(skipped.StartWindUp, Is.False, "ResetProjectileGroupCounter changes nothing");
        }

        // 0x6F78B0: winding up with the wound-up flag set (the sete at 0x6F78C2) answers "not yet" only while not wound up, and
        // the start request is dropped once either flag is set; with neither flag the request stays and it answers "yes".
        [Test]
        public void BodySlamWindingUpTruthTable()
        {
            foreach ((bool windingUp, bool woundUp, bool expected, bool startKept) in new[]
            {
                (false, false, true, true), (true, false, true, false), (true, true, false, false), (false, true, false, false),
            })
            {
                var ability = new BossAbility_BodySlam();
                ability.SetAttackFlow(true);
                Set(ability, "m_isWindingUp", windingUp);
                Set(ability, "m_isWoundUp", woundUp);
                Assert.That(Call(ability, "WindingUp"), Is.EqualTo(expected), $"{windingUp}/{woundUp}");
                Assert.That(ability.StartWindUp, Is.EqualTo(startKept), $"{windingUp}/{woundUp} request");
            }
        }

        // 0x6F7C60: in range when the squared distance is not above the current far range and not below the definition's squared
        // near range (both are negated less-than tests, cmpnltps at 0x6F7DBF/0x6F7DD3); the boss stores the crab body attack type
        // (0xFA184CA1) for a hit and none for a miss, and a miss grows the far range (0x6F7EB4: extra += delta*growth, far =
        // (extra + far range)^2). The far range starts at zero.
        [Test]
        public void BodySlamCanAttackBetweenTheNearAndTheGrowingFarRange()
        {
            WithCharacter((owned, character) =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d =>
                {
                    d.TargetValidRangeFar = 10f;
                    d.TargetValidRangeNear = 2f;
                    d.TargetGrowthPerSecond = 5f;
                });
                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, 6f));

                Assert.That(ability.CanAttack(boss, character, null, 1f), Is.False, "the far range starts at zero");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Get<float>(ability, "m_extraRange"), Is.EqualTo(5f));
                Assert.That(Get<float>(ability, "m_currentFarRangeSquared"), Is.EqualTo(225f), "(5 + 10)^2");

                Assert.That(ability.CanAttack(boss, character, null, 1f), Is.True, "36 lies between 4 and 225");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_Body));
                Assert.That(Get<float>(ability, "m_extraRange"), Is.EqualTo(5f), "a hit does not grow the range");

                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, 1f));
                Assert.That(ability.CanAttack(boss, character, null, 2f), Is.False, "1 is below the squared near range 4");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Get<float>(ability, "m_extraRange"), Is.EqualTo(15f), "2*5 + 5");
                Assert.That(Get<float>(ability, "m_currentFarRangeSquared"), Is.EqualTo(625f), "(15 + 10)^2");

                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, 2f));
                Assert.That(ability.CanAttack(boss, character, null, 0f), Is.True, "a squared distance equal to the near range is in range");
                Set(ability, "m_currentFarRangeSquared", 4f);
                Assert.That(ability.CanAttack(boss, character, null, 0f), Is.True, "and one equal to the far range");
                Set(ability, "m_currentFarRangeSquared", 3.9f);
                Assert.That(ability.CanAttack(boss, character, null, 0f), Is.False, "just past the far range");
            });
        }

        // The same tests are negated less-than comparisons, so a NaN squared distance (cmpnltps is true for unordered operands)
        // counts as in range; a plain >= / <= formulation would reject it.
        [Test]
        public void BodySlamCanAttackTreatsANaNDistanceAsInRange()
        {
            WithCharacter((owned, character) =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d => d.TargetValidRangeNear = 2f);
                SetActorProperty(character, "WorldPosition", new Vector3(float.NaN, 0f, 0f));
                Assert.That(ability.CanAttack(boss, character, null, 1f), Is.True);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_Body));
            });
        }

        // 0x6F78E0: the first call raises the trigger flag; until the animation has started it answers "still attacking". Once
        // started, the target glides from the pre-jump position to the current triangle point (x and z by the traversal curve
        // over the traversal time, clamped to 0..1; y stays) while the timer runs, and at the animation length the
        // animation flag and timer reset and the answer is false (0x6F79FE). The projectile wave (0x6F7931) is skipped here.
        [Test]
        public void BodySlamAttackingGlidesToTheTrianglePointThenEnds()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(2f, 5f, 4f));
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d =>
                {
                    d.AttackAnimationLength = 1f;
                    d.TriggerWaveTime = 0.8f;
                    d.TraversalTime = 0.5f;
                    d.TraversalCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                });
                Set(ability, "m_preJumpPosition", new Vector3(2f, 5f, 4f));
                ((Vector3[])Get<object>(ability, "m_coordinateArray"))[0] = new Vector3(10f, 99f, 20f);
                Set(ability, "m_waveSpawned", true);

                Assert.That(Call(ability, "Attacking", 0.25f), Is.EqualTo(true));
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.Zero, "the animation has not started");

                ability.SetAttackAnimStarted();
                Assert.That(Call(ability, "Attacking", 0.25f), Is.EqualTo(true));
                AssertNear(ability.CurrentTargetPosition, new Vector3(2f, 5f, 4f), "traversal 0 at timer 0");
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(0.25f));

                Assert.That(Call(ability, "Attacking", 0.25f), Is.EqualTo(true));
                AssertNear(ability.CurrentTargetPosition, new Vector3(6f, 5f, 12f), "traversal 0.25/0.5 = 0.5 halfway to (10, _, 20)");
                Call(ability, "Attacking", 0.25f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(10f, 5f, 20f), "traversal 1 at timer 0.5");
                Call(ability, "Attacking", 0.25f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(10f, 5f, 20f), "the traversal is clamped to one");

                Assert.That(Call(ability, "Attacking", 0.25f), Is.EqualTo(false), "the timer reached the animation length");
                Assert.That(Get<bool>(ability, "m_attackAnimStarted"), Is.False);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.Zero);
            });
        }

        // The triangle point used is chosen by the attack index modulo three (0x6F7966).
        [Test]
        public void BodySlamAttackingUsesTheTrianglePointOfTheAttackIndex()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d =>
                {
                    d.TraversalTime = 1f;
                    d.AttackAnimationLength = 10f;
                    d.TraversalCurve = AnimationCurve.Constant(0f, 1f, 1f);
                });
                var points = (Vector3[])Get<object>(ability, "m_coordinateArray");
                points[0] = new Vector3(1f, 0f, 2f);
                points[1] = new Vector3(3f, 0f, 4f);
                points[2] = new Vector3(5f, 0f, 6f);
                Set(ability, "m_waveSpawned", true);
                ability.SetAttackAnimStarted();
                Call(ability, "Attacking", 0f);
                foreach ((int index, Vector3 expected) in new[] { (0, points[0]), (1, points[1]), (2, points[2]), (3, points[0]), (7, points[1]) })
                {
                    typeof(BossAbility_BodySlam).GetProperty("AttackIndex").GetSetMethod(true).Invoke(ability, new object[] { index });
                    Call(ability, "Attacking", 0f);
                    AssertNear(ability.CurrentTargetPosition, new Vector3(expected.x, 0f, expected.z), "attack index " + index);
                }
            });
        }

        // DoOnUpdate (0x6F7850): nothing happens until the ability is activated and wound up; then Attacking runs and its end
        // finishes the ability. DoOnLeave (0x6F7810) clears the activation, the animation timer, the extra range and the flags.
        [Test]
        public void BodySlamUpdateFinishesTheAbilityWhenTheSlamEndsAndLeaveResets()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d => d.AttackAnimationLength = 1f);
                ability.Enter();
                Set(ability, "m_waveSpawned", true);
                Set(ability, "m_canTriggerAttackAnim", true);
                Set(ability, "m_attackAnimStarted", true);
                Set(ability, "m_attackAnimationTimer", 1f);
                Set(ability, "m_extraRange", 3f);

                ability.Update(0.1f);
                Assert.That(ability.AbilityFinished, Is.False, "not activated yet");
                ability.SetAttackFlow(true);
                ability.Update(0.1f);
                Assert.That(ability.AbilityFinished, Is.False, "activated but not wound up");
                ability.SetAttackWoundUp();
                ability.Update(0.1f);
                Assert.That(ability.AbilityFinished, Is.True, "the animation length was reached");

                ability.Leave();
                Assert.That(ability.AbilityFinished, Is.False);
                Assert.That((Get<bool>(ability, "m_abilityActivated"), ability.StartWindUp, Get<bool>(ability, "m_attackAnimStarted"), Get<bool>(ability, "m_waveSpawned")),
                    Is.EqualTo((false, false, false, false)));
                Assert.That((Get<float>(ability, "m_attackAnimationTimer"), Get<float>(ability, "m_extraRange")), Is.EqualTo((0f, 0f)));
                // 0x6F7836 (dword at 0x20) and 0x6F783D (word at 0x24) clear all six flags.
                Assert.That((Get<bool>(ability, "m_isWindingUp"), Get<bool>(ability, "m_isWoundUp"), Get<bool>(ability, "m_canTriggerAttackAnim")),
                    Is.EqualTo((false, false, false)), "leaving clears the wind-up and trigger flags too");
            });
        }

        // 0x6F7440 starts at the boss's position and 0x6F7490 lays out the triangle: for the first jump of a cycle (stored count
        // zero) the first point is on the circle of the jump distance round the origin at the stored angle change, which defaults
        // to the boss's bearing atan2(x, z) and is stored; the other points are the angle of jumps either side (0x6F76DC..0x6F7794).
        // The angle change then grows by a random amount between the minimum and maximum rotation (AdjustValue at 0x6F77FD).
        [Test]
        public void BodySlamEnterLaysOutTheTriangleAroundTheBossBearing()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 7f, 4f));
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d =>
                {
                    d.TriangleJumpDistance = 30f;
                    d.AngleOfJumps = 36f;
                    d.MinTriangleRotationDegreesPerCycle = 30f;
                    d.MaxTriangleRotationDegreesPerCycle = 90f;
                });
                ability.Enter();

                Assert.That(ability.AttackIndex, Is.Zero);
                Assert.That(Get<Vector3>(ability, "m_preJumpPosition"), Is.EqualTo(new Vector3(3f, 7f, 4f)));
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(3f, 7f, 4f)));
                double bearing = Math.Atan2(3.0, 4.0);
                double jump = 36.0 * Math.PI / 180.0;
                var points = (Vector3[])Get<object>(ability, "m_coordinateArray");
                foreach ((int index, double angle) in new[] { (0, bearing), (1, bearing + jump), (2, bearing - jump) })
                {
                    Assert.That(points[index].x, Is.EqualTo((float)(30.0 * Math.Cos(angle))).Within(1e-3f), "point " + index + " x");
                    Assert.That(points[index].y, Is.EqualTo(7f), "point " + index + " y is the boss height");
                    Assert.That(points[index].z, Is.EqualTo((float)(30.0 * Math.Sin(angle))).Within(1e-3f), "point " + index + " z");
                }
                float angleChange = Stored<float>(boss, BossFSMKeys.BossAttackAngleChange);
                Assert.That(angleChange, Is.GreaterThanOrEqualTo((float)(bearing + 30.0 * Math.PI / 180.0) - 1e-4f));
                Assert.That(angleChange, Is.LessThanOrEqualTo((float)(bearing + 90.0 * Math.PI / 180.0) + 1e-4f));
            });
        }

        // 0x6F7660 reads the angle change from storage (key at 0x6F75C7) and only defaults to the bearing when nothing is stored,
        // so a stored angle change (the per-cycle rotation) replaces the bearing as the first point's angle.
        [Test]
        public void BodySlamEnterStartsTheTriangleAtTheStoredAngleChange()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 7f, 4f));
                boss.Storage.SetValue(BossFSMKeys.BossAttackAngleChange, 1f);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss, d =>
                {
                    d.TriangleJumpDistance = 30f;
                    d.AngleOfJumps = 36f;
                    d.MinTriangleRotationDegreesPerCycle = 30f;
                    d.MaxTriangleRotationDegreesPerCycle = 90f;
                });
                ability.Enter();

                double jump = 36.0 * Math.PI / 180.0;
                var points = (Vector3[])Get<object>(ability, "m_coordinateArray");
                foreach ((int index, double angle) in new[] { (0, 1.0), (1, 1.0 + jump), (2, 1.0 - jump) })
                {
                    Assert.That(points[index].x, Is.EqualTo((float)(30.0 * Math.Cos(angle))).Within(1e-3f), "point " + index + " x");
                    Assert.That(points[index].y, Is.EqualTo(7f), "point " + index + " y is the boss height");
                    Assert.That(points[index].z, Is.EqualTo((float)(30.0 * Math.Sin(angle))).Within(1e-3f), "point " + index + " z");
                }
                float angleChange = Stored<float>(boss, BossFSMKeys.BossAttackAngleChange);
                Assert.That(angleChange, Is.GreaterThanOrEqualTo(1f + (float)(Math.PI / 6) - 1e-4f));
                Assert.That(angleChange, Is.LessThanOrEqualTo(1f + (float)(Math.PI / 2) + 1e-4f));
            });
        }

        // A stored count other than zero (0x6F7574) leaves the layout and the angle change alone and is the attack index.
        [Test]
        public void BodySlamEnterKeepsTheTriangleAfterTheFirstJump()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 7f, 4f));
                boss.Storage.SetValue(BodySlamCount, 2);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss);
                ability.Enter();
                Assert.That(ability.AttackIndex, Is.EqualTo(2));
                Assert.That(((Vector3[])Get<object>(ability, "m_coordinateArray")), Is.All.EqualTo(Vector3.zero));
                Assert.That(Has(boss, BossFSMKeys.BossAttackAngleChange), Is.False);

                boss.Storage.RemoveValue<int>(BodySlamCount);
                var fresh = NewBodySlam(owned, boss);
                fresh.Enter();
                Assert.That(fresh.AttackIndex, Is.Zero);
                Assert.That(Stored<int>(boss, BodySlamCount), Is.Zero, "the missing count is stored as zero");
            });
        }

        // 0x6F7A20 publishes the strike effect trigger (0x337EF8B7) and fires the boss's effects.
        [Test]
        public void BodySlamStrikePublishesTheAttackStrikeEffect()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BodySlam ability = NewBodySlam(owned, boss);
                Call(ability, "TriggerAttackStrikeEffects");
                Assert.That(Stored<BossEffectTriggerTypes>(boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
            });
        }

        // ---- BossAbility_BossCrabClawAttack ----

        private const string Choice = "Lucid claw choice";
        private const string UseLeft = "Lucid claw left";
        private const string UseRight = "Lucid claw right";

        private sealed class ClawScene
        {
            public Boss Boss;
            public Animator Animator;
            public ActorAnimationDefinition Animation;
            public BossAbility_BossCrabClawAttack Ability;
            public BossAbilityDefinition_BossCrabClawAttack Definition;
        }

        // A boss with both claw points (no hazards), an animator and the animation definition for the three claw parameters.
        private static ClawScene NewClawScene(Owned owned, Action<BossAbilityDefinition_BossCrabClawAttack> setup = null)
        {
            var scene = new ClawScene { Boss = NewBoss(owned, Vector3.zero) };
            AddBodyPart(scene.Boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, false);
            AddBodyPart(scene.Boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, false);
            scene.Animator = AttachBossAnimator(owned, scene.Boss, Choice, UseLeft, UseRight);
            scene.Animation = NewAnimationDefinition(owned,
                (ActorAnimationType.Boss_Telegraph_Choice, Choice), (ActorAnimationType.Boss_Use_Left, UseLeft), (ActorAnimationType.Boss_Use_Right, UseRight));
            scene.Definition = NewDefinition<BossAbilityDefinition_BossCrabClawAttack>(owned, setup);
            scene.Ability = NewAbility<BossAbility_BossCrabClawAttack>(scene.Boss, scene.Definition);
            return scene;
        }

        private static (bool choice, bool left, bool right) Claws(ClawScene scene) =>
            (scene.Animator.GetBool(Choice), scene.Animator.GetBool(UseLeft), scene.Animator.GetBool(UseRight));

        // 0x6F8954..0x6F8979 and 0x6F8944: single flag stores; the flow also raises the start request; the group counter reset
        // (0x6F8C65) is empty and AbilityActivated (0x6F8C74) reads +0x20.
        [Test]
        public void ClawAttackFlagsFollowTheNativeStores()
        {
            var ability = new BossAbility_BossCrabClawAttack();
            Assert.That((ability.IsTriggered, ability.StartWindUp, ability.WoundUp), Is.EqualTo((false, false, false)));
            ability.SetAttackFlow(true);
            Assert.That(ability.StartWindUp, Is.True);
            Assert.That(Call(ability, "AbilityActivated"), Is.EqualTo(true));
            ability.SetAttackFlow(false);
            Assert.That(Call(ability, "AbilityActivated"), Is.EqualTo(false));
            ability.SetAttackWindingUp();
            ability.SetAttackWoundUp();
            ability.SetAttackAnimStarted();
            Assert.That((Get<bool>(ability, "m_isWindingUp"), ability.WoundUp, Get<bool>(ability, "m_attackAnimStarted")), Is.EqualTo((true, true, true)));
            Assert.That(ability.IsTriggered, Is.False);
            ability.ResetProjectileGroupCounter();
            Assert.That(ability.IsTriggered, Is.False);
        }

        // 0x6F8384: still winding up (true) until the wind-up is wound up; the request is dropped once either flag is set; for the
        // attack whose index equals the long wind-up comparison the answer stays true until the extra wind-up seconds have
        // passed (timer += delta first, then seconds > timer at 0x6F83C9).
        [Test]
        public void ClawAttackWindingUpWaitsOutTheExtraSecondsOfTheLongAttack()
        {
            var definition = ScriptableObject.CreateInstance<BossAbilityDefinition_BossCrabClawAttack>();
            try
            {
                definition.LongWindUpAttackComparison = 2;
                definition.ExtraWindUpSeconds = 1f;
                foreach ((bool windingUp, bool woundUp, int index, bool expected, bool startKept) in new[]
                {
                    (false, false, 2, true, true), (true, false, 2, true, false), (false, true, 1, false, false), (true, true, 1, false, false),
                })
                {
                    var ability = new BossAbility_BossCrabClawAttack { Definition = definition };
                    ability.SetAttackFlow(true);
                    Set(ability, "m_isWindingUp", windingUp);
                    Set(ability, "m_isWoundUp", woundUp);
                    Set(ability, "m_thisAttackIndex", index);
                    Assert.That(Call(ability, "WindingUp", 0.25f), Is.EqualTo(expected), $"{windingUp}/{woundUp}/{index}");
                    Assert.That(ability.StartWindUp, Is.EqualTo(startKept), $"{windingUp}/{woundUp}/{index} request");
                    Assert.That(Get<float>(ability, "m_extraWindUpTimer"), Is.Zero, $"{windingUp}/{woundUp}/{index} timer");
                }

                var longAttack = new BossAbility_BossCrabClawAttack { Definition = definition };
                Set(longAttack, "m_isWoundUp", true);
                Set(longAttack, "m_thisAttackIndex", 2);
                Assert.That(new[] { 0.25f, 0.25f, 0.25f, 0.25f, 0.25f }.Select(delta => (bool)Call(longAttack, "WindingUp", delta)).ToArray(),
                    Is.EqualTo(new[] { true, true, true, false, false }), "0.25, 0.5, 0.75, then 1.0 which is not above 1.0");
                Assert.That(Get<float>(longAttack, "m_extraWindUpTimer"), Is.EqualTo(1.25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        // 0x6F82F6 runs the strike only after the wind-up has been wound up (and a started long wind-up has waited out its extra
        // seconds); the strike's end sets AbilityFinished (0x6F8369). 0x6F8358..0x6F8365 call Attacking with the delta.
        [Test]
        public void ClawAttackUpdateRunsTheStrikeOnlyWhenWoundUp()
        {
            WithManager((owned, manager, character) =>
            {
                ClawScene scene = NewClawScene(owned, d =>
                {
                    d.PostWindUpAttackTimeOut = 2f;
                    d.TargetValidRangeNear = 3f;
                });
                BossAbility_BossCrabClawAttack ability = scene.Ability;
                ability.Enter();
                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.Zero, "not activated: nothing runs");
                ability.SetAttackFlow(true);
                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.Zero, "activated but not wound up");
                ability.SetAttackWoundUp();
                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.EqualTo(0.5f), "wound up: Attacking waits out the post wind-up timeout");
                Assert.That(ability.AbilityFinished, Is.False);
            });
        }

        // 0x6F83E0: the post wind-up timeout runs (timer += delta, answer true) while there is no valid character or the character
        // stands inside the near range (0x6F84B1/0x6F84C0); past the timeout, or with the character farther away, the first call
        // raises the trigger flag and answers true until the strike animation has started, then the animation timer counts
        // up to the animation length (0x6F8514).
        [Test]
        public void ClawAttackAttackingWaitsForTheCharacterThenTheAnimation()
        {
            WithManager((owned, manager, character) =>
            {
                ClawScene scene = NewClawScene(owned, d =>
                {
                    d.PostWindUpAttackTimeOut = 2f;
                    d.AttackAnimationLength = 1f;
                    d.TargetValidRangeNear = 3f;
                });
                BossAbility_BossCrabClawAttack ability = scene.Ability;

                Assert.That(Call(ability, "Attacking", 0.5f), Is.EqualTo(true), "no valid character: the timeout runs");
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.EqualTo(0.5f));
                Assert.That(ability.IsTriggered, Is.False);

                MakeCurrent(manager, character, new Vector3(0f, 0f, 2f));
                Assert.That(Call(ability, "Attacking", 0.5f), Is.EqualTo(true), "inside the near range (4 < 9): the timeout runs");
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.EqualTo(1f));

                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, 5f));
                Assert.That(Call(ability, "Attacking", 0.5f), Is.EqualTo(true), "outside the near range: past the timeout, the trigger is raised");
                Assert.That(Get<float>(ability, "m_attackTimeoutTimer"), Is.EqualTo(1f), "the timeout stops");
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.Zero, "the animation has not started");

                ability.SetAttackAnimStarted();
                Call(ability, "Attacking", 0.25f);
                Call(ability, "Attacking", 0.5f);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(0.75f));

                Set(ability, "m_attackTimeoutTimer", 2f);
                MakeCurrent(manager, character, new Vector3(0f, 0f, 1f));
                Call(ability, "Attacking", 0.25f);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(1f), "an elapsed timeout lets the animation run even inside the near range");
            });
        }

        // 0x6F8C8D: a target is valid between the squared near and the current squared far range within the valid attack angle, or
        // inside the telegraph range; an invalid one stores no claw, clears the claw animation flags and grows the ranges (0x6F8554:
        // extra += delta*growth, far = (extra + far)^2, telegraph = (extra + telegraph range)^2). The valid claw is the side the
        // character stands on: the left claw for a negative signed angle (0 > angle, 0x6F8F36) and the right one otherwise.
        [Test]
        public void ClawAttackCanAttackPicksTheClawOnTheSideOfTheCharacter()
        {
            WithManager((owned, manager, character) =>
            {
                ClawScene scene = NewClawScene(owned, d =>
                {
                    d.TargetValidRangeNear = 1f;
                    d.TargetValidRangeFar = 8f;
                    d.TelegraphClawChoiceRange = 6f;
                    d.TargetGrowthPerSecond = 10f;
                    d.ValidAttackAngle = 45;
                });
                Boss boss = scene.Boss;
                MakeCurrent(manager, character, new Vector3(0f, 0f, 5f));
                SetActorProperty(character, "ForwardDirection", new Vector3(0f, 0f, -1f));

                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 1f), Is.False, "both ranges start at zero");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Claws(scene), Is.EqualTo((false, false, false)));
                Assert.That(Get<float>(scene.Ability, "m_extraRange"), Is.EqualTo(10f));
                Assert.That(Get<float>(scene.Ability, "m_currentFarRangeSquared"), Is.EqualTo(324f), "(10 + 8)^2");
                Assert.That(Get<float>(scene.Ability, "m_currentTelegraphRangeSquared"), Is.EqualTo(256f), "(10 + 6)^2");

                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 0f), Is.True, "straight ahead of the character: angle 0 within 45");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_RightClaw), "0 > 0 is false: the right claw");
                Assert.That(Claws(scene), Is.EqualTo((true, false, true)));

                boss.SetWorldPosition(new Vector3(3f, 0f, 0f));
                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 0f), Is.True, "the angle is about -31 degrees");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_LeftClaw));
                Assert.That(Claws(scene), Is.EqualTo((true, true, false)));

                // 0x6F8D4B..0x6F8D60: inside the near and far ranges (289 of 1..324) but outside the telegraph range (256) and at +90
                // degrees (beyond the 45 valid attack angle), so only the angle gate rejects the target.
                boss.SetWorldPosition(new Vector3(0f, 0f, -12f));
                SetActorProperty(character, "ForwardDirection", new Vector3(1f, 0f, 0f));
                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 0f), Is.False, "inside the range but outside the valid attack angle");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Claws(scene), Is.EqualTo((false, false, false)));
                Assert.That(Get<float>(scene.Ability, "m_extraRange"), Is.EqualTo(10f), "no time passed: the ranges stay as they were");

                boss.SetWorldPosition(new Vector3(0f, 0f, -5f));
                SetActorProperty(character, "ForwardDirection", new Vector3(1f, 0f, 0f));
                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 0f), Is.True, "the telegraph range accepts any angle: (0,0,-10) is at 100 < 256");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_RightClaw), "the signed angle from (1,0,0) to (0,0,-10) about up is +90 degrees");

                boss.SetWorldPosition(new Vector3(0f, 0f, 40f));
                Assert.That(scene.Ability.CanAttack(boss, character, scene.Animation, 0.5f), Is.False, "far beyond both ranges (1225)");
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None), "the choice is cleared");
                Assert.That(Claws(scene), Is.EqualTo((false, false, false)));
                Assert.That(Get<float>(scene.Ability, "m_extraRange"), Is.EqualTo(15f), "0.5*10 + 10");
            });
        }

        // With a destroyed claw (the left point checked first, then the right) the choice alternates against the stored attack: the
        // right claw after a left attack and the left claw otherwise (0x6F8F00..0x6F8F10).
        [Test]
        public void ClawAttackCanAttackAlternatesTheClawWhenAClawIsDestroyed()
        {
            WithManager((owned, manager, character) =>
            {
                foreach ((bool leftDestroyed, bool rightDestroyed, BossAttackTypes stored, BossAttackTypes expected) in new[]
                {
                    (true, false, BossAttackTypes.None, BossAttackTypes.BossCrab_LeftClaw),
                    (true, false, BossAttackTypes.BossCrab_LeftClaw, BossAttackTypes.BossCrab_RightClaw),
                    (false, true, BossAttackTypes.BossCrab_RightClaw, BossAttackTypes.BossCrab_LeftClaw),
                    (false, true, BossAttackTypes.BossCrab_LeftClaw, BossAttackTypes.BossCrab_RightClaw),
                    (true, true, BossAttackTypes.BossCrab_Body, BossAttackTypes.BossCrab_LeftClaw),
                })
                {
                    Boss boss = NewBoss(owned, Vector3.zero);
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, leftDestroyed);
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, rightDestroyed);
                    Animator animator = AttachBossAnimator(owned, boss, Choice, UseLeft, UseRight);
                    ActorAnimationDefinition animation = NewAnimationDefinition(owned,
                        (ActorAnimationType.Boss_Telegraph_Choice, Choice), (ActorAnimationType.Boss_Use_Left, UseLeft), (ActorAnimationType.Boss_Use_Right, UseRight));
                    BossAbility_BossCrabClawAttack ability = NewAbility<BossAbility_BossCrabClawAttack>(boss, NewDefinition<BossAbilityDefinition_BossCrabClawAttack>(owned,
                        d => d.TargetValidRangeNear = 1f));
                    Set(ability, "m_currentFarRangeSquared", 100f);
                    MakeCurrent(manager, character, new Vector3(0f, 0f, 5f));
                    SetActorProperty(character, "ForwardDirection", new Vector3(0f, 0f, -1f));
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, stored);

                    Assert.That(ability.CanAttack(boss, character, animation, 0f), Is.True);
                    string label = $"{leftDestroyed}/{rightDestroyed}/{stored}";
                    Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(expected), label);
                    Assert.That((animator.GetBool(UseLeft), animator.GetBool(UseRight)),
                        Is.EqualTo((expected == BossAttackTypes.BossCrab_LeftClaw, expected == BossAttackTypes.BossCrab_RightClaw)), label);
                }
            });
        }

        // 0x6F8980 skips the preparation and applies the stored attack override (read with a stored default of none, 0x6F89F1..);
        // 0x6F8AA0 stores the choice and sets the telegraph flag for either claw and the left/right flags for the matching one.
        [Test]
        public void ClawAttackSkipPreparationAppliesTheStoredOverride()
        {
            Owned.Run(owned =>
            {
                ClawScene scene = NewClawScene(owned);
                Boss boss = scene.Boss;
                scene.Ability.SkipAttackPreparation(scene.Animation);
                Assert.That((Get<bool>(scene.Ability, "m_isWindingUp"), scene.Ability.WoundUp), Is.EqualTo((true, true)));
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Has(boss, BossFSMKeys.BossAttackTypeOverride), Is.True, "the missing override is stored as its default");
                Assert.That(Claws(scene), Is.EqualTo((false, false, false)));

                boss.Storage.SetValue(BossFSMKeys.BossAttackTypeOverride, BossAttackTypes.BossCrab_LeftClaw);
                scene.Ability.SkipAttackPreparation(scene.Animation);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_LeftClaw));
                Assert.That(Claws(scene), Is.EqualTo((true, true, false)));

                boss.Storage.SetValue(BossFSMKeys.BossAttackTypeOverride, BossAttackTypes.BossCrab_RightClaw);
                scene.Ability.SkipAttackPreparation(scene.Animation);
                Assert.That(Claws(scene), Is.EqualTo((true, false, true)));

                boss.Storage.SetValue(BossFSMKeys.BossAttackTypeOverride, BossAttackTypes.BossCrab_Body);
                scene.Ability.SkipAttackPreparation(scene.Animation);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_Body), "any other type is stored as it is");
                Assert.That(Claws(scene), Is.EqualTo((false, false, false)));
            });
        }

        // 0x6F81F2: entering reads the stored claw attack count as the attack index (default zero, stored); 0x6F82B6: leaving
        // resets the timers, the extra range, the activation flags and the wind-up flags.
        [Test]
        public void ClawAttackEnterReadsTheAttackIndexAndLeaveResets()
        {
            Owned.Run(owned =>
            {
                ClawScene scene = NewClawScene(owned);
                scene.Boss.Storage.SetValue(ClawAttackCount, 3);
                BossAbility_BossCrabClawAttack ability = scene.Ability;
                ability.Enter();
                Assert.That(Get<int>(ability, "m_thisAttackIndex"), Is.EqualTo(3));

                ability.SetAttackFlow(true);
                ability.SetAttackWindingUp();
                ability.SetAttackWoundUp();
                ability.SetAttackAnimStarted();
                Set(ability, "m_canTriggerAttackAnim", true);
                Set(ability, "m_attackTimeoutTimer", 1f);
                Set(ability, "m_attackAnimationTimer", 2f);
                Set(ability, "m_extraWindUpTimer", 3f);
                Set(ability, "m_extraRange", 4f);
                ability.Leave();
                Assert.That((Get<bool>(ability, "m_abilityActivated"), ability.IsTriggered, Get<bool>(ability, "m_attackAnimStarted"), ability.StartWindUp,
                    Get<bool>(ability, "m_isWindingUp"), ability.WoundUp), Is.EqualTo((false, false, false, false, false, false)));
                Assert.That((Get<float>(ability, "m_attackTimeoutTimer"), Get<float>(ability, "m_attackAnimationTimer"), Get<float>(ability, "m_extraWindUpTimer"),
                    Get<float>(ability, "m_extraRange")), Is.EqualTo((0f, 0f, 0f, 0f)));

                scene.Boss.Storage.RemoveValue<int>(ClawAttackCount);
                ability.Enter();
                Assert.That(Get<int>(ability, "m_thisAttackIndex"), Is.Zero);
                Assert.That(Stored<int>(scene.Boss, ClawAttackCount), Is.Zero, "the missing count is stored as zero");
            });
        }

        // 0x6F8670 publishes the strike effect trigger (0x337EF8B7).
        [Test]
        public void ClawAttackStrikePublishesTheAttackStrikeEffect()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ClawScene scene = NewClawScene(owned);
                scene.Ability.TriggerAttackStrikeEffects();
                Assert.That(Stored<BossEffectTriggerTypes>(scene.Boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
            });
        }

        // CharacterInAttackRange (0x6F8580, unused by the retail code) is true at or beyond the squared near range and for a
        // NaN distance (setbe after ucomiss), and false inside it or without a current character.
        [Test]
        public void ClawAttackCharacterInAttackRangeIsTrueAtOrBeyondTheNearRange()
        {
            WithManager((owned, manager, character) =>
            {
                ClawScene scene = NewClawScene(owned, d => d.TargetValidRangeNear = 3f);
                Assert.That(Call(scene.Ability, "CharacterInAttackRange"), Is.EqualTo(false), "no current character");
                MakeCurrent(manager, character, new Vector3(0f, 0f, 2f));
                Assert.That(Call(scene.Ability, "CharacterInAttackRange"), Is.EqualTo(false), "4 < 9");
                SetActorProperty(character, "WorldPosition", new Vector3(0f, 0f, 3f));
                Assert.That(Call(scene.Ability, "CharacterInAttackRange"), Is.EqualTo(true), "9 == 9");
                SetActorProperty(character, "WorldPosition", new Vector3(float.NaN, 0f, 0f));
                Assert.That(Call(scene.Ability, "CharacterInAttackRange"), Is.EqualTo(true), "unordered counts as not below");
            });
        }
    }
}
