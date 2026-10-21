using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss graph states of Game.Runtime (020001fa..02000208): BossState_ActiveBossChange/Increment/Reset/Setup (the Fourmen rotation),
    // ClearAttackOverride, CalculateClawRegrowth (RegrowClaw), BodySlam, DeathFlight, CoolDownProjectilesOfType, and DecideMovement. Each
    // state is built from its JSON through the node factory and driven through its own DoOnEnter/DoUpdate/DoOnLeave bodies against a real
    // Boss on an owned inactive host, which gets the animation storage and the time-manager reference a started Actor would have created.
    // The ability states key their ability by an ActorAbilityType the case chooses (any value of the enum works, the state only forwards
    // it). Not covered: BossState_DecideMovementOrAttack, whose constructor reads the ability definition by name through the App's
    // DataManager (DataManagerUtilities.TryGetAbilityDefinition, 06003952) and so needs a live App; its declaration is covered by the
    // declaration rows. The ability == null early returns of the ability states are unreachable: the base update and enter bodies
    // dereference the same lookup first (ActorState_Ability<T>.GetAnimationDefinition).
    public sealed class BossStateTests
    {
        private static string Name() => "Lucid-boss-state-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json = "{}") =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

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

        private static void WithBoss(Action<Owned, Boss> body) => Owned.Run(owned => body(owned, Prime(NewBoss(owned, Vector3.zero))));

        // Reflection: Actor builds its abilities from the FSM's ability definitions (CreateAbility); the test supplies its own.
        private static void AddAbility(Boss boss, ActorAbilityType type, ActorAbility ability) =>
            Get<Dictionary<ActorAbilityType, ActorAbility>>(boss, "m_abilities")[type] = ability;

        // A rig of three bosses, the first of which runs the states.
        private static void WithFourmen(Action<FourmenRig, Boss> body) => Owned.Run(owned =>
        {
            FourmenRig rig = NewFourmenRig(owned, new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, 6f));
            foreach (Boss boss in rig.Bosses)
                Prime(boss);
            body(rig, rig.Bosses[0]);
        });

        // ---- the Fourmen rotation ----

        // BossState_ActiveBossSetup (020001fd): with no active boss unit the loop status is cleared and the active boss is configured;
        // the boss at the active index becomes the active one with movement priority one, all others get priority zero. An already
        // active unit leaves everything as it was.
        [Test]
        public void ActiveBossSetupConfiguresTheFirstBossUnlessOneIsActive()
        {
            WithFourmen((rig, user) =>
            {
                rig.Bosses[1].SetActiveBossEntity();
                rig.Bosses[1].ClearActiveBossEntity();
                Assert.That(rig.Bosses[1].HasBeenActiveThisLoop, Is.True);
                Enter(NewState(nameof(BossState_ActiveBossSetup)), user);
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { true, false, false }));
                Assert.That(rig.Bosses.Select(boss => boss.MovementPriority), Is.EqualTo(new[] { 1, 0, 0 }));
                Assert.That(rig.Bosses[1].HasBeenActiveThisLoop, Is.False, "the loop status is cleared first");
            });

            WithFourmen((rig, user) =>
            {
                rig.Bosses[1].SetActiveBossEntity();
                rig.Bosses[1].ClearActiveBossEntity();
                rig.Bosses[2].SetActiveBossEntity();
                Enter(NewState(nameof(BossState_ActiveBossSetup)), user);
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { false, false, true }));
                Assert.That(rig.Bosses.Select(boss => boss.MovementPriority), Is.EqualTo(new[] { 0, 0, 0 }), "nothing is configured");
                Assert.That(rig.Bosses[1].HasBeenActiveThisLoop, Is.True, "the loop status is kept");
            });
        }

        // BossState_ActiveBossIncrement (020001fb) only advances the index.
        [Test]
        public void ActiveBossIncrementAdvancesTheIndexOnly()
        {
            WithFourmen((rig, user) =>
            {
                IFSMState state = NewState(nameof(BossState_ActiveBossIncrement));
                Enter(state, user);
                Assert.That(Get<int>(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(1));
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { false, false, false }), "nobody is configured");
                Enter(NewState(nameof(BossState_ActiveBossIncrement)), user);
                Assert.That(Get<int>(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(2));
            });
        }

        // BossState_ActiveBossChange (020001fa): the active timer is dropped, the active boss is retired (and counts as having had its
        // turn), the index advances and the next boss around the ring becomes the active one.
        [Test]
        public void ActiveBossChangePassesTheTurnToTheNextBoss()
        {
            WithFourmen((rig, user) =>
            {
                rig.Bosses[0].SetActiveBossEntity();
                user.Storage.SetValue(BossFSMKeys.ActiveBossTimer, 5f);
                Enter(NewState(nameof(BossState_ActiveBossChange)), user);

                Assert.That(Has(user, BossFSMKeys.ActiveBossTimer), Is.False);
                Assert.That(Get<int>(rig.Fourmen, "m_activeBossIndex"), Is.EqualTo(1));
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { false, true, false }));
                Assert.That(rig.Bosses.Select(boss => boss.MovementPriority), Is.EqualTo(new[] { 0, 1, 0 }));
                Assert.That(rig.Bosses.Select(boss => boss.HasBeenActiveThisLoop), Is.EqualTo(new[] { true, false, false }));

                for (int i = 0; i < 3; i++)
                    rig.Fourmen.IncrementActiveBossIndex();
                Enter(NewState(nameof(BossState_ActiveBossChange)), user);
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { false, false, true }), "index 5 wraps to the third boss");
            });
        }

        // BossState_ActiveBossReset (020001fc): the index restarts, then the loop status is cleared, then the active boss is retired
        // (so the boss that was active counts as having had its turn), then the first boss is configured.
        [Test]
        public void ActiveBossResetRestartsTheRotationInTheNativeOrder()
        {
            WithFourmen((rig, user) =>
            {
                rig.Fourmen.IncrementActiveBossIndex();
                rig.Fourmen.IncrementActiveBossIndex();
                rig.Bosses[1].SetActiveBossEntity();
                rig.Bosses[2].SetActiveBossEntity();
                rig.Bosses[2].ClearActiveBossEntity();
                rig.Bosses[1].SetMovementPriority(1);
                Enter(NewState(nameof(BossState_ActiveBossReset)), user);

                Assert.That(Get<int>(rig.Fourmen, "m_activeBossIndex"), Is.Zero);
                Assert.That(rig.Bosses.Select(boss => boss.IsActivePartOfMultiBodyBoss), Is.EqualTo(new[] { true, false, false }));
                Assert.That(rig.Bosses.Select(boss => boss.MovementPriority), Is.EqualTo(new[] { 1, 0, 0 }));
                Assert.That(rig.Bosses.Select(boss => boss.HasBeenActiveThisLoop), Is.EqualTo(new[] { false, true, false }),
                    "the loop status is cleared before the active boss is retired, so only the boss that was active has had its turn");
            });
        }

        // ---- the crab states ----

        // BossState_ClearAttackOverride (02000201) removes only the attack type override.
        [Test]
        public void ClearAttackOverrideRemovesOnlyTheOverride()
        {
            WithBoss((owned, boss) =>
            {
                boss.Storage.SetValue(BossFSMKeys.BossAttackTypeOverride, BossAttackTypes.BossCrab_Body);
                boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.BossCrab_LeftClaw);
                Enter(NewState(nameof(BossState_ClearAttackOverride)), boss);
                Assert.That(Has(boss, BossFSMKeys.BossAttackTypeOverride), Is.False);
                Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_LeftClaw));
                Enter(NewState(nameof(BossState_ClearAttackOverride)), boss);
                Assert.That(Has(boss, BossFSMKeys.BossAttackTypeOverride), Is.False, "removing an absent override is fine");
            });
        }

        private const ActorAbilityType RegrowAbility = ActorAbilityType.Boss_RepairDamage;

        // BossState_CalculateClawRegrowth (020001ff): the attack just made counts as an attack with a deflated claw
        // and, once the regrow ability says so, the destroyed claw is repaired. The right claw and the left claw are tracked separately;
        // any other attack type does nothing. Entering also marks the ability as in use.
        [Test]
        public void ClawRegrowthRepairsTheClawOnceTheUsesAreReached()
        {
            foreach ((BossAttackTypes attack, ActorAttachPointType claw, GraphStorageKey count) in new[]
            {
                (BossAttackTypes.BossCrab_RightClaw, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, BossFSMKeys.BossCrabDeflatedRightCount),
                (BossAttackTypes.BossCrab_LeftClaw, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, BossFSMKeys.BossCrabDeflatedLeftCount),
            })
            {
                WithBoss((owned, boss) =>
                {
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, true);
                    AddBodyPart(boss, ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, true);
                    AddAbility(boss, RegrowAbility, NewAbility<BossAbility_RegrowClaw>(boss,
                        NewDefinition<BossAbilityDefinition_RegrowClaw>(owned, d => d.UsesToTriggerRegrowth = 2)));
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, attack);

                    Enter(NewState(nameof(BossState_CalculateClawRegrowth), AbilityJson(RegrowAbility)), boss);
                    Assert.That(boss.IsComponentDestroyed(claw), Is.True, attack + ": one deflated attack is below two uses");
                    Assert.That(Stored<int>(boss, count), Is.EqualTo(1));

                    var inUse = Stored<Dictionary<ActorAbilityType, ActorAbilityInUseData>>(boss, ActorFSMKeys.AbilitiesInUse);
                    Assert.That(inUse[RegrowAbility].InUse, Is.True, "entering marks the ability as in use");

                    Enter(NewState(nameof(BossState_CalculateClawRegrowth), AbilityJson(RegrowAbility)), boss);
                    Assert.That(boss.IsComponentDestroyed(claw), Is.False, attack + ": the second deflated attack regrows the claw");
                    Assert.That(Stored<int>(boss, count), Is.Zero, "and restarts the count");
                    ActorAttachPointType other = claw == ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint
                        ? ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint
                        : ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint;
                    Assert.That(boss.IsComponentDestroyed(other), Is.True, "the other claw is untouched");
                });
            }
        }

        [Test]
        public void ClawRegrowthIgnoresOtherAttackTypes()
        {
            WithBoss((owned, boss) =>
            {
                AddBodyPart(boss, ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, true);
                AddAbility(boss, RegrowAbility, NewAbility<BossAbility_RegrowClaw>(boss,
                    NewDefinition<BossAbilityDefinition_RegrowClaw>(owned, d => d.UsesToTriggerRegrowth = 1)));
                boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.BossCrab_Body);
                Enter(NewState(nameof(BossState_CalculateClawRegrowth), AbilityJson(RegrowAbility)), boss);
                Assert.That(boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint), Is.True);
                Assert.That(Has(boss, BossFSMKeys.BossCrabDeflatedRightCount), Is.False);
                Assert.That(Has(boss, BossFSMKeys.BossCrabDeflatedLeftCount), Is.False);
            });
        }

        // The slam is entered for real with the given stored slam count (06000a37): its jump target is the boss's position at entry
        // and its attack index is the stored count (a non-zero count also skips the triangle layout).
        private static BossAbility_BodySlam NewEnteredBodySlam(Owned owned, Boss boss, Action<BossAbilityDefinition_BodySlam> setup, int attackIndex)
        {
            BossAbility_BodySlam ability = NewAbility<BossAbility_BodySlam>(boss, NewDefinition<BossAbilityDefinition_BodySlam>(owned, setup));
            AddAbility(boss, ActorAbilityType.Boss_BodySlam, ability);
            boss.Storage.SetValue(new GraphStorageKey("bodySlamCount", 0, 0), attackIndex);
            ability.Enter();
            return ability;
        }

        // BossState_BodySlam (020001fe): once the slam is wound up the boss moves to the ability's jump target and turns a full turn-speed
        // step towards the character's side (+1 or -1 times speed and delta time, about the boss's up axis) while the angle to the
        // character exceeds the face-angle tolerance, and not at all within it.
        [Test]
        public void BodySlamStateTurnsTowardsTheCharacterAndMovesToTheJumpTarget()
        {
            foreach ((Vector3 character, float tolerance, float expectedDegrees) in new[]
            {
                (new Vector3(10f, 0f, 0f), 5f, 10f), (new Vector3(-10f, 0f, 0f), 5f, -10f), (new Vector3(10f, 0f, 0f), 120f, 0f),
            })
            {
                WithManager((owned, manager, characterObject) =>
                {
                    // The boss enters the slam at the jump target, then stands at the origin.
                    Boss boss = Prime(NewBoss(owned, new Vector3(1f, 2f, 3f)));
                    MakeCurrent(manager, characterObject, character);
                    BossAbility_BodySlam ability = NewEnteredBodySlam(owned, boss, d =>
                    {
                        d.FaceAngleTolerance = tolerance;
                        d.TurnSpeed = 20f;
                    }, 1);
                    boss.SetWorldPosition(Vector3.zero);
                    IFSMState state = NewState(nameof(BossState_BodySlam), AbilityJson(ActorAbilityType.Boss_BodySlam));

                    UpdateState(state, boss, 0.5f);
                    Assert.That(boss.WorldPosition, Is.EqualTo(Vector3.zero), "not wound up: nothing happens");

                    ability.SetAttackWoundUp();
                    UpdateState(state, boss, 0.5f);
                    string label = $"character {character} tolerance {tolerance}";
                    Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)), label);
                    Vector3 expectedForward = Quaternion.AngleAxis(expectedDegrees, Vector3.up) * Vector3.forward;
                    AssertNear(boss.ForwardDirection, expectedForward, label + " forward", 1e-3f);
                });
            }
        }

        // 0x704611..0x70461E: the initial face-angle tolerance applies only to the first slam (attack index 0) and only when the
        // definition's custom initial tolerance flag is set; later slams, or a first slam without the flag, use the normal tolerance.
        [Test]
        public void BodySlamStateUsesTheInitialToleranceForTheFirstSlam()
        {
            foreach ((int attackIndex, bool customInitial, float expectedDegrees) in new[] { (0, true, 0f), (1, true, 10f), (0, false, 10f) })
            {
                WithManager((owned, manager, characterObject) =>
                {
                    Boss boss = Prime(NewBoss(owned, Vector3.zero));
                    MakeCurrent(manager, characterObject, new Vector3(10f, 0f, 0f));
                    BossAbility_BodySlam ability = NewEnteredBodySlam(owned, boss, d =>
                    {
                        d.FaceAngleTolerance = 5f;
                        d.CustomInitialFaceAngleTolerance = customInitial;
                        d.InitialFaceAngleTolerance = 120f;
                        d.TurnSpeed = 20f;
                    }, attackIndex);
                    ability.SetAttackWoundUp();
                    UpdateState(NewState(nameof(BossState_BodySlam), AbilityJson(ActorAbilityType.Boss_BodySlam)), boss, 0.5f);
                    AssertNear(boss.ForwardDirection, Quaternion.AngleAxis(expectedDegrees, Vector3.up) * Vector3.forward, $"slam {attackIndex} custom initial tolerance {customInitial}", 1e-3f);
                });
            }
        }

        private const ActorAbilityType FlightAbility = ActorAbilityType.Boss_Death;

        // BossState_DeathFlight (02000204): while the ability is enabled the boss spins by the ability's spin rate times the delta time
        // (Euler degrees, applied in front of its rotation) and moves to the flight target; a disabled ability does nothing.
        [Test]
        public void DeathFlightStateSpinsAndMovesWhileEnabled()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(2f, 0f, 3f)));
                MakeCurrent(manager, characterObject, Vector3.zero);
                SetActorProperty(characterObject, "ForwardDirection", new Vector3(1f, 0f, 0f));
                BossAbility_DeathFlight ability = NewAbility<BossAbility_DeathFlight>(boss, NewDefinition<BossAbilityDefinition_DeathFlight>(owned, d =>
                {
                    d.SpinRateDegreesPerSecond = new Vector3(0f, 90f, 0f);
                    d.HeightCurve = AnimationCurve.Constant(0f, 10f, 4f);
                }));
                AddAbility(boss, FlightAbility, ability);
                IFSMState state = NewState(nameof(BossState_DeathFlight), AbilityJson(FlightAbility));

                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(2f, 0f, 3f)), "a disabled ability does nothing");

                ability.Enter();
                UpdateState(state, boss, 0.5f);
                Assert.That(boss.WorldPosition, Is.EqualTo(new Vector3(3f, 4f, 3f)), "the flight target one metre along the character's forward direction");
                AssertNear(boss.ForwardDirection, Quaternion.Euler(0f, 45f, 0f) * Vector3.forward, "spun 90 degrees a second for half a second", 1e-3f);
                UpdateState(state, boss, 0.5f);
                AssertNear(boss.ForwardDirection, Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, "and again", 1e-3f);
            });
        }

        // ---- the guardian and movement states ----

        private static ProjectileType CooledType => ProjectileType.GuardianKillerProjectile;

        // The rows GuardianProjectile.Fire reads (0x57BD00).
        private static void RegisterGuardianRows(Owned owned)
        {
            owned.LeaseRow<SaveManager>(); // BossManager's field initialiser caches a typed SaveManager reference.
            owned.Register(owned.Host("Lucid cool-down boss manager", false).AddComponent<BossManager>());
            owned.Register(owned.Host("Lucid cool-down guardian manager", false).AddComponent<GuardianManager>());
        }

        // The projectile is fired for real (state Fired, movement state from the boss storage), so that its cool-down is reachable.
        private static GuardianProjectile NewProjectile(Owned owned, ProjectileManager manager, Boss boss, ProjectileType type, out int[] coolDowns,
            GuardianProjectile.ProjectileMovementState movement = GuardianProjectile.ProjectileMovementState.FiredAsArc)
        {
            var projectile = EnemyAbilityTestSupport.AddTimeScaled<GuardianProjectile>(owned.Host("Lucid cool-down projectile", false));
            var definition = owned.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            // Reflection: Initialise needs the App's DataManager hazard table, and the type and definition are otherwise serialized.
            Set(definition, "m_projectileType", type);
            Set(projectile, "m_definition", definition);
            var counter = new int[1];
            var cooling = new UnityEvent();
            cooling.AddListener(() => counter[0]++);
            // Reflection: the cool-down event is a serialized field that the scene normally supplies.
            Set(projectile, "m_onCoolingDown", cooling);
            projectile.SetBoss(boss);
            boss.Storage.SetValue(BossFSMKeys.GuardianProjectileMovement, movement);
            projectile.Fire();
            // Reflection: the manager's own spawn path needs the pooled prefab set-up this test does not build.
            Get<List<Projectile>>(manager, "m_activeProjectiles").Add(projectile);
            coolDowns = counter;
            return projectile;
        }

        private static string CoolDownJson(bool onEntry, bool onLeave, bool allowExceptions, ProjectileType type) =>
            "{\"CoolDownAllOnEntry\":" + (onEntry ? "true" : "false") + ",\"CoolDownAllOnLeave\":" + (onLeave ? "true" : "false") +
            ",\"AllowExceptions\":" + (allowExceptions ? "true" : "false") + ",\"ProjectileType\":" + (int)type + "}";

        // BossState_CoolDownProjectilesOfType (02000202): the flags decide whether the projectiles of the type are forced into their
        // cool-down on entry and on leave; other types are left alone; with exceptions allowed the guardian projectiles that are
        // returning or resting keep going. Neither flag touches the projectile manager (none is registered in the first case).
        [Test]
        public void CoolDownStateForcesTheTypeOnEntryAndLeaveAsConfigured()
        {
            WithBoss((owned, boss) =>
            {
                Enter(NewState(nameof(BossState_CoolDownProjectilesOfType), CoolDownJson(false, false, false, CooledType)), boss);
                Leave(NewState(nameof(BossState_CoolDownProjectilesOfType), CoolDownJson(false, false, false, CooledType)), boss);
            });

            WithBoss((owned, boss) =>
            {
                RegisterGuardianRows(owned);
                ProjectileManager manager = owned.Register(owned.Host("Lucid cool-down projectile manager", false).AddComponent<ProjectileManager>());
                NewProjectile(owned, manager, boss, CooledType, out int[] cooled);
                NewProjectile(owned, manager, boss, ProjectileType.GuardianReturnableProjectile, out int[] other);

                IFSMState onEntry = NewState(nameof(BossState_CoolDownProjectilesOfType), CoolDownJson(true, false, false, CooledType));
                Leave(onEntry, boss);
                Assert.That(cooled[0], Is.Zero, "the entry-only state does nothing on leave");
                Enter(onEntry, boss);
                Assert.That((cooled[0], other[0]), Is.EqualTo((1, 0)), "only the matching type cools down");
            });

            WithBoss((owned, boss) =>
            {
                RegisterGuardianRows(owned);
                ProjectileManager manager = owned.Register(owned.Host("Lucid cool-down projectile manager", false).AddComponent<ProjectileManager>());
                NewProjectile(owned, manager, boss, CooledType, out int[] cooled);
                IFSMState onLeave = NewState(nameof(BossState_CoolDownProjectilesOfType), CoolDownJson(false, true, false, CooledType));
                Enter(onLeave, boss);
                Assert.That(cooled[0], Is.Zero, "the leave-only state does nothing on entry");
                Leave(onLeave, boss);
                Assert.That(cooled[0], Is.EqualTo(1));
            });
        }

        [Test]
        public void CoolDownStateSparesTheResumingGuardianProjectilesOnlyWhenExceptionsAreAllowed()
        {
            foreach ((bool allowExceptions, int expected) in new[] { (true, 0), (false, 1) })
            {
                WithBoss((owned, boss) =>
                {
                    RegisterGuardianRows(owned);
                    ProjectileManager manager = owned.Register(owned.Host("Lucid cool-down projectile manager", false).AddComponent<ProjectileManager>());
                    NewProjectile(owned, manager, boss, CooledType, out int[] cooled, GuardianProjectile.ProjectileMovementState.Resting);
                    Enter(NewState(nameof(BossState_CoolDownProjectilesOfType), CoolDownJson(true, false, allowExceptions, CooledType)), boss);
                    Assert.That(cooled[0], Is.EqualTo(expected), "exceptions allowed " + allowExceptions);
                });
            }
        }

        // BossState_DecideMovement (02000205): nothing happens without a current character. Otherwise the target is the point of the
        // movement range round the origin point (default zero, which is stored) opposite the character, both flattened onto the boss's
        // ground plane; it is placed 50 above the boss and dropped onto the track mask by a ray twice its height long, keeping its own
        // height when nothing is hit. It is stored as the desired movement position on entry and on every update.
        [Test]
        public void DecideMovementStoresTheCircleTargetOppositeTheCharacter()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(0f, 2f, 0f)));
                var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                definition.Traits = new BossTraits { TrackMask = 0 };
                // Reflection: the boss definition is a serialized field that the scene supplies (no App-free setter).
                Set(boss, "m_definition", definition);
                IFSMState state = NewState(nameof(BossState_DecideMovement), "{\"BossMovementRange\":10}");

                Enter(state, boss);
                Assert.That(Has(boss, BossFSMKeys.DesiredMovementPosition), Is.False, "no current character");
                Assert.That(Has(boss, BossFSMKeys.OriginPoint), Is.False);

                MakeCurrent(manager, characterObject, new Vector3(4f, 7f, 0f));
                UpdateState(state, boss, 0.1f);
                AssertNear(Stored<Vector3>(boss, BossFSMKeys.DesiredMovementPosition), new Vector3(-10f, 52f, 0f), "opposite the character at the range", 1e-3f);
                Assert.That(Has(boss, BossFSMKeys.OriginPoint), Is.True, "the default origin is stored");

                boss.Storage.SetValue(BossFSMKeys.OriginPoint, new Vector3(2f, 5f, 0f));
                MakeCurrent(manager, characterObject, new Vector3(5f, -3f, 4f));
                Enter(state, boss);
                AssertNear(Stored<Vector3>(boss, BossFSMKeys.DesiredMovementPosition), new Vector3(-4f, 52f, -8f), "round the stored origin, on the flattened plane", 1e-3f);
            });
        }

        [Test]
        public void DecideMovementDropsTheTargetOntoTheTrack()
        {
            WithManager((owned, manager, characterObject) =>
            {
                Boss boss = Prime(NewBoss(owned, new Vector3(0f, 2f, 0f)));
                var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                definition.Traits = new BossTraits();
                // Reflection: the boss definition is a serialized field that the scene supplies (no App-free setter).
                Set(boss, "m_definition", definition);
                GameObject track = owned.Host("Lucid boss track", true);
                track.transform.position = new Vector3(-10f, 2f, 0f);
                track.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, 4f);
                Physics.SyncTransforms();

                MakeCurrent(manager, characterObject, new Vector3(4f, 7f, 0f));
                Enter(NewState(nameof(BossState_DecideMovement), "{\"BossMovementRange\":10}"), boss);
                AssertNear(Stored<Vector3>(boss, BossFSMKeys.DesiredMovementPosition), new Vector3(-10f, 3f, 0f), "the top face of the track", 1e-3f);
            });
        }
    }
}
