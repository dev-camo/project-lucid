using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using Random = UnityEngine.Random;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.EnemyAbilityTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // EnemyAbility_FireProjectile (02000634) against the 1.10.1 x86-64 bodies at 0x56ED00..0x56FFA0. The enemy and the
    // targets are real components on owned inactive hosts (see EnemyAbilityTestSupport); a target's aim point is the node of
    // its real attachment points. The definition is authored through the editor JSON overwrite, as authored data is.
    // Not covered: WarmupProjectile (0x56FCB0), which acquires the projectile from the registered ProjectileManager's
    // pools (built from the data manager's projectile definitions) and attaches it to the enemy's node; the
    // tests put a real projectile into the warm-up slot instead, as the warm-up leaves it (a `// Reflection:` write, the
    // slot being private). The target position is published and consumed through the real flow: a sensed target and an
    // update trigger the ability, EnemyBrainState_TurnTowardsPosition removes the published position. The gizmo drawing at the end
    // of OnDrawGizmosSelected (0x56FF3D), which the engine accepts only inside its gizmo callback.
    public sealed class EnemyFireProjectileAbilityTests
    {
        // Global UnityEngine.Random state is borrowed: whatever a test draws, the incoming state is put back.
        private UnityEngine.Random.State _incomingRandomState;

        [NUnit.Framework.SetUp]
        public void SaveIncomingRandomState()
        {
            _incomingRandomState = UnityEngine.Random.state;
        }

        [NUnit.Framework.TearDown]
        public void RestoreIncomingRandomState()
        {
            UnityEngine.Random.state = _incomingRandomState;
        }

        private static EnemyAbility_FireProjectile Fire(Scene scene, string json = null) =>
            scene.Ability<EnemyAbility_FireProjectile, EnemyAbilityDefinition_FireProjectile>(definition =>
            {
                if (json != null)
                {
                    JsonUtility.FromJsonOverwrite(json, definition);
                }
            });

        private static EnemyProjectileProbe NewProjectile(Scene scene) =>
            AddTimeScaled<EnemyProjectileProbe>(scene.Owned.Host(Name(), false));

        // Straight ahead of the enemy (+z) and inside the default range and elevation band.
        private static readonly Vector3 Aim = new Vector3(0f, 0f, 10f);

        private static bool BrainHoldsTarget(Scene scene) => scene.Brain.Storage.HasValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition);

        // ---- declarations ----

        // Flags, member order and the Il2CppSetOption order from the DummyDll tables (1 = NullChecks,
        // 2 = ArrayBoundsChecks). An emission self-check of the restored declarations, not a native behaviour oracle.
        [Test]
        public void EmissionCheck_FireProjectileKeepsItsFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(EnemyAbility_FireProjectile), 0x00100001,
                new[] { ("<TargetPosition>k__BackingField", 0x0001), ("m_projectileInWarmup", 0x0001), ("m_windUpElapsedTime", 0x0001) },
                new[]
                {
                    ("get_DefinitionBase", 0x08c6), ("get_TargetPosition", 0x0886), ("set_TargetPosition", 0x0881), ("DoOnEnter", 0x00c4),
                    ("SetTriggered", 0x0081), ("DoOnUpdate", 0x00c4), ("UpdateWhileTriggered", 0x0081), ("TargetValid", 0x0081),
                    ("ApplyAccuracy", 0x0081), ("DoOnLeave", 0x00c4), ("WarmupProjectile", 0x0081), ("FireProjectile", 0x0081),
                    ("OnDrawGizmosSelected", 0x00c6), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(EnemyAbility_FireProjectile), 1, 2);
            Assert.That(typeof(EnemyAbility_FireProjectile).BaseType, Is.EqualTo(typeof(EnemyAbility_Movement<EnemyAbilityDefinition_FireProjectile>)));
            Assert.That(typeof(EnemyAbility_FireProjectile).GetMethod("TargetValid", Instance).GetParameters()[1].IsOut, Is.True);
        }

        // ---- triggering and clearing ----

        // 0x56ED50 enters with the ability unfinished and cleared; 0x56ED80 (cleared) removes the published target
        // position from the brain's storage, the turn-complete and wind-up-complete flags from the actor's storage, and
        // kills a projectile still warming up (its expiry callback runs and the slot is emptied).
        [Test]
        public void EnteringClearsTheTriggeredStateAndKillsAWarmingProjectile()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene);
                var projectile = NewProjectile(scene);
                int expired = 0;
                projectile.OnExpired += _ => expired++;
                scene.TriggerFireProjectile(ability, Aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);
                scene.Enemy.Storage.SetValue(ActorFSMKeys.EnemyFireProjectileTurnComplete, true);
                scene.Enemy.Storage.SetValue(ActorFSMKeys.EnemyFireProjectileWindUpComplete, true);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(BrainHoldsTarget(scene), Is.True, "triggering publishes the target position");

                ability.Enter();
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(ability.AbilityFinished, Is.False);
                Assert.That(BrainHoldsTarget(scene), Is.False);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileTurnComplete), Is.False);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileWindUpComplete), Is.False);
                Assert.That(expired, Is.EqualTo(1), "the warming projectile is killed");
                Assert.That(Get<Projectile>(ability, "m_projectileInWarmup"), Is.Null);

                ability.Enter();
                Assert.That(expired, Is.EqualTo(1), "nothing left to kill");
            });
        }

        // 0x56FEB0: leaving an entered ability clears it the same way; leaving one that is not entered does nothing. A second
        // ability of the same enemy that was never entered leaves the first one's published position on the brain alone.
        [Test]
        public void LeavingClearsTheTriggeredStateOnlyForAnEnteredAbility()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene);
                var neverEntered = Fire(scene);
                scene.TriggerFireProjectile(ability, Aim);
                neverEntered.Leave();
                Assert.That(ability.IsTriggered, Is.True, "not entered: nothing to leave");
                Assert.That(BrainHoldsTarget(scene), Is.True);

                ability.Leave();
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(BrainHoldsTarget(scene), Is.False);
            });
        }

        // 0x56ED80 (triggered) restarts the wind-up clock, raises the flag and publishes the target position, and
        // returns without touching the actor's flags or the projectile.
        [Test]
        public void TriggeringRestartsTheClockAndPublishesTheTargetPosition()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene);
                var projectile = NewProjectile(scene);
                ability.Enter();
                // Reflection: the warm-up slot and the clock only advance past zero with a warmed-up projectile, which
                // needs the registered ProjectileManager; the real trigger below must reset them.
                Set(ability, "m_projectileInWarmup", projectile);
                Set(ability, "m_windUpElapsedTime", 0.75f);
                scene.Enemy.Storage.SetValue(ActorFSMKeys.EnemyFireProjectileTurnComplete, true);
                scene.Sense(scene.Target(Aim));
                ability.Update(0.02f);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0f));
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.FireProjectileTargetPosition, Vector3.one), Is.EqualTo(Aim));
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileTurnComplete), Is.True);
                Assert.That(Get<Projectile>(ability, "m_projectileInWarmup"), Is.SameAs(projectile));
            });
        }

        // ---- choosing a target ----

        // 0x56F180 with TargetValid inlined (0x56F43E..0x56F5F0): every target whose aim node exists and passes the range
        // and elevation band is considered, and the nearest wins; of equal distances the later one does. The enemy
        // stands at the origin facing +z with up +y, with the default range 20 and band of plus and minus 60 degrees.
        [Test]
        public void TheNearestValidTargetIsChosenAndPublishedToTheBrain()
        {
            WithScene(scene =>
            {
                foreach ((string why, Vector3[] aims, int chosen) in new[]
                {
                    ("one target ahead", new[] { new Vector3(0f, 0f, 10f) }, 0),
                    ("the nearer of two", new[] { new Vector3(0f, 0f, 15f), new Vector3(0f, 0f, 5f) }, 1),
                    ("the nearer of two in the other order", new[] { new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 15f) }, 0),
                    ("equal distances: the later one", new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 0f) }, 1),
                    ("beyond the range", new[] { new Vector3(0f, 0f, 21f) }, -1),
                    ("exactly at the range", new[] { new Vector3(0f, 0f, 20f) }, 0),
                    ("too high", new[] { new Vector3(0f, 10f, 5f) }, -1),
                    ("too low", new[] { new Vector3(0f, -10f, 5f) }, -1),
                    ("inside the band", new[] { new Vector3(0f, 5f, 5f) }, 0),
                    ("a nearer target that is out of the band", new[] { new Vector3(0f, 10f, 2f), new Vector3(0f, 0f, 9f) }, 1),
                })
                {
                    ((List<Actor>)scene.Manager.GetTargetsForEnemies()).Clear();
                    var targets = new List<Actor>();
                    foreach (Vector3 aim in aims) targets.Add(scene.Target(aim));
                    scene.Sense(targets.ToArray());
                    var ability = Fire(scene);
                    ability.Enter();
                    ability.Update(0.02f);
                    if (chosen < 0)
                    {
                        Assert.That(ability.IsTriggered, Is.False, why);
                        Assert.That(BrainHoldsTarget(scene), Is.False, why);
                        continue;
                    }
                    Assert.That(ability.IsTriggered, Is.True, why);
                    AssertNear(ability.TargetPosition, aims[chosen], why);
                    AssertNear(scene.BrainStore(EnemyBrainFSMKeys.FireProjectileTargetPosition, Vector3.zero), aims[chosen], why + ", published");
                    ability.Leave();
                }
            });
        }

        // 0x56F43E: a target without the aim node is skipped, even when it is nearer; without any target nothing happens.
        [Test]
        public void ATargetWithoutAnAimNodeIsSkippedAndNoTargetsLeavesTheAbilityIdle()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene);
                ability.Enter();
                ability.Update(0.02f);
                Assert.That(ability.IsTriggered, Is.False, "no targets at all");

                scene.Sense(scene.Target(new Vector3(0f, 0f, 3f), withNode: false), scene.Target(new Vector3(0f, 0f, 9f)));
                ability.Update(0.02f);
                Assert.That(ability.IsTriggered, Is.True);
                AssertNear(ability.TargetPosition, new Vector3(0f, 0f, 9f), "the target with a node");
            });
        }

        // 0x56FC50: the chosen position is offset by a random point of the unit sphere scaled by the accuracy; with an
        // accuracy of zero it is exact.
        [Test]
        public void AnAccuracyOffsetStaysWithinItsRadius()
        {
            WithScene(scene =>
            {
                Vector3 aim = new Vector3(0f, 0f, 10f);
                scene.Sense(scene.Target(aim));
                var exact = Fire(scene);
                exact.Enter();
                exact.Update(0.02f);
                AssertNear(exact.TargetPosition, aim, "accuracy zero");

                bool anyOffset = false;
                for (int trial = 0; trial < 20; trial++)
                {
                    var inaccurate = Fire(scene, "{\"m_accuracyMetres\":2}");
                    inaccurate.Enter();
                    inaccurate.Update(0.02f);
                    float offset = (inaccurate.TargetPosition - aim).magnitude;
                    Assert.That(offset, Is.LessThanOrEqualTo(2.0001f), "trial " + trial);
                    anyOffset |= offset > 0f;
                    Assert.That(inaccurate.IsTriggered, Is.True);
                }
                Assert.That(anyOffset, Is.True, "an accuracy of two metres moves the target");

                var direct = Fire(scene);
                AssertNear((Vector3)Call(direct, "ApplyAccuracy", new Vector3(1f, 2f, 3f)), new Vector3(1f, 2f, 3f), "ApplyAccuracy with accuracy zero");
            });
        }

        // 0x56FC83 (inlined again at 0x56F6AC): the offset is Random.insideUnitSphere scaled by the accuracy, so with a
        // seeded generator it is exactly the next sample times two; a point on the sphere surface would not be.
        [Test]
        public void AnAccuracyOffsetIsTheSeededInsideUnitSphereSampleTimesTheAccuracy()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene, "{\"m_accuracyMetres\":2}");
                Vector3 aim = new Vector3(1f, 2f, 3f);
                var previousState = Random.state;
                try
                {
                    Random.InitState(1234);
                    Vector3 sample = Random.insideUnitSphere;
                    Random.InitState(1234);
                    AssertNear((Vector3)Call(ability, "ApplyAccuracy", aim), aim + sample * 2f, "aim plus the sample scaled by the accuracy");
                }
                finally
                {
                    Random.state = previousState;
                }
            });
        }

        // 0x56FAA0: the distance squared is always reported; the target is valid when it is within the range (equal
        // included) and the sine of its elevation above the enemy's up direction is within the band (both ends
        // included). The band here is 0 degrees (sine 0) to -30 degrees; the enemy stands at x = 10.
        [Test]
        public void TargetValidChecksRangeAndTheElevationBand()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene, "{\"m_verticalAngleMaxDegrees\":0,\"m_verticalAngleMinDegrees\":-30}");
                scene.Enemy.SetWorldPosition(new Vector3(10f, 0f, 0f));
                foreach ((Vector3 position, bool valid, float distanceSqr, string why) in new[]
                {
                    (new Vector3(10f, 0f, 5f), true, 25f, "level, the upper end of the band"),
                    (new Vector3(10f, 0.5f, 5f), false, 25.25f, "slightly above the band"),
                    (new Vector3(10f, -2f, 5f), true, 29f, "below, inside the band"),
                    (new Vector3(10f, -4f, 5f), false, 41f, "below the band"),
                    (new Vector3(10f, 0f, 20f), true, 400f, "exactly at the range"),
                    (new Vector3(10f, 0f, 20.01f), false, 400.4001f, "just beyond the range"),
                    (new Vector3(5f, 0f, 0f), true, 25f, "measured from the enemy, not the origin"),
                })
                {
                    object[] arguments = { position, -1f };
                    bool result = (bool)Call(ability, "TargetValid", arguments);
                    Assert.That(result, Is.EqualTo(valid), why);
                    Assert.That((float)arguments[1], Is.EqualTo(distanceSqr).Within(1e-3f), why + ": distance squared");
                }

                // The elevation is measured against the enemy's own up direction: pitched 90 degrees, up is +z.
                scene.Enemy.SetWorldPosition(Vector3.zero);
                scene.Enemy.SetWorldRotation(Quaternion.Euler(90f, 0f, 0f));
                AssertNear(scene.Enemy.UpDirection, Vector3.forward, "pitched up direction");
                object[] ahead = { new Vector3(0f, 0f, 5f), -1f };
                Assert.That((bool)Call(ability, "TargetValid", ahead), Is.False, "straight along the up direction");
                object[] beside = { new Vector3(5f, 0f, 0f), -1f };
                Assert.That((bool)Call(ability, "TargetValid", beside), Is.True, "level with the up direction");
            });
        }

        // 0x56F5B5 / 0x56FC3A: the lower end of the band is inclusive too (dot >= min). Both ends at sine 0 leave only
        // a target exactly level with the enemy valid.
        [Test]
        public void TargetValidIncludesTheLowerEndOfTheElevationBand()
        {
            WithScene(scene =>
            {
                var ability = Fire(scene, "{\"m_verticalAngleMaxDegrees\":0,\"m_verticalAngleMinDegrees\":0}");
                scene.Enemy.SetWorldPosition(Vector3.zero);
                object[] arguments = { new Vector3(0f, 0f, 5f), -1f };
                Assert.That((bool)Call(ability, "TargetValid", arguments), Is.True, "level target on the lower end");
                Assert.That((float)arguments[1], Is.EqualTo(25f).Within(1e-3f));

                scene.Sense(scene.Target(new Vector3(0f, 0f, 5f)));
                ability.Enter();
                ability.Update(0.02f);
                Assert.That(ability.IsTriggered, Is.True);
                AssertNear(ability.TargetPosition, new Vector3(0f, 0f, 5f), "published through the update");
            });
        }

        // ---- wind-up, firing and finishing ----

        // 0x56F770: while the brain still holds the published target position nothing advances. Once it is taken the
        // wind-up clock advances by the frame time; when the clock crosses the wind-up time (1) the projectile is
        // detached and fired at the target, the wind-up-complete flag is raised, and the ability finishes (flag, cleared
        // state) when the clock reaches wind-up plus wind-down (1.5). The new clock reaches a threshold inclusively
        // (jb at 0x56F979, jae at 0x56FA73) while the previous clock must be strictly below it (jbe at 0x56F970 and
        // 0x56FA69 after the ucomiss at 0x56F96D and 0x56FA66), so each threshold is crossed once.
        [Test]
        public void TheProjectileFiresWhenTheWindUpTimeIsCrossedAndTheAbilityEndsAfterTheWindDown()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene, "{\"m_windUpTime\":1,\"m_windDownTime\":0.5}");
                var projectile = NewProjectile(scene);
                scene.TriggerFireProjectile(ability, Aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);

                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0f), "the brain still holds the target position");
                scene.ConsumeFireTargetPosition();
                Assert.That(BrainHoldsTarget(scene), Is.False, "the turn state took the position");

                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0.5f));
                Assert.That(projectile.Fires, Is.EqualTo(0));
                ability.Update(0.5f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(1f));
                Assert.That(projectile.Fires, Is.EqualTo(1), "fired at the wind-up time itself");
                Assert.That(projectile.Focuses, Is.EqualTo(new[] { Aim }));
                Assert.That(Get<Projectile>(ability, "m_projectileInWarmup"), Is.Null);
                Assert.That(scene.ActorStore(ActorFSMKeys.EnemyFireProjectileWindUpComplete, false), Is.True);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(ability.AbilityFinished, Is.False);

                ability.Update(0.5f);
                Assert.That(ability.AbilityFinished, Is.True, "finished at wind-up plus wind-down itself");
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileWindUpComplete), Is.False, "cleared with the state");
                Assert.That(projectile.Fires, Is.EqualTo(1));

                ability.Enter();
                Assert.That(ability.AbilityFinished, Is.False, "entering again restarts the ability");
            });
        }

        // 0x56FA53..0x56FA73: after firing, the update falls through to the finishing check, so a single update that
        // crosses both the wind-up time and the wind-up plus wind-down time fires and finishes together. The
        // wind-up-complete flag written by the fire is removed again by the clearing state change.
        [Test]
        public void FiringAndFinishingCanHappenInTheSameUpdate()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene, "{\"m_windUpTime\":1,\"m_windDownTime\":0.5}");
                var projectile = NewProjectile(scene);
                scene.TriggerFireProjectile(ability, Aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);
                scene.ConsumeFireTargetPosition();
                ability.Update(0.5f);
                Assert.That(projectile.Fires, Is.EqualTo(0), "the clock is at 0.5, below the wind-up time");
                ability.Update(1.0f);
                Assert.That(projectile.Fires, Is.EqualTo(1));
                Assert.That(projectile.Focuses, Is.EqualTo(new[] { ability.TargetPosition }));
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(1.5f));
                Assert.That(ability.AbilityFinished, Is.True);
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileWindUpComplete), Is.False);
            });
        }

        // 0x56F96D (ucomiss wind-up, previous clock; jbe to the return at 0x56FA75): a projectile fires only when the previous
        // clock is strictly below the wind-up time, and that return skips the finishing check. A wind-up time of 0, authored
        // through the definition, is therefore never crossed: the previous clock (0, then 0.4) is never below it, so the
        // projectile stays in the slot, and the ability does not finish although the clock passes wind-up plus wind-down (0.5).
        [Test]
        public void AProjectilePastTheWindUpTimeIsNeverFiredAndTheAbilityNeverFinishes()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene, "{\"m_windUpTime\":0,\"m_windDownTime\":0.5}");
                var projectile = NewProjectile(scene);
                scene.TriggerFireProjectile(ability, Aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);
                scene.ConsumeFireTargetPosition();
                ability.Update(0.4f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0.4f).Within(1e-6f), "the clock runs");
                Assert.That(projectile.Fires, Is.EqualTo(0), "the previous clock 0 is not below the wind-up time 0");
                ability.Update(0.4f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0.8f).Within(1e-6f), "the clock still runs");
                Assert.That(projectile.Fires, Is.EqualTo(0));
                Assert.That(Get<Projectile>(ability, "m_projectileInWarmup"), Is.SameAs(projectile));
                Assert.That(ability.AbilityFinished, Is.False);
                Assert.That(ability.IsTriggered, Is.True);
            });
        }

        // 0x56F872 (ucomiss 0, elapsed; jae): the warm-up branch is taken only when 0 >= elapsed. An unordered NaN clock
        // therefore stays on the clock-only path, where `elapsed > 0` written the other way round would warm up a projectile.
        // The NaN clock comes from a real update with a NaN frame time after the projectile was fired.
        [Test]
        public void ANaNClockWithNoProjectileOnlyAdvancesTheClock()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene, "{\"m_windUpTime\":1,\"m_windDownTime\":0.5}");
                var projectile = NewProjectile(scene);
                scene.TriggerFireProjectile(ability, Aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);
                scene.ConsumeFireTargetPosition();
                ability.Update(1.0f);
                Assert.That(projectile.Fires, Is.EqualTo(1), "fired at the wind-up time, leaving the slot empty");

                ability.Update(float.NaN);
                ability.Update(0.4f);
                Assert.That(float.IsNaN(Get<float>(ability, "m_windUpElapsedTime")), Is.True, "the clock keeps running as NaN");
                Assert.That(Get<Projectile>(ability, "m_projectileInWarmup"), Is.Null, "no projectile is warmed up");
                Assert.That(scene.Enemy.Storage.HasValue(ActorFSMKeys.EnemyFireProjectileTurnComplete), Is.False);
                Assert.That(ability.AbilityFinished, Is.False);
                Assert.That(ability.IsTriggered, Is.True);
            });
        }

        // ---- the turn state that consumes the published position ----

        // End to end: the ability publishes the target position under "FireProjectileTargetPosition" (0x56ED80) and waits while the
        // brain holds it (0x56F834). EnemyBrainState_TurnTowardsPosition configured with that key and RemoveWhenComplete turns the
        // enemy on the spot (0x573C22 passes ignoreForwardMovement) and removes the position only once the enemy faces it
        // (0x573C2C test al, then the flag at 0x573C34); only then does the ability's clock run. The target is on the +x axis
        // of an enemy facing +z, so the first update turns right (impulse x = 1, no forward y) and keeps the position.
        [Test]
        public void TheTurnTowardsPositionStateConsumesThePositionTheAbilityPublished()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene);
                var projectile = NewProjectile(scene);
                Vector3 aim = new Vector3(10f, 0f, 0f);
                scene.TriggerFireProjectile(ability, aim);
                // Reflection: the warm-up slot is private and WarmupProjectile needs the registered ProjectileManager.
                Set(ability, "m_projectileInWarmup", projectile);
                AssertNear(scene.BrainStore(EnemyBrainFSMKeys.FireProjectileTargetPosition, Vector3.zero), aim, "published");

                scene.ConsumeFireTargetPosition();
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.MovementImpulse, new Vector2(9f, 9f)), Is.EqualTo(new Vector2(1f, 0f)), "turning right, not moving");
                Assert.That(BrainHoldsTarget(scene), Is.True, "not facing the position yet");
                ability.Update(0.25f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0f), "the ability waits for the position to be taken");

                scene.Enemy.SetWorldRotation(Quaternion.LookRotation(Vector3.right));
                scene.ConsumeFireTargetPosition();
                Assert.That(scene.BrainStore(EnemyBrainFSMKeys.MovementImpulse, new Vector2(9f, 9f)), Is.EqualTo(Vector2.zero), "facing it");
                Assert.That(BrainHoldsTarget(scene), Is.False, "removed once faced");
                ability.Update(0.25f);
                Assert.That(Get<float>(ability, "m_windUpElapsedTime"), Is.EqualTo(0.25f), "the wind-up clock runs once the position is gone");
            });
        }

        // ---- gizmos ----

        // 0x56FED0: the base answer (the ability is entered) is false for an ability that is not entered, and the
        // draw is only reached for a triggered ability whose enemy has the shoot node; otherwise the answer is false.
        [Test]
        public void GizmosAreDrawnOnlyForAnEnteredTriggeredAbilityWithAShootNode()
        {
            WithScene(scene =>
            {
                scene.AttachPoints(scene.Enemy, null, Vector3.zero);
                var ability = Fire(scene);
                Assert.That(ability.OnDrawGizmosSelected(), Is.False, "not entered");
                ability.Enter();
                Assert.That(ability.OnDrawGizmosSelected(), Is.False, "entered but not triggered");
                scene.TriggerFireProjectile(ability, Aim);
                Assert.That(ability.OnDrawGizmosSelected(), Is.False, "triggered, but the enemy has no shoot node");
            });
        }
    }
}
