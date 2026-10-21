using System;
using System.Collections.Generic;
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
    // The boss movement abilities of Game.Runtime: BossAbility_MoveTowardsTarget (020001e7, 0x6FB720..0x6FC8E0) with its derived
    // MoveToArenaEdge (020001e3), MoveToBigCircleWithCounterSpin (020001e4), MoveToTargetPoint (020001e5) and MoveToTriangleFormation
    // (020001e6); MoveBigCircleWithCounterSpin (020001e0, 0x6F9EF0), MoveBigCircleWithDecayingRadius (020001e1, 0x6FA400), MoveRandom
    // (020001e2, 0x6FA890), TurnTowardTarget (020001f2, 0x700640), SpinOnSpot (020001f0, 0x6FF5E0), StayAheadOfPlayer (020001f1,
    // 0x6FF8E0), Scuttle (020001ef, 0x6FEFE0) and RetreatingJump (020001ee, 0x6FE9B0). Expected values come from the x86-64 bodies
    // cited per case; circle points are written out with double-precision trigonometry rather than with the extension methods.
    // The abilities derived from MoveTowardsTarget cache a typed FourmenManager reference when they are built, so each case registers
    // its FourmenManager first. The BUILD_DEVELOPMENT debug helpers (SetDebugActive, SetDebugPositions) are compiled out of retail
    // and have no calls in the restored bodies, so they are covered by the declaration rows only.
    public sealed class BossMovementAbilityTests
    {
        // centre + (cos, sin) * distance on X and Z, at a fixed height: PointOnCircleWithFixedY (0x1AB8620).
        private static Vector3 Circle(Vector3 centre, double angle, double distance, float y) =>
            new Vector3((float)(centre.x + Math.Cos(angle) * distance), y, (float)(centre.z + Math.Sin(angle) * distance));

        // centre + (sin, cos) * distance: PointOnCircleWithFixedYLHS (0x1AB8680).
        private static Vector3 CircleLhs(Vector3 centre, double angle, double distance, float y) =>
            new Vector3((float)(centre.x + Math.Sin(angle) * distance), y, (float)(centre.z + Math.Cos(angle) * distance));

        // The ability's own update body, for cases that do not enter the ability (Update runs only once it is enabled).
        private static void Tick(ActorAbility ability, float deltaTime) => Call(ability, "DoOnUpdate", deltaTime);

        // ---- BossAbility_MoveTowardsTarget ----

        // 0x6FC5F0..0x6FC650: the base movement exposes no turning (0, 0, 0, false, false), and its DefinitionBase is a get-only
        // auto-property nothing assigns (0x6FC654 reads +0x60), so it stays null even once initialised.
        [Test]
        public void MoveTowardsTargetExposesNoTurningAndNoDefinitionBase()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, Vector3.zero);
                var definition = NewDefinition<BossAbilityDefinition_MoveTowardsTarget>(owned);
                BossAbility_MoveTowardsTarget ability = NewAbility<BossAbility_MoveTowardsTarget>(rig.Bosses[0], definition);
                Assert.That(ability.Definition, Is.SameAs(definition));
                Assert.That(ability.DefinitionBase, Is.Null);
                Assert.That((ability.MovementAnimationAngleMultiplier, ability.FaceAngleTolerance, ability.TurnSpeed, ability.IsMomentum, ability.TurnToCharacter),
                    Is.EqualTo((0f, 0f, 0f, false, false)));
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Get<Vector3>(ability, "ArenaCentre"), Is.EqualTo(Vector3.up), "the arena centre is Vector3.up (0x6FBA4D)");
            });
        }

        // 0x6FB950: entering starts the target at the boss's position and lists the boss once among the Fourmen manager's bosses in
        // motion (AddUnique); 0x6FC660: the speed is the acceleration curve at timer/AccelerationTime times the top speed, the target
        // moves from the boss towards the end position by speed*delta (Vector3.MoveTowards), and the timer then grows. Within
        // sqrt(1e-10) of the end position the target snaps to it and the movement is reached.
        [Test]
        public void MoveTowardsTargetGlidesAlongTheAccelerationCurveAndReachesItsTarget()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, Vector3.zero);
                Boss boss = rig.Bosses[0];
                var definition = NewDefinition<BossAbilityDefinition_MoveTowardsTarget>(owned, d =>
                {
                    d.AccelerationTime = 1f;
                    d.TopSpeed = 10f;
                    d.AccelerationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                });
                BossAbility_MoveTowardsTarget ability = NewAbility<BossAbility_MoveTowardsTarget>(boss, definition);
                // Reflection: the base ability has no target setter (only the derived abilities assign it), and the base is the ability under test.
                Set(ability, "m_targetPosition", new Vector3(10f, 0f, 0f));

                ability.Enter();
                ability.Enter();
                Assert.That(rig.Fourmen.BossesInMotion, Is.EqualTo(new[] { boss }), "listed once");
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero));

                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, Vector3.zero, "speed factor 0 at timer 0");
                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(2.5f, 0f, 0f), "factor 0.5 * 10 * 0.5s from the boss at the origin");
                Assert.That(Get<float>(ability, "m_movementTimer"), Is.EqualTo(1f));
                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(5f, 0f, 0f), "factor 1 * 10 * 0.5s");
                ability.Update(2f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(10f, 0f, 0f), "a step longer than the distance stops at the target");
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.False, "the boss itself has not arrived");

                boss.SetWorldPosition(new Vector3(10f - 2e-5f, 0f, 0f));
                ability.Update(0f);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.False, "4e-10 is not below 1e-10");
                boss.SetWorldPosition(new Vector3(10f - 5e-6f, 0f, 0f));
                ability.Update(0f);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.True, "2.5e-11 is below 1e-10");
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That(ability.AbilityFinished, Is.False, "finishing happens on the next update");

                ability.Update(0.1f);
                Assert.That(ability.AbilityFinished, Is.True);
                Assert.That(rig.Fourmen.BossesInMotion, Is.Empty, "reaching the target removes the boss from the bosses in motion");
            });
        }

        // 0x6FC170: leaving clears the finished and reached flags and zeroes the destination timer in the FSM storage; the movement
        // timer is deliberately kept.
        [Test]
        public void MoveTowardsTargetLeaveResetsTheDestinationTimerButKeepsTheMovementTimer()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, Vector3.zero);
                Boss boss = rig.Bosses[0];
                BossAbility_MoveTowardsTarget ability = NewAbility<BossAbility_MoveTowardsTarget>(boss,
                    NewDefinition<BossAbilityDefinition_MoveTowardsTarget>(owned, d =>
                    {
                        d.AccelerationTime = 1f;
                        d.AccelerationCurve = AnimationCurve.Constant(0f, 1f, 0f);
                    }));
                // Reflection: the base ability has no target setter (only the derived abilities assign it), and the base is the ability under test.
                Set(ability, "m_targetPosition", new Vector3(10f, 0f, 0f));
                boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 3f);
                ability.Enter();
                ability.Update(0.75f);
                // The boss arriving at the target is the update that reaches it (Vector3 equality), and the next update finishes.
                boss.SetWorldPosition(new Vector3(10f, 0f, 0f));
                ability.Update(0.1f);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.True);
                ability.Update(0.1f);
                Assert.That(ability.AbilityFinished, Is.True);

                ability.Leave();
                Assert.That(ability.AbilityFinished, Is.False);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.False);
                Assert.That(Stored<float>(boss, BossFSMKeys.DestinationReachedTimer), Is.Zero);
                Assert.That(Get<float>(ability, "m_movementTimer"), Is.EqualTo(0.75f));
            });
        }

        // ---- the abilities derived from MoveTowardsTarget ----

        // 0x6FB889: the target is the point at -90 degrees (-pi/2) of the circle of the definition's radius round the arena centre
        // (Vector3.up), at the boss's height; the base entry then starts the movement from the boss's position.
        [Test]
        public void MoveToArenaEdgeAimsAtTheEdgePointAtMinusNinetyDegrees()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, new Vector3(5f, 2f, 7f));
                Boss boss = rig.Bosses[0];
                var definition = NewDefinition<BossAbilityDefinition_MoveToArenaEdge>(owned, d => d.Radius = 30f);
                BossAbility_MoveToArenaEdge ability = NewAbility<BossAbility_MoveToArenaEdge>(boss, definition);
                Assert.That(ability.DerivedDefinition, Is.SameAs(definition));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));

                ability.Enter();
                AssertNear(Get<Vector3>(ability, "m_targetPosition"), Circle(Vector3.up, -Math.PI / 2, 30.0, 2f), "edge point", 1e-3f);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(5f, 2f, 7f)), "the base entry starts at the boss");
                Assert.That(rig.Fourmen.BossesInMotion, Is.EqualTo(new[] { boss }));

                ability.Initialise(boss, NewDefinition<BossAbilityDefinition_MoveTowardsTarget>(owned));
                Assert.That(ability.DerivedDefinition, Is.Null, "a plain MoveTowardsTarget definition is not a MoveToArenaEdge definition");
                Assert.That(ability.Definition, Is.Not.Null);
            });
        }

        // 0x6FBC30: the small circle's centre is the big circle's point at angle zero (BigCircleRadius round the arena centre); the
        // target is the point of that small circle (CounterSpinRadius) at the boss's index in the Fourmen manager times pi.
        [Test]
        public void MoveToBigCircleWithCounterSpinPlacesEachBossOppositeOnTheSmallCircle()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, new Vector3(1f, 3f, 1f), new Vector3(2f, 4f, 2f));
                var definition = NewDefinition<BossAbilityDefinition_MoveToBigCircleWithCounterSpin>(owned, d =>
                {
                    d.BigCircleRadius = 20f;
                    d.CounterSpinRadius = 7f;
                });
                Vector3 smallCentre0 = Circle(Vector3.up, 0.0, 20.0, 3f);
                BossAbility_MoveToBigCircleWithCounterSpin first = NewAbility<BossAbility_MoveToBigCircleWithCounterSpin>(rig.Bosses[0], definition);
                first.Enter();
                AssertNear(Get<Vector3>(first, "m_targetPosition"), Circle(smallCentre0, 0.0, 7.0, 3f), "boss 0: angle 0", 1e-3f);

                Vector3 smallCentre1 = Circle(Vector3.up, 0.0, 20.0, 4f);
                BossAbility_MoveToBigCircleWithCounterSpin second = NewAbility<BossAbility_MoveToBigCircleWithCounterSpin>(rig.Bosses[1], definition);
                second.Enter();
                AssertNear(Get<Vector3>(second, "m_targetPosition"), Circle(smallCentre1, Math.PI, 7.0, 4f), "boss 1: angle pi", 1e-3f);
                Assert.That(Get<Vector3>(second, "m_targetPosition").x, Is.EqualTo(13f).Within(1e-3f), "20 + cos(pi) * 7");
                Assert.That(second.DerivedDefinition, Is.SameAs(definition));
                Assert.That(rig.Fourmen.BossesInMotion, Is.EquivalentTo(new[] { rig.Bosses[0], rig.Bosses[1] }));
            });
        }

        // 0x6FC400: the bosses share a circle of the definition's starting radius round the centroid of all bosses, spaced
        // evenly by 2*pi over the boss count in order of their index.
        [Test]
        public void MoveToTriangleFormationSpacesTheBossesEvenlyAroundTheirCentroid()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, 6f));
                var definition = NewDefinition<BossAbilityDefinition_MoveToTriangleFormation>(owned, d => d.StartingRadius = 10f);
                Vector3 centroid = new Vector3(1f, 0f, 2f);
                for (int index = 0; index < 3; index++)
                {
                    BossAbility_MoveToTriangleFormation ability = NewAbility<BossAbility_MoveToTriangleFormation>(rig.Bosses[index], definition);
                    ability.Enter();
                    AssertNear(Get<Vector3>(ability, "m_targetPosition"), Circle(centroid, index * 2 * Math.PI / 3, 10.0, 0f), "boss " + index, 1e-3f);
                    Assert.That(ability.DerivedDefinition, Is.SameAs(definition));
                }
                Assert.That(rig.Fourmen.BossesInMotion.Count, Is.EqualTo(3));
            });
        }

        // get_TurnSpeed (0x6FBDF0): the TurnSpeed comes from the derived definition. The target is its fixed Target, or, with TargetIsBossOrigin,
        // the origin point from the FSM storage (default zero, which is stored). Leaving repeats the base leave's resets.
        [Test]
        public void MoveToTargetPointChoosesTheFixedTargetOrTheBossOrigin()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, Vector3.zero);
                Boss boss = rig.Bosses[0];
                var fixedTarget = NewDefinition<BossAbilityDefinition_MoveToTarget>(owned, d =>
                {
                    d.Target = new Vector3(1f, 2f, 3f);
                    d.TurnSpeed = 33f;
                });
                BossAbility_MoveToTargetPoint ability = NewAbility<BossAbility_MoveToTargetPoint>(boss, fixedTarget);
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(ability.TurnSpeed, Is.EqualTo(33f));
                Assert.That(ability.DerivedDefinition, Is.SameAs(fixedTarget));
                Assert.That(ability.DefinitionBase, Is.SameAs(fixedTarget));
                Assert.That(Has(boss, BossFSMKeys.OriginPoint), Is.False, "the fixed target never reads the origin");

                var fromOrigin = NewDefinition<BossAbilityDefinition_MoveToTarget>(owned, d =>
                {
                    d.TargetIsBossOrigin = true;
                    d.Target = new Vector3(9f, 9f, 9f);
                    d.AccelerationCurve = AnimationCurve.Constant(0f, 1f, 0f);
                });
                BossAbility_MoveToTargetPoint origin = NewAbility<BossAbility_MoveToTargetPoint>(boss, fromOrigin);
                Assert.That(Get<Vector3>(origin, "m_targetPosition"), Is.EqualTo(Vector3.zero), "no stored origin: the default");
                Assert.That(Stored<Vector3>(boss, BossFSMKeys.OriginPoint), Is.EqualTo(Vector3.zero));
                Assert.That(Has(boss, BossFSMKeys.OriginPoint), Is.True, "and the default is stored");
                boss.Storage.SetValue(BossFSMKeys.OriginPoint, new Vector3(4f, 5f, 6f));
                origin.Initialise(boss, fromOrigin);
                Assert.That(Get<Vector3>(origin, "m_targetPosition"), Is.EqualTo(new Vector3(4f, 5f, 6f)));

                boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 2f);
                origin.Enter();
                boss.SetWorldPosition(new Vector3(4f, 5f, 6f));
                origin.Update(0.1f);
                Assert.That(Get<bool>(origin, "m_reachedTarget"), Is.True, "the boss at the target reaches it");
                origin.Leave();
                Assert.That(Stored<float>(boss, BossFSMKeys.DestinationReachedTimer), Is.Zero);
                Assert.That(Get<bool>(origin, "m_reachedTarget"), Is.False);
                Assert.That(origin.AbilityFinished, Is.False);
            });
        }

        // ---- BossAbility_MoveBigCircleWithCounterSpin ----

        // 0x6FA12D..0x6FA1BE: the circle centre is the arena centre (Vector3.up) and the big circle's point at angle zero is the counter
        // circle's centre; the counter-spin angle starts at the boss's index times pi. The speeds are stored as seconds per radian
        // (radius over speed). 0x6FA2A5: each update moves the big arc backwards and the counter-spin arc forwards by delta over
        // those seconds. 0x6FA27B: leaving restarts the big arc and flips the direction flag.
        [Test]
        public void MoveBigCircleWithCounterSpinOrbitsAPointThatTravelsTheBigCircle()
        {
            Owned.Run(owned =>
            {
                FourmenRig rig = NewFourmenRig(owned, new Vector3(0f, 2f, 0f), new Vector3(0f, 3f, 0f));
                var definition = NewDefinition<BossAbilityDefinition_MoveBigCircleWithCounterSpin>(owned, d =>
                {
                    d.BigCircleSpeed = 4f;
                    d.CounterRotateSpeed = 2f;
                    d.BigCircleRadius = 20f;
                    d.CounterSpinRadius = 7f;
                    d.FaceAngleTolerance = 11f;
                    d.TurnSpeed = 12f;
                    d.MovementAnimationAngleMultiplier = 13f;
                });
                BossAbility_MoveBigCircleWithCounterSpin ability = NewAbility<BossAbility_MoveBigCircleWithCounterSpin>(rig.Bosses[1], definition);
                Assert.That((ability.IsTriggered, ability.IsMomentum, ability.TurnToCharacter), Is.EqualTo((true, false, true)));
                Assert.That((ability.FaceAngleTolerance, ability.TurnSpeed, ability.MovementAnimationAngleMultiplier), Is.EqualTo((11f, 12f, 13f)));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));

                ability.Enter();
                Vector3 bigPoint = Circle(Vector3.up, 0.0, 20.0, 3f);
                AssertNear(ability.CurrentTargetPosition, Circle(bigPoint, Math.PI, 7.0, 3f), "boss 1 starts at angle pi on the counter circle", 1e-3f);
                Assert.That(Get<float>(ability, "m_speedBigCircleNormalised"), Is.EqualTo(5f), "20 / 4");
                Assert.That(Get<float>(ability, "m_speedCounterSpinNormalised"), Is.EqualTo(3.5f), "7 / 2");
                Assert.That(Get<float>(ability, "m_progressionCounterSpinArcRadians"), Is.EqualTo((float)Math.PI));

                ability.Update(0.5f);
                double big = -0.5 / 5.0;
                double spin = Math.PI + 0.5 / 3.5;
                AssertNear(ability.CurrentTargetPosition, Circle(Circle(Vector3.up, big, 20.0, 3f), spin, 7.0, 3f), "after 0.5s", 1e-3f);
                Assert.That(Get<float>(ability, "m_progressionBigCircleArcRadians"), Is.EqualTo(-0.1f).Within(1e-6f));

                bool inverted = Get<bool>(ability, "m_invertDirection");
                ability.Leave();
                Assert.That(Get<float>(ability, "m_progressionBigCircleArcRadians"), Is.Zero);
                Assert.That(Get<bool>(ability, "m_invertDirection"), Is.EqualTo(!inverted));
                ability.Enter();
                ability.Leave();
                Assert.That(Get<bool>(ability, "m_invertDirection"), Is.EqualTo(inverted), "every leave flips it");
            });
        }

        // ---- BossAbility_MoveBigCircleWithDecayingRadius ----

        private static BossAbility_MoveBigCircleWithDecayingRadius NewDecayingRadius(Owned owned, Boss boss)
        {
            var definition = NewDefinition<BossAbilityDefinition_MoveBigCircleWithDecayingRadius>(owned, d =>
            {
                d.MaxMovementSpeed = 3f;
                d.MovementSpeedCurve = AnimationCurve.Linear(0f, 2f, 10f, 2f);
                d.StartingRadius = 25f;
                d.EndingRadius = 3f;
                d.RadiusDecayCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.TimeToDecayFully = 2f;
                d.EndDistanceTolerance = 1.1f;
            });
            return NewAbility<BossAbility_MoveBigCircleWithDecayingRadius>(boss, definition);
        }

        // 0x6FA5D0: the spiral starts on the starting radius round the origin at the boss's bearing atan2(z, x), at the speed
        // curve's value at zero (not scaled by the maximum speed); the charging point is the arena centre (zero) copied on
        // initialisation. 0x6FA6B0: the speed is the curve at the elapsed time times the maximum, the radius lerps from the starting to
        // the ending radius over the decay time by the (clamped) curve, and the arc runs backwards by delta over radius/speed seconds.
        [Test]
        public void MoveBigCircleWithDecayingRadiusSpiralsInFromTheBossBearing()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 2f, 4f));
                BossAbility_MoveBigCircleWithDecayingRadius ability = NewDecayingRadius(owned, boss);
                Assert.That((ability.MovementAnimationAngleMultiplier, ability.FaceAngleTolerance, ability.TurnSpeed, ability.IsMomentum, ability.TurnToCharacter),
                    Is.EqualTo((0f, 0f, 0f, false, false)));
                Assert.That(Get<Vector3>(ability, "m_bossChargingPoint"), Is.EqualTo(Vector3.zero));

                ability.Enter();
                AssertNear(ability.CurrentTargetPosition, new Vector3(15f, 2f, 20f), "25 * (cos, sin) of atan2(4, 3) = (15, 20)", 1e-3f);
                Assert.That(Get<float>(ability, "m_speed"), Is.EqualTo(2f));
                Assert.That(Get<float>(ability, "m_speedNormalised"), Is.EqualTo(12.5f), "25 / 2");

                ability.Update(0.5f);
                double arc = Math.Atan2(4.0, 3.0) - 0.5 / (25.0 / 6.0);
                Assert.That(Get<float>(ability, "m_speed"), Is.EqualTo(6f), "curve 2 * maximum 3");
                Assert.That(Get<float>(ability, "m_radiusOfCircle"), Is.EqualTo(19.5f).Within(1e-5f), "25 + (3 - 25) * 0.25");
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)arc).Within(1e-5f));
                AssertNear(ability.CurrentTargetPosition, Circle(Vector3.zero, arc, 19.5, 2f), "after 0.5s", 1e-3f);
            });
        }

        // 0x6FA7B7..0x6FA819: the movement finishes once the radius is exactly the ending radius and the boss is within the end
        // tolerance of the charging point, the tolerance being compared with the squared distance; the update does nothing once finished.
        [Test]
        public void MoveBigCircleWithDecayingRadiusFinishesAtTheEndingRadiusNearTheChargingPoint()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(6f, 0f, 8f));
                BossAbility_MoveBigCircleWithDecayingRadius ability = NewDecayingRadius(owned, boss);
                ability.Enter();
                ability.Update(1f);
                Assert.That(Get<float>(ability, "m_radiusOfCircle"), Is.EqualTo(14f).Within(1e-5f), "halfway: 25 + (3-25)*0.5");
                Assert.That(ability.AbilityFinished, Is.False);
                ability.Update(1f);
                Assert.That(Get<float>(ability, "m_radiusOfCircle"), Is.EqualTo(3f), "the decay curve is clamped, so the radius is exactly the ending radius");
                Assert.That(ability.AbilityFinished, Is.False, "but the boss (squared distance 100) is far from the charging point");

                boss.SetWorldPosition(new Vector3(1f, 0f, 0.1f));
                float time = Get<float>(ability, "m_totalMovementTime");
                ability.Update(1f);
                Assert.That(ability.AbilityFinished, Is.True, "1.01 is below the tolerance 1.1");
                Assert.That(Get<float>(ability, "m_totalMovementTime"), Is.EqualTo(time + 1f));
                ability.Update(1f);
                Assert.That(Get<float>(ability, "m_totalMovementTime"), Is.EqualTo(time + 1f), "nothing runs once finished");

                ability.Leave();
                Assert.That((ability.AbilityFinished, Get<float>(ability, "m_progressionArcRadians"), Get<float>(ability, "m_totalMovementTime")), Is.EqualTo((false, 0f, 0f)));

                Boss near = NewBoss(owned, new Vector3(1f, 0f, 0.1f));
                BossAbility_MoveBigCircleWithDecayingRadius early = NewDecayingRadius(owned, near);
                early.Enter();
                early.Update(0.5f);
                Assert.That(early.AbilityFinished, Is.False, "near the charging point but the radius has not reached the ending radius");
            });
        }

        // ---- BossAbility_TurnTowardTarget ----

        // 0x700810: the target is the offset from the boss to the current character; 0x700660..0x7006A0: the definition supplies the
        // face-angle tolerance and turn speed, and the movement never tilts, is not momentum based and turns to the character.
        [Test]
        public void TurnTowardTargetAimsAtTheOffsetToTheCharacter()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, new Vector3(1f, 2f, 3f));
                MakeCurrent(manager, character, new Vector3(5f, 1f, -2f));
                var definition = NewDefinition<BossAbilityDefinition_MovementTurnTowardTarget>(owned, d =>
                {
                    d.FaceAngleTolerance = 4f;
                    d.TurnSpeed = 6f;
                });
                BossAbility_TurnTowardTarget ability = NewAbility<BossAbility_TurnTowardTarget>(boss, definition);
                Assert.That((ability.MovementAnimationAngleMultiplier, ability.FaceAngleTolerance, ability.TurnSpeed, ability.IsMomentum, ability.TurnToCharacter),
                    Is.EqualTo((0f, 4f, 6f, false, true)));
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));
                ability.Enter();
                ability.Update(0.1f);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(4f, -1f, -5f)));
                boss.SetWorldPosition(new Vector3(5f, 1f, -2f));
                ability.Update(0.1f);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero));
            });
        }

        // ---- BossAbility_SpinOnSpot ----

        private static BossAbility_SpinOnSpot NewSpin(Owned owned, Action<BossAbilityDefinition_SpinOnSpot> setup)
        {
            Boss boss = NewBoss(owned, Vector3.zero);
            var definition = NewDefinition<BossAbilityDefinition_SpinOnSpot>(owned, d =>
            {
                d.SpinRotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.TimeToFullyRotateInSeconds = 2f;
                d.RotationsUntilComplete = 1;
                d.DesiredEndRotationToleranceInDegrees = 5;
                setup(d);
            });
            BossAbility_SpinOnSpot ability = NewAbility<BossAbility_SpinOnSpot>(boss, definition);
            ability.Enter();
            return ability;
        }

        // 0x6FF7C7: the angle of a tick is the curve value (a fraction of a turn) times 360 clamped to a full turn; the tick turns by
        // the change in that angle, always in the spin direction (|change| for clockwise, -|change| otherwise), wrapped by fmod
        // 360; the completed rotations are the truncated normalised time. It finishes once the truncated angle lies within the
        // tolerance of the desired end rotation and enough rotations are completed, and then snaps to the desired end rotation.
        [Test]
        public void SpinOnSpotTurnsClockwiseUntilTheEndRotationIsReached()
        {
            Owned.Run(owned =>
            {
                BossAbility_SpinOnSpot ability = NewSpin(owned, d => d.Clockwise = true);
                ability.Update(1f);
                Assert.That((ability.RotationInDegrees, ability.RotationsCompleted, ability.AbilityFinished), Is.EqualTo((180f, 0, false)));
                ability.Update(1f);
                Assert.That((ability.RotationInDegrees, ability.RotationsCompleted, ability.AbilityFinished), Is.EqualTo((0f, 1, true)),
                    "360 wraps to 0, which is within 5 of the desired 0 with one rotation done");

                ability.Leave();
                Assert.That(ability.AbilityFinished, Is.False);
                ability.Enter();
                Assert.That((ability.RotationInDegrees, ability.RotationsCompleted), Is.EqualTo((0f, 0)), "entering restarts the spin");
                Assert.That(Get<float>(ability, "m_rotationPreviousTick"), Is.Zero);
            });
        }

        [Test]
        public void SpinOnSpotTurnsAnticlockwiseThroughNegativeAngles()
        {
            Owned.Run(owned =>
            {
                BossAbility_SpinOnSpot ability = NewSpin(owned, d => d.Clockwise = false);
                ability.Update(1f);
                Assert.That((ability.RotationInDegrees, ability.RotationsCompleted, ability.AbilityFinished), Is.EqualTo((-180f, 0, false)),
                    "fmod keeps the sign: -180 is not within 5 of 0");
                ability.Update(1f);
                Assert.That(ability.RotationsCompleted, Is.EqualTo(1));
                Assert.That(ability.AbilityFinished, Is.True, "-360 fmod 360 is -0, which truncates to 0");
                Assert.That(ability.RotationInDegrees, Is.Zero);
            });
        }

        // The tolerance test is inclusive at both ends: the truncated angle 90 is accepted for desired 88 +- 2, 92 +- 2 and rejected for
        // 93 +- 2 (a strict comparison would reject the first two).
        [Test]
        public void SpinOnSpotAcceptsAnAngleAtTheToleranceBoundary()
        {
            foreach ((int desired, bool finished) in new[] { (88, true), (92, true), (93, false), (87, false) })
            {
                Owned.Run(owned =>
                {
                    BossAbility_SpinOnSpot ability = NewSpin(owned, d =>
                    {
                        d.Clockwise = true;
                        d.SpinRotationCurve = AnimationCurve.Constant(0f, 1f, 0.25f);
                        d.RotationsUntilComplete = 0;
                        d.DesiredEndRotation = desired;
                        d.DesiredEndRotationToleranceInDegrees = 2;
                    });
                    ability.Update(0.1f);
                    Assert.That(ability.AbilityFinished, Is.EqualTo(finished), "desired " + desired);
                    Assert.That(ability.RotationInDegrees, Is.EqualTo(finished ? desired : 90f), "desired " + desired);
                });
            }
        }

        // ---- BossAbility_StayAheadOfPlayer ----

        private static BossAbility_StayAheadOfPlayer NewStayAhead(Owned owned, Boss boss, Action<BossAbilityDefinition_StayAheadOfPlayer> setup = null) =>
            NewAbility<BossAbility_StayAheadOfPlayer>(boss, NewDefinition<BossAbilityDefinition_StayAheadOfPlayer>(owned, d =>
            {
                d.MinRadiansAheadOfPlayer = 0.5f;
                d.MaxRadiansAheadOfPlayer = 1.5f;
                setup?.Invoke(d);
            }));

        // The ground position at an angle from the forward axis, at the circle radius of 25 (the definition default).
        private static Vector3 OnCircle(double degrees, float y = 0f) =>
            new Vector3((float)(25.0 * Math.Sin(degrees * Math.PI / 180.0)), y, (float)(25.0 * Math.Cos(degrees * Math.PI / 180.0)));

        // 0x6FFF15: the signed angle from the forward axis (scaled by the radius) to the position projected on the ground, about
        // up, shifted into 0..360 degrees (360 added to a negative angle).
        [Test]
        public void StayAheadArcAngleIsTheGroundAngleFromForwardInDegrees()
        {
            WithManager((owned, manager, character) =>
            {
                MakeCurrent(manager, character, Vector3.zero);
                BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, NewBoss(owned, Vector3.zero));
                foreach ((Vector3 position, float degrees) in new[]
                {
                    (new Vector3(0f, 0f, 25f), 0f), (new Vector3(25f, 0f, 0f), 90f), (new Vector3(0f, 0f, -25f), 180f), (new Vector3(-25f, 0f, 0f), 270f),
                    (new Vector3(0f, 99f, 25f), 0f), (OnCircle(350.0), 350f),
                })
                    Assert.That((float)Call(ability, "GetArcAngle", position), Is.EqualTo(degrees).Within(1e-3f), position.ToString());
            });
        }

        // 0x7002B0: the character's angle in radians, the boss's in degrees, and zero for both arc limits.
        [Test]
        public void StayAheadCalculatesTheCharacterAngleInRadiansAndTheBossAngleInDegrees()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, new Vector3(-25f, 0f, 0f));
                MakeCurrent(manager, character, new Vector3(25f, 0f, 0f));
                BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, boss);
                object[] arguments = { character, 0f, 0f, 9f, 9f };
                Call(ability, "CalculateAngles", arguments);
                Assert.That((float)arguments[1], Is.EqualTo((float)(Math.PI / 2)).Within(1e-5f));
                Assert.That((float)arguments[2], Is.EqualTo(270f).Within(1e-3f));
                Assert.That(((float)arguments[3], (float)arguments[4]), Is.EqualTo((0f, 0f)));
            });
        }

        // 0x7004F7: the boss's angle (wrapped a full turn towards the character's side) must lie strictly between the character's angle
        // plus the minimum and maximum arc ahead (clockwise), or minus the maximum and minimum (counter-clockwise); the wrapped angle is kept
        // as the progression angle.
        [Test]
        public void StayAheadShouldActivateOnlyInsideTheArcAheadOfTheCharacter()
        {
            foreach ((bool clockwise, double character, double boss, bool expected, double kept) in new[]
            {
                (true, 90.0, 120.0, true, 120.0), (true, 90.0, 100.0, false, 100.0), (true, 90.0, 180.0, false, 180.0),
                (true, 350.0, 10.0, false, 370.0), (true, 350.0, 20.0, true, 380.0),
                (false, 90.0, 40.0, true, 40.0), (false, 90.0, 300.0, false, -60.0), (false, 10.0, 320.0, true, -40.0),
            })
            {
                WithManager((owned, manager, characterObject) =>
                {
                    Boss bossObject = NewBoss(owned, OnCircle(boss));
                    MakeCurrent(manager, characterObject, OnCircle(character));
                    BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, bossObject, d => d.Clockwise = clockwise);
                    string label = $"{(clockwise ? "clockwise" : "counter-clockwise")} character {character} boss {boss}";
                    Assert.That(ability.ShouldAbilityActivate(), Is.EqualTo(expected), label);
                    Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)(kept * Math.PI / 180.0)).Within(2e-3f), label);
                });
            }
        }

        // Without a current character the ability cannot activate.
        [Test]
        public void StayAheadShouldNotActivateWithoutACharacter()
        {
            WithManager((owned, manager, character) =>
            {
                BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, NewBoss(owned, OnCircle(120.0)));
                Assert.That(ability.ShouldAbilityActivate(), Is.False);
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.Zero);
            });
        }

        // 0x700155: the boss's angle moves by delta*MovementPerSecond (wrapping a full turn towards the character's side) and is kept between
        // the lower and upper arc limit; at the upper limit it is "at maximum range" (0x70026A..0x70027A). The target is the point of
        // that angle on the circle round the arena centre (sin on X, cos on Z).
        [Test]
        public void StayAheadUpdateKeepsTheBossBetweenTheArcLimits()
        {
            WithManager((owned, manager, character) =>
            {
                MakeCurrent(manager, character, OnCircle(90.0));
                double lower = Math.PI / 2 + 0.5;
                double upper = Math.PI / 2 + 1.5;
                foreach ((double bossDegrees, double expected, bool atMax) in new[]
                {
                    (120.0, 120.0 * Math.PI / 180.0 + 0.01, false), (100.0, lower, false), (200.0, upper, true),
                })
                {
                    Boss boss = NewBoss(owned, OnCircle(bossDegrees, 7f));
                    BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, boss, d => d.MovementPerSecond = 0.01f);
                    Tick(ability, 1f);
                    Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)expected).Within(2e-3f), "boss at " + bossDegrees);
                    Assert.That(ability.AtMaxRange, Is.EqualTo(atMax), "boss at " + bossDegrees);
                    AssertNear(ability.CurrentTargetPosition, CircleLhs(Vector3.zero, (float)expected, 25.0, 7f), "target for boss at " + bossDegrees, 0.05f);
                }
            });
        }

        // 0x7001EE..0x70020C (clockwise, newBoss < character adds 2*pi = 6.2831855) and 0x70021D..0x70023C (counter-clockwise,
        // character < newBoss adds -2*pi = -6.2831855): the boss's angle wraps a full turn towards the character's side before the clamp.
        [Test]
        public void StayAheadUpdateWrapsTheBossAngleAFullTurnTowardsTheCharacter()
        {
            foreach ((bool clockwise, double character, double boss, double expected, double lower, double upper, bool atMax) in new[]
            {
                (true, 350.0, 20.0, 0.349066 + 0.01 + 6.283185, 6.608652, 7.608652, false),
                (false, 10.0, 320.0, 5.585053 - 0.01 - 6.283185, -1.325467, -0.325467, false),
            })
            {
                WithManager((owned, manager, characterObject) =>
                {
                    MakeCurrent(manager, characterObject, OnCircle(character));
                    Boss bossObject = NewBoss(owned, OnCircle(boss, 7f));
                    BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, bossObject, d =>
                    {
                        d.Clockwise = clockwise;
                        d.MovementPerSecond = 0.01f;
                    });
                    Tick(ability, 1f);
                    string label = $"clockwise {clockwise} character {character} boss {boss}";
                    Assert.That(expected, Is.InRange(lower, upper), label);
                    Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)expected).Within(2e-3f), label);
                    Assert.That(ability.AtMaxRange, Is.EqualTo(atMax), label);
                    AssertNear(ability.CurrentTargetPosition, CircleLhs(Vector3.zero, (float)expected, 25.0, 7f), "target " + label, 0.05f);
                });
            }
        }

        // 0x70021D..0x70027A: counter-clockwise the boss's angle moves down and the limits swap (character - maximum, character - minimum).
        // The clamp tests below the lower limit first and then takes the lesser of the upper limit and the value, so a minimum above
        // the maximum yields the lower limit for values under it and the upper limit otherwise (a max(lower, min(upper, v)) clamp would
        // give the lower limit for both). Character at 90 degrees, movement 0.01 per second.
        [Test]
        public void StayAheadUpdateClampsCounterClockwiseAndWithAMinimumAboveTheMaximum()
        {
            WithManager((owned, manager, character) =>
            {
                MakeCurrent(manager, character, OnCircle(90.0));
                double half = Math.PI / 2;
                foreach ((bool clockwise, float min, float max, double bossDegrees, double expected, bool atMax) in new[]
                {
                    (false, 0.5f, 1.5f, 60.0, 60.0 * Math.PI / 180.0 - 0.01, false),
                    (false, 0.5f, 1.5f, 1.0, half - 1.5, false),
                    (false, 0.5f, 1.5f, 80.0, half - 0.5, true),
                    (true, 1.5f, 0.5f, 120.0, half + 1.5, false),
                    (true, 1.5f, 0.5f, 200.0, half + 0.5, true),
                })
                {
                    Boss boss = NewBoss(owned, OnCircle(bossDegrees, 7f));
                    BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, boss, d =>
                    {
                        d.Clockwise = clockwise;
                        d.MinRadiansAheadOfPlayer = min;
                        d.MaxRadiansAheadOfPlayer = max;
                        d.MovementPerSecond = 0.01f;
                    });
                    Tick(ability, 1f);
                    string label = $"clockwise {clockwise} min {min} max {max} boss {bossDegrees}";
                    Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)expected).Within(2e-3f), label);
                    Assert.That(ability.AtMaxRange, Is.EqualTo(atMax), label);
                }
            });
        }

        // 0x70017E: a finished ability does nothing in its update. 0x6FFB5D: entering resumes from the stored angle, or starts the
        // minimum arc ahead of the current character, and publishes the circling direction; without a character or stored angle it only
        // sets up the circle; 0x700020: leaving stores the angle.
        [Test]
        public void StayAheadEnterResumesFromTheStoredAngleOrTheCharacterAndLeaveStoresIt()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, OnCircle(120.0, 7f));
                BossAbility_StayAheadOfPlayer ability = NewStayAhead(owned, boss);
                ability.Enter();
                Assert.That(Has(boss, BossFSMKeys.GuardianIsClockwise), Is.False, "no character and nothing stored: only the circle is set up");
                Assert.That(Get<float>(ability, "m_radiusOfCircle"), Is.EqualTo(25f));
                ability.Leave();
                Assert.That(Stored<float>(boss, BossFSMKeys.RadiansCurrentPosition), Is.Zero, "leaving stores the progression angle");

                boss.Storage.RemoveValue<float>(BossFSMKeys.RadiansCurrentPosition);
                MakeCurrent(manager, character, OnCircle(90.0));
                ability.Enter();
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo((float)(Math.PI / 2 + 0.5)).Within(1e-4f), "character angle + minimum arc");
                Assert.That(Stored<bool>(boss, BossFSMKeys.GuardianIsClockwise), Is.True);
                Assert.That(ability.AtMaxRange, Is.False);
                AssertNear(ability.CurrentTargetPosition, CircleLhs(Vector3.zero, Math.PI / 2 + 0.5, 25.0, 7f), "start target", 0.05f);
                ability.Leave();
                Assert.That(Stored<float>(boss, BossFSMKeys.RadiansCurrentPosition), Is.EqualTo((float)(Math.PI / 2 + 0.5)).Within(1e-4f));

                boss.Storage.SetValue(BossFSMKeys.RadiansCurrentPosition, 1f);
                BossAbility_StayAheadOfPlayer counter = NewStayAhead(owned, boss, d => d.Clockwise = false);
                counter.Enter();
                Assert.That(Get<float>(counter, "m_progressionArcRadians"), Is.EqualTo(1f), "a stored angle wins over the character");
                Assert.That(Stored<bool>(boss, BossFSMKeys.GuardianIsClockwise), Is.False);
                Assert.That(Has(boss, BossFSMKeys.RadiansCurrentPosition), Is.True);

                counter.Update(1f);
                Assert.That(counter.AbilityFinished, Is.False);
                Assert.That(counter.FaceAngleTolerance, Is.EqualTo(1f));
                Assert.That((counter.MovementAnimationAngleMultiplier, counter.IsMomentum, counter.TurnToCharacter), Is.EqualTo((0f, false, true)));
            });
        }

        // ---- BossAbility_Scuttle ----

        private static BossAbility_Scuttle NewScuttle(Owned owned, Boss boss) =>
            NewAbility<BossAbility_Scuttle>(boss, NewDefinition<BossAbilityDefinition_MovementScuttle>(owned, d =>
            {
                d.ScuttleRange = 10f;
                d.ScuttleStartMinimumMovementDistance = 0.5f;
                d.ScuttleEndDistanceTolerance = 1.1f;
                d.ScuttleDestinationReachedSeconds = 0.2f;
            }));

        // 0x6FF014..0x6FF044: the ranges are the definition's cached squares: range, minimum movement distance and end tolerance; the
        // follow-up range is a plain property.
        [Test]
        public void ScuttleReportsTheSquaredRangesOfItsDefinition()
        {
            Owned.Run(owned =>
            {
                BossAbility_Scuttle ability = NewScuttle(owned, NewBoss(owned, Vector3.zero));
                Assert.That((ability.MaxRangeSquared, ability.MinRangeSquared), Is.EqualTo((100f, 0.25f)));
                Assert.That(ability.DestinationReachedToleranceSquared, Is.EqualTo(1.21f).Within(1e-5f));
                Assert.That(ability.MaxFollowUpRangeSquared, Is.Zero);
                ability.MaxFollowUpRangeSquared = 4f;
                Assert.That(ability.MaxFollowUpRangeSquared, Is.EqualTo(4f));
                Assert.That(ability.DefinitionBase, Is.Not.Null);
            });
        }

        // 0x6FF2ED: the target is the stored desired position, infinitely far when none is stored (and nothing is stored for it). The
        // ability can start strictly between the minimum movement distance and the range (both tests strict); the destination timer
        // runs while the boss is within the end tolerance (or equal to it), restarts otherwise, and the ability finishes when the target
        // is out of range or the timer passed the reached seconds (also strict).
        [Test]
        public void ScuttleFollowsTheDesiredPositionAndFinishesAfterWaitingAtIt()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_Scuttle ability = NewScuttle(owned, boss);

                Tick(ability, 0.1f);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.positiveInfinity));
                Assert.That(Has(boss, BossFSMKeys.DesiredMovementPosition), Is.False);
                Assert.That((ability.IsTriggered, ability.AbilityFinished), Is.EqualTo((false, true)), "infinitely far is out of range");

                foreach ((float z, bool canScuttle, bool finished, float timer) in new[]
                {
                    (4f, true, false, 0f), (20f, false, true, 0f), (10f, false, false, 0f), (0.5f, false, false, 0.1f), (0.6f, true, false, 0.1f), (0.04f, false, false, 0.1f),
                })
                {
                    boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(0f, 0f, z));
                    boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
                    Tick(ability, 0.1f);
                    string label = "distance " + z;
                    Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(0f, 0f, z)), label);
                    Assert.That(ability.IsTriggered, Is.EqualTo(canScuttle), label + " can scuttle");
                    Assert.That(ability.AbilityFinished, Is.EqualTo(finished), label + " finished");
                    Assert.That(Stored<float>(boss, BossFSMKeys.DestinationReachedTimer), Is.EqualTo(timer).Within(1e-6f), label + " timer");
                }

                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(0f, 0f, 1f));
                boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
                Tick(ability, 0.1f);
                Tick(ability, 0.1f);
                Assert.That(ability.AbilityFinished, Is.False, "0.2 is not above 0.2");
                Tick(ability, 0.1f);
                Assert.That(ability.AbilityFinished, Is.True, "0.3 is");

                // Leave only acts on an enabled ability (ActorAbility.Leave), so the ability is entered first.
                ability.Enter();
                ability.Leave();
                Assert.That((ability.AbilityFinished, ability.IsTriggered, Stored<float>(boss, BossFSMKeys.DestinationReachedTimer)), Is.EqualTo((false, false, 0f)));
            });
        }

        // ---- BossAbility_RetreatingJump ----

        private static BossAbility_RetreatingJump NewRetreatingJump(Owned owned, Boss boss) =>
            NewAbility<BossAbility_RetreatingJump>(boss, NewDefinition<BossAbilityDefinition_MovementRetreatingJump>(owned, d =>
            {
                d.JumpHeightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.JumpSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.JumpMaxHeight = 10f;
                d.MiniumJumpRange = 35f;
            }));

        // 0x6FE9C4..0x6FEA24: both ranges are the cached squared jump range, the tolerances are the never-assigned zeros.
        [Test]
        public void RetreatingJumpReportsTheSquaredJumpRange()
        {
            Owned.Run(owned =>
            {
                BossAbility_RetreatingJump ability = NewRetreatingJump(owned, NewBoss(owned, Vector3.zero));
                Assert.That((ability.MaxRangeSquared, ability.MinRangeSquared), Is.EqualTo((1225f, 1225f)));
                Assert.That((ability.DestinationReachedToleranceSquared, ability.MaxFollowUpRangeSquared), Is.EqualTo((0f, 0f)));
                Assert.That((ability.IsTriggered, ability.JumpProgress), Is.EqualTo((false, 0f)));
                ability.SetAnimationComplete();
                Assert.That(ability.AbilityFinished, Is.True, "0x6FEFC4 sets the finished flag");
            });
        }

        // 0x6FEBFD: a jump is triggered once the desired position (the boss's own when none is stored, which is not stored) is farther
        // than the squared jump range (strict); it starts from the boss's position. Once the animation is triggered the progress
        // runs by delta; x/z follow the speed curve and y the height curve towards the target height plus the maximum jump height. At
        // progress one the jump ends (flags and progress reset) without finishing the ability.
        [Test]
        public void RetreatingJumpFliesAlongTheCurvesAndEndsAtFullProgress()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_RetreatingJump ability = NewRetreatingJump(owned, boss);

                Tick(ability, 0.25f);
                Assert.That(ability.IsTriggered, Is.False, "the boss's own position is not farther than the range");
                Assert.That(Has(boss, BossFSMKeys.DesiredMovementPosition), Is.False);

                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(0f, 0f, 35f));
                Tick(ability, 0.25f);
                Assert.That(ability.IsTriggered, Is.False, "1225 is not above 1225");
                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(0f, 0f, 50f));
                Tick(ability, 0.25f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(ability.JumpProgress, Is.Zero, "the animation has not been triggered");

                ability.SetAnimationTriggered();
                foreach ((float progress, Vector3 expected) in new[]
                {
                    (0.25f, new Vector3(0f, 2.5f, 12.5f)), (0.5f, new Vector3(0f, 5f, 25f)), (0.75f, new Vector3(0f, 7.5f, 37.5f)), (1f, new Vector3(0f, 10f, 50f)),
                })
                {
                    Tick(ability, 0.25f);
                    Assert.That(ability.JumpProgress, Is.EqualTo(progress));
                    AssertNear(ability.CurrentTargetPosition, expected, "progress " + progress);
                }
                Tick(ability, 0.25f);
                Assert.That((ability.JumpProgress, ability.IsTriggered, ability.AbilityFinished), Is.EqualTo((0f, false, false)));
                Assert.That(Get<bool>(ability, "m_animationTriggered"), Is.False);

                Tick(ability, 0.25f);
                Assert.That(ability.IsTriggered, Is.True, "still far: the next jump is triggered again");
            });
        }

        // 0x6FEB94: entering notes the boss's position; 0x6FEBC6: leaving clears the finished flag, the trigger and the progress.
        [Test]
        public void RetreatingJumpEnterNotesThePositionAndLeaveResets()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(4f, 5f, 6f));
                BossAbility_RetreatingJump ability = NewRetreatingJump(owned, boss);
                ability.Enter();
                Assert.That(Get<Vector3>(ability, "m_preJumpPosition"), Is.EqualTo(new Vector3(4f, 5f, 6f)));
                ability.SetAnimationComplete();
                // A desired position beyond the jump range triggers a jump, and the triggered animation then runs the progress.
                boss.Storage.SetValue(BossFSMKeys.DesiredMovementPosition, new Vector3(4f, 5f, 50f));
                Tick(ability, 0.25f);
                ability.SetAnimationTriggered();
                Tick(ability, 0.25f);
                Assert.That((ability.IsTriggered, ability.JumpProgress), Is.EqualTo((true, 0.25f)));
                ability.Leave();
                Assert.That((ability.AbilityFinished, ability.IsTriggered, ability.JumpProgress), Is.EqualTo((false, false, 0f)));
            });
        }

        // ---- BossAbility_MoveRandom ----

        private static BossAbility_MoveRandom NewMoveRandom(Owned owned, Boss boss, Action<BossAbilityDefinition_MoveRandom> setup = null) =>
            NewAbility<BossAbility_MoveRandom>(boss, NewDefinition<BossAbilityDefinition_MoveRandom>(owned, d =>
            {
                d.MinimumSpeed = 1f;
                d.MaximumSpeed = 4f;
                d.AccelerationPerSecond = 2f;
                d.MinDistanceTargetX = 3f;
                d.EndDistanceTolerance = 0.01f;
                d.DistanceAllowedFromOrigin = 1000f;
                d.FaceAngleTolerance = 14f;
                d.TurnSpeed = 15f;
                d.MovementAnimationAngleMultiplier = 16f;
                setup?.Invoke(d);
            }));

        // A started update with the enemy-movement time category scaled to zero, so the update the entry runs at once has delta zero.
        private static void WithMoveRandomWorld(Action<Owned> body) => Owned.Run(owned =>
        {
            owned.Register(NewTimeManager(owned, TimeCategory.EnemyMovement, 0f));
            body(owned);
        });

        // 0x6FA8A0..0x6FA8F4: the definition supplies the tilt, face-angle tolerance and turn speed; the movement is not momentum based and turns
        // to the character. 0x6FB548 and 0x6FB54F: the first circle's arc starts at infinity, the recursion limit at 100.
        [Test]
        public void MoveRandomStartsWithAnUnchosenArcAndTheDefinitionsTurning()
        {
            Owned.Run(owned =>
            {
                BossAbility_MoveRandom ability = NewMoveRandom(owned, NewBoss(owned, Vector3.zero));
                Assert.That((ability.MovementAnimationAngleMultiplier, ability.FaceAngleTolerance, ability.TurnSpeed, ability.IsMomentum, ability.TurnToCharacter),
                    Is.EqualTo((16f, 14f, 15f, false, true)));
                Assert.That(Get<float>(ability, "m_originalArcRadians"), Is.EqualTo(float.PositiveInfinity));
                Assert.That(Get<int>(ability, "m_recursionLimit"), Is.EqualTo(100));
                Assert.That(Get<Vector3>(ability, "ArenaCentre"), Is.EqualTo(Vector3.up));
                Assert.That(Get<Vector3>(ability, "m_centerOfCircle"), Is.EqualTo(Vector3.zero));
                Assert.That(ability.DefinitionBase, Is.Not.Null);
            });
        }

        // A boss 5 from the arena centre with a range of 11 makes the circle radius Random.Range(3, |11 - 3 - 5|) = 3 (to the engine's float rounding) and keeps
        // every point of the circle inside the allowed diameter, so the random choices are fixed.
        private static BossAbility_MoveRandom NewBoundedMoveRandom(Owned owned, Boss boss) =>
            NewMoveRandom(owned, boss, d =>
            {
                d.DistanceAllowedFromOrigin = 11f;
                d.EndDistanceTolerance = 0f;
            });

        // 0x6FAA60: entering takes the boss's speed from its velocity, notes its position, picks a circle that passes through the
        // boss (the centre lies the radius from it towards the origin, the stored arc is the direction from the centre back to the
        // boss), picks a final target on that circle, flips the direction and runs an update with the enemy-movement delta. With delta
        // zero the target of that update is the boss's own position on the circle.
        [Test]
        public void MoveRandomEnterChoosesACircleThroughTheBossAndAFinalTargetOnIt()
        {
            WithMoveRandomWorld(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 1f, 4f));
                boss.SetWorldVelocity(new Vector3(0f, 0f, 3f));
                BossAbility_MoveRandom ability = NewBoundedMoveRandom(owned, boss);
                ability.Enter();

                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(3f), "the velocity magnitude");
                Assert.That(Get<Vector3>(ability, "m_bossEntryPoint"), Is.EqualTo(new Vector3(3f, 1f, 4f)));
                // Engine rounding, not our code: Random.Range(3f, 3f) can return 3.00000024 (one ulp up; run77d).
                Assert.That(Get<float>(ability, "m_radiusOfCircle"), Is.EqualTo(3f).Within(1e-5f));
                AssertNear(Get<Vector3>(ability, "m_centerOfCircle"), new Vector3(1.2f, 1f, 1.6f), "centre 3 from the boss towards the origin", 1e-4f);
                Assert.That(Get<float>(ability, "m_originalArcRadians"), Is.EqualTo(Mathf.Atan2(4f - 1.6f, 3f - 1.2f)).Within(1e-4f),
                    "the stored arc is the direction from the centre to the boss");
                Vector3 centre = Get<Vector3>(ability, "m_centerOfCircle");
                Vector3 target = Get<Vector3>(ability, "m_finalTargetPosition");
                Assert.That(Mathf.Sqrt((target.x - centre.x) * (target.x - centre.x) + (target.z - centre.z) * (target.z - centre.z)), Is.EqualTo(3f).Within(1e-3f),
                    "the final target is on the circle");
                Assert.That(target.y, Is.EqualTo(1f));
                Assert.That(Get<bool>(ability, "m_invertDirection"), Is.True);
                Assert.That((Get<bool>(ability, "m_reachedTopSpeed"), Get<bool>(ability, "m_pumpTheBrakes")), Is.EqualTo((false, false)),
                    "3 is not above the maximum 4");
                Assert.That(Get<int>(ability, "m_recursionCount"), Is.EqualTo(1), "the first target choice");
                AssertNear(ability.CurrentTargetPosition, new Vector3(3f, 1f, 4f), "delta zero keeps the target on the boss", 1e-3f);

                ability.Leave();
                Assert.That(Get<float>(ability, "m_originalArcRadians"), Is.EqualTo(float.PositiveInfinity), "the next entry chooses afresh");
            });
        }

        private static BossAbility_MoveRandom NewCruising(Owned owned, Boss boss, Action<BossAbilityDefinition_MoveRandom> setup = null)
        {
            BossAbility_MoveRandom ability = NewMoveRandom(owned, boss, setup);
            // Reflection: Enter draws the circle and the final target with Random.Range, so no real path reaches this fixed cruising state.
            Set(ability, "m_radiusOfCircle", 10f);
            Set(ability, "m_speedAdjusted", 2f);
            Set(ability, "m_finalTargetPosition", new Vector3(100f, 0f, 0f));
            return ability;
        }

        // 0x6FB025: while neither at top speed nor braking the speed rises by acceleration*delta (and the top-speed flag is set when
        // it exceeds the maximum speed); the arc moves by delta over radius/speed seconds, negated while the direction is inverted; the
        // target is the circle point of that arc. At top speed the speed holds (0x6FB049).
        [Test]
        public void MoveRandomAcceleratesToTopSpeedAndThenCruises()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_MoveRandom ability = NewCruising(owned, boss);

                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(4f), "2*1 + 2");
                Assert.That(Get<bool>(ability, "m_reachedTopSpeed"), Is.False, "4 is not above the maximum 4");
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo(0.4f).Within(1e-6f), "1 / (10 / 4)");
                AssertNear(ability.CurrentTargetPosition, Circle(Vector3.zero, 0.4, 10.0, 0f), "target after the first update", 1e-3f);

                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(6f));
                Assert.That(Get<bool>(ability, "m_reachedTopSpeed"), Is.True);
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo(1.0f).Within(1e-5f), "0.4 + 1 / (10 / 6)");

                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(6f), "cruising holds the speed");
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo(1.6f).Within(1e-5f));

                // Reflection: the direction flag is flipped by Enter's circle choice, which also redraws the fixed circle this case runs on.
                Set(ability, "m_invertDirection", true);
                Tick(ability, 0.5f);
                Assert.That(Get<float>(ability, "m_progressionArcRadians"), Is.EqualTo(1.3f).Within(1e-5f), "an inverted direction runs backwards");
            });
        }

        // 0x6FB0FE: once the boss is closer to the final target than the distance it needed to reach full speed (and above the minimum
        // speed) the brakes go on; braking lowers the speed by acceleration*delta and the brakes stay on while the speed stays above the
        // minimum speed.
        [Test]
        public void MoveRandomBrakesBeforeTheFinalTarget()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_MoveRandom ability = NewCruising(owned, boss);
                // Reflection: the speed state and the full-acceleration distance come from Enter's random circle and the acceleration run, not from a fixed one.
                Set(ability, "m_speedAdjusted", 6f);
                Set(ability, "m_reachedTopSpeed", true);
                Set(ability, "m_distanceFullAccelerationReachedSquared", 5000f);

                Tick(ability, 0.1f);
                Assert.That(Get<bool>(ability, "m_pumpTheBrakes"), Is.False, "10000 away is not within the 5000 acceleration distance");
                // Reflection: the acceleration distance is derived from the cruising speed on the fixed circle; there is no real path to a chosen value.
                Set(ability, "m_distanceFullAccelerationReachedSquared", 20000f);
                Tick(ability, 0.1f);
                Assert.That(Get<bool>(ability, "m_pumpTheBrakes"), Is.True, "20000 > 10000 and the speed 6 is above the minimum 1");
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(6f), "the update that decided to brake still cruised");

                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(4f), "6 - 2*1");
                Assert.That(Get<bool>(ability, "m_pumpTheBrakes"), Is.True);
                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.EqualTo(2f));
                Assert.That(Get<bool>(ability, "m_pumpTheBrakes"), Is.True, "2 is above the minimum 1");
                Tick(ability, 1f);
                Assert.That(Get<float>(ability, "m_speedAdjusted"), Is.Zero);
                Assert.That(Get<bool>(ability, "m_pumpTheBrakes"), Is.False, "0 is not above 1");
            });
        }

        // 0x6FB13D: close enough to the final target (the tolerance is compared with the squared distance), or too many retries, sets up a
        // new circle (flipping the direction, clearing both speed flags, one target choice done).
        [Test]
        public void MoveRandomSetsUpANewCircleNearTheFinalTarget()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(3f, 1f, 4f));
                BossAbility_MoveRandom ability = NewCruising(owned, boss, d =>
                {
                    d.DistanceAllowedFromOrigin = 11f;
                    d.EndDistanceTolerance = 1e9f;
                });
                // Reflection: the flags and the retry count are state a circle choice resets; the case starts the update from a late stage of one.
                Set(ability, "m_reachedTopSpeed", true);
                Set(ability, "m_pumpTheBrakes", true);
                Set(ability, "m_invertDirection", false);
                Set(ability, "m_recursionCount", 7);
                Tick(ability, 0.1f);
                Assert.That((Get<bool>(ability, "m_reachedTopSpeed"), Get<bool>(ability, "m_pumpTheBrakes")), Is.EqualTo((false, false)));
                Assert.That(Get<bool>(ability, "m_invertDirection"), Is.True);
                Assert.That(Get<int>(ability, "m_recursionCount"), Is.EqualTo(1));
                Assert.That(Get<Vector3>(ability, "m_bossEntryPoint"), Is.EqualTo(new Vector3(3f, 1f, 4f)));

                BossAbility_MoveRandom retry = NewCruising(owned, boss, d =>
                {
                    d.DistanceAllowedFromOrigin = 11f;
                    d.EndDistanceTolerance = 0f;
                });
                // Reflection: the recursion count is only reached by 101 target choices inside one circle setup.
                Set(retry, "m_recursionCount", 101);
                Set(retry, "m_invertDirection", false);
                Tick(retry, 0.1f);
                Assert.That(Get<bool>(retry, "m_invertDirection"), Is.True, "more than 100 target choices set up a new circle");
                Assert.That(Get<int>(retry, "m_recursionCount"), Is.EqualTo(1));
            });
        }

        // 0x6FB1EA: the ability finishes when the boss lies beyond the allowed distance from the origin (compared squared) and the target
        // is even farther out than the boss.
        [Test]
        public void MoveRandomFinishesOnceTheBossAndItsTargetAreBeyondTheAllowedDistance()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, new Vector3(50f, 0f, 0f));
                BossAbility_MoveRandom outward = NewCruising(owned, boss, d => d.DistanceAllowedFromOrigin = 10f);
                // Reflection: Enter draws the circle centre at random; the case needs fixed centres inside and outside the allowed distance.
                Set(outward, "m_centerOfCircle", new Vector3(100f, 0f, 0f));
                Set(outward, "m_radiusOfCircle", 5f);
                Tick(outward, 0.1f);
                Assert.That(outward.AbilityFinished, Is.True, "the boss (2500) is beyond 100 and the target (about 105) is farther out");

                BossAbility_MoveRandom inward = NewCruising(owned, boss, d => d.DistanceAllowedFromOrigin = 10f);
                // Reflection: as above, a fixed circle centre.
                Set(inward, "m_centerOfCircle", new Vector3(10f, 0f, 0f));
                Set(inward, "m_radiusOfCircle", 5f);
                Tick(inward, 0.1f);
                Assert.That(inward.AbilityFinished, Is.False, "the target is nearer the origin than the boss");

                BossAbility_MoveRandom inside = NewCruising(owned, NewBoss(owned, new Vector3(5f, 0f, 0f)), d => d.DistanceAllowedFromOrigin = 10f);
                // Reflection: as above, a fixed circle centre.
                Set(inside, "m_centerOfCircle", new Vector3(100f, 0f, 0f));
                Tick(inside, 0.1f);
                Assert.That(inside.AbilityFinished, Is.False, "the boss is within the allowed distance");
            });
        }
    }
}
