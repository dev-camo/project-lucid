using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Whole original Game.Runtime 020002a8, methods06000e99..ea2.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossTimedObjectPool : PrefabPool<TimedLifetimeObject>, ISystem, ITimeScaled
    {
        private readonly List<PooledPrefab<TimedLifetimeObject>> m_currentObjects = new List<PooledPrefab<TimedLifetimeObject>>();
        private readonly List<PooledPrefab<TimedLifetimeObject>> m_expiredObjects = new List<PooledPrefab<TimedLifetimeObject>>();
        private bool m_objectsExpiredThisFrame;
        [SerializeField] private bool m_unparentSelfOnAwake;
        private bool m_forceDespawn;
        public Action<BossTimedObjectPool> WasDestroyed;

        // 06000e99: enable callbacks and subscribe before optional unparenting,
        // then perform the genuine generic base pool Awake.
        protected override void Awake()
        {
            SpawnDespawnCallbackEnabled = true;
            ProcessManager.GetSystemRef<TimeManager>(null, true).Get().Subscribe(this, TimeCategory.EnemyMovement, UpdateOn.Update, "");
            if (m_unparentSelfOnAwake && transform.parent != null) transform.parent = null;
            base.Awake();
        }

        // 06000e9a: expiry is checked before the live force flag. Despawn precedes
        // the deferred removal append; faults leave the original completed prefix.
        // Both live enumerations dispose before their subsequent flag/list changes.
        public void OnUpdate(float deltaTime)
        {
            foreach (PooledPrefab<TimedLifetimeObject> currentObject in m_currentObjects)
            {
                if (currentObject.Instance.ObjectExpired || m_forceDespawn)
                {
                    DespawnInstance(in currentObject);
                    m_expiredObjects.Add(currentObject);
                    m_objectsExpiredThisFrame = true;
                }
            }
            m_forceDespawn = false;
            if (m_objectsExpiredThisFrame)
            {
                foreach (PooledPrefab<TimedLifetimeObject> expiredObject in m_expiredObjects)
                    m_currentObjects.Remove(expiredObject);
                m_expiredObjects.Clear();
                m_objectsExpiredThisFrame = false;
            }
        }

        // 06000e9b..e9c: original empty interface implementations.
        public void OnFixedUpdate(float deltaTime) { }
        public void OnLateUpdate(float deltaTime) { }
        // 06000e9d..e9e: retain the original ordinary automatic property.
        public bool IsPaused { get; set; }

        // 06000e9f: callback is ignored; append only, without invoking the base hook.
        protected override void Spawned(in PooledPrefab<TimedLifetimeObject> spawnedObject, UpdateSpawnedObjectCallback callback)
        {
            m_currentObjects.Add(spawnedObject);
        }

        // 06000ea0: original CLR-null short circuit, then the destruction callback,
        // followed by the base pool teardown. A callback fault skips the base call.
        protected override void OnDestroy()
        {
            ProcessManager.GetSystemRef<TimeManager>(null, true).GetSafe()?.Unsubscribe(this, TimeCategory.EnemyMovement, UpdateOn.Update);
            WasDestroyed?.Invoke(this);
            base.OnDestroy();
        }

        // 06000ea1 only sets a flag consumed by a later completed OnUpdate.
        public void ForceDespawnAllObjects() => m_forceDespawn = true;
        // 06000ea2 is the ordered two list initializers followed by the real base
        // constructor. No additional constructor or manager facade is invented.
    }
}
