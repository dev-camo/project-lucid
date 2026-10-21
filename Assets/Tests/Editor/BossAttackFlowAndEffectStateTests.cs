using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss graph states of Game.Runtime that drive the offensive abilities, the guardian and the boss's own storage, effects and
    // transforms (token, first constructor): ResetGuardianProjectileCount (0200020e, 0x707DA0), SetAttackAnimationStarted (02000212,
    // 0x708FE0), SetAttackFlow (02000213, 0x709240), SetAttackIsWindingUp (02000214, 0x7094A0), SetAttackIsWoundUp (02000215, 0x709700),
    // SetSkipAttackPreparation (0200021e, 0x70A2C0), GuardianReadyForFinalHit (0200020a, 0x706E40), KillProjectilesOfType (0200020b,
    // 0x706FB0), RestoreAllHealth (0200020f, 0x708000), SetAttackOverride (02000216, 0x709960), SetProjectileCount (0200021a, 0x709E70),
    // TriggerEffectOnEntry (02000227, 0x70B440) and TriggerEffectOnLeave (02000229, 0x70B670), the four SetValue states SetBossScale
    // (02000218, 0x709BE0), SetProjectileSpawnPoint (0200021c, 0x70A050), SetWorldY (02000221, 0x70A590) and SnapBodyToRotation
    // (02000224, 0x70B0A0), and SwitchActiveProjectileReturnCollider (02000226, 0x70B2D0). Each state is built from its JSON through the node factory and
    // driven through its own DoOnEnter/DoOnLeave bodies against a real Boss on an owned inactive host, which gets the animation storage and the
    // time-manager reference a started Actor would have created.
    // The IOffensiveAbility states are driven against a recording implementation of that interface, whose slot order (0x138 + 16 *
    // slot in the native vtable reads) decides which member each state calls.
    // Not covered: the publication of the effect message by Boss.TriggerEffects (it needs a registered MessageManager and a subscriber;
    // the state tests only lease the idle row it looks up), and the filter of the SetValue states' animation entries (the Actor
    // animation callbacks return early until the Actor is initialised).
    public sealed class BossAttackFlowAndEffectStateTests
    {
        // Fixture value, not an original constant: any key works for a boss that owns its ability dictionary.
        private const ActorAbilityType StateAbility = (ActorAbilityType)0x4C554349;

        // CreateInstance sends Awake and OnEnable, and both reach ActorDefinition.UpdateCachedValues (0x7A8060), which calls
        // Settings.UpdateCachedValues on the unassigned Settings with no null test: two faults each.
        private static readonly Regex ActorDefinitionSettingsFault =
            new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        private static string Name() => "Lucid-boss-flow-state-" + Guid.NewGuid().ToString("N");

        private static string AbilityJson(string extraFields = "") =>
            "{\"AbilityType\":" + (int)StateAbility + extraFields + "}";

        private static IFSMState NewState(string node, string json = "{}") =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static void Enter(IFSMState state, Boss boss) => Call(state, "DoOnEnter", boss, null);

        private static void Leave(IFSMState state, Boss boss) => Call(state, "DoOnLeave", boss, null);

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

        // Reflection: Boss.Initialise fills the attach point dictionary from the authored body components, which needs the full actor
        // setup. The record is the nested class the boss itself holds, with the given public field set.
        private static object AddBody(Boss boss, ActorAttachPointType point, string field, object value)
        {
            object body = Activator.CreateInstance(typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic));
            Set(body, field, value);
            ((IDictionary)Get<object>(boss, "m_attachPointDictionary"))[point] = body;
            return body;
        }

        // Implements only what the states under test call; every call is recorded in order.
        private sealed class RecordingOffensiveAbility : ActorAbility, IOffensiveAbility
        {
            public readonly List<string> Calls = new List<string>();

            public override AbilityDefinition DefinitionBase => null;
            public override ActorFormType GetFormType() => ActorFormType.None;
            public override ActorAnimationDefinition GetAnimationDefinition() => null;

            public bool StartWindUp { get { Calls.Add("get_StartWindUp"); return false; } }
            public bool WoundUp { get { Calls.Add("get_WoundUp"); return false; } }
            public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime)
            {
                Calls.Add("CanAttack");
                return false;
            }
            public void SetAttackFlow(bool abilityActive) => Calls.Add("SetAttackFlow(" + abilityActive + ")");
            public void SetAttackWindingUp() => Calls.Add("SetAttackWindingUp");
            public void SetAttackWoundUp() => Calls.Add("SetAttackWoundUp");
            public void SetAttackAnimStarted() => Calls.Add("SetAttackAnimStarted");
            public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition) =>
                Calls.Add("SkipAttackPreparation(" + (actorAnimationDefinition == null ? "null" : actorAnimationDefinition.name) + ")");
            public void ResetProjectileGroupCounter() => Calls.Add("ResetProjectileGroupCounter");
        }

        // ---- the IOffensiveAbility states ----

        // Each state calls exactly one member of the boss's offensive ability on entry (the interface slots read at 0x707FBA, 0x7091FA,
        // 0x70945A, 0x7096BA, 0x70991A and, for the skip, 0x70A4D7 and 0x70A557): ResetProjectileGroupCounter (8), SetAttackAnimStarted (6),
        // SetAttackFlow(true) (3), SetAttackWindingUp (4), SetAttackWoundUp (5), and SetAttackFlow(true) then SkipAttackPreparation (7) with
        // the state's own animation definition.
        [Test]
        public void OffensiveAbilityStatesCallTheirNativeInterfaceMemberOnEntry()
        {
            foreach ((string node, string[] expected) in new[]
            {
                (nameof(BossState_ResetGuardianProjectileCount), new[] { "ResetProjectileGroupCounter" }),
                (nameof(BossState_SetAttackAnimationStarted), new[] { "SetAttackAnimStarted" }),
                (nameof(BossState_SetAttackFlow), new[] { "SetAttackFlow(True)" }),
                (nameof(BossState_SetAttackIsWindingUp), new[] { "SetAttackWindingUp" }),
                (nameof(BossState_SetAttackIsWoundUp), new[] { "SetAttackWoundUp" }),
                (nameof(BossState_SetSkipAttackPreparation), new[] { "SetAttackFlow(True)", "SkipAttackPreparation(Lucid skip animation)" }),
            })
            {
                WithBoss((owned, boss) =>
                {
                    var ability = new RecordingOffensiveAbility();
                    AddAbility(boss, ability);
                    IFSMState state = NewState(node, AbilityJson());
                    var animation = owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                    animation.name = "Lucid skip animation";
                    // Reflection: the state's animation definition is looked up by name in the App's DataManager.
                    Set(state, "m_stateAnimationDefinition", animation);

                    Enter(state, boss);
                    Assert.That(ability.Calls, Is.EqualTo(expected), node);
                });
            }
        }

        // ---- BossState_GuardianReadyForFinalHit ----

        // 0x706F59: entry tells the guardian manager's final-hit callback, once per entry.
        [Test]
        public void GuardianReadyForFinalHitInvokesTheGuardianManagersCallback()
        {
            WithBoss((owned, boss) =>
            {
                GuardianManager guardian = owned.Register(owned.Host("Lucid final hit guardian", false).AddComponent<GuardianManager>());
                int ready = 0;
                guardian.FinalHitReady += () => ready++;
                IFSMState state = NewState(nameof(BossState_GuardianReadyForFinalHit));
                Enter(state, boss);
                Assert.That(ready, Is.EqualTo(1));
                Enter(state, boss);
                Assert.That(ready, Is.EqualTo(2), "every entry reports again");
            });
        }

        [Test]
        public void GuardianReadyForFinalHitWithoutACallbackDoesNothing()
        {
            WithBoss((owned, boss) =>
            {
                owned.Register(owned.Host("Lucid final hit guardian", false).AddComponent<GuardianManager>());
                Assert.DoesNotThrow(() => Enter(NewState(nameof(BossState_GuardianReadyForFinalHit)), boss));
            });
        }

        // ---- BossState_KillProjectilesOfType ----

        private static ProjectileType KilledType => ProjectileType.GuardianKillerProjectile;

        // The rows GuardianProjectile's construction reads (as the cool-down cases of BossStateTests register them).
        private static void RegisterGuardianRows(Owned owned)
        {
            owned.LeaseRow<TimeManager>(); // the time-scaled projectile subscribes to the TimeManager row
            owned.LeaseRow<SaveManager>(); // BossManager's field initialiser caches a typed SaveManager reference.
            owned.Register(owned.Host("Lucid kill boss manager", false).AddComponent<BossManager>());
            owned.Register(owned.Host("Lucid kill guardian manager", false).AddComponent<GuardianManager>());
        }

        // A projectile of the type that the manager lists as active; the killed ones are collected through the projectile's own expiry callback.
        private static GuardianProjectile NewListedProjectile(Owned owned, ProjectileManager manager, ProjectileType type, List<Projectile> expired)
        {
            var projectile = EnemyAbilityTestSupport.AddTimeScaled<GuardianProjectile>(owned.Host("Lucid kill projectile", false));
            var definition = owned.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            // Reflection: the type and definition are serialized and the pool normally initialises them (Initialise needs the App's DataManager).
            Set(definition, "m_projectileType", type);
            Set(projectile, "m_definition", definition);
            projectile.OnExpired = expired.Add;
            // Reflection: the manager's own spawn path needs the pooled prefab set-up this test does not build.
            Get<List<Projectile>>(manager, "m_activeProjectiles").Add(projectile);
            return projectile;
        }

        private static string KillJson(bool onEntry, bool onLeave, ProjectileType type) =>
            "{\"KillAllOnEntry\":" + (onEntry ? "true" : "false") + ",\"KillAllOnLeave\":" + (onLeave ? "true" : "false") +
            ",\"ProjectileType\":" + (int)type + "}";

        // 0x7070EB and 0x70719B: the flags decide whether the projectiles of the type are killed on entry and on leaving; projectiles
        // of other types are left alone.
        [Test]
        public void KillProjectilesStateKillsTheTypeOnEntryAndLeaveAsConfigured()
        {
            foreach ((bool onEntry, bool onLeave, int killedOnEntry, int killedAfterLeave) in new[]
            {
                (false, false, 0, 0), (true, false, 1, 1), (false, true, 0, 1),
            })
            {
                WithBoss((owned, boss) =>
                {
                    RegisterGuardianRows(owned);
                    ProjectileManager manager = owned.Register(owned.Host("Lucid kill projectile manager", false).AddComponent<ProjectileManager>());
                    var expired = new List<Projectile>();
                    GuardianProjectile target = NewListedProjectile(owned, manager, KilledType, expired);
                    NewListedProjectile(owned, manager, ProjectileType.GuardianReturnableProjectile, expired);
                    IFSMState state = NewState(nameof(BossState_KillProjectilesOfType), KillJson(onEntry, onLeave, KilledType));

                    Enter(state, boss);
                    string label = $"on entry {onEntry} on leave {onLeave}";
                    Assert.That(expired.Count, Is.EqualTo(killedOnEntry), label + " after entry");
                    Leave(state, boss);
                    Assert.That(expired.Count, Is.EqualTo(killedAfterLeave), label + " after leave");
                    if (killedAfterLeave > 0)
                        Assert.That(expired, Is.All.SameAs(target), label + " only the matching type");
                });
            }
        }

        // ---- BossState_RestoreAllHealth ----

        // 0x7080E2: the trigger restores the boss's health to its definition's pool (and the manager's current pool with it) on the
        // event its trigger type selects: entry (0), or leaving (1).
        [Test]
        public void RestoreAllHealthRefillsTheBossOnTheSelectedTriggerEvent()
        {
            foreach ((int eventType, bool restoredOnEnter) in new[] { (0, true), (1, false) })
            {
                Owned.Run(owned =>
                {
                    FourmenRig rig = NewFourmenRig(owned, Vector3.zero, Vector3.zero);
                    Boss boss = Prime(rig.Bosses[0]);
                    for (int i = 0; i < 2; i++) LogAssert.Expect(LogType.Exception, ActorDefinitionSettingsFault);
                    var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
                    definition.Traits = new BossTraits { HealthPool = 4 };
                    Set(boss, "m_definition", definition); // Reflection: serialized field without a setter
                    Call(boss, "RegisterBoss", rig.Manager); // Boss.Awake's callback; Edit Mode runs no Awake
                    Assert.That((boss.CurrentHealth, rig.Manager.BossHealthCurrent), Is.EqualTo((0, 0)));
                    IFSMState state = NewState(nameof(BossState_RestoreAllHealth), "{\"ActiveEventType\":" + eventType + "}");

                    Enter(state, boss);
                    Assert.That((boss.CurrentHealth, rig.Manager.BossHealthCurrent), Is.EqualTo(restoredOnEnter ? (4, 4) : (0, 0)), "after entry");
                    Leave(state, boss);
                    Assert.That((boss.CurrentHealth, rig.Manager.BossHealthCurrent), Is.EqualTo((4, 4)), "after leaving");
                });
            }
        }

        // ---- BossState_SetAttackOverride ----

        // 0x709B30: entry stores the configured attack type as the override in the graph user's storage, leaving the other keys alone.
        [Test]
        public void SetAttackOverrideStoresTheConfiguredAttackType()
        {
            foreach (BossAttackTypes attack in new[] { BossAttackTypes.BossCrab_Body, BossAttackTypes.BossCrab_LeftClaw })
            {
                WithBoss((owned, boss) =>
                {
                    boss.Storage.SetValue(BossFSMKeys.BossAttackType, BossAttackTypes.BossCrab_RightClaw);
                    IFSMState state = NewState(nameof(BossState_SetAttackOverride), "{\"AttackTypeToSet\":" + (int)attack + "}");
                    Assert.That(Has(boss, BossFSMKeys.BossAttackTypeOverride), Is.False);
                    Enter(state, boss);
                    Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackTypeOverride), Is.EqualTo(attack));
                    Assert.That(Stored<BossAttackTypes>(boss, BossFSMKeys.BossAttackType), Is.EqualTo(BossAttackTypes.BossCrab_RightClaw));
                });
            }
        }

        // ---- BossState_TriggerEffectOnEntry / BossState_TriggerEffectOnLeave ----

        // 0x70B5A6 and 0x70B7D6: the configured effect type is stored as the boss's effect type, on entry for one state and on leaving for
        // the other, and nothing is stored at the other moment.
        [Test]
        public void TriggerEffectStatesStoreTheEffectTypeOnTheirOwnEventOnly()
        {
            foreach ((string node, bool onEntry) in new[] { (nameof(BossState_TriggerEffectOnEntry), true), (nameof(BossState_TriggerEffectOnLeave), false) })
            {
                WithBoss((owned, boss) =>
                {
                    owned.LeaseRow<MessageManager>(); // Boss.TriggerEffects looks the row up; it is idle, so nothing is published
                    IFSMState state = NewState(node, "{\"TriggerEffect\":" + (int)BossEffectTriggerTypes.Injured + "}");
                    if (onEntry)
                    {
                        Leave(state, boss);
                        Assert.That(Has(boss, BossFSMKeys.BossEffectType), Is.False, node + " leaving");
                        Enter(state, boss);
                    }
                    else
                    {
                        Enter(state, boss);
                        Assert.That(Has(boss, BossFSMKeys.BossEffectType), Is.False, node + " entering");
                        Leave(state, boss);
                    }
                    Assert.That(Stored<BossEffectTriggerTypes>(boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.Injured), node);
                });
            }
        }

        // ---- BossState_SetProjectileCount ----

        // 0x70A021..0x70A038: entry gives the one-at-a-time projectile ability the configured count, which divides the definition's group
        // launch and spawn times (BossAbility_ProjectileSpawnAndShootOneAtATime.SetProjectileCount, 06000b07).
        [Test]
        public void SetProjectileCountPassesTheCountToTheOneAtATimeAbility()
        {
            foreach ((int count, float launch, float spawn) in new[] { (4, 0.5f, 0.25f), (8, 0.25f, 0.125f) })
            {
                Owned.Run(owned =>
                {
                    FourmenRig rig = NewFourmenRig(owned, Vector3.zero);
                    Boss boss = Prime(rig.Bosses[0]);
                    var ability = NewAbility<BossAbility_ProjectileSpawnAndShootOneAtATime>(boss,
                        NewDefinition<BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime>(owned, d =>
                        {
                            d.ProjectileGroupLaunchTimeSeconds = 2f;
                            d.ProjectileGroupSpawnTimeSeconds = 1f;
                        }));
                    AddAbility(boss, ability);

                    Enter(NewState(nameof(BossState_SetProjectileCount), AbilityJson(",\"ProjectileCount\":" + count)), boss);
                    Assert.That(Get<int>(ability, "m_projectileCount"), Is.EqualTo(count));
                    Assert.That(Get<float>(ability, "m_projectileFireDivisible"), Is.EqualTo(launch));
                    Assert.That(Get<float>(ability, "m_projectSpawnDivisible"), Is.EqualTo(spawn));
                });
            }
        }

        // ---- the SetValue states ----

        private static readonly (bool onEntry, bool onLeave)[] SetValueFlags = { (true, false), (false, true), (false, false), (true, true) };

        private static string SetValueJson(bool onEntry, bool onLeave, string fields) =>
            "{\"SetOnEntry\":" + (onEntry ? "true" : "false") + ",\"SetOnLeave\":" + (onLeave ? "true" : "false") + "," + fields + "}";

        private static SmoothedTransformRotator GiveRotator(Owned owned, Boss boss)
        {
            SmoothedTransformRotator rotator = owned.Host("Lucid boss body rotator", false).AddComponent<SmoothedTransformRotator>();
            Set(boss, "m_smoothedTransformRotator", rotator); // serialized scene reference
            return rotator;
        }

        // 0x709D4F and 0x709DEF: the scale of the rotator's object is set on entry and/or leaving according to the flags.
        [Test]
        public void SetBossScaleScalesTheRotatorObjectOnTheConfiguredEvents()
        {
            foreach ((bool onEntry, bool onLeave) in SetValueFlags)
            {
                WithBoss((owned, boss) =>
                {
                    SmoothedTransformRotator rotator = GiveRotator(owned, boss);
                    IFSMState state = NewState(nameof(BossState_SetBossScale),
                        SetValueJson(onEntry, onLeave, "\"Scale\":{\"x\":2,\"y\":3,\"z\":4}"));
                    Enter(state, boss);
                    Assert.That(rotator.transform.localScale, Is.EqualTo(onEntry ? new Vector3(2f, 3f, 4f) : Vector3.one), $"entry {onEntry}/{onLeave}");
                    rotator.transform.localScale = Vector3.one;
                    Leave(state, boss);
                    Assert.That(rotator.transform.localScale, Is.EqualTo(onLeave ? new Vector3(2f, 3f, 4f) : Vector3.one), $"leave {onEntry}/{onLeave}");
                });
            }
        }

        // 0x70A19B and 0x70A24B: the projectile spawn point of the attach point is moved to the local position on the configured events.
        [Test]
        public void SetProjectileSpawnPointMovesTheSpawnPointOnTheConfiguredEvents()
        {
            const ActorAttachPointType point = ActorAttachPointType.Boss_Crab_Body_VulnerablePoint;
            foreach ((bool onEntry, bool onLeave) in SetValueFlags)
            {
                WithBoss((owned, boss) =>
                {
                    Transform spawn = owned.Host("Lucid projectile spawn", false).transform;
                    AddBody(boss, point, "ProjectileSpawnPoint", spawn);
                    IFSMState state = NewState(nameof(BossState_SetProjectileSpawnPoint),
                        SetValueJson(onEntry, onLeave, "\"Position\":{\"x\":1,\"y\":2,\"z\":3},\"AttachPoint\":" + (int)point));
                    Enter(state, boss);
                    Assert.That(spawn.localPosition, Is.EqualTo(onEntry ? new Vector3(1f, 2f, 3f) : Vector3.zero), $"entry {onEntry}/{onLeave}");
                    spawn.localPosition = Vector3.zero;
                    Leave(state, boss);
                    Assert.That(spawn.localPosition, Is.EqualTo(onLeave ? new Vector3(1f, 2f, 3f) : Vector3.zero), $"leave {onEntry}/{onLeave}");
                });
            }
        }

        // 0x70A6CF and 0x70A77F: the boss's height is set (x and z kept) on the configured events, and the rotator's smoothing is
        // switched back on afterwards whatever it was (it is off while the position is set).
        [Test]
        public void SetWorldYSetsTheHeightAndReenablesTheRotatorsSmoothing()
        {
            foreach ((bool onEntry, bool onLeave) in SetValueFlags)
            {
                WithBoss((owned, boss) =>
                {
                    SmoothedTransformRotator rotator = GiveRotator(owned, boss);
                    boss.SetWorldPosition(new Vector3(1f, 5f, 2f));
                    rotator.ToggleSmoothing(false);
                    IFSMState state = NewState(nameof(BossState_SetWorldY), SetValueJson(onEntry, onLeave, "\"YValue\":9"));

                    Enter(state, boss);
                    Assert.That(boss.WorldPosition, Is.EqualTo(onEntry ? new Vector3(1f, 9f, 2f) : new Vector3(1f, 5f, 2f)), $"entry {onEntry}/{onLeave}");
                    Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.EqualTo(onEntry), "smoothing is restored only when the height was set");
                    boss.SetWorldPosition(new Vector3(1f, 5f, 2f));
                    rotator.ToggleSmoothing(false);
                    Leave(state, boss);
                    Assert.That(boss.WorldPosition, Is.EqualTo(onLeave ? new Vector3(1f, 9f, 2f) : new Vector3(1f, 5f, 2f)), $"leave {onEntry}/{onLeave}");
                    Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.EqualTo(onLeave));
                });
            }
        }

        // 0x70B1DF and 0x70B26F: the rotator's object is set to the local Euler rotation on the configured events, with smoothing
        // switched back on afterwards.
        [Test]
        public void SnapBodyToRotationSetsTheLocalRotationAndReenablesTheRotatorsSmoothing()
        {
            foreach ((bool onEntry, bool onLeave) in SetValueFlags)
            {
                WithBoss((owned, boss) =>
                {
                    SmoothedTransformRotator rotator = GiveRotator(owned, boss);
                    Call(rotator, "Initialise"); // Awake does not run in Edit Mode; this is its body (it caches the transform).
                    rotator.ToggleSmoothing(false);
                    IFSMState state = NewState(nameof(BossState_SnapBodyToRotation),
                        SetValueJson(onEntry, onLeave, "\"Rotation\":{\"x\":0,\"y\":90,\"z\":0}"));

                    Enter(state, boss);
                    Assert.That(Quaternion.Angle(rotator.transform.localRotation, onEntry ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity),
                        Is.LessThan(0.05f), $"entry {onEntry}/{onLeave}");
                    Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.EqualTo(onEntry));
                    rotator.transform.localRotation = Quaternion.identity;
                    rotator.ToggleSmoothing(false);
                    Leave(state, boss);
                    Assert.That(Quaternion.Angle(rotator.transform.localRotation, onLeave ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity),
                        Is.LessThan(0.05f), $"leave {onEntry}/{onLeave}");
                    Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.EqualTo(onLeave));
                });
            }
        }

        // ---- BossState_SwitchActiveProjectileReturnCollider ----

        // 0x70B3E2..0x70B431: the guardian's phase-four projectile spawn collider is activated and its projectile return collider is
        // deactivated (the other way round would be the reverse of the native order of the two attach points).
        [Test]
        public void SwitchActiveProjectileReturnColliderActivatesTheSpawnColliderAndDeactivatesTheReturnCollider()
        {
            WithBoss((owned, boss) =>
            {
                GameObject spawn = owned.Host("Lucid phase four spawn collider", false);
                GameObject returning = owned.Host("Lucid return collider", true);
                AddBody(boss, ActorAttachPointType.BossGuardian_ProjectileSpawnPoint_Phase4, "Collider", spawn.AddComponent<BoxCollider>());
                AddBody(boss, ActorAttachPointType.BossGuardian_ProjectileReturnPoint, "Collider", returning.AddComponent<BoxCollider>());

                Enter(NewState(nameof(BossState_SwitchActiveProjectileReturnCollider)), boss);
                Assert.That((spawn.activeSelf, returning.activeSelf), Is.EqualTo((true, false)));
            });
        }
    }
}
