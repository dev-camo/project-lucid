using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // SplineProjectileSpawner (Game.Runtime 02000677; x86-64 0x582850..0x582E60). Real components on owned hosts. No Awake runs in
    // Edit Mode, so the manager and fired list are reached through the members the cases cover.
    // Fire's pooled paths run on a ProjectileManager whose pool factory returns a prepared projectile.
    public sealed class SplineProjectileSpawnerTests
    {
        private static readonly Type ConfigType = typeof(ProjectileSpawnerBase).GetNestedType("ProjectilesAndCount", BindingFlags.NonPublic);

        private static SplineProjectileSpawner NewSpawner(Owned owned)
        {
            owned.LeaseRow<GameplayIslandManager>();
            GameObject host = owned.Host("Lucid spline spawner", false);
            host.AddComponent<BoxCollider>();
            return host.AddComponent<SplineProjectileSpawner>();
        }

        private static CoolDownRecordingProjectileProbe NewProjectile(Owned owned, string name) =>
            EnemyAbilityTestSupport.AddTimeScaled<CoolDownRecordingProjectileProbe>(owned.Host(name, false));

        private static Array Configs(params (int count, ProjectileType type)[] rows)
        {
            Array configs = Array.CreateInstance(ConfigType, rows.Length);
            for (int i = 0; i < rows.Length; ++i)
            {
                object config = Activator.CreateInstance(ConfigType);
                ConfigType.GetField("ProjectileCount").SetValue(config, rows[i].count);
                ConfigType.GetField("ProjectileType").SetValue(config, rows[i].type);
                configs.SetValue(config, i);
            }
            return configs;
        }

        // 0x582850: the outer loop runs only for a non-empty config array (jle 0x582beb) and the inner loop only for a positive count
        // (jle at 0x582913); neither reads m_projectileManager before then, so a spawner with no configs and one with a zero count
        // both return without a manager.
        [Test]
        public void FireWithNoConfiguredProjectilesTouchesNothing()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                Set(spawner, "m_projectileConfigs", Configs((0, ProjectileType.BuzzBomberProjectile)));
                Assert.DoesNotThrow(() => Call(spawner, "Fire"));
                Set(spawner, "m_projectileConfigs", Configs());
                Assert.DoesNotThrow(() => Call(spawner, "Fire"));
                Assert.That(Get<List<Projectile>>(spawner, "m_firedProjectiles"), Is.Empty);
            });
        }

        // 0x582974: a positive count reaches the manager's pool dictionary indexer with the configured type. The manager holds a pool
        // only for another type, so the failure names the configured key, not the pooled one.
        [Test]
        public void FireLooksUpTheConfiguredProjectileTypeInTheManagerPools()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                ProjectileManager manager = owned.Host("Lucid spline manager", false).AddComponent<ProjectileManager>();
                // The pool factory is never run: acquiring the configured type fails before any pool is used.
                Get<Dictionary<ProjectileType, UnityEngine.Pool.ObjectPool<Projectile>>>(manager, "m_projectilePools")
                    [ProjectileType.GuardianKillerProjectile] = new UnityEngine.Pool.ObjectPool<Projectile>(() => null);
                Set(spawner, "m_projectileManager", manager);
                Set(spawner, "m_projectileConfigs", Configs((1, ProjectileType.BuzzBomberProjectile)));
                var thrown = Assert.Throws<KeyNotFoundException>(() => Call(spawner, "Fire"));
                Assert.That(thrown.Message, Does.Contain(nameof(ProjectileType.BuzzBomberProjectile)));
                Assert.That(Get<List<Projectile>>(spawner, "m_firedProjectiles"), Is.Empty);
            });
        }

        private static ProjectileManager NewManagerReturning(Owned owned, ProjectileType type, Projectile projectile)
        {
            ProjectileManager manager = owned.Host("Lucid spline manager", false).AddComponent<ProjectileManager>();
            Get<Dictionary<ProjectileType, UnityEngine.Pool.ObjectPool<Projectile>>>(manager, "m_projectilePools")[type] =
                new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
            return manager;
        }

        // 0x582A9E..0x582B78: for each acquired NightmareEggmanProjectile Setup runs inlined with focus (0, 50, 0) (constant 0x29a7990
        // loaded at 0x582ac1, z zeroed by xorps at 0x582abb) and a null owner collider ([r13+0x80] = 0), leaving the base state
        // WarmingUp (0); the spline is stored (0x582b00) before the virtual Fire (slot 33, 0x582b1a); AddUnique (0x582b31) keeps the
        // one pooled instance in the list once although it is acquired twice; each iteration combines a spawner callback into
        // OnExpired (0x582b78).
        [Test]
        public void FireSetsUpEachNightmareProjectileOnTheSplineAndSubscribesToItsExpiry()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CharacterManager>(); // Setup caches it
                SplineProjectileSpawner spawner = NewSpawner(owned);
                SplineSpawnRecordingNightmareProjectile projectile =
                    EnemyAbilityTestSupport.AddTimeScaled<SplineSpawnRecordingNightmareProjectile>(owned.Host("Lucid spline nightmare", false));
                // The base component's UnityEvent is a serialized field the prefab would supply; Setup invokes it.
                Set(projectile, "m_onWarmingUp", new UnityEngine.Events.UnityEvent());
                Spline spline = owned.Host("Lucid spline", false).AddComponent<Spline>();
                Set(spawner, "m_spline", spline);
                Set(spawner, "m_projectileManager", NewManagerReturning(owned, ProjectileType.BuzzBomberProjectile, projectile));
                Set(spawner, "m_projectileConfigs", Configs((2, ProjectileType.BuzzBomberProjectile)));

                Call(spawner, "Fire");

                Assert.That(projectile.FocusTargets, Is.EqualTo(new[] { new Vector3(0f, 50f, 0f), new Vector3(0f, 50f, 0f) }));
                Assert.That(Get<Collider>(projectile, "m_ownerCollider"), Is.Null);
                Assert.That(projectile.StatesAtFire, Is.EqualTo(new[] { 0, 0 }), "Setup ran before Fire");
                Assert.That(projectile.SplinesAtFire, Is.EqualTo(new[] { spline, spline }), "SetSpline ran before Fire");
                Assert.That(Get<List<Projectile>>(spawner, "m_firedProjectiles"), Is.EqualTo(new Projectile[] { projectile }));
                Delegate[] callbacks = projectile.OnExpired.GetInvocationList();
                Assert.That(callbacks.Length, Is.EqualTo(2));
                Assert.That(callbacks, Has.All.Matches<Delegate>(callback =>
                    callback.Target == spawner && callback.Method.Name == "OnProjectileExpired"));
            });
        }

        // 0x5829fa: any other acquired projectile type is reported through String.Format(message, projectile, GetString(type)) and
        // skipped (the loop goes on, 0x58293c); it is not set up, listed or subscribed.
        [Test]
        public void FireReportsAnAcquiredProjectileThatIsNotANightmareProjectile()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                CoolDownRecordingProjectileProbe other = NewProjectile(owned, "Lucid spline unsupported");
                var definition = owned.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
                Set(definition, "m_projectileType", ProjectileType.BuzzBomberProjectile);
                Set(other, "m_definition", definition); // Initialise needs the App's hazard table
                Set(spawner, "m_projectileManager", NewManagerReturning(owned, ProjectileType.BuzzBomberProjectile, other));
                Set(spawner, "m_projectileConfigs", Configs((2, ProjectileType.BuzzBomberProjectile)));

                List<string> errors = HLOutputDiagnosticCapture.CaptureErrors(() => Call(spawner, "Fire"));

                const string Expected = "Lucid spline unsupported (ProjectLucid.Tests.WorldSurfaceShared.CoolDownRecordingProjectileProbe)"
                    + " is of type BuzzBomberProjectile and is not currently supported as a spline following projectile.:0";
                Assert.That(errors, Is.EqualTo(new[] { Expected, Expected }));
                Assert.That(Get<List<Projectile>>(spawner, "m_firedProjectiles"), Is.Empty);
                Assert.That(other.OnExpired, Is.Null);
            });
        }

        // 0x582C10/0x582CCF: the base trigger exit runs, then every fired projectile gets the virtual cooldown call (slot 39) with
        // esi = 0, so allowExceptions is false; the list itself is left as it was.
        [Test]
        public void TriggerExitSendsEveryFiredProjectileIntoCooldownWithoutExceptions()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                CoolDownRecordingProjectileProbe first = NewProjectile(owned, "Lucid spline first");
                CoolDownRecordingProjectileProbe second = NewProjectile(owned, "Lucid spline second");
                var fired = Get<List<Projectile>>(spawner, "m_firedProjectiles");
                fired.Add(first);
                fired.Add(second);

                Call(spawner, "DoTriggerExit", new object[] { null });

                Assert.That(first.ForcedCoolDowns, Is.EqualTo(new[] { false }));
                Assert.That(second.ForcedCoolDowns, Is.EqualTo(new[] { false }));
                Assert.That(fired, Is.EqualTo(new Projectile[] { first, second }));
            });
        }

        // 0x582D50: the projectile leaves the fired list (List.Remove) and the spawner's callback is taken off its OnExpired delegate
        // (Delegate.Remove, 0x582dee); another subscriber stays. Kill is the shipped path that invokes OnExpired.
        [Test]
        public void ExpiredProjectileLeavesTheFiredListAndLosesTheSpawnerCallback()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                CoolDownRecordingProjectileProbe expiring = NewProjectile(owned, "Lucid spline expiring");
                CoolDownRecordingProjectileProbe other = NewProjectile(owned, "Lucid spline other");
                var fired = Get<List<Projectile>>(spawner, "m_firedProjectiles");
                fired.Add(expiring);
                fired.Add(other);
                // Fire's subscription is covered above; here the same delegate is built directly so the removal is tested alone.
                var callback = (Action<Projectile>)Delegate.CreateDelegate(typeof(Action<Projectile>), spawner,
                    typeof(SplineProjectileSpawner).GetMethod("OnProjectileExpired", BindingFlags.NonPublic | BindingFlags.Instance));
                expiring.OnExpired += callback;
                var seen = new List<Projectile>();
                expiring.OnExpired += seen.Add;

                expiring.Kill();

                Assert.That(fired, Is.EqualTo(new Projectile[] { other }));
                Assert.That(seen, Is.EqualTo(new Projectile[] { expiring }), "the other subscriber still ran");
                Assert.That(expiring.OnExpired.GetInvocationList().Length, Is.EqualTo(1), "the spawner callback was removed");
                expiring.Kill();
                Assert.That(seen.Count, Is.EqualTo(2), "only the other subscriber is left to run");
                Assert.That(fired, Is.EqualTo(new Projectile[] { other }));
            });
        }
    }
}
