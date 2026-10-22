using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss states that run the vulnerable window and the wind-up flag (Game.Runtime 0200022f BossState_Vulnerable, 02000231
    // BossState_Vulnerable_Target, 02000200 BossState_BossCrab_Vulnerable_Claws, 02000233 BossState_WindUpAttack). Each state is built
    // from its JSON through the node factory and driven through its own DoOnEnter/DoUpdate/DoOnLeave against a real Boss on an owned
    // inactive host, which gets the animation storage and the time-manager reference a started Actor would have created. Expected
    // values come from the x86-64 bodies at the addresses named per case. The homing targets are real HomingTarget components on
    // owned inactive hosts; Boss.SetActiveHomingTarget flips their game objects, which is what the cases read.
    // Not covered: the development-only calls that are compiled out of retail; the ability == null early exit of the wind-up state is
    // unreachable because the base update dereferences the same lookup first.
    public sealed class BossVulnerableStateTests : OwnedGraphNamesFixture
    {
        public BossVulnerableStateTests() : base("Lucid-vulnerable-state-") { }

        private const ActorAttachPointType LeftClaw = ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint;
        private const ActorAttachPointType RightClaw = ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint;

        private static string Name() => "Lucid-vulnerable-state-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json) =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static void Enter(IFSMState state, Boss boss) => Call(state, "DoOnEnter", boss, null);

        private static void Leave(IFSMState state, Boss boss) => Call(state, "DoOnLeave", boss, null);

        private static void UpdateState(IFSMState state, Boss boss, float deltaTime) =>
            Call(state, "DoUpdate", boss, new FSMUpdateContext(deltaTime, FSMUpdateType.FixedUpdate));

        private static string VulnerableJson(float time) => "{\"VulnerableTime\":" + time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";

        // The two dictionaries the effect states read from the actor's storage and the time-manager reference the ability states read.
        private static Boss Prime(Boss boss)
        {
            boss.Storage.SetValue(ActorFSMKeys.ActiveAnimationLookup, new Dictionary<string, ActorAnimationDefinition>());
            boss.Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
            // Reflection: Actor.OnEnable (the only setter) needs a running FSM; this is the unresolved reference it leaves with no TimeManager row.
            Set(boss, "m_timeManagerRef", new SystemRef<TimeManager>(ProcessManager.GetDefaultName<TimeManager>(), null));
            return boss;
        }

        private static void WithBoss(Action<Owned, Boss> body) => Owned.Run(owned => body(owned, Prime(NewBoss(owned, Vector3.zero))));

        // A boss with a real homing target at each attach point. The target hosts start inactive, as the scene authors them.
        private static void WithHomingBoss(Action<Owned, Boss, Dictionary<ActorAttachPointType, HomingTarget>> body, params ActorAttachPointType[] points) =>
            Owned.Run(owned =>
            {
                // HomingTarget's field initialiser takes a typed CharacterManager reference and its base takes the TimeManager row.
                owned.LeaseRow<CharacterManager>();
                owned.LeaseRow<TimeManager>();
                Boss boss = Prime(NewBoss(owned, Vector3.zero));
                var targets = new Dictionary<ActorAttachPointType, HomingTarget>();
                Type part = typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic);
                foreach (ActorAttachPointType point in points)
                {
                    GameObject host = owned.Host("Lucid vulnerable homing target " + point, false);
                    host.AddComponent<BoxCollider>(); // HomingTarget requires a Collider
                    HomingTarget target = EnemyAbilityTestSupport.AddTimeScaled<HomingTarget>(host);
                    object record = Activator.CreateInstance(part);
                    part.GetField("AttachPoint").SetValue(record, point);
                    part.GetField("HomingTarget").SetValue(record, target);
                    ((IDictionary)Get<object>(boss, "m_attachPointDictionary")).Add(point, record);
                    targets[point] = target;
                }
                body(owned, boss, targets);
            });

        // ---- BossState_Vulnerable ----

        // 0x704FE0: after the effect entry the vulnerable clock is restarted at zero (a stale value is overwritten) and the boss is
        // marked vulnerable.
        [Test]
        public void VulnerableEntryRestartsTheClockAndMarksTheBossVulnerable()
        {
            WithBoss((owned, boss) =>
            {
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 3f);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
                Enter(NewState(nameof(BossState_Vulnerable), VulnerableJson(2f)), boss);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.Zero);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True);
            });
        }

        // 0x70D110..0x70D343: each update adds the delta to the clock (AdjustValue from zero), then the vulnerability ends when the clock is
        // strictly above the authored time (ucomiss + ja at 0x70D281/0x70D28A: a clock equal to the time is still vulnerable) or when
        // ForceNotVulnerable is set. The forced flag is only read while the time has not passed (0x70D290 is skipped by the ja), and
        // reading it stores its default; ending the vulnerability stores false for both the vulnerable flag and the forced flag.
        [Test]
        public void VulnerableUpdateEndsTheVulnerabilityOnlyOnceTheClockIsStrictlyPastTheTime()
        {
            WithBoss((owned, boss) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable), VulnerableJson(2f));
                Enter(state, boss);
                UpdateState(state, boss, 1f);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.EqualTo(1f));
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True);
                Assert.That(Has(boss, BossFSMKeys.ForceNotVulnerable), Is.True, "the forced flag was read, which stored its default");
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False);

                UpdateState(state, boss, 1f);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.EqualTo(2f));
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True, "a clock equal to the time is still vulnerable");

                UpdateState(state, boss, 1f);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.EqualTo(3f));
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False, "strictly past the time");
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False);
            });

            WithBoss((owned, boss) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable), VulnerableJson(1f));
                Enter(state, boss);
                UpdateState(state, boss, 2f);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False);
                Assert.That(Has(boss, BossFSMKeys.ForceNotVulnerable), Is.True, "ending stores false for the forced flag");
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False);
            });
        }

        [Test]
        public void VulnerableUpdateHonoursAndConsumesForceNotVulnerable()
        {
            WithBoss((owned, boss) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable), VulnerableJson(10f));
                Enter(state, boss);
                boss.Storage.SetValue(BossFSMKeys.ForceNotVulnerable, true);
                UpdateState(state, boss, 0.5f);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.EqualTo(0.5f), "far inside the time");
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False, "the forced flag is consumed");
            });
        }

        // 0x70D281/0x70D28A: ja is not taken on an unordered compare, so a NaN vulnerable time does not skip the forced-flag read: the
        // boss stays vulnerable (the forced flag is false) and the read stores the flag's default. A rewrite as !(clock <= time) would
        // end the vulnerability.
        [Test]
        public void VulnerableUpdateWithANaNTimeStaysVulnerableAndReadsTheForcedFlag()
        {
            WithBoss((owned, boss) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable), VulnerableJson(2f));
                Set(state, "m_vulnerableTime", float.NaN); // JSON cannot carry NaN
                Enter(state, boss);
                UpdateState(state, boss, 1f);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True);
                Assert.That(Has(boss, BossFSMKeys.ForceNotVulnerable), Is.True, "the forced flag was read");
                Assert.That(Stored<bool>(boss, BossFSMKeys.ForceNotVulnerable), Is.False);
            });
        }

        // 0x7053C0: leaving zeroes the clock and clears both the vulnerable flag and the flag that the boss hit the player.
        [Test]
        public void VulnerableLeaveResetsTheClockAndTheFlags()
        {
            WithBoss((owned, boss) =>
            {
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 1.5f);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, true);
                boss.Storage.SetValue(BossFSMKeys.BossHitPlayer, true);
                Leave(NewState(nameof(BossState_Vulnerable), VulnerableJson(2f)), boss);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.Zero);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.BossHitPlayer), Is.False);
            });
        }

        // ---- BossState_Vulnerable_Target ----

        private static string TargetJson(float time, ActorAttachPointType target, float delay) =>
            "{\"VulnerableTime\":" + time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            ",\"VulnerableTarget\":" + (int)target +
            ",\"TargetActivateDelaySeconds\":" + delay.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";

        // 0x70D5D0: entering the targeted state marks the vulnerable target as not yet active (through the user's own storage), after
        // the vulnerable entry.
        [Test]
        public void TargetEntryMarksTheTargetInactiveAfterTheVulnerableEntry()
        {
            WithHomingBoss((owned, boss, targets) =>
            {
                boss.Storage.SetValue(BossFSMKeys.VulnerableTargetActive, true);
                Enter(NewState(nameof(BossState_Vulnerable_Target), TargetJson(5f, LeftClaw, 1f)), boss);
                Assert.That(Stored<bool>(boss, BossFSMKeys.VulnerableTargetActive), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True, "the base entry ran");
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.False, "entering does not activate the target");
            }, LeftClaw);
        }

        // 0x70D750..0x70D9D9: after the vulnerable update, the target is activated once the activation delay is not above the clock
        // (ucomiss + ja at 0x70D954/0x70D957: a delay equal to the clock activates), and once it is active nothing is repeated.
        // Delay 1 with 0.5-second steps: 0.5 keeps it off, 1.0 turns it on.
        [Test]
        public void TargetUpdateActivatesTheTargetOnceTheDelayIsReached()
        {
            WithHomingBoss((owned, boss, targets) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable_Target), TargetJson(5f, LeftClaw, 1f));
                Enter(state, boss);
                UpdateState(state, boss, 0.5f);
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.False, "the delay 1 is above the clock 0.5");
                Assert.That(Stored<bool>(boss, BossFSMKeys.VulnerableTargetActive), Is.False);

                UpdateState(state, boss, 0.5f);
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.True, "a delay equal to the clock activates");
                Assert.That(Stored<bool>(boss, BossFSMKeys.VulnerableTargetActive), Is.True);

                targets[LeftClaw].gameObject.SetActive(false);
                UpdateState(state, boss, 0.5f);
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.False, "an already active target is not activated again");
            }, LeftClaw);
        }

        // 0x70D954/0x70D957: ja is not taken on an unordered compare, so a NaN activation delay activates the target on the first update.
        // A rewrite as `if (!(clock >= delay)) return` would never activate it.
        [Test]
        public void TargetUpdateWithANaNDelayActivatesTheTarget()
        {
            WithHomingBoss((owned, boss, targets) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable_Target), TargetJson(5f, LeftClaw, 1f));
                Set(state, "m_targetActivateDelaySeconds", float.NaN); // JSON cannot carry NaN
                Enter(state, boss);
                UpdateState(state, boss, 0.1f);
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.True);
                Assert.That(Stored<bool>(boss, BossFSMKeys.VulnerableTargetActive), Is.True);
            }, LeftClaw);
        }

        // 0x70DA40: leaving switches the target off, zeroes the clock, clears the vulnerable flag (base leave) and removes the
        // target-active flag from the storage instead of storing false.
        [Test]
        public void TargetLeaveSwitchesTheTargetOffAndRemovesTheActiveFlag()
        {
            WithHomingBoss((owned, boss, targets) =>
            {
                IFSMState state = NewState(nameof(BossState_Vulnerable_Target), TargetJson(5f, RightClaw, 0f));
                Enter(state, boss);
                UpdateState(state, boss, 0.25f);
                Assert.That(targets[RightClaw].gameObject.activeSelf, Is.True, "a zero delay activates on the first update");
                Leave(state, boss);
                Assert.That(targets[RightClaw].gameObject.activeSelf, Is.False);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.Zero);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False);
                Assert.That(Has(boss, BossFSMKeys.VulnerableTargetActive), Is.False, "the key is removed");
                Assert.That(Stored<bool>(boss, BossFSMKeys.BossHitPlayer), Is.False, "the base leave ran");
            }, RightClaw);
        }

        // ---- BossState_BossCrab_Vulnerable_Claws ----

        // 0x704D00..0x704FD1: the claw that made the last attack becomes the active homing target and the boss publishes the vulnerable
        // effect; the other claw stays off. An attack made with neither claw (BossCrab_Body here, or none at all) returns before
        // the effect type is stored.
        [Test]
        public void ClawsEntryActivatesTheClawThatAttackedAndPublishesTheVulnerableEffect()
        {
            foreach ((BossAttackTypes attack, ActorAttachPointType active, ActorAttachPointType idle) in new[]
            {
                (BossAttackTypes.BossCrab_RightClaw, RightClaw, LeftClaw),
                (BossAttackTypes.BossCrab_LeftClaw, LeftClaw, RightClaw),
            })
            {
                WithHomingBoss((owned, boss, targets) =>
                {
                    owned.LeaseRow<MessageManager>(); // Boss.TriggerEffects looks the manager up
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, attack);
                    Enter(NewState(nameof(BossState_BossCrab_Vulnerable_Claws), VulnerableJson(3f)), boss);
                    Assert.That(targets[active].gameObject.activeSelf, Is.True, attack + " activates its claw");
                    Assert.That(targets[idle].gameObject.activeSelf, Is.False, "the other claw is untouched");
                    Assert.That(Stored<BossEffectTriggerTypes>(boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.Vulnerable));
                    Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True, "the base entry ran");
                }, LeftClaw, RightClaw);
            }
        }

        [Test]
        public void ClawsEntryDoesNothingFurtherForAnyOtherAttack()
        {
            foreach (BossAttackTypes attack in new[] { BossAttackTypes.BossCrab_Body, BossAttackTypes.None, BossAttackTypes.BossFourmen_LaserFence })
            {
                WithHomingBoss((owned, boss, targets) =>
                {
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, attack);
                    Enter(NewState(nameof(BossState_BossCrab_Vulnerable_Claws), VulnerableJson(3f)), boss);
                    Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.False, attack.ToString());
                    Assert.That(targets[RightClaw].gameObject.activeSelf, Is.False, attack.ToString());
                    Assert.That(Has(boss, BossFSMKeys.BossEffectType), Is.False, "no vulnerable effect is published");
                    Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.True, "the base entry still ran");
                }, LeftClaw, RightClaw);
            }
        }

        // 0x7051C0: leaving switches both claw targets off and clears the clock and the vulnerable flag (also after the base leave,
        // which clears the player-hit flag).
        [Test]
        public void ClawsLeaveSwitchesBothClawsOff()
        {
            WithHomingBoss((owned, boss, targets) =>
            {
                targets[LeftClaw].gameObject.SetActive(true);
                targets[RightClaw].gameObject.SetActive(true);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 2f);
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, true);
                boss.Storage.SetValue(BossFSMKeys.BossHitPlayer, true);
                Leave(NewState(nameof(BossState_BossCrab_Vulnerable_Claws), VulnerableJson(3f)), boss);
                Assert.That(targets[LeftClaw].gameObject.activeSelf, Is.False);
                Assert.That(targets[RightClaw].gameObject.activeSelf, Is.False);
                Assert.That(Stored<float>(boss, BossFSMKeys.CurrentlyVulnerableTime), Is.Zero);
                Assert.That(Stored<bool>(boss, BossFSMKeys.CurrentlyVulnerable), Is.False);
                Assert.That(Stored<bool>(boss, BossFSMKeys.BossHitPlayer), Is.False);
            }, LeftClaw, RightClaw);
        }

        // ---- BossState_WindUpAttack ----

        private const ActorAbilityType OffensiveAbility = ActorAbilityType.Boss_BodySlam;
        private const ActorAbilityType PassiveAbility = ActorAbilityType.Boss_RepairDamage;

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

        private static void AddAbility(Boss boss, ActorAbilityType type, ActorAbility ability) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(boss, "m_abilities")[type] = ability; // Actor builds its abilities from the FSM's definitions; the test supplies its own

        // 0x70DE20..0x70E00C: each update publishes the offensive ability's wound-up flag as AttackWoundUp, false and then true.
        [Test]
        public void WindUpUpdatePublishesTheOffensiveAbilitysWoundUpFlag()
        {
            WithBoss((owned, boss) =>
            {
                BossAbility_BodySlam slam = NewAbility<BossAbility_BodySlam>(boss, NewDefinition<BossAbilityDefinition_BodySlam>(owned));
                AddAbility(boss, OffensiveAbility, slam);
                IFSMState state = NewState(nameof(BossState_WindUpAttack), AbilityJson(OffensiveAbility));

                UpdateState(state, boss, 0.1f);
                Assert.That(Has(boss, BossFSMKeys.AttackWoundUp), Is.True);
                Assert.That(Stored<bool>(boss, BossFSMKeys.AttackWoundUp), Is.False, "not wound up yet");

                slam.SetAttackWoundUp();
                UpdateState(state, boss, 0.1f);
                Assert.That(Stored<bool>(boss, BossFSMKeys.AttackWoundUp), Is.True);
            });
        }

        // 0x70DEC6..0x70DEDC: an ability that is not an IOffensiveAbility is skipped by the type test, leaving the flag untouched.
        [Test]
        public void WindUpUpdateLeavesTheFlagAloneForAnAbilityThatIsNotOffensive()
        {
            WithBoss((owned, boss) =>
            {
                AddAbility(boss, PassiveAbility, NewAbility<BossAbility_RegrowClaw>(boss, NewDefinition<BossAbilityDefinition_RegrowClaw>(owned)));
                IFSMState state = NewState(nameof(BossState_WindUpAttack), AbilityJson(PassiveAbility));

                UpdateState(state, boss, 0.1f);
                Assert.That(Has(boss, BossFSMKeys.AttackWoundUp), Is.False, "nothing is stored");

                boss.Storage.SetValue(BossFSMKeys.AttackWoundUp, true);
                UpdateState(state, boss, 0.1f);
                Assert.That(Stored<bool>(boss, BossFSMKeys.AttackWoundUp), Is.True, "a stored flag is not cleared");
            });
        }
    }
}
