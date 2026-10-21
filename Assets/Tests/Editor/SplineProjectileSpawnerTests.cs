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
    // Not covered: the pooled-acquisition paths of Fire (the NightmareEggmanProjectile branch with Setup, SetSpline, Fire and the
    // OnExpired subscription, and the log branch for another projectile type), which need a ProjectileManager with real object
    // pools of prefab projectiles.
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

        // 0x582974: a positive count reaches the manager's pool dictionary indexer for the configured type, which fails on a manager
        // with no pools.
        [Test]
        public void FireAcquiresTheConfiguredProjectileTypeFromTheManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                SplineProjectileSpawner spawner = NewSpawner(owned);
                Set(spawner, "m_projectileManager", owned.Host("Lucid spline manager", false).AddComponent<ProjectileManager>());
                Set(spawner, "m_projectileConfigs", Configs((1, ProjectileType.BuzzBomberProjectile)));
                Assert.Throws<KeyNotFoundException>(() => Call(spawner, "Fire"));
                Assert.That(Get<List<Projectile>>(spawner, "m_firedProjectiles"), Is.Empty);
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
                // Only Fire subscribes the callback, and it needs pooled projectiles, so the same delegate is built directly.
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
