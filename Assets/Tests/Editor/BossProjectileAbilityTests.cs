using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The guardian's projectile attacks of Game.Runtime: BossAbility_ProjectileSpawnAndShootBase<T> (020001e8, shared generic code
    // 0xE3DE10..0xE3E85C), BossAbility_ProjectileSpawnAndShootInRows (020001ea, 0x6FC8E0..0x6FD4A0) and
    // BossAbility_ProjectileSpawnAndShootOneAtATime (020001eb, 0x6FD4A0..0x6FE0F0). Expected values come from the x86-64 bodies cited per
    // case; the abilities are built by their real constructors against a real Boss whose projectile spawn point has real lane children.
    // Not covered, because they need the BossManager's projectile pools or a live Projectile: SpawnAProjectile (06000ade) and with it the
    // spawning of the group, the Projectile.Fire() call of the spawned projectiles, and the OneAtATime GetSpline error log.
    public sealed class BossProjectileAbilityTests
    {
        private const ActorAttachPointType SpawnAttach = ActorAttachPointType.Boss_Crab_Body_VulnerablePoint;

        private sealed class ProjectileScene
        {
            public FourmenRig Rig;
            public Boss Boss;
            public Transform Spawn;
            public List<Transform> Lanes = new List<Transform>();
        }

        // A boss whose attach point carries a projectile spawn transform with the given number of lane children, as the authored
        // boss prefab has. The boss manager is registered first because the base ability asks for it when it is initialised.
        private static ProjectileScene NewScene(Owned owned, int laneCount, Vector3 spawnPosition)
        {
            var scene = new ProjectileScene { Rig = NewFourmenRig(owned, Vector3.zero) };
            scene.Boss = scene.Rig.Bosses[0];
            scene.Spawn = owned.Host("Lucid projectile spawn", true).transform;
            scene.Spawn.position = spawnPosition;
            for (int i = 0; i < laneCount; i++)
            {
                Transform lane = owned.Host("Lucid projectile lane " + i, true).transform;
                lane.SetParent(scene.Spawn, false);
                lane.localPosition = new Vector3(i * 2f, 0f, 0f);
                scene.Lanes.Add(lane);
            }
            // Reflection: Boss.Initialise builds the attach point dictionary from the serialized body components of the scene's boss prefab.
            Type part = typeof(Boss).GetNestedType("BossBodyComponents", BindingFlags.NonPublic);
            object record = Activator.CreateInstance(part);
            part.GetField("AttachPoint").SetValue(record, SpawnAttach);
            part.GetField("ProjectileSpawnPoint").SetValue(record, scene.Spawn);
            part.GetMethod("Initialise").Invoke(record, null);
            ((IDictionary)Get<object>(scene.Boss, "m_attachPointDictionary")).Add(SpawnAttach, record);
            return scene;
        }

        private static BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData Row(string lanes, string returnable) =>
            new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData { LanesInUseCSV = lanes, ReturnableLanesCSV = returnable };

        private static BossAbility_ProjectileSpawnAndShootInRows NewRows(Owned owned, ProjectileScene scene,
            Action<BossAbilityDefinition_ProjectileSpawnAndShootInRows> setup = null) =>
            NewAbility<BossAbility_ProjectileSpawnAndShootInRows>(scene.Boss, NewDefinition<BossAbilityDefinition_ProjectileSpawnAndShootInRows>(owned, d =>
            {
                d.SpawnPointAttach = SpawnAttach;
                d.KillerProjectile = ProjectileType.GuardianKillerProjectile;
                d.ReturningProjectile = ProjectileType.GuardianReturnableProjectile;
                d.ProjectileGroupToContainReturnable = 2;
                d.SpawnOrders = new[] { Row("0,1,2", "1") };
                setup?.Invoke(d);
            }));

        private static BossAbility_ProjectileSpawnAndShootOneAtATime NewOneAtATime(Owned owned, ProjectileScene scene,
            Action<BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime> setup = null) =>
            NewAbility<BossAbility_ProjectileSpawnAndShootOneAtATime>(scene.Boss, NewDefinition<BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime>(owned, d =>
            {
                d.SpawnPointAttach = SpawnAttach;
                d.KillerProjectile = ProjectileType.GuardianKillerProjectile;
                d.ReturningProjectile = ProjectileType.GuardianReturnableProjectile;
                d.ProjectileGroupToContainReturnable = 2;
                setup?.Invoke(d);
            }));

        private static ProjectileType Decide(object ability, int index)
        {
            object[] arguments = { index, default(ProjectileType) };
            Call(ability, "DecideProjectileType", arguments);
            return (ProjectileType)arguments[1];
        }

        // ---- the shared base ----

        // The base's own DefinitionBase auto-property is never assigned (06000ad2); the derived abilities override it.
        // 06000ae5 lets every attack start; 06000ae6 stores the active flag and raises the start request; 06000ae7/06000ae8/06000ae9 wind
        // up, wound up and animation started; 06000aea skips the preparation (wind up and wound up); 06000aec restarts the group counter at one.
        [Test]
        public void ProjectileFlagsFollowTheNativeStores()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene);
                Assert.That(ability.CanAttack(scene.Boss, null, null, 0f), Is.True);
                Assert.That((ability.StartWindUp, ability.WoundUp, ability.IsTriggered), Is.EqualTo((false, false, false)));
                ability.SetAttackFlow(false);
                Assert.That((ability.StartWindUp, Get<bool>(ability, "m_abilityActivated")), Is.EqualTo((true, false)), "the request is raised without activating");
                ability.SetAttackFlow(true);
                Assert.That(Get<bool>(ability, "m_abilityActivated"), Is.True);
                ability.SetAttackWindingUp();
                ability.SetAttackWoundUp();
                ability.SetAttackAnimStarted();
                Assert.That((ability.WoundUp, Get<bool>(ability, "m_isWindingUp"), Get<bool>(ability, "m_attackAnimStarted")), Is.EqualTo((true, true, true)));

                BossAbility_ProjectileSpawnAndShootInRows skipped = NewRows(owned, scene);
                skipped.SkipAttackPreparation(null);
                Assert.That((skipped.WoundUp, Get<bool>(skipped, "m_isWindingUp")), Is.EqualTo((true, true)));
                Assert.That(Get<int>(skipped, "m_projectileGroupCount"), Is.Zero);
                skipped.ResetProjectileGroupCounter();
                Assert.That(Get<int>(skipped, "m_projectileGroupCount"), Is.EqualTo(1));
            });
        }

        // 06000ae2: true while the wind-up is not wound up; the start request drops once winding up or wound up.
        [Test]
        public void WindingUpTruthTable()
        {
            foreach ((bool windingUp, bool woundUp, bool expected, bool startKept) in new[]
            {
                (false, false, true, true), (true, false, true, false), (true, true, false, false), (false, true, false, false),
            })
            {
                Owned.Run(owned =>
                {
                    BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, NewScene(owned, 3, Vector3.zero));
                    ability.SetAttackFlow(true);
                    if (windingUp)
                        ability.SetAttackWindingUp();
                    if (woundUp)
                        ability.SetAttackWoundUp();
                    string label = $"winding up {windingUp}, wound up {woundUp}";
                    Assert.That((bool)Call(ability, "WindingUp"), Is.EqualTo(expected), label);
                    Assert.That(ability.StartWindUp, Is.EqualTo(startKept), label + " start request");
                });
            }
        }

        // 06000ae3: the animation trigger rises on the first call; nothing spawns until the animation has started and its length (a
        // strict comparison) has run; with no spawn time the group counts as spawned at once, which ends the spawning.
        [Test]
        public void SpawningWaitsForTheAttackAnimationThenEnds()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.AttackAnimationLengthSeconds = 1f;
                    d.ProjectileGroupSpawnTimeSeconds = 0f;
                });
                Assert.That((bool)Call(ability, "Spawning", 0.5f), Is.True);
                Assert.That(ability.IsTriggered, Is.True, "the first call raises the animation trigger");
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.Zero, "the animation has not started");

                ability.SetAttackAnimStarted();
                Assert.That((bool)Call(ability, "Spawning", 0.5f), Is.True);
                Assert.That((bool)Call(ability, "Spawning", 0.5f), Is.True);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(1f));
                Assert.That(Get<bool>(ability, "m_projectilesSpawned"), Is.False);
                Assert.That((bool)Call(ability, "Spawning", 0.5f), Is.False, "1 is not above 1: spawning is over");
                Assert.That(Get<bool>(ability, "m_projectilesSpawned"), Is.True);
            });
        }

        // 06000ad8/06000ad9: entering fetches the spawn transform of the definition's attach point; leaving clears every flag, timer and
        // list, and counts the group.
        [Test]
        public void LeaveClearsTheGroupAndCountsIt()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                // Two equal patterns: the second Enter must find one outside the recently used queue (06000af4 loops until it does).
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene,
                    d =>
                    {
                        d.SpawnOrders = new[] { Row("0,1,2", "1"), Row("0,1,2", "1") };
                        d.AttackAnimationLengthSeconds = 1f;
                        d.ProjectileGroupLaunchTimeSeconds = 1f;
                    });
                ability.Enter();
                Assert.That(Get<Transform>(ability, "m_spawnTransform"), Is.SameAs(scene.Spawn));
                ability.SetAttackFlow(true);
                ability.SkipAttackPreparation(null);
                ability.SetAttackAnimStarted();
                // Group 0 and the returnable lane make the returning projectile; the updates run the animation (1 second), mark the
                // projectiles spawned (the spawn time is zero) and start the launch time.
                Decide(ability, 1);
                foreach (float delta in new[] { 0.5f, 0.5f, 0.5f })
                    ability.Update(delta);
                Assert.That((ability.IsTriggered, Get<bool>(ability, "m_projectilesSpawned"), Get<bool>(ability, "m_hasReturningProjectile")), Is.EqualTo((true, true, true)));
                Assert.That((Get<float>(ability, "m_attackAnimationTimer"), Get<float>(ability, "m_projectileFireTimer")), Is.EqualTo((1f, 0.5f)));
                // Reflection: these are reached only by spawning a pooled projectile (BossManager.SpawnProjectileFromBoss), which this case cannot build.
                Set(ability, "m_spawnedCurrentProjectile", true);
                Set(ability, "m_currentlyFiringProjectileIndex", 2);
                Set(ability, "m_currentlySpawningProjectileIndex", 2);
                Set(ability, "m_projectileSpawnTimer", 1f);
                // Reflection: the spawn delay timer is advanced only by the one-at-a-time attack's Spawning override, not by this attack.
                Set(ability, "m_spawnDelayTimer", 1f);
                Get<List<Projectile>>(ability, "m_projectiles").Add(null); // Reflection: only SpawnAProjectile fills the list

                ability.Leave();
                Assert.That((ability.AbilityFinished, ability.StartWindUp, ability.WoundUp, ability.IsTriggered), Is.EqualTo((false, false, false, false)));
                foreach (string flag in new[] { "m_abilityActivated", "m_hasReturningProjectile", "m_spawnedCurrentProjectile", "m_isWindingUp", "m_attackAnimStarted", "m_projectilesSpawned" })
                    Assert.That(Get<bool>(ability, flag), Is.False, flag);
                foreach (string index in new[] { "m_currentlyFiringProjectileIndex", "m_currentlySpawningProjectileIndex" })
                    Assert.That(Get<int>(ability, index), Is.Zero, index);
                foreach (string timer in new[] { "m_attackAnimationTimer", "m_projectileFireTimer", "m_projectileSpawnTimer", "m_spawnDelayTimer" })
                    Assert.That(Get<float>(ability, timer), Is.Zero, timer);
                Assert.That(Get<List<Projectile>>(ability, "m_projectiles"), Is.Empty);
                Assert.That(Get<int>(ability, "m_projectileGroupCount"), Is.EqualTo(1));
                ability.Enter(); // a second Leave only acts on an enabled ability (ActorAbility.Leave)
                ability.Leave();
                Assert.That(Get<int>(ability, "m_projectileGroupCount"), Is.EqualTo(2));
            });
        }

        // 06000adb: the strike effect trigger is published and the boss's effects fire.
        [Test]
        public void ProjectileStrikePublishesTheAttackStrikeEffect()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene);
                Call(ability, "TriggerAttackStrikeEffects");
                Assert.That(Stored<BossEffectTriggerTypes>(scene.Boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
            });
        }

        // ---- BossAbility_ProjectileSpawnAndShootInRows ----

        // 06000af2: the definition is the derived class's own cast of the supplied one, and the declaration of the base ability
        // keeps no definition of its own.
        [Test]
        public void RowsTakesItsDefinitionFromItsOwnInitialise()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                var definition = NewDefinition<BossAbilityDefinition_ProjectileSpawnAndShootInRows>(owned, d => d.SpawnOrders = new[] { Row("0", "0") });
                BossAbility_ProjectileSpawnAndShootInRows ability = NewAbility<BossAbility_ProjectileSpawnAndShootInRows>(scene.Boss, definition);
                Assert.That(ability.DefinitionBase, Is.SameAs(definition));
                Assert.That(Get<BossAbilityDefinition_ProjectileSpawnAndShootInRows>(ability, "Definition"), Is.SameAs(definition));
                Assert.That(Get<Boss>(ability, "m_boss"), Is.SameAs(scene.Boss));
            });
        }

        // 06000af3: entering aims at the boss, publishes the definition's initial movement state, collects the lanes from the spawn
        // point's children and takes a random row pattern: its parsed lane count is the group size, the launch and spawn times are
        // divided by it, its lanes are the spawn order and one of its returnable lanes is chosen. The stale spawn order is cleared.
        [Test]
        public void RowsEnterSetsUpTheRowPattern()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                scene.Boss.SetWorldPosition(new Vector3(7f, 8f, 9f));
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.InitialMovementState = GuardianProjectile.ProjectileMovementState.FiredOverhead;
                    d.ProjectileGroupLaunchTimeSeconds = 3f;
                    d.ProjectileGroupSpawnTimeSeconds = 1.5f;
                    d.SpawnOrders = new[] { Row("0, 2,x", "2,1") };
                });
                Get<List<int>>(ability, "m_projectileSpawnOrder").Add(99);
                ability.Enter();

                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                Assert.That(Stored<GuardianProjectile.ProjectileMovementState>(scene.Boss, BossFSMKeys.GuardianProjectileMovement),
                    Is.EqualTo(GuardianProjectile.ProjectileMovementState.FiredOverhead));
                Assert.That(Get<List<Transform>>(ability, "m_availableLanes"), Is.EqualTo(scene.Lanes));
                Assert.That(Get<List<Spline>>(ability, "m_splines").Count, Is.EqualTo(3), "an overhead attack looks up a spline under every lane");
                Assert.That(Get<int>(ability, "m_projectileCount"), Is.EqualTo(2), "the entry that does not parse is skipped");
                Assert.That(Get<float>(ability, "m_projectileFireDivisible"), Is.EqualTo(1.5f));
                Assert.That(Get<float>(ability, "m_projectSpawnDivisible"), Is.EqualTo(0.75f));
                Assert.That(Get<List<int>>(ability, "m_projectileSpawnOrder"), Is.EqualTo(new[] { 0, 2 }));
                var row = Get<BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData>(ability, "m_spawnDataThisRow");
                Assert.That(row.ReturnableLane, Is.EqualTo(2).Or.EqualTo(1));
            });
        }

        // The splines are only looked up for an overhead attack, and the lanes are only rebuilt when their number changed.
        [Test]
        public void RowsEnterKeepsTheLanesAndSkipsTheSplinesOtherwise()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene);
                var kept = Get<List<Transform>>(ability, "m_availableLanes");
                kept.AddRange(scene.Lanes.AsEnumerable().Reverse());
                ability.Enter();
                Assert.That(Get<List<Transform>>(ability, "m_availableLanes"), Is.EqualTo(scene.Lanes.AsEnumerable().Reverse()), "three lanes are already listed");
                Assert.That(Get<List<Spline>>(ability, "m_splines"), Is.Empty);
            });
        }

        // 06000af4: a pattern used within the last RowPatternIgnoreCount entries is not picked again, so four entries over four
        // patterns with an ignore count of three use every pattern once.
        [Test]
        public void RowsEnterAvoidsTheRecentlyUsedPatterns()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.RowPatternIgnoreCount = 3;
                    d.SpawnOrders = new[] { Row("0", "0"), Row("0,1", "1"), Row("0,1,2", "2"), Row("2,1", "1") };
                });
                var definition = Get<BossAbilityDefinition_ProjectileSpawnAndShootInRows>(ability, "Definition");
                var used = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    ability.Enter();
                    var row = Get<BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData>(ability, "m_spawnDataThisRow");
                    used.Add(Array.IndexOf(definition.SpawnOrders, row));
                    Assert.That(Get<int>(ability, "m_projectileCount"), Is.EqualTo(row.LanesInUseInt.Count));
                }
                Assert.That(used.OrderBy(pattern => pattern), Is.EqualTo(new[] { 0, 1, 2, 3 }), "used " + string.Join(",", used));
                Assert.That(Get<Queue<int>>(ability, "m_recentlyUsedPatterns").ToArray(), Is.EqualTo(used.Skip(1).ToArray()),
                    "the queue holds the last three");
            });
        }

        // 06000af5: the returning projectile is the row's returnable lane, in the groups whose number divides evenly by the group size
        // of the returnable; every other projectile is a killer. A zero group size divides by zero, as in the original.
        [Test]
        public void RowsDecideTheReturningProjectileByGroupAndLane()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                // Two equal patterns with an ignore count of one: each re-entry must find the one outside the recently used queue (06000af4 loops until it does).
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.RowPatternIgnoreCount = 1;
                    d.SpawnOrders = new[] { Row("0,1,2", "1"), Row("0,1,2", "1") };
                });
                ability.Enter();
                Assert.That(Decide(ability, 0), Is.EqualTo(ProjectileType.GuardianKillerProjectile), "group 0, another lane");
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.False);
                Assert.That(Decide(ability, 1), Is.EqualTo(ProjectileType.GuardianReturnableProjectile), "group 0, the returnable lane");
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.True);

                ability.ResetProjectileGroupCounter();
                Assert.That(Decide(ability, 1), Is.EqualTo(ProjectileType.GuardianKillerProjectile), "group 1 is not a multiple of 2");
                for (int group = 2; group <= 4; group++)
                {
                    ability.Leave(); // each leave counts the group
                    ability.Enter();
                }
                Assert.That(Decide(ability, 1), Is.EqualTo(ProjectileType.GuardianReturnableProjectile), "group 4 is");

                BossAbility_ProjectileSpawnAndShootInRows divisionByZero = NewRows(owned, scene, d =>
                {
                    d.ProjectileGroupToContainReturnable = 0;
                    d.SpawnOrders = new[] { Row("0", "0") };
                });
                divisionByZero.Enter();
                Assert.Throws<DivideByZeroException>(() => Decide(divisionByZero, 0));
            });
        }

        // 06000af6..06000afb: the lane of a projectile index is the listed lane at that index; the projectile is placed at it and the
        // spawn origin is its position; spline lookups index the spline list; blocking a lane does nothing.
        [Test]
        public void RowsLaneHelpersUseTheListedLanes()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 3, new Vector3(10f, 0f, 0f));
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene);
                ability.Enter();
                Assert.That((Transform)Call(ability, "GetLaneChoice", 2), Is.SameAs(scene.Lanes[2]));
                Assert.That((Vector3)Call(ability, "GetSpawnOrigin", 1), Is.EqualTo(new Vector3(12f, 0f, 0f)));
                Transform moved = owned.Host("Lucid moved projectile", true).transform;
                Call(ability, "OffsetSpawnLocation", 2, moved);
                Assert.That(moved.position, Is.EqualTo(new Vector3(14f, 0f, 0f)));
                Call(ability, "BlockSpawnLane", scene.Lanes[0]);
                Assert.That(Get<List<Transform>>(ability, "m_availableLanes"), Is.EqualTo(scene.Lanes), "nothing is blocked");
                Get<List<Spline>>(ability, "m_splines").Add(null);
                Assert.That(Call(ability, "GetSpline", 0), Is.Null);
            });
        }

        // 06000afa: the launch time counts up while it is running; the result is whether it was still running at the start of the call.
        // Once it has run out the strike effects fire and the animation restarts, and the call reports that the attack is over.
        [Test]
        public void RowsAttackingCountsTheLaunchTimeThenStrikes()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.ProjectileGroupLaunchTimeSeconds = 1f;
                    d.AttackAnimationLengthSeconds = 1f;
                });
                ability.SetAttackAnimStarted();
                Call(ability, "Spawning", 0.5f);
                Call(ability, "Spawning", 0.25f);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(0.75f), "the animation has run for 0.75 seconds");
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.True);
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.True, "0.5 is below 1");
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(1f));
                Assert.That(Has(scene.Boss, BossFSMKeys.BossEffectType), Is.False);
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.False, "1 is not above 1");
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(1f), "the timer no longer runs");
                Assert.That(Stored<BossEffectTriggerTypes>(scene.Boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
                Assert.That((Get<bool>(ability, "m_attackAnimStarted"), Get<float>(ability, "m_attackAnimationTimer")), Is.EqualTo((false, 0f)));
            });
        }

        // 06000ada: nothing runs before the attack flow is active or while winding up; then spawning and attacking run in turn and the
        // ability finishes in the update in which the attack reports that it is over.
        [Test]
        public void RowsUpdateRunsTheAttackAndFinishesWhenItIsOver()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ProjectileScene scene = NewScene(owned, 3, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootInRows ability = NewRows(owned, scene, d =>
                {
                    d.AttackAnimationLengthSeconds = 0f;
                    d.ProjectileGroupSpawnTimeSeconds = 0f;
                    d.ProjectileGroupLaunchTimeSeconds = 1f;
                });
                ability.Enter();
                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.Zero, "not activated");

                ability.SetAttackFlow(true);
                ability.SetAttackAnimStarted();
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.False, "still winding up");

                ability.SkipAttackPreparation(null);
                ability.Update(0.5f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(0.5f));
                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.False);
                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.True);
            });
        }

        // ---- BossAbility_ProjectileSpawnAndShootOneAtATime ----

        // 06000afe: the queue of blocked lanes is rebuilt; the base ability keeps the supplied boss and its own definition cast.
        [Test]
        public void OneAtATimeTakesItsOwnDefinition()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene);
                Assert.That(ability.DefinitionBase, Is.Not.Null);
                Assert.That(Get<Queue<Transform>>(ability, "m_blockedLanes"), Is.Empty);
            });
        }

        // 06000aff: entering runs the base entry first and last, aims at the boss, publishes the movement state, lists every child as a
        // lane (the previous lanes and blocked lanes are dropped) and shuffles the numbers 0..count-1 into the spawn order.
        [Test]
        public void OneAtATimeEnterListsTheLanesAndShufflesTheSpawnOrder()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                scene.Boss.SetWorldPosition(new Vector3(1f, 2f, 3f));
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene);
                ability.SetProjectileCount(4);
                Get<List<Transform>>(ability, "m_availableLanes").Add(scene.Spawn);
                Get<Queue<Transform>>(ability, "m_blockedLanes").Enqueue(scene.Spawn);
                ability.Enter();

                Assert.That(ability.CurrentTargetPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(Stored<GuardianProjectile.ProjectileMovementState>(scene.Boss, BossFSMKeys.GuardianProjectileMovement),
                    Is.EqualTo(GuardianProjectile.ProjectileMovementState.FiredAsArc));
                Assert.That(Get<List<Transform>>(ability, "m_availableLanes"), Is.EqualTo(scene.Lanes));
                Assert.That(Get<Queue<Transform>>(ability, "m_blockedLanes"), Is.Empty);
                Assert.That(Get<List<int>>(ability, "m_projectileSpawnOrder").OrderBy(number => number), Is.EqualTo(new[] { 0, 1, 2, 3 }));
                Assert.That(Get<Transform>(ability, "m_spawnTransform"), Is.SameAs(scene.Spawn));

                var order = Get<List<int>>(ability, "m_projectileSpawnOrder");
                order.Add(7);
                ability.Enter();
                Assert.That(order.OrderBy(number => number), Is.EqualTo(new[] { 0, 1, 2, 3 }), "a spawn order of the wrong length is rebuilt");
            });
        }

        // 06000b07: the group size divides the launch and spawn times of the definition.
        [Test]
        public void OneAtATimeSetProjectileCountDividesTheGroupTimes()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d =>
                {
                    d.ProjectileGroupLaunchTimeSeconds = 2f;
                    d.ProjectileGroupSpawnTimeSeconds = 1f;
                });
                ability.SetProjectileCount(4);
                Assert.That(Get<int>(ability, "m_projectileCount"), Is.EqualTo(4));
                Assert.That(Get<float>(ability, "m_projectileFireDivisible"), Is.EqualTo(0.5f));
                Assert.That(Get<float>(ability, "m_projectSpawnDivisible"), Is.EqualTo(0.25f));
            });
        }

        // 06000b00: the returning projectile is chosen only in the groups that carry one and only while none was chosen; the last
        // projectile of the group is always the returning one (its chance is one), the others are killers. The choice sticks.
        [Test]
        public void OneAtATimeDecidesTheReturningProjectile()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene);
                ability.SetProjectileCount(3);

                ability.ResetProjectileGroupCounter();
                Assert.That(Decide(ability, 2), Is.EqualTo(ProjectileType.GuardianKillerProjectile), "group 1 carries no returnable");
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.False);

                ability.Enter();
                ability.Leave(); // leaving counts the group: 2
                Assert.That(Decide(ability, 2), Is.EqualTo(ProjectileType.GuardianReturnableProjectile), "the last of the group is always chosen");
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.True);
                Assert.That(Decide(ability, 0), Is.EqualTo(ProjectileType.GuardianKillerProjectile), "once chosen the rest are killers");
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.True);

                // Two more groups (3, 4); leaving also clears the chosen returning projectile.
                for (int group = 3; group <= 4; group++)
                {
                    ability.Enter();
                    ability.Leave();
                }
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.False);
                ProjectileType drawn = Decide(ability, 0);
                Assert.That(drawn, Is.EqualTo(ProjectileType.GuardianKillerProjectile).Or.EqualTo(ProjectileType.GuardianReturnableProjectile));
                Assert.That(Get<bool>(ability, "m_hasReturningProjectile"), Is.EqualTo(drawn == ProjectileType.GuardianReturnableProjectile),
                    "the flag records whether the chance came up");

                BossAbility_ProjectileSpawnAndShootOneAtATime divisionByZero = NewOneAtATime(owned, scene, d => d.ProjectileGroupToContainReturnable = 0);
                Assert.Throws<DivideByZeroException>(() => Decide(divisionByZero, 0));
            });
        }

        // 06000b02: the projectiles spawn on a circle round the spawn point, spaced by a whole number of degrees (integer division of
        // 360 by the group size), the circle point handed to the spawn transform as (y, z, x). The spawn origin is the spawn point.
        [Test]
        public void OneAtATimeSpawnsTheProjectilesOnACircleAroundTheSpawnPoint()
        {
            foreach ((int count, int index, double degrees) in new[] { (4, 1, 90.0), (3, 1, 120.0), (7, 1, 51.0), (4, 0, 0.0) })
            {
                Owned.Run(owned =>
                {
                    var spawnPosition = new Vector3(10f, 20f, 30f);
                    ProjectileScene scene = NewScene(owned, 4, spawnPosition);
                    BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d => d.ProjectileSpawnCircleRadius = 6f);
                    ability.SetProjectileCount(count);
                    ability.Enter();
                    Transform moved = owned.Host("Lucid spawned projectile", true).transform;
                    Call(ability, "OffsetSpawnLocation", index, moved);
                    double radians = degrees * Math.PI / 180.0;
                    // The circle point is (sin * r, 0, cos * r); it is handed over as (y, z, x).
                    var expected = spawnPosition + new Vector3(0f, (float)(Math.Cos(radians) * 6.0), (float)(Math.Sin(radians) * 6.0));
                    AssertNear(moved.position, expected, $"count {count} index {index}", 2e-3f);
                    Assert.That((Vector3)Call(ability, "GetSpawnOrigin", 0), Is.EqualTo(spawnPosition));
                });
            }
        }

        // 06000b05: with the base spawning over, the delay between spawning and moving to the lanes (a strict comparison) is waited out.
        [Test]
        public void OneAtATimeSpawningWaitsOutTheDelayBeforeTheLaneMove()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d =>
                {
                    d.AttackAnimationLengthSeconds = 0f;
                    d.ProjectileGroupSpawnTimeSeconds = 0f;
                    d.TimeBetweenSpawnAndLaneMoveSeconds = 0.25f;
                });
                Assert.That((bool)Call(ability, "Spawning", 0.25f), Is.True, "the animation has not started");
                ability.SetAttackAnimStarted();
                Assert.That((bool)Call(ability, "Spawning", 0.25f), Is.True);
                Assert.That(Get<float>(ability, "m_spawnDelayTimer"), Is.EqualTo(0.25f));
                Assert.That((bool)Call(ability, "Spawning", 0.25f), Is.False, "0.25 is not above 0.25");
                Assert.That(Get<float>(ability, "m_spawnDelayTimer"), Is.EqualTo(0.25f));
            });
        }

        // 06000b08: a used lane is blocked and leaves the available lanes; once LaneIgnoreCount lanes are blocked, using another lane frees
        // the one blocked longest (it goes to the end of the available lanes).
        [Test]
        public void OneAtATimeBlocksUsedLanesForTheIgnoreCount()
        {
            Owned.Run(owned =>
            {
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d => d.LaneIgnoreCount = 2);
                ability.SetProjectileCount(1);
                ability.Enter();
                var available = Get<List<Transform>>(ability, "m_availableLanes");
                var blocked = Get<Queue<Transform>>(ability, "m_blockedLanes");
                Transform l0 = scene.Lanes[0], l1 = scene.Lanes[1], l2 = scene.Lanes[2], l3 = scene.Lanes[3];

                Call(ability, "BlockSpawnLane", l0);
                Assert.That(available, Is.EqualTo(new[] { l1, l2, l3 }));
                Assert.That(blocked.ToArray(), Is.EqualTo(new[] { l0 }));
                Call(ability, "BlockSpawnLane", l1);
                Assert.That(available, Is.EqualTo(new[] { l2, l3 }));
                Assert.That(blocked.ToArray(), Is.EqualTo(new[] { l0, l1 }));
                Call(ability, "BlockSpawnLane", l2);
                Assert.That(available, Is.EqualTo(new[] { l3, l0 }), "the lane blocked longest comes back at the end");
                Assert.That(blocked.ToArray(), Is.EqualTo(new[] { l1, l2 }));
                Assert.That(scene.Lanes.Contains((Transform)Call(ability, "GetLaneChoice", 0)), Is.True);
                Assert.That(available.Contains((Transform)Call(ability, "GetLaneChoice", 0)), Is.True, "a lane is drawn from the available ones");
            });
        }

        // 06000b06: once the launch time has run out the strike effects fire and the animation restarts; the call reports whether the
        // launch time was still running at its start.
        [Test]
        public void OneAtATimeAttackingStrikesOnceTheLaunchTimeHasRunOut()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d =>
                {
                    d.ProjectileGroupLaunchTimeSeconds = 1f;
                    d.AttackAnimationLengthSeconds = 1f;
                });
                ability.SetProjectileCount(1);
                // The one projectile of the group glides while the launch time runs (see the glide case); a projectile is a TimeScaledComponent_SDT.
                owned.LeaseRow<TimeManager>();
                var projectile = EnemyAbilityTestSupport.AddTimeScaled<GuardianProjectile>(owned.Host("Lucid striking projectile", false));
                projectile.transform.SetParent(scene.Spawn, false);
                Get<List<Projectile>>(ability, "m_projectiles").Add(projectile); // Reflection: only SpawnAProjectile fills the list
                ability.SetAttackAnimStarted();
                Call(ability, "Spawning", 0.5f);
                Assert.That(Get<float>(ability, "m_attackAnimationTimer"), Is.EqualTo(0.5f));
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.True);
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.True, "0.5 is below 1");
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(1f));
                Assert.That((bool)Call(ability, "Attacking", 0.5f), Is.False, "1 is not above 1");
                Assert.That(Stored<BossEffectTriggerTypes>(scene.Boss, BossFSMKeys.BossEffectType), Is.EqualTo(BossEffectTriggerTypes.AttackStrike));
                Assert.That((Get<bool>(ability, "m_attackAnimStarted"), Get<float>(ability, "m_attackAnimationTimer")), Is.EqualTo((false, 0f)));
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(1f));
            });
        }

        // 0x6FDC3D..0x6FDCD1: while the launch time runs, once the fire timer is past index*divisible the current projectile's local
        // position moves towards zero by delta*ProjectileMoveToLaneVelocity (MoveTowards); it is fired only past (index+1)*divisible.
        // Launch time 2 over 2 projectiles gives a divisible of 1; after 0.25 s the projectile at local (3,4,0) has moved 4*0.25 = 1
        // along its 5-unit distance to (2.4,3.2,0) and has not been fired (0.25 is not above 1).
        [Test]
        public void OneAtATimeAttackingGlidesTheCurrentProjectileTowardsItsLane()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MessageManager>();
                ProjectileScene scene = NewScene(owned, 4, Vector3.zero);
                BossAbility_ProjectileSpawnAndShootOneAtATime ability = NewOneAtATime(owned, scene, d =>
                {
                    d.ProjectileGroupLaunchTimeSeconds = 2f;
                    d.ProjectileMoveToLaneVelocity = 4f;
                });
                ability.SetProjectileCount(2);
                // A projectile is a TimeScaledComponent_SDT: AddComponent runs the editor Reset, so an owned empty lookup stands in (AddTimeScaled).
                owned.LeaseRow<TimeManager>();
                var projectile = EnemyAbilityTestSupport.AddTimeScaled<GuardianProjectile>(owned.Host("Lucid gliding projectile", false));
                projectile.transform.SetParent(scene.Spawn, false);
                projectile.transform.localPosition = new Vector3(3f, 4f, 0f);
                Get<List<Projectile>>(ability, "m_projectiles").Add(projectile); // Reflection: only SpawnAProjectile fills the list

                Assert.That((bool)Call(ability, "Attacking", 0.25f), Is.True, "the launch time is still running");
                AssertNear(projectile.transform.localPosition, new Vector3(2.4f, 3.2f, 0f), "glided one unit towards the lane", 1e-4f);
                Assert.That(projectile.transform.parent, Is.SameAs(scene.Spawn), "not fired yet");
                Assert.That(Get<int>(ability, "m_currentlyFiringProjectileIndex"), Is.Zero);
                Assert.That(Get<float>(ability, "m_projectileFireTimer"), Is.EqualTo(0.25f));
            });
        }
    }
}
