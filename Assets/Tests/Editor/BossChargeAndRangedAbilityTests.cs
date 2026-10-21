using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // BossAbility_BossFourmenChargeAttack (020001dd, 0x6F8FD0..0x6F99A8) and BossAbility_RangedAttack (020001ec, 0x6FE0F0..0x6FE4D0).
    // Expected values come from the x86-64 bodies cited per case; every ability is built by its real constructor and initialised
    // against a real Boss on an owned inactive host. Not covered: the line-of-sight wind-up (the cone test needs the physics scene) and
    // the debug helper of the ranged attack, which is compiled out of retail.
    public sealed class BossChargeAndRangedAbilityTests
    {
        // ---- BossAbility_BossFourmenChargeAttack ----

        private static BossAbility_BossFourmenChargeAttack NewCharge(Owned owned, Boss boss) =>
            NewAbility<BossAbility_BossFourmenChargeAttack>(boss, NewDefinition<BossAbilityDefinition_ChargeAttack>(owned, d =>
            {
                d.AccelerationTime = 1f;
                d.TopSpeed = 10f;
                d.AccelerationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                d.DecelerationTime = 1f;
                d.DecelerationCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                d.DistanceAllowedFromOrigin = 30f;
            }));

        // 06000a65..06000a69 (the property bodies): the definition supplies the tilt, face-angle tolerance and turn speed; the charge is
        // momentum based and does not turn to the character, and it can always start (06000a6f).
        [Test]
        public void ChargeReportsTheDefinitionsTurningAndCanAlwaysAttack()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, boss);
                Assert.That((ability.MovementAnimationAngleMultiplier, ability.FaceAngleTolerance, ability.TurnSpeed, ability.IsMomentum, ability.TurnToCharacter),
                    Is.EqualTo((15f, 1f, 10f, true, false)));
                Assert.That(ability.CanAttack(boss, null, null, 0f), Is.True);
                Assert.That(Get<float>(ability, "m_previousSquareMagnitude"), Is.EqualTo(float.PositiveInfinity));
                Assert.That(Get<float>(ability, "m_debugWindUpTime"), Is.EqualTo(1f));
                Assert.That(Get<Vector3>(ability, "ArenaCentre"), Is.EqualTo(Vector3.up));
                Assert.That(ability.DefinitionBase, Is.Not.Null);
            });
        }

        // 06000a70: the attack flow starts the wind-up as well; 06000a71/06000a72 wind up and wound up; 06000a73 is empty; 06000a74 skips
        // the preparation by setting both; 06000a75 is empty.
        [Test]
        public void ChargeFlagsFollowTheNativeStores()
        {
            Owned.Run(owned =>
            {
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, NewBoss(owned, Vector3.zero));
                Assert.That((ability.StartWindUp, ability.WoundUp, ability.IsTriggered), Is.EqualTo((false, false, false)));
                ability.SetAttackFlow(true);
                Assert.That((ability.StartWindUp, ability.WoundUp), Is.EqualTo((true, false)));
                Assert.That(Get<bool>(ability, "m_abilityActivated"), Is.True);
                Assert.That(Get<bool>(ability, "m_isWindingUp"), Is.True);
                ability.SetAttackFlow(false);
                Assert.That(Get<bool>(ability, "m_abilityActivated"), Is.False);
                Assert.That(ability.StartWindUp, Is.True, "the start request is raised even when the ability is not active");

                BossAbility_BossFourmenChargeAttack other = NewCharge(owned, NewBoss(owned, Vector3.zero));
                other.SetAttackWoundUp();
                Assert.That((other.WoundUp, Get<bool>(other, "m_isWindingUp")), Is.EqualTo((true, false)));
                other.SetAttackWindingUp();
                other.SetAttackAnimStarted();
                other.ResetProjectileGroupCounter();
                Assert.That(Get<bool>(other, "m_isWindingUp"), Is.True);

                BossAbility_BossFourmenChargeAttack skipped = NewCharge(owned, NewBoss(owned, Vector3.zero));
                skipped.SkipAttackPreparation(null);
                Assert.That((skipped.WoundUp, Get<bool>(skipped, "m_isWindingUp")), Is.EqualTo((true, true)));
            });
        }

        // 06000a6c: an ability that has not been activated does nothing. 06000a77: while winding up before the debug wind-up second
        // has passed, the wind-up timer runs and the boss aims at the character: the target and the charge direction are the unit
        // vector from the boss to the character. Without a current character the aim is left alone.
        [Test]
        public void ChargeWindUpAimsAtTheCharacterWhileTheDebugWindUpRuns()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, boss);
                ability.Enter();
                ability.Update(0.25f);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero), "not activated: nothing happens");
                Assert.That(Get<float>(ability, "m_debugWindUpTimer"), Is.Zero);

                ability.SetAttackFlow(true);
                ability.Update(0.25f);
                Assert.That(Get<float>(ability, "m_debugWindUpTimer"), Is.EqualTo(0.25f));
                Assert.That(ability.StartWindUp, Is.False, "winding up drops the start request");
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero), "no character yet");

                MakeCurrent(manager, character, new Vector3(0f, 0f, 10f));
                ability.Update(0.25f);
                Assert.That(Get<float>(ability, "m_debugWindUpTimer"), Is.EqualTo(0.5f));
                Assert.That(Get<Vector3>(ability, "m_playerTargetPosition"), Is.EqualTo(new Vector3(0f, 0f, 10f)));
                AssertNear(ability.CurrentTargetPosition, new Vector3(0f, 0f, 1f), "aim");
                AssertNear(Get<Vector3>(ability, "m_normalisedDirection"), new Vector3(0f, 0f, 1f), "charge direction");
                Assert.That(ability.AbilityFinished, Is.False);

                boss.SetWorldPosition(new Vector3(3f, 0f, 4f));
                MakeCurrent(manager, character, new Vector3(3f, 0f, 14f));
                ability.Update(0.25f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(0f, 0f, 1f), "aim from the new position");
            });
        }

        // One update while winding up aims at the character at (0, 0, 10) (06000a6c), then the wind-up is wound up.
        private static void Wound(BossAbility_BossFourmenChargeAttack ability, CharacterManager manager, Character character)
        {
            ability.Enter();
            ability.SetAttackFlow(true);
            MakeCurrent(manager, character, new Vector3(0f, 0f, 10f));
            ability.Update(0.25f);
            ability.SetAttackWoundUp();
        }

        // 06000a6d: the charge accelerates along the curve while the boss is not past the aimed position; the target is the charge
        // direction times the curve speed. Passing the aimed position (the distance grows) starts the deceleration, which runs for
        // the deceleration time along its curve and then strikes and ends the attack.
        [Test]
        public void ChargeAcceleratesToThePlayerPositionThenDeceleratesAndEnds()
        {
            WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<MessageManager>();
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, boss);
                Wound(ability, manager, character);

                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.True, "the first attack tick raises the animation trigger");
                AssertNear(ability.CurrentTargetPosition, new Vector3(0f, 0f, 5f), "curve 0.5 * top speed 10");
                Assert.That(Get<float>(ability, "m_previousSquareMagnitude"), Is.EqualTo(100f));
                Assert.That(ability.AbilityFinished, Is.False);

                boss.SetWorldPosition(new Vector3(0f, 0f, 10f));
                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, new Vector3(0f, 0f, 10f), "full speed at one second");
                Assert.That(Get<float>(ability, "m_previousSquareMagnitude"), Is.Zero);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.False);

                boss.SetWorldPosition(new Vector3(0f, 0f, 12f));
                ability.Update(0.5f);
                Assert.That(Get<bool>(ability, "m_reachedTarget"), Is.True, "4 > 0: the aimed position has been passed");
                AssertNear(ability.CurrentTargetPosition, new Vector3(0f, 0f, 5f), "deceleration curve 0.5 * 10");
                Assert.That(ability.AbilityFinished, Is.False);

                ability.Update(0.5f);
                AssertNear(ability.CurrentTargetPosition, Vector3.zero, "deceleration curve 0");
                Assert.That(Get<float>(ability, "m_decelerationTimer"), Is.EqualTo(1f));
                Assert.That(ability.AbilityFinished, Is.False);

                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.True, "the deceleration time has passed: the strike ends the attack");
                Assert.That(Stored<BossEffectTriggerTypes>(boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.Zero);
            });
        }

        // 06000a6d: past the allowed distance from the arena centre (the squared distance is compared, strictly) and heading away from
        // it (the forward direction against the direction to the centre, strictly) the charge stops: the target is zero and the strike
        // fires. Heading towards the centre, or within the distance, it carries on.
        [Test]
        public void ChargeStopsWhenItLeavesTheAllowedDistanceHeadingOutward()
        {
            WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<MessageManager>();
                Boss outward = NewBoss(owned, new Vector3(0f, 1f, 30f));
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, outward);
                Wound(ability, manager, character);
                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.False, "30 from the centre is not beyond 30");

                outward.SetWorldPosition(new Vector3(0f, 1f, 31.5f));
                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.True);
                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Stored<BossEffectTriggerTypes>(outward, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));

                Boss inward = NewBoss(owned, new Vector3(0f, 1f, 40f));
                SetActorProperty(inward, "ForwardDirection", new Vector3(0f, 0f, -1f));
                BossAbility_BossFourmenChargeAttack returning = NewCharge(owned, inward);
                Wound(returning, manager, character);
                returning.Update(0.5f);
                Assert.That(returning.AbilityFinished, Is.False, "heading towards the centre it carries on");
                Assert.That(Has(inward, BossFSMKeys.BossEffectType), Is.False);
            });
        }

        // 06000a6b: leaving clears the activation, the flags, the timers and sets the previous distance back to infinity.
        [Test]
        public void ChargeLeaveResetsTheChargeState()
        {
            WithManager((owned, manager, character) =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                BossAbility_BossFourmenChargeAttack ability = NewCharge(owned, boss);
                Wound(ability, manager, character);
                ability.Update(0.5f);
                Set(ability, "m_reachedTarget", true);
                Set(ability, "m_decelerationTimer", 0.75f);
                Set(ability, "m_debugWindUpTimer", 0.5f);
                ability.Leave();
                Assert.That((ability.AbilityFinished, ability.IsTriggered, ability.StartWindUp, ability.WoundUp), Is.EqualTo((false, false, false, false)));
                Assert.That((Get<bool>(ability, "m_abilityActivated"), Get<bool>(ability, "m_isWindingUp"), Get<bool>(ability, "m_reachedTarget")),
                    Is.EqualTo((false, false, false)));
                Assert.That((Get<float>(ability, "m_attackAnimationTimer"), Get<float>(ability, "m_decelerationTimer"), Get<float>(ability, "m_debugWindUpTimer")),
                    Is.EqualTo((0f, 0f, 0f)));
                Assert.That(Get<float>(ability, "m_previousSquareMagnitude"), Is.EqualTo(float.PositiveInfinity));
            });
        }

        // ---- BossAbility_RangedAttack ----

        private static void SetTarget(Boss boss, Transform target) =>
            typeof(Boss).GetProperty("Target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetSetMethod(true).Invoke(boss, new object[] { target });

        private static BossAbility_RangedAttack NewRanged(Owned owned, Boss boss, System.Action<BossAbilityDefinition_RangedAttack> setup = null) =>
            NewAbility<BossAbility_RangedAttack>(boss, NewDefinition<BossAbilityDefinition_RangedAttack>(owned, d =>
            {
                d.ProjectileCount = 2;
                d.WaveCount = 2;
                d.WaveCooldownSeconds = 1f;
                d.ProjectileCooldownSeconds = 0.5f;
                d.MinRange = 3f;
                d.MaxRange = 7f;
                setup?.Invoke(d);
            }));

        // 06000b0d..06000b11: the ranges are the definition's raw ranges (not squared, despite the names); the tolerance is a get-only
        // auto-property nothing assigns; the follow-up range is a plain property.
        [Test]
        public void RangedAttackReportsTheDefinitionsRangesAndNoTolerance()
        {
            Owned.Run(owned =>
            {
                BossAbility_RangedAttack ability = NewRanged(owned, NewBoss(owned, Vector3.zero));
                Assert.That((ability.MaxRangeSquared, ability.MinRangeSquared), Is.EqualTo((7f, 3f)));
                Assert.That(ability.DestinationReachedToleranceSquared, Is.Zero);
                ability.MaxFollowUpRangeSquared = 9f;
                Assert.That(ability.MaxFollowUpRangeSquared, Is.EqualTo(9f));
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(ability.DefinitionBase, Is.Not.Null);
            });
        }

        // 06000b15: shots are taken at the boss's target while the projectile cooldown (strict) allows; a wave of ProjectileCount shots
        // is followed by the wave cooldown (the wave cooldown wins over the projectile one) and the ability finishes when the last wave
        // has been fired.
        [Test]
        public void RangedAttackFiresWavesOfShotsSeparatedByTheCooldowns()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                SetTarget(boss, owned.Host("Lucid ranged target", true).transform);
                boss.Target.position = new Vector3(4f, 5f, 6f);
                BossAbility_RangedAttack ability = NewRanged(owned, boss);
                ability.Enter();

                var steps = new (float delta, bool shot)[]
                {
                    (0.25f, true), (0.25f, false), (0.25f, true), (0.25f, false), (0.25f, false), (0.5f, false), (0.5f, true), (0.5f, true), (0.5f, false),
                };
                var wave = new[] { 0, 0, 0, 0, 1, 1, 1, 1, 2 };
                var shots = new[] { 1, 1, 2, 2, 0, 0, 1, 2, 0 };
                for (int i = 0; i < steps.Length; i++)
                {
                    ability.Update(steps[i].delta);
                    string label = "step " + i;
                    Assert.That(ability.IsTriggered, Is.EqualTo(steps[i].shot), label + " shot");
                    Assert.That(Get<int>(ability, "m_activeWave"), Is.EqualTo(wave[i]), label + " wave");
                    Assert.That(Get<int>(ability, "m_shotsTakenInWave"), Is.EqualTo(shots[i]), label + " shots in the wave");
                    if (steps[i].shot)
                        Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)), label + " aims at the target");
                    Assert.That(ability.AbilityFinished, Is.EqualTo(i == steps.Length - 1), label + " finished");
                }

                ability.Update(1f);
                Assert.That(ability.IsTriggered, Is.False, "a finished ability takes no more shots");
                Assert.That(Get<int>(ability, "m_activeWave"), Is.EqualTo(2));
            });
        }

        // 06000b15: every shot is offset by a random value of at most the accuracy on each axis.
        [Test]
        public void RangedAttackSpreadsEachShotWithinTheAccuracy()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                SetTarget(boss, owned.Host("Lucid ranged target", true).transform);
                boss.Target.position = new Vector3(4f, 5f, 6f);
                BossAbility_RangedAttack ability = NewRanged(owned, boss, d => d.ProjectileAccuracy = new Vector3(1f, 2f, 3f));
                var offsets = new List<Vector3>();
                for (int i = 0; i < 30; i++)
                {
                    ability.Leave();
                    ability.Enter();
                    ability.Update(0.25f);
                    Assert.That(ability.IsTriggered, Is.True);
                    Vector3 offset = ability.CurrentTargetPosition - new Vector3(4f, 5f, 6f);
                    Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(1f + 1e-5f));
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(2f + 1e-5f));
                    Assert.That(Mathf.Abs(offset.z), Is.LessThanOrEqualTo(3f + 1e-5f));
                    offsets.Add(offset);
                }
                Assert.That(offsets.Exists(offset => Mathf.Abs(offset.x) > 1e-4f), Is.True, "the spread is random on X");
                Assert.That(offsets.Exists(offset => Mathf.Abs(offset.y) > 1e-4f), Is.True, "the spread is random on Y");
                Assert.That(offsets.Exists(offset => Mathf.Abs(offset.z) > 1e-4f), Is.True, "the spread is random on Z");
            });
        }

        // 06000b14: leaving clears the finished flag, the shot flag, both timers, the wave and shot counts and both cooldown flags.
        [Test]
        public void RangedAttackLeaveResetsTheWaves()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                SetTarget(boss, owned.Host("Lucid ranged target", true).transform);
                BossAbility_RangedAttack ability = NewRanged(owned, boss);
                ability.Enter();
                foreach (float delta in new[] { 0.25f, 0.25f, 0.25f, 0.25f, 0.25f })
                    ability.Update(delta);
                Assert.That(Get<bool>(ability, "m_waveOnCooldown"), Is.True);
                ability.Leave();
                Assert.That((ability.AbilityFinished, ability.IsTriggered, Get<bool>(ability, "m_waveOnCooldown"), Get<bool>(ability, "m_projectileOnCooldown")),
                    Is.EqualTo((false, false, false, false)));
                Assert.That((Get<float>(ability, "m_projectileTimerInSeconds"), Get<float>(ability, "m_waveTimerInSeconds")), Is.EqualTo((0f, 0f)));
                Assert.That((Get<int>(ability, "m_activeWave"), Get<int>(ability, "m_shotsTakenInWave")), Is.EqualTo((0, 0)));
            });
        }
    }
}
