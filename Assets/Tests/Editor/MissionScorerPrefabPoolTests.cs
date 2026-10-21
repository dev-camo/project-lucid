using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // MissionScorerPrefabPool (SetRenderSettings 0x5D6B50, Start 0x5D6D40, OnCharacterManagerValid 0x5D71E0, IsWithinDistance
    // 0x5D72F0 and the RefreshPooledPrefabs state machine 0x5D7520). The base pool builds its instances from a prefab in
    // Awake, which Edit Mode never runs, so the spawn branch of the refresh is not exercised; the render settings come from a
    // real ScalablePerformance holding a real profile.
    public sealed class MissionScorerPrefabPoolTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Type Quality = typeof(MissionScorerPrefabPool).GetNestedType("QualityAttributes", BindingFlags.NonPublic);

        private sealed class Rig
        {
            internal World World;
            internal MissionScorerPrefabPool Pool;
            internal CharacterManager Manager;
            internal Character Character;
        }

        private static object QualitySettings(params (PerformanceProfile.QualityLevel level, float distance, float refresh)[] rows)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Quality));
            foreach ((PerformanceProfile.QualityLevel level, float distance, float refresh) in rows)
            {
                object item = Activator.CreateInstance(Quality);
                Quality.GetField("QualityLevel").SetValue(item, level);
                Quality.GetField("DistanceMeters").SetValue(item, distance);
                Quality.GetField("RefreshRateSeconds").SetValue(item, refresh);
                list.Add(item);
            }
            return list;
        }

        // A pool whose render settings are read from a registered ScalablePerformance, whose active profile rates the
        // pool's feature at the given level. The settings are applied by the pool's own SetRenderSettings.
        private static Rig NewPool(World world, CharacterManager manager, Character character, PerformanceProfile.QualityLevel level,
            params (PerformanceProfile.QualityLevel level, float distance, float refresh)[] rows)
        {
            var feature = world.Owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            feature.ID = "lucid-mission-scorer-pool";
            var profile = world.Owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
            Get<List<PerformanceAttribute>>(profile, "m_attributes").Add(new PerformanceAttribute { Feature = feature, Level = level });
            var performance = world.Owned.Register(world.Owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
            performance.SetPerformanceProfile(profile);
            var pool = world.Owned.Host("Lucid scorer prefab pool", false).AddComponent<MissionScorerPrefabPool>();
            Set(pool, "m_scalableFeature", feature); // Reflection: authored serialized settings
            Set(pool, "m_qualitySettings", QualitySettings(rows));
            Call(pool, "SetRenderSettings");
            return new Rig { World = world, Pool = pool, Manager = manager, Character = character };
        }

        private static void Run(Action<Rig> body, PerformanceProfile.QualityLevel level, params (PerformanceProfile.QualityLevel level, float distance, float refresh)[] rows)
        {
            WithCharacterWorld((world, manager, character) => body(NewPool(world, manager, character, level, rows)));
        }

        // 0x5D6C43 and 0x5D6C49 store the first match and fall through to Enumerator.Dispose at 0x5D6C51: the loop stops at
        // the first entry of the active level; the later High row is the negative control.
        [Test]
        public void TheFirstQualityEntryOfTheActiveLevelSuppliesTheDistanceAndRefreshRate()
        {
            Run(rig =>
            {
                Assert.That(Get<float>(rig.Pool, "m_renderDistanceMeters"), Is.EqualTo(20f));
                Assert.That(Get<float>(rig.Pool, "m_renderRefreshRateSeconds"), Is.EqualTo(0.25f));
                Assert.That(Get<float>(rig.Pool, "m_distanceSqr"), Is.EqualTo(400f));
                Assert.That(OriginalInOutTestSupport.Seconds(Get<WaitForSeconds>(rig.Pool, "m_refreshWaitForSeconds")), Is.EqualTo(0.25f));
                Assert.That(Get<bool>(rig.Pool, "m_resizeWhenFull"), Is.False);
            }, PerformanceProfile.QualityLevel.High,
                (PerformanceProfile.QualityLevel.Low, 5f, 0.5f), (PerformanceProfile.QualityLevel.High, 20f, 0.25f),
                (PerformanceProfile.QualityLevel.VeryHigh, 99f, 9f), (PerformanceProfile.QualityLevel.High, 30f, 0.75f));
        }

        // 0x5D6CBF..0x5D6CCD: the pool may grow only when neither a distance nor a refresh rate is configured.
        [Test]
        public void ThePoolMayResizeOnlyWhenNeitherADistanceNorARefreshRateIsConfigured()
        {
            Run(rig =>
            {
                Assert.That(Get<float>(rig.Pool, "m_distanceSqr"), Is.Zero);
                Assert.That(Get<bool>(rig.Pool, "m_resizeWhenFull"), Is.True, "no entry matches the medium level");
            }, PerformanceProfile.QualityLevel.Medium, (PerformanceProfile.QualityLevel.High, 20f, 0.25f));
            Run(rig => Assert.That(Get<bool>(rig.Pool, "m_resizeWhenFull"), Is.True, "a matching entry of zero and zero"),
                PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 0f, 0f));
            Run(rig => Assert.That(Get<bool>(rig.Pool, "m_resizeWhenFull"), Is.False, "a refresh rate alone"),
                PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 0f, 0.5f));
            Run(rig => Assert.That(Get<bool>(rig.Pool, "m_resizeWhenFull"), Is.False, "a distance alone"),
                PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0f));
        }

        // 0x5D7300: without a configured distance every objective counts as in range; otherwise the squared distance
        // to the character must be strictly less than the squared range.
        [Test]
        public void AnObjectiveIsInRangeBelowTheConfiguredDistanceOrAlwaysWithoutOne()
        {
            Run(rig =>
            {
                MissionScorerObjective objective = rig.World.NewObjective<MissionScorerObjective>();
                foreach ((Vector3 position, bool inRange) in new[]
                {
                    (new Vector3(0f, 0f, 4.9f), true), (new Vector3(3f, 4f, 0f), false), (new Vector3(0f, 0f, 5f), false),
                    (new Vector3(0f, 0f, 5.1f), false), (new Vector3(-1f, 2f, 2f), true),
                })
                {
                    objective.transform.position = position;
                    Assert.That(Call(rig.Pool, "IsWithinDistance", objective, rig.Character), Is.EqualTo(inRange), position.ToString());
                }
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
            Run(rig =>
            {
                MissionScorerObjective objective = rig.World.NewObjective<MissionScorerObjective>();
                objective.transform.position = new Vector3(0f, 0f, 1000f);
                Assert.That(Call(rig.Pool, "IsWithinDistance", objective, rig.Character), Is.EqualTo(true));
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 0f, 0.5f));
        }

        // 0x5D6D40: each objective of the pooled type gets an empty entry (no instance, index -1); other types and plain
        // objectives get none. The character manager is then found and the refresh coroutine requested.
        [Test]
        public void StartPoolsOnlyTheObjectivesOfTheConfiguredTypeAndRequestsTheRefresh()
        {
            Run(rig =>
            {
                MissionScorerObjective near = rig.World.NewObjective<MissionScorerObjective>();
                MissionScorerObjective far = rig.World.NewObjective<MissionScorerObjective>();
                MissionScorerObjective other = rig.World.NewObjective<MissionScorerObjective>();
                MissionObjective plain = rig.World.NewObjective<MissionObjective>();
                Set(near, "m_type", MissionScorerObjectiveType.ProximityTarget); // Reflection: authored serialized types
                Set(far, "m_type", MissionScorerObjectiveType.ProximityTarget);
                Set(other, "m_type", MissionScorerObjectiveType.Crate_Zone1);
                var tracker = rig.World.NewTracker<MissionInstancedTracker>();
                Set(tracker, "m_missionObjectives", new List<MissionObjective> { near, plain, other, far }); // Reflection: authored list
                Set(rig.Pool, "m_instancedTracker", tracker);
                Set(rig.Pool, "m_type", MissionScorerObjectiveType.ProximityTarget);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(rig.Pool, "Start");

                var entries = Get<Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>>>(rig.Pool, "m_instancesByObjective");
                Assert.That(entries.Keys, Is.EquivalentTo(new[] { near, far }));
                Assert.That(entries.Values.Select(entry => entry.Index).ToArray(), Is.All.EqualTo(-1));
                Assert.That(entries.Values.Select(entry => entry.Instance == null).ToArray(), Is.All.True);
                Assert.That(Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_objectiveKeys"), Is.EquivalentTo(new[] { near, far }));
                Assert.That(Get<CharacterManager>(rig.Pool, "m_characterManager"), Is.SameAs(rig.Manager));
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
        }

        // 0x5D798E: without a current character the refresh only waits for the next interval.
        [Test]
        public void TheRefreshWaitsTheConfiguredIntervalWhenThereIsNoCurrentCharacter()
        {
            Run(rig =>
            {
                Set(rig.Pool, "m_characterManager", rig.Manager); // Reflection: stored by OnCharacterManagerValid, which starts this coroutine
                var refresh = (IEnumerator)Call(rig.Pool, "RefreshPooledPrefabs");
                Assert.That(refresh.MoveNext(), Is.True);
                Assert.That(OriginalInOutTestSupport.Seconds((WaitForSeconds)refresh.Current), Is.EqualTo(0.5f));
                Assert.That(refresh.Current, Is.SameAs(Get<WaitForSeconds>(rig.Pool, "m_refreshWaitForSeconds")));
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
        }

        // 0x5D76B7..0x5D77E1: an incomplete active objective that has left range is removed from the active set and
        // despawned; one still in range stays active, and a completed one is not examined at all.
        [Test]
        public void ARefreshDespawnsIncompleteObjectivesOutOfRangeAndSkipsCompletedOnes()
        {
            Run(rig =>
            {
                MissionScorerObjective inRange = rig.World.NewObjective<MissionScorerObjective>();
                MissionScorerObjective outOfRange = rig.World.NewObjective<MissionScorerObjective>();
                MissionScorerObjective completed = rig.World.NewObjective<MissionScorerObjective>();
                inRange.transform.position = new Vector3(0f, 0f, 1f);
                outOfRange.transform.position = new Vector3(0f, 0f, 50f);
                completed.transform.position = new Vector3(0f, 0f, 50f);
                completed.Action_ObjectiveComplete();
                var entries = Get<Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>>>(rig.Pool, "m_instancesByObjective");
                var keys = Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_objectiveKeys");
                var active = Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_activeObjectives");
                // Reflection: Start fills the entries, and the spawn branch that fills the active set needs the
                // prefab instances Awake builds; the objectives are placed as those branches would leave them.
                foreach (MissionScorerObjective objective in new[] { inRange, outOfRange, completed })
                {
                    entries.Add(objective, new PooledPrefab<MissionScorerInstance>(null, -1));
                    keys.Add(objective);
                    active.Add(objective);
                }
                Set(rig.Pool, "m_characterManager", rig.Manager);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, true);

                var refresh = (IEnumerator)Call(rig.Pool, "RefreshPooledPrefabs");
                Assert.That(refresh.MoveNext(), Is.True);

                Assert.That(active, Is.EquivalentTo(new[] { inRange, completed }));
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
        }

        // A pool with a prefab of one scorer instance and its own evaluator, built by the pool's own Awake. The pool
        // manager row is real, so the base pool registers itself as it does at startup.
        private static void BuildInstances(Rig rig, int initialObjectCount)
        {
            Owned owned = rig.World.Owned;
            owned.Register(owned.Host("Lucid prefab pool manager", false).AddComponent<PrefabPoolManager>());
            GameObject prefabHost = owned.Host("Lucid scorer prefab", false);
            var prefab = prefabHost.AddComponent<MissionScorerInstance>();
            Set(prefab, "m_evaluator", prefabHost.AddComponent<MissionScorerProximityEvaluator>()); // Reflection: serialized prefab reference
            Set(rig.Pool, "m_prefab", prefab); // Reflection: authored serialized pool settings
            Set(rig.Pool, "m_initialObjectCount", initialObjectCount);
            // The pool registers under its type, an authored asset that is a key by its GUID (ScriptableObjectWithGuid equality).
            var poolType = owned.Track(ScriptableObject.CreateInstance<PrefabPoolType>());
            Set(poolType, "m_guid", "lucid-scorer-pool-type");
            Set(rig.Pool, "m_objectPoolType", poolType);
            Call(rig.Pool, "Awake");
        }

        // 0x5D77FB..0x5D7985: an in-range incomplete objective gets a pooled instance placed at its transform with the
        // objective's local scale (not the lossy one), bound to the objective and its own evaluator, switched on and
        // recorded as active. A pool with no free instance leaves the objective untouched.
        [Test]
        public void ARefreshSpawnsAnInstanceForAnInRangeObjectiveAndBindsItsEvaluator()
        {
            Run(rig =>
            {
                BuildInstances(rig, 2);
                Transform parent = rig.World.Owned.Host("Lucid scaled parent", false).transform;
                parent.localScale = new Vector3(2f, 2f, 2f);
                MissionScorerObjective objective = rig.World.NewObjective<MissionScorerObjective>(parent);
                objective.transform.localPosition = new Vector3(0f, 0f, 0.5f); // world z 1, inside the 5 m range
                objective.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                objective.transform.localScale = new Vector3(1f, 2f, 3f);
                var entries = Get<Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>>>(rig.Pool, "m_instancesByObjective");
                var active = Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_activeObjectives");
                // Reflection: Start fills these, and it needs the tracker's objectives and a character manager callback.
                entries.Add(objective, new PooledPrefab<MissionScorerInstance>(null, -1));
                Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_objectiveKeys").Add(objective);
                Set(rig.Pool, "m_characterManager", rig.Manager);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, true);

                var refresh = (IEnumerator)Call(rig.Pool, "RefreshPooledPrefabs");
                Assert.That(refresh.MoveNext(), Is.True);

                Assert.That(active, Is.EquivalentTo(new[] { objective }));
                MissionScorerInstance instance = entries[objective].Instance;
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.Objective, Is.SameAs(objective));
                Assert.That(objective.ScorerEvaluator, Is.SameAs(instance.Evaluator));
                Assert.That(instance.Evaluator.gameObject, Is.SameAs(instance.gameObject), "the clone's own evaluator, not the prefab's");
                Assert.That(instance.gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.position, Is.EqualTo(new Vector3(0f, 0f, 1f)));
                Assert.That(Quaternion.Angle(instance.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
                Assert.That(instance.transform.localScale, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
        }

        [Test]
        public void ARefreshLeavesTheObjectiveInactiveWhenThePoolHasNoFreeInstance()
        {
            Run(rig =>
            {
                BuildInstances(rig, 0);
                MissionScorerObjective objective = rig.World.NewObjective<MissionScorerObjective>();
                objective.transform.position = new Vector3(0f, 0f, 1f);
                var entries = Get<Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>>>(rig.Pool, "m_instancesByObjective");
                entries.Add(objective, new PooledPrefab<MissionScorerInstance>(null, -1)); // Reflection: Start fills these
                Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_objectiveKeys").Add(objective);
                Set(rig.Pool, "m_characterManager", rig.Manager);
                CharacterTriggerTestSupport.SetCurrent(rig.Manager, rig.Character, true);

                var refresh = (IEnumerator)Call(rig.Pool, "RefreshPooledPrefabs");
                Assert.That(refresh.MoveNext(), Is.True);

                Assert.That(Get<HashSet<MissionScorerObjective>>(rig.Pool, "m_activeObjectives"), Is.Empty);
                Assert.That(entries[objective].Instance == null, Is.True);
                Assert.That(objective.ScorerEvaluator, Is.Null);
            }, PerformanceProfile.QualityLevel.Low, (PerformanceProfile.QualityLevel.Low, 5f, 0.5f));
        }
    }
}
