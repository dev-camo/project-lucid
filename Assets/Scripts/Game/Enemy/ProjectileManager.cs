using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000672; methods 060022de..060022ec.
    // The pool factory retains the original definition capture. Its two natural
    // display-class methods (060022ed..060022ee) require separate emission binding.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ProjectileManager : MonoBehaviour, ISystem
    {
        [SerializeField]
        [Tooltip("Where projectiles will be spawn and held while they are pooled")]
        private Transform m_projectileHolder;
        [SerializeField] private int m_initialPoolSizes = 2;
        private readonly Dictionary<ProjectileType, ObjectPool<Projectile>> m_projectilePools =
            new Dictionary<ProjectileType, ObjectPool<Projectile>>(HardlightEnumComparers.ProjectileTypeComparer);
        private readonly List<Projectile> m_activeProjectiles = new List<Projectile>();
        private readonly List<Projectile> m_temporaryProjectileList = new List<Projectile>();
        private SystemRef<LevelManager> m_levelManagerSystemRef;

        // 060022de: DataManager readiness is invoked before storing the level ref.
        private void Awake()
        {
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(OnDataManagerReady);
            m_levelManagerSystemRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
            m_levelManagerSystemRef.InvokeOnValid(OnLevelManagerReady);
        }

        // 060022df: replacing an existing key is the original dictionary operation.
        // Each factory closes over that entry's definition, not a mutable shared one.
        private void OnDataManagerReady(DataManager dataManager)
        {
            foreach (var (projectileType, projectileDefinition) in dataManager.ProjectileDefinitions)
            {
                m_projectilePools[projectileType] = new ObjectPool<Projectile>(
                    () => InstantiateProjectile(projectileDefinition),
                    OnPoolGet, OnPoolRelease, OnPoolDestroy, true, m_initialPoolSizes, 10000);
            }
            ProcessManager.RegisterSystem(this, null, false, false);
        }

        // 060022e0: existing level notification is requested immediately.
        private void OnLevelManagerReady(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        // 060022e1: killing in reverse permits the original callbacks to remove items.
        // Trimming obtains excess inactive items through Get, so its callback still
        // runs. The final clear also discards those temporary active registrations.
        private void OnLevelActivated(LevelManagerLevel _)
        {
            for (int i = m_activeProjectiles.Count - 1; i >= 0; --i)
                m_activeProjectiles[i].Kill();

            foreach (var (projectileType, pool) in m_projectilePools)
            {
                while (pool.CountInactive > m_initialPoolSizes)
                    Destroy(pool.Get().gameObject);
            }
            m_activeProjectiles.Clear();
        }

        // 060022e2: pool teardown precedes active-object cleanup, unsubscribe and
        // unregister. Callback failures leave that original partial teardown intact.
        private void OnDestroy()
        {
            foreach (var (projectileType, pool) in m_projectilePools)
                pool.Dispose();
            m_projectilePools.Clear();

            foreach (Projectile projectile in m_activeProjectiles)
            {
                if (projectile == null) continue;
                Destroy(projectile.gameObject);
            }
            m_activeProjectiles.Clear();

            if (m_levelManagerSystemRef.IsValid())
                m_levelManagerSystemRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
            m_levelManagerSystemRef = null;
            ProcessManager.UnregisterSystem(this);
        }

        // 060022e3: acquiring tracks the projectile without activating its object.
        private void OnPoolGet(Projectile projectile) => m_activeProjectiles.Add(projectile);

        // 060022e4: the shipping reset uses an all-zero quaternion. Preserve it
        // independently of the usual identity quaternion used by other game code.
        private void OnPoolRelease(Projectile projectile)
        {
            m_activeProjectiles.Remove(projectile);
            projectile.gameObject.SetActive(false);
            projectile.transform.SetParent(m_projectileHolder);
            projectile.transform.SetPositionAndRotation(Vector3.zero, default(Quaternion));
        }

        // 060022e5: remove this subscription before deferring object destruction.
        private void OnPoolDestroy(Projectile projectile)
        {
            projectile.OnExpired -= ReturnToPool;
            Destroy(projectile.gameObject);
        }

        // 060022e6: initialization precedes expiration subscription and deactivation.
        private Projectile InstantiateProjectile(ProjectileDefinition definition)
        {
            Projectile projectile = Instantiate(definition.Projectile, m_projectileHolder);
            projectile.Initialise(definition);
            projectile.OnExpired += ReturnToPool;
            projectile.gameObject.SetActive(false);
            return projectile;
        }

        // 060022e7: destroyed Unity objects take the removal branch. Missing pool
        // keys remain errors; an unknown projectile is not silently discarded.
        private void ReturnToPool(Projectile projectile)
        {
            if (projectile == null)
                m_activeProjectiles.Remove(projectile);
            else
                m_projectilePools[projectile.ProjectileType].Release(projectile);
        }

        // 060022e8.
        public Projectile AcquireProjectile(ProjectileType projectileType) => m_projectilePools[projectileType].Get();

        // 060022e9: compute the deficit once. Keep acquired objects until all Gets
        // finish, then Kill them through their original expiration callbacks.
        public void PreWarmProjectile(ProjectileType projectileType, int minimum)
        {
            m_temporaryProjectileList.Clear();
            int count = unchecked(minimum - m_projectilePools[projectileType].CountInactive);
            for (int i = 0; i < count; ++i)
                m_temporaryProjectileList.Add(AcquireProjectile(projectileType));
            foreach (Projectile projectile in m_temporaryProjectileList)
                projectile.Kill();
            m_temporaryProjectileList.Clear();
        }

        // 060022ea: retain the second indexed lookup for the matching object; do
        // not cache it across original getter/callback execution.
        public void ForceCoolDownOfType(ProjectileType typeToCoolDown, bool allowExceptions)
        {
            for (int i = m_activeProjectiles.Count - 1; i >= 0; --i)
            {
                if (m_activeProjectiles[i].ProjectileType == typeToCoolDown)
                    m_activeProjectiles[i].ForceEnterCoolDown(allowExceptions);
            }
        }

        // 060022eb.
        public void KillAllOfType(ProjectileType typeToKill)
        {
            for (int i = m_activeProjectiles.Count - 1; i >= 0; --i)
            {
                if (m_activeProjectiles[i].ProjectileType == typeToKill)
                    m_activeProjectiles[i].Kill();
            }
        }
        // 060022ec is emitted by the ordered field initializers and base constructor.
    }
}
