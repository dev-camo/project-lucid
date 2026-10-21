using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss graph transitions: expected values come from the 1.10.1 x86-64 bodies cited per case. Each transition is
    // built from its JSON through the node factory and updated against a real Boss on an owned inactive host. The boss
    // gets the FSM storage Actor.Awake would create (an inactive host runs no Awake); its attach points are the real
    // BossBodyComponents records, initialised and hit through their own methods.
    public sealed class BossTransitionTests
    {
        // Fixture values, not original constants: any two distinct ability types work for a boss that owns its dictionary.
        private const ActorAbilityType ConfiguredAbility = (ActorAbilityType)0x4C554346;
        private const ActorAbilityType OtherAbility = (ActorAbilityType)0x4C554347;

        private static readonly (string, int)[] NoFields = new (string, int)[0];

        private static string Name() => "Lucid-boss-transition-" + Guid.NewGuid().ToString("N");

        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static IFSMTransition Build(string node, string json = "") =>
            FSMClassFactory.ConstructTransition(Machine(), node, new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, Boss boss, float deltaTime = 0.25f) =>
            transition.Update(boss, new FSMUpdateContext(deltaTime, FSMUpdateType.FixedUpdate));

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

        private static void WithBoss(Action<Owned, Boss> body) => Owned.Run(owned =>
        {
            Boss boss = owned.Host("Lucid boss transition boss", false).AddComponent<Boss>();
            typeof(Actor).GetProperty(nameof(Actor.Storage)).GetSetMethod(true).Invoke(boss, new object[] { new FSMStorage(0) });
            body(owned, boss);
        });

        // One of the boss's attach points, as the initialised record Boss.Initialise would add.
        private static void AddBodyPart(Boss boss, ActorAttachPointType point, bool destroyed)
        {
            Type part = typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic);
            object record = Activator.CreateInstance(part);
            part.GetField("AttachPoint").SetValue(record, point);
            part.GetMethod("Initialise").Invoke(record, null);
            if (destroyed)
            {
                part.GetMethod("RegisterHit").Invoke(record, null);
            }
            ((IDictionary)Get<object>(boss, "m_attachPointDictionary")).Add(point, record);
        }

        private static readonly Regex ActorDefinitionSettingsFault =
            new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        // Boss.RegisterBoss (060009ec) is the callback Awake hands the BossManager reference; an inactive host runs no Awake,
        // so it is invoked here as that reference would. Register (06000b73) reads the definition's health pool, and the
        // first listed boss switches HUD focus through the App, which these cases do not have, so a second boss is already
        // listed. CreateInstance sends Awake and OnEnable, which each reach ActorDefinition.UpdateCachedValues (0x7A8060)
        // with no Settings: two faults.
        private static void RegisterWithManager(Owned owned, Boss boss, BossManager manager, int healthPool)
        {
            for (int i = 0; i < 2; i++) LogAssert.Expect(LogType.Exception, ActorDefinitionSettingsFault);
            var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
            definition.Traits = new BossTraits { HealthPool = healthPool };
            Set(boss, "m_definition", definition); // Reflection: serialized field without a setter
            manager.Bosses.Add(owned.Host("Lucid boss transition placeholder", false).AddComponent<Boss>());
            Call(boss, "RegisterBoss", manager);
        }

        // RestoreAllBossHealth (060009ee) sets the current health to the definition's health pool.
        private static void SetHealth(Boss boss, int health)
        {
            boss.Definition.Traits.HealthPool = health;
            boss.RestoreAllBossHealth();
        }

        private static bool Has(Boss boss, GraphStorageKey key) => boss.Storage.GetCollection().ContainsKey(key);

        private static T Stored<T>(Boss boss, GraphStorageKey key) => boss.Storage.GetValue(key, default(T), false);

        private class PlainAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }

        private sealed class OffensiveAbility : PlainAbility, IOffensiveAbility
        {
            public bool StartWindUp { get; set; }
            public bool WoundUp { get; set; }
            public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime) => false;
            public void SetAttackFlow(bool abilityActive) { }
            public void SetAttackWindingUp() { }
            public void SetAttackWoundUp() { }
            public void SetAttackAnimStarted() { }
            public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition) { }
            public void ResetProjectileGroupCounter() { }
        }

        // ---- BossTransition_AttackWindUpComplete (02000234) and BossTransition_StartWindUp (0200024e) ----

        // 0x70E0B0 reads IOffensiveAbility slot 1 (get_WoundUp) and 0x710FA0 slot 0 (get_StartWindUp) of the boss's ability
        // of the configured type (Actor.TryGetAbility, 0x7C7460); a missing ability, or one that is not offensive, is false.
        [Test]
        public void WindUpTransitionsReadDifferentFlagsOfTheConfiguredOffensiveAbility()
        {
            WithBoss((owned, boss) =>
            {
                var abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(boss, "m_abilities");
                IFSMTransition complete = Build(nameof(BossTransition_AttackWindUpComplete), AbilityJson(ConfiguredAbility));
                IFSMTransition start = Build(nameof(BossTransition_StartWindUp), AbilityJson(ConfiguredAbility));

                Assert.That((Update(complete, boss), Update(start, boss)), Is.EqualTo((false, false)), "no ability");
                abilities[OtherAbility] = new OffensiveAbility { StartWindUp = true, WoundUp = true };
                Assert.That((Update(complete, boss), Update(start, boss)), Is.EqualTo((false, false)), "only another type is registered");
                abilities[ConfiguredAbility] = new PlainAbility();
                Assert.That((Update(complete, boss), Update(start, boss)), Is.EqualTo((false, false)), "not an offensive ability");
                abilities[ConfiguredAbility] = new OffensiveAbility { StartWindUp = true, WoundUp = false };
                Assert.That((Update(complete, boss), Update(start, boss)), Is.EqualTo((false, true)), "winding up, not wound up");
                abilities[ConfiguredAbility] = new OffensiveAbility { StartWindUp = false, WoundUp = true };
                Assert.That((Update(complete, boss), Update(start, boss)), Is.EqualTo((true, false)), "wound up, not winding up");
            });
        }

        // ---- BossTransition_BodyBroken (02000235) ----

        // 0x70E290: false until the crab body's vulnerable point (0xD4AE0E5A) is destroyed; then the stored phase wait
        // (PhaseTransitionWait) gains the update's delta (AdjustValue, 0x1AEE740) and the result is wait > delay with ucomiss/seta,
        // so a wait equal to the delay does not fire. A wait that was never stored starts from zero.
        [Test]
        public void BodyBrokenAccumulatesThePhaseWaitOnlyWhileTheBodyPointIsDestroyed()
        {
            WithBoss((owned, boss) =>
            {
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_Body_VulnerablePoint, false);
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, true);
                IFSMTransition transition = Build(nameof(BossTransition_BodyBroken), "{\"DelayTransitionFiringTime\":0.5}");
                Assert.That(Get<float>(transition, "m_delayTransitionFiringTime"), Is.EqualTo(0.5f));

                Assert.That(Update(transition, boss), Is.False, "the body point is intact; a destroyed claw point does not count");
                Assert.That(Has(boss, BossFSMKeys.PhaseTransitionWait), Is.False, "the wait does not advance");

                var part = ((IDictionary)Get<object>(boss, "m_attachPointDictionary"))[ActorAttachPointType.Boss_Crab_Body_VulnerablePoint];
                part.GetType().GetMethod("RegisterHit").Invoke(part, null);
                Assert.That(Update(transition, boss), Is.False, "0.25 of 0.5");
                Assert.That(Update(transition, boss), Is.False, "a wait equal to the delay does not fire");
                Assert.That(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait), Is.EqualTo(0.5f));
                Assert.That(Update(transition, boss), Is.True, "0.75 of 0.5");
                Assert.That(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait), Is.EqualTo(0.75f));
            });
        }

        // 0x70E3D0 stores 0 into PhaseTransitionWait (SetValue<float>, xorps xmm0) and chains to the base hook. 0x70E570
        // stores the default BossAttackTypes (None) through the boss's own storage and then jumps to 0x1ACDA60, the base
        // DoOnEnter hook, rather than the DoOnLeave hook at 0x1AC78F0: both are empty, so the leave leaves the wait alone.
        [Test]
        public void BodyBrokenEnterResetsTheWaitAndLeaveClearsTheStoredAttackOnly()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_BodyBroken), "{\"DelayTransitionFiringTime\":1}");
                boss.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 3f);
                boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.BossCrab_Body);

                transition.OnEnter(boss, null);
                Assert.That(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait), Is.Zero);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_Body), "enter keeps the attack");

                boss.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 3f);
                transition.OnLeave(boss, null);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.None));
                Assert.That(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait), Is.EqualTo(3f), "leave keeps the wait");
            });
        }

        // The leave calls the base DoOnEnter, not DoOnLeave (see the case above); the compiled IL shows which.
        [Test]
        public void BodyBrokenLeaveChainsToTheBaseEnterHook()
        {
            Assert.That(CallNames(typeof(BossTransition_BodyBroken), "DoOnLeave"), Is.EqualTo(new[]
            {
                "IGraphUserExtensions.GetAs", "Actor.get_Storage", "IGraphStorage.SetValue", "FSMTransition.DoOnEnter",
            }));
            Assert.That(CallNames(typeof(BossTransition_BodyBroken), "DoOnEnter"), Is.EqualTo(new[]
            {
                "IGraphUser.get_Storage", "IGraphStorage.SetValue", "FSMTransition.DoOnEnter",
            }));
        }

        // ---- BossTransition_BothClawsBroken (0200023b) ----

        // 0x70EFA0 calls IsComponentDestroyed for the left claw point (0x1B78064A) and then the right (0xE083E777) before it
        // tests either, then accumulates the phase wait only when both are destroyed (same wait > delay as above).
        [Test]
        public void BothClawsBrokenNeedsBothClawPointsDestroyed()
        {
            foreach ((bool left, bool right, bool fires) in new[] { (false, false, false), (true, false, false), (false, true, false), (true, true, true) })
            {
                WithBoss((owned, boss) =>
                {
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, left);
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, right);
                    IFSMTransition transition = Build(nameof(BossTransition_BothClawsBroken), "{\"DelayTransitionFiringTime\":0.125}");
                    Assert.That(Get<float>(transition, "m_delayTransitionFiringTime"), Is.EqualTo(0.125f));

                    Assert.That(Update(transition, boss, 0.125f), Is.False, left && right ? $"{left}/{right}: a wait equal to the delay does not fire" : $"{left}/{right}: a claw point is intact");
                    Assert.That(Update(transition, boss, 0.125f), Is.EqualTo(fires), $"{left}/{right}");
                    Assert.That(Has(boss, BossFSMKeys.PhaseTransitionWait), Is.EqualTo(left && right), $"{left}/{right}: the wait advances only for both");
                });
            }
        }

        // The right point is read even when the left one already rules the transition out: a boss without a right claw
        // point faults on the dictionary lookup (0x70F022 precedes the tests at 0x70F027).
        [Test]
        public void BothClawsBrokenReadsTheRightClawPointEvenWhenTheLeftIsIntact()
        {
            WithBoss((owned, boss) =>
            {
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, false);
                IFSMTransition transition = Build(nameof(BossTransition_BothClawsBroken), "{\"DelayTransitionFiringTime\":0}");
                Assert.Throws<KeyNotFoundException>(() => Update(transition, boss));
            });
        }

        // 0x70F110: enter stores 0 into the phase wait and chains to the base hook.
        [Test]
        public void BothClawsBrokenEnterResetsThePhaseWait()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_BothClawsBroken), "{\"DelayTransitionFiringTime\":0}");
                boss.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 2f);
                transition.OnEnter(boss, null);
                Assert.That(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait), Is.Zero);
            });
        }

        // ---- BossTransition_ClearLineOfSight (0200023d) ----

        // 0x70F2C0 copies MaxDistanceToCheck and ConeOfViewAngle (one 8 byte move into +0x20/+0x24) and never writes the
        // field at +0x1C, m_preAttackWaitSeconds.
        [Test]
        public void ClearLineOfSightKeepsItsRangeAndConeAndNeverSetsThePreAttackWait()
        {
            IFSMTransition transition = Build(nameof(BossTransition_ClearLineOfSight), "{\"MaxDistanceToCheck\":12.5,\"ConeOfViewAngle\":30}");
            Assert.That(Get<float>(transition, "m_maxDistanceToCheck"), Is.EqualTo(12.5f));
            Assert.That(Get<float>(transition, "m_coneOfViewAngle"), Is.EqualTo(30f));
            Assert.That(Get<float>(transition, "m_preAttackWaitSeconds"), Is.Zero);
        }

        // 0x70F390 ends in a tail call of Boss.HasLineOfSightOnCharacter (0x6F5CD0), whose first answer without a character
        // manager is false. The line of sight itself needs a character and the physics scene, which this case does not build.
        [Test]
        public void ClearLineOfSightIsFalseWhenTheBossHasNoCharacterManager()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_ClearLineOfSight), "{\"MaxDistanceToCheck\":100,\"ConeOfViewAngle\":0}");
                Assert.That(Update(transition, boss), Is.False);
            });
        }

        // 0x70F390: the base update runs first and its result is dropped (no test follows the call at 0x70F3CA); the boss's
        // check then gets the range (+0x20), esi = 1 for fullConeMustBeClear and the cone (+0x24), in that order.
        [Test]
        public void ClearLineOfSightDropsTheBaseResultAndPassesRangeFullConeAndCone()
        {
            MethodInfo update = typeof(BossTransition_ClearLineOfSight).GetMethod("DoUpdate", Declared);
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(update);
            Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[]
            {
                "FSMTransition.DoUpdate", "IGraphUserExtensions.GetAs", "Boss.HasLineOfSightOnCharacter",
            }));
            List<(int Offset, OpCode Code)> il = OriginalILCallSequence.Instructions(update);
            Assert.That(il[il.FindIndex(i => i.Offset == calls[0].Offset) + 1].Code, Is.EqualTo(OpCodes.Pop), "the base result is dropped");
            byte[] bytes = update.GetMethodBody().GetILAsByteArray();
            var loads = il.Where(i => i.Code == OpCodes.Ldfld)
                .Select(i => (i.Offset, update.Module.ResolveField(BitConverter.ToInt32(bytes, i.Offset + 1)).Name)).ToList();
            Assert.That(loads.Select(l => l.Item2), Is.EqualTo(new[] { "m_maxDistanceToCheck", "m_coneOfViewAngle" }));
            Assert.That(il.Where(i => i.Offset > loads[0].Offset && i.Offset < loads[1].Offset && i.Code == OpCodes.Ldc_I4_1).Count(), Is.EqualTo(1),
                "true is loaded between the range and the cone");
        }

        // ---- BossTransition_ConsumeBossManagerFlag (0200023f) ----

        // 0x70F490 tail-calls Boss.ManagerHasFlagForPhaseContinuation (0x6F4370) on the boss's manager: the flag the manager
        // holds is reported once and then cleared.
        [Test]
        public void ConsumeBossManagerFlagReportsTheManagersPhaseFlagOnce()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<SaveManager>();
                BossManager manager = owned.Host("Lucid boss transition manager", false).AddComponent<BossManager>();
                Boss boss = owned.Host("Lucid boss transition boss", false).AddComponent<Boss>();
                RegisterWithManager(owned, boss, manager, 1);
                IFSMTransition transition = Build(nameof(BossTransition_ConsumeBossManagerFlag));

                Assert.That(Update(transition, boss), Is.False, "no flag");
                manager.SetPhaseContinue();
                Assert.That(Update(transition, boss), Is.True, "the flag is set");
                Assert.That(Update(transition, boss), Is.False, "reading it cleared it");
            });
        }

        // ---- BossTransition_ExitedAttackAndVulnerableAnimation (02000241) ----

        // 0x70F860 reads AttackAndVulnerableAnimationExited as GetValue<bool>(key, false, true): a missing flag is stored
        // as false. 0x70F9E0 chains to the base leave hook first and then stores false under the same key.
        [Test]
        public void ExitedAnimationReadsTheStoredFlagAndLeaveClearsIt()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_ExitedAttackAndVulnerableAnimation));
                Assert.That(Update(transition, boss), Is.False);
                Assert.That(Has(boss, BossFSMKeys.AttackAndVulnerableAnimationExited), Is.True, "the default is stored");
                boss.Storage.SetValue(BossFSMKeys.AttackAndVulnerableAnimationExited, true);
                boss.Storage.SetValue(BossFSMKeys.WasHit, true);
                Assert.That(Update(transition, boss), Is.True);
                transition.OnLeave(boss, null);
                Assert.That(Update(transition, boss), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.WasHit), Is.True, "leave touches only its own flag");
            });
        }

        // ---- BossTransition_HealthCheck (02000245) ----

        // 0x710170 copies Value and Evaluation (one 8 byte move). 0x710240 switches on Evaluation through the table at
        // 0x7102F0 (entries 0..4 reach setg, setge, setl, setle, sete) against Boss.CurrentHealth (+0x1DC); any other value
        // is false.
        [Test]
        public void HealthCheckComparesTheBossHealthWithTheConfiguredValue()
        {
            (int comparison, bool below, bool equal, bool above)[] table =
            {
                (0, false, false, true),   // GreaterThan
                (1, false, true, true),    // GreaterThanEqual
                (2, true, false, false),   // LessThan
                (3, true, true, false),    // LessThanEqual
                (4, false, true, false),   // Equal
                (5, false, false, false),  // outside the enumeration
                (-1, false, false, false),
            };
            WithBoss((owned, boss) =>
            {
                owned.LeaseRow<SaveManager>();
                RegisterWithManager(owned, boss, owned.Host("Lucid boss transition manager", false).AddComponent<BossManager>(), 1);
                foreach ((int comparison, bool below, bool equal, bool above) in table)
                {
                    IFSMTransition transition = Build(nameof(BossTransition_HealthCheck), "{\"Value\":5,\"Evaluation\":" + comparison + "}");
                    Assert.That(Get<int>(transition, "m_evalutionValue"), Is.EqualTo(5));
                    Assert.That((int)Get<FSMTransitionCheckInt.Comparison>(transition, "m_evalutionMethod"), Is.EqualTo(comparison));
                    SetHealth(boss, 4);
                    Assert.That(Update(transition, boss), Is.EqualTo(below), "comparison " + comparison + ", health 4");
                    SetHealth(boss, 5);
                    Assert.That(Update(transition, boss), Is.EqualTo(equal), "comparison " + comparison + ", health 5");
                    SetHealth(boss, 6);
                    Assert.That(Update(transition, boss), Is.EqualTo(above), "comparison " + comparison + ", health 6");
                }
            });
        }

        // 0x710310 only forwards to the base leave hook.
        [Test]
        public void HealthCheckLeaveChangesNothing()
        {
            WithBoss((owned, boss) =>
            {
                boss.Storage.SetValue(BossFSMKeys.WasHit, true);
                int before = boss.Storage.GetCollection().Count;
                Build(nameof(BossTransition_HealthCheck), "{\"Value\":1,\"Evaluation\":0}").OnLeave(boss, null);
                Assert.That(boss.Storage.GetCollection().Count, Is.EqualTo(before));
                Assert.That(Stored<bool>(boss, BossFSMKeys.WasHit), Is.True);
            });
        }

        // ---- BossTransition_HitPlayer (02000247) ----

        // 0x7103A0 reads BossHitPlayer as GetValue<bool>(key, false, true). 0x710520 chains to the base leave hook and
        // then stores false into BossHitPlayer and into ForceNotVulnerable, leaving CurrentlyVulnerable alone.
        [Test]
        public void HitPlayerReadsTheStoredFlagAndLeaveClearsBothFlags()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_HitPlayer));
                Assert.That(Update(transition, boss), Is.False);
                Assert.That(Has(boss, BossFSMKeys.BossHitPlayer), Is.True, "the default is stored");
                boss.Storage.SetValue(BossFSMKeys.BossHitPlayer, true);
                boss.Storage.SetValue(BossFSMKeys.ForceNotVulnerable, true);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, true);
                Assert.That(Update(transition, boss), Is.True);
                transition.OnLeave(boss, null);
                Assert.That(Stored<bool>(boss, BossFSMKeys.BossHitPlayer), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True);
            });
        }

        // ---- BossTransition_InRange (02000248) and BossTransition_NotInRange (0200024d) ----

        // 0x710830 returns GetValue<bool>(MoveToRange, false, true); 0x710D90 is the same call followed by xor al, 1.
        [Test]
        public void RangeTransitionsReadTheMoveToRangeFlagAndNegateOneOfThem()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition inRange = Build(nameof(BossTransition_InRange));
                IFSMTransition notInRange = Build(nameof(BossTransition_NotInRange));
                Assert.That((Update(inRange, boss), Update(notInRange, boss)), Is.EqualTo((false, true)), "no flag");
                Assert.That(Has(boss, BossFSMKeys.MoveToRange), Is.True, "the default is stored");
                boss.Storage.SetValue(BossFSMKeys.MoveToRange, true);
                Assert.That((Update(inRange, boss), Update(notInRange, boss)), Is.EqualTo((true, false)));
                boss.Storage.SetValue(BossFSMKeys.MoveToRange, false);
                Assert.That((Update(inRange, boss), Update(notInRange, boss)), Is.EqualTo((false, true)));
            });
        }

        // ---- BossTransition_IsActiveBoss (02000249) and BossTransition_IsNotActiveBoss (0200024c) ----

        // 0x710A10 loads the byte at Boss+0x208 (m_activeBossEntity); 0x710CD0 compares it with zero (sete).
        [Test]
        public void ActiveBossTransitionsFollowTheBossActiveEntityFlag()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition active = Build(nameof(BossTransition_IsActiveBoss));
                IFSMTransition notActive = Build(nameof(BossTransition_IsNotActiveBoss));
                Assert.That((Update(active, boss), Update(notActive, boss)), Is.EqualTo((false, true)));
                boss.SetActiveBossEntity();
                Assert.That((Update(active, boss), Update(notActive, boss)), Is.EqualTo((true, false)));
                boss.ClearActiveBossEntity();
                Assert.That((Update(active, boss), Update(notActive, boss)), Is.EqualTo((false, true)));
            });
        }

        // ---- BossTransition_IsActiveBossTimeout (0200024a) ----

        // 0x710A60 copies Timeout. 0x710B30 drops the base result, adds the delta to ActiveBossTimer (AdjustValue) and fires
        // when the total is greater than the timeout (ucomiss, seta): a total equal to it does not.
        [Test]
        public void ActiveBossTimeoutFiresOnceTheTimerPassesTheTimeout()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_IsActiveBossTimeout), "{\"Timeout\":0.5}");
                Assert.That(Get<float>(transition, "m_timeout"), Is.EqualTo(0.5f));
                Assert.That(Update(transition, boss), Is.False, "0.25 of 0.5");
                Assert.That(Update(transition, boss), Is.False, "a timer equal to the timeout does not fire");
                Assert.That(Update(transition, boss), Is.True, "0.75 of 0.5");
                Assert.That(Stored<float>(boss, BossFSMKeys.ActiveBossTimer), Is.EqualTo(0.75f));
            });
        }

        // The wait comparisons are ucomiss followed by seta (0x70E3B6, 0x70F0EA, 0x710C34): an unordered result (a NaN wait)
        // sets the carry flag and does not fire, which `!(wait <= delay)` would.
        [Test]
        public void TimerTransitionsDoNotFireOnANaNWait()
        {
            WithBoss((owned, boss) =>
            {
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_Body_VulnerablePoint, true);
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, true);
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, true);
                foreach (string node in new[] { nameof(BossTransition_BodyBroken), nameof(BossTransition_BothClawsBroken) })
                {
                    IFSMTransition transition = Build(node, "{\"DelayTransitionFiringTime\":0}");
                    Assert.That(Update(transition, boss, float.NaN), Is.False, node);
                    Assert.That(float.IsNaN(Stored<float>(boss, BossFSMKeys.PhaseTransitionWait)), Is.True, node);
                    boss.Storage.SetValue(BossFSMKeys.PhaseTransitionWait, 0f);
                }
                Assert.That(Update(Build(nameof(BossTransition_IsActiveBossTimeout), "{\"Timeout\":0}"), boss, float.NaN), Is.False);
            });
        }

        // ---- BossTransition_UsedClawCheck (0200024f) ----

        // 0x7110B0 copies Claw (+0x1C) and Deflated (+0x20). 0x711180 reads the stored BossAttackType, then tests the
        // configured claw against two constants: 0xD2F17A66 (BossCrab_RightClaw) reads the right claw point (0xE083E777) and
        // 0xBBCA983D (BossCrab_LeftClaw) the left (0x1B78064A); the result is "stored attack equals the claw" and "the
        // point's destroyed state equals Deflated". Any other claw is false without reading a point.
        [Test]
        public void UsedClawCheckMatchesTheConfiguredClawAttackAndDeflatedState()
        {
            BossAttackTypes[] claws = { BossAttackTypes.BossCrab_LeftClaw, BossAttackTypes.BossCrab_RightClaw };
            WithBoss((owned, boss) =>
            {
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, true);
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, false);
                foreach (BossAttackTypes claw in claws)
                {
                    bool destroyed = claw == BossAttackTypes.BossCrab_LeftClaw;
                    foreach (bool deflated in new[] { false, true })
                    {
                        IFSMTransition transition = Build(nameof(BossTransition_UsedClawCheck),
                            "{\"Claw\":" + (int)claw + ",\"Deflated\":" + (deflated ? "true" : "false") + "}");
                        Assert.That((int)Get<BossAttackTypes>(transition, "m_claw"), Is.EqualTo((int)claw));
                        Assert.That(Get<bool>(transition, "m_clawDeflated"), Is.EqualTo(deflated));
                        boss.Storage.SetValue(BossFSMKeys.BossAttackType, claw);
                        Assert.That(Update(transition, boss), Is.EqualTo(deflated == destroyed), $"{claw} deflated {deflated}");
                        boss.Storage.SetValue(BossFSMKeys.BossAttackType, claw == claws[0] ? claws[1] : claws[0]);
                        Assert.That(Update(transition, boss), Is.False, $"{claw} deflated {deflated}: the other claw attacked");
                        boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None);
                        Assert.That(Update(transition, boss), Is.False, $"{claw} deflated {deflated}: no attack");
                    }
                }
            });
        }

        // The configured claw's point is read before the attack is compared (0x711339 and 0x711356 precede the compare at
        // 0x711340 and 0x71135B), so a boss missing that point faults whatever attack is stored; any other claw reads nothing.
        [Test]
        public void UsedClawCheckReadsTheClawPointBeforeComparingTheAttackAndIgnoresOtherClaws()
        {
            WithBoss((owned, boss) =>
            {
                boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None);
                foreach (BossAttackTypes claw in new[] { BossAttackTypes.BossCrab_LeftClaw, BossAttackTypes.BossCrab_RightClaw })
                {
                    IFSMTransition transition = Build(nameof(BossTransition_UsedClawCheck), "{\"Claw\":" + (int)claw + ",\"Deflated\":false}");
                    Assert.Throws<KeyNotFoundException>(() => Update(transition, boss), claw.ToString());
                }
                foreach (BossAttackTypes other in new[] { BossAttackTypes.None, BossAttackTypes.BossCrab_Body, BossAttackTypes.BossFourmen_ChargeAttack })
                {
                    IFSMTransition transition = Build(nameof(BossTransition_UsedClawCheck), "{\"Claw\":" + (int)other + ",\"Deflated\":false}");
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, other);
                    Assert.That(Update(transition, boss), Is.False, other.ToString());
                }
            });
        }

        // ---- BossTransition_VulnerableTimeOut (02000251) ----

        // 0x711400 returns the negation (xor al, 1) of GetValue<bool>(CurrentlyVulnerable, false, true).
        [Test]
        public void VulnerableTimeOutFiresWhileTheBossIsNotVulnerable()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_VulnerableTimeOut));
                Assert.That(Update(transition, boss), Is.True, "no flag");
                Assert.That(Has(boss, BossFSMKeys.CurrentlyVulnerable), Is.True, "the default is stored");
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, true);
                Assert.That(Update(transition, boss), Is.False);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
                Assert.That(Update(transition, boss), Is.True);
            });
        }

        // ---- BossTransition_WasHit (02000252) ----

        // 0x7115E0 returns GetValue<bool>(WasHit, false, true); 0x711750 chains to the base leave hook and stores false.
        [Test]
        public void WasHitReadsTheStoredFlagAndLeaveClearsIt()
        {
            WithBoss((owned, boss) =>
            {
                IFSMTransition transition = Build(nameof(BossTransition_WasHit));
                Assert.That(Update(transition, boss), Is.False);
                Assert.That(Has(boss, BossFSMKeys.WasHit), Is.True, "the default is stored");
                boss.Stagger();
                Assert.That(Update(transition, boss), Is.True);
                transition.OnLeave(boss, null);
                Assert.That(Update(transition, boss), Is.False);
            });
        }

        // ---- Declarations ----

        private static List<string> CallNames(Type owner, string name) =>
            OriginalILCallSequence.Names(OriginalILCallSequence.Calls(owner.GetMethods(Declared).Single(m => m.Name == name))).ToList();

        private static Type Nested(Type owner, string name) => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        private sealed class Shape
        {
            public readonly Type Type;
            public readonly int TypeFlags;
            public readonly (string, int)[] Fields;
            public readonly (string, int)[] Methods;
            public readonly string[] Attributes;

            public Shape(Type type, int typeFlags, (string, int)[] fields, (string, int)[] methods, string[] attributes)
            {
                Type = type;
                TypeFlags = typeFlags;
                Fields = fields;
                Methods = methods;
                Attributes = attributes;
            }
        }

        // Type flags, declared fields and methods (metadata order, with their flags) and custom attribute order from the
        // DummyDll tables; Serializable on the argument classes is TypeDef flag 0x2000 and has no attribute row.
        private static readonly Shape[] Shapes =
        {
            new Shape(typeof(AnimatorSetBossStorageBool), 0x00100001,
                new[] { ("m_setOnEntry", 0x0001), ("m_fsmVarOnEntry", 0x0001), ("m_entryValue", 0x0001), ("m_setOnExit", 0x0001), ("m_fsmVarOnExit", 0x0001), ("m_exitValue", 0x0001), ("m_varNamesCreated", 0x0001), ("m_entryVarName", 0x0001), ("m_exitVarName", 0x0001), ("m_boss", 0x0001), ("m_bossManagerRef", 0x0001), ("m_bossManager", 0x0001) },
                new[] { ("RegisterBossManager", 0x0081), ("OnStateEnter", 0x00c6), ("OnStateExit", 0x00c6), (".ctor", 0x1886) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2" }),
            new Shape(typeof(AppRatingRequesterBoss), 0x00100101,
                new[] { ("m_bossDefinition", 0x0001), ("m_missionManagerRef", 0x0021) },
                new[] { ("Validate", 0x00c4), (".ctor", 0x1886) },
                new[] { "CreateAssetMenu", "Il2CppSetOption2", "Il2CppSetOption1" }),
            new Shape(typeof(BossTransition_AttackWindUpComplete), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "GraphNodeMenuFormat(Boss/{0})", "GraphNodeDefaultName(StartWindUp)", "Il2CppSetOption2", "Il2CppSetOption1" }),
            new Shape(typeof(BossTransition_BodyBroken), 0x00100001,
                new[] { ("m_delayTransitionFiringTime", 0x0001) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnEnter", 0x00c4), ("DoOnLeave", 0x00c4) },
                new[] { "Il2CppSetOption1", "GraphNodeDefaultName(BodyBroken)", "GraphNodeMenuFormat(Boss/Crab/{0})", "Il2CppSetOption2" }),
            new Shape(Nested(typeof(BossTransition_BodyBroken), "DelayTransitionJSONCtorArgs"), 0x00102004,
                new[] { ("DelayTransitionFiringTime", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "FiniteStateMachineJsonCtorArgs", "Il2CppSetOption2", "Il2CppSetOption1" }),
            new Shape(typeof(BossTransition_BothClawsBroken), 0x00100001,
                new[] { ("m_delayTransitionFiringTime", 0x0001) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnEnter", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(BothClawsBroken)", "GraphNodeMenuFormat(Boss/Crab/{0})" }),
            new Shape(Nested(typeof(BossTransition_BothClawsBroken), "DelayTransitionJSONCtorArgs"), 0x00102004,
                new[] { ("DelayTransitionFiringTime", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "FiniteStateMachineJsonCtorArgs" }),
            new Shape(typeof(BossTransition_ClearLineOfSight), 0x00100001,
                new[] { ("m_preAttackWaitSeconds", 0x0021), ("m_maxDistanceToCheck", 0x0021), ("m_coneOfViewAngle", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(BossTransition_ClearLineOfSight)", "GraphNodeMenuFormat(Boss/Fourmen/{0})" }),
            new Shape(Nested(typeof(BossTransition_ClearLineOfSight), "JSONCtorArgs"), 0x00102004,
                new[] { ("MaxDistanceToCheck", 0x0006), ("ConeOfViewAngle", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "FiniteStateMachineJsonCtorArgs", "Il2CppSetOption1", "Il2CppSetOption2" }),
            new Shape(typeof(BossTransition_ConsumeBossManagerFlag), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption2", "GraphNodeDefaultName(FoundFlagOnBossManager)", "GraphNodeMenuFormat(Boss/{0})", "Il2CppSetOption1" }),
            new Shape(typeof(BossTransition_ExitedAttackAndVulnerableAnimation), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) },
                new[] { "GraphNodeMenuFormat(Boss/{0})", "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(ExitedAttackAndVulnerableAnimation)" }),
            new Shape(typeof(BossTransition_HealthCheck), 0x00100001,
                new[] { ("m_evalutionValue", 0x0024), ("m_evalutionMethod", 0x0024) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(CheckHealthIs)", "GraphNodeMenuFormat(Boss/{0})" }),
            new Shape(Nested(typeof(BossTransition_HealthCheck), "HealthJSONCtorArgs"), 0x00102004,
                new[] { ("Value", 0x0006), ("Evaluation", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "FiniteStateMachineJsonCtorArgs", "Il2CppSetOption1", "Il2CppSetOption2" }),
            new Shape(typeof(BossTransition_HitPlayer), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(HitPlayer)", "GraphNodeMenuFormat(Boss/{0})" }),
            new Shape(typeof(BossTransition_InRange), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "GraphNodeDefaultName(IsInRange)", "GraphNodeMenuFormat(Boss/{0})", "Il2CppSetOption2", "Il2CppSetOption1" }),
            new Shape(typeof(BossTransition_IsActiveBoss), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "GraphNodeMenuFormat(Boss/Fourmen/{0})", "GraphNodeDefaultName(ActiveBoss)", "Il2CppSetOption2" }),
            new Shape(typeof(BossTransition_IsActiveBossTimeout), 0x00100001,
                new[] { ("m_timeout", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption2", "GraphNodeMenuFormat(Boss/Fourmen/{0})", "Il2CppSetOption1", "GraphNodeDefaultName(ActiveBossTimeout)" }),
            new Shape(Nested(typeof(BossTransition_IsActiveBossTimeout), "JSONCtorArgs"), 0x00102004,
                new[] { ("Timeout", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "FiniteStateMachineJsonCtorArgs" }),
            new Shape(typeof(BossTransition_IsNotActiveBoss), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(NotActiveBoss)", "GraphNodeMenuFormat(Boss/Fourmen/{0})" }),
            new Shape(typeof(BossTransition_NotInRange), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "GraphNodeDefaultName(NotInRange)", "GraphNodeMenuFormat(Boss/{0})", "Il2CppSetOption2" }),
            new Shape(typeof(BossTransition_StartWindUp), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(StartWindUp)", "GraphNodeMenuFormat(Boss/{0})" }),
            new Shape(typeof(BossTransition_UsedClawCheck), 0x00100001,
                new[] { ("m_claw", 0x0001), ("m_clawDeflated", 0x0001) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName(UsedClawCheck)", "GraphNodeMenuFormat(Boss/Crab/{0})" }),
            new Shape(Nested(typeof(BossTransition_UsedClawCheck), "ClawJSONCtorArgs"), 0x00102004,
                new[] { ("Claw", 0x0006), ("Deflated", 0x0006) },
                new[] { (".ctor", 0x1886) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "FiniteStateMachineJsonCtorArgs" }),
            new Shape(typeof(BossTransition_VulnerableTimeOut), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4) },
                new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeMenuFormat(Boss/{0})", "GraphNodeDefaultName(VulnerableTimedOut)" }),
            new Shape(typeof(BossTransition_WasHit), 0x00100001,
                NoFields,
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) },
                new[] { "GraphNodeDefaultName(WasHit)", "GraphNodeMenuFormat(Boss/{0})", "Il2CppSetOption2", "Il2CppSetOption1" }),
        };

        private static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name.Replace("Attribute", "") + AttributeDetail(data)).ToArray();

        private static string AttributeDetail(CustomAttributeData data)
        {
            switch (data.AttributeType.Name)
            {
                case "Il2CppSetOptionAttribute":
                    return Convert.ToInt32(data.ConstructorArguments[0].Value).ToString();
                case "GraphNodeMenuFormatAttribute":
                case "GraphNodeDefaultNameAttribute":
                    return "(" + data.ConstructorArguments[0].Value + ")";
                default:
                    return "";
            }
        }

        // The DummyDll field rows 0x04000994-0x04000999 carry SerializeField; the runtime fields after them do not.
        [Test]
        public void AnimatorSetBossStorageBoolSerializesExactlyItsSixConfigurationFields()
        {
            Assert.That(
                typeof(AnimatorSetBossStorageBool).GetFields(Declared).Where(f => f.GetCustomAttribute<SerializeField>() != null).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "m_setOnEntry", "m_fsmVarOnEntry", "m_entryValue", "m_setOnExit", "m_fsmVarOnExit", "m_exitValue" }));
        }

        [Test]
        public void DeclarationShapeTypesKeepTheirFlagsMembersAndAttributeOrder()
        {
            foreach (Shape shape in Shapes)
            {
                AssertMembers(shape.Type, shape.TypeFlags, shape.Fields, shape.Methods);
                Assert.That(AttributeNames(shape.Type), Is.EqualTo(shape.Attributes), shape.Type.FullName + " attributes");
                Assert.That(shape.Type.GetCustomAttributesData().Where(d => d.AttributeType.Name == "Il2CppSetOptionAttribute")
                    .All(d => Equals(d.ConstructorArguments[1].Value, false)), Is.True, shape.Type.FullName + " options are off");
            }
        }

        [Test]
        public void DeclarationShapeBasesAndJsonArgumentClassesMatchTheMetadata()
        {
            foreach (Type node in Shapes.Select(s => s.Type).Where(t => typeof(FSMTransition).IsAssignableFrom(t)))
            {
                Type expectedBase = node == typeof(BossTransition_AttackWindUpComplete) || node == typeof(BossTransition_StartWindUp)
                    ? typeof(Transition_Ability) : typeof(FSMTransition);
                Assert.That(node.BaseType, Is.EqualTo(expectedBase), node.Name);
            }
            foreach (Type args in Shapes.Select(s => s.Type).Where(t => t.IsNested))
            {
                Assert.That(args.IsNestedFamily && args.IsSerializable, Is.True, args.FullName);
                Assert.That(args.GetCustomAttribute<FiniteStateMachineJsonCtorArgsAttribute>(false), Is.Not.Null, args.FullName);
            }
            Assert.That(typeof(AppRatingRequesterBoss).BaseType, Is.EqualTo(typeof(AppRatingRequesterBase)));
            Assert.That(typeof(AnimatorSetBossStorageBool).BaseType, Is.EqualTo(typeof(StateMachineBehaviour)));
        }

        [Test]
        public void DeclarationShapeClassFactoryRegistersEachNodeWithItsArgumentType()
        {
            (Type node, string args, Type owner)[] nodes =
            {
                (typeof(BossTransition_AttackWindUpComplete), "AbilityJSONCtorArgs", typeof(Transition_Ability)),
                (typeof(BossTransition_StartWindUp), "AbilityJSONCtorArgs", typeof(Transition_Ability)),
                (typeof(BossTransition_BodyBroken), "DelayTransitionJSONCtorArgs", typeof(BossTransition_BodyBroken)),
                (typeof(BossTransition_BothClawsBroken), "DelayTransitionJSONCtorArgs", typeof(BossTransition_BothClawsBroken)),
                (typeof(BossTransition_ClearLineOfSight), "JSONCtorArgs", typeof(BossTransition_ClearLineOfSight)),
                (typeof(BossTransition_HealthCheck), "HealthJSONCtorArgs", typeof(BossTransition_HealthCheck)),
                (typeof(BossTransition_IsActiveBossTimeout), "JSONCtorArgs", typeof(BossTransition_IsActiveBossTimeout)),
                (typeof(BossTransition_UsedClawCheck), "ClawJSONCtorArgs", typeof(BossTransition_UsedClawCheck)),
                (typeof(BossTransition_ConsumeBossManagerFlag), null, null),
                (typeof(BossTransition_ExitedAttackAndVulnerableAnimation), null, null),
                (typeof(BossTransition_HitPlayer), null, null),
                (typeof(BossTransition_InRange), null, null),
                (typeof(BossTransition_IsActiveBoss), null, null),
                (typeof(BossTransition_IsNotActiveBoss), null, null),
                (typeof(BossTransition_NotInRange), null, null),
                (typeof(BossTransition_VulnerableTimeOut), null, null),
                (typeof(BossTransition_WasHit), null, null),
            };
            foreach ((Type node, string args, Type owner) in nodes)
            {
                Assert.That(FSMClassFactory.Transitions.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
                Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
                Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
                if (args == null)
                {
                    Assert.That(factory.JSONCtorArgsType, Is.Null, node.Name);
                    continue;
                }
                Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(args), node.Name);
                Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(owner), node.Name);
            }
        }
    }
}
