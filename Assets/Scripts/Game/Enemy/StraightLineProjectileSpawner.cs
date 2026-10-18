using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000679; methods 0600230e..06002310.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StraightLineProjectileSpawner : ProjectileSpawnerBase
    {
        private const float Target_Distance = 100f;
        [SerializeField]
        private Collider m_ownColliderToIgnore;
        private Transform m_transform;

        protected override void Awake()
        {
            base.Awake();
            m_transform = transform;
        }

        // 0600230f: each acquired projectile of another kind is logged and left
        // acquired; straight-line ones start at the spawner and aim 100 units
        // along its forward axis. m_firedProjectiles is not touched here.
        [ContextMenu("Fire!")]
        protected override void Fire()
        {
            foreach (ProjectilesAndCount projectileConfig in m_projectileConfigs)
            {
                for (int i = 0; i < projectileConfig.ProjectileCount; i++)
                {
                    Projectile projectile = m_projectileManager.AcquireProjectile(projectileConfig.ProjectileType);
                    if (projectile is StraightLineProjectile straightLineProjectile)
                    {
                        straightLineProjectile.transform.position = m_transform.position;
                        straightLineProjectile.Setup(m_transform.position + m_transform.forward * Target_Distance, m_ownColliderToIgnore);
                        straightLineProjectile.Fire();
                    }
                    else
                    {
                        HLOutput.LogError(string.Format("{0} is of type {1} and is not currently supported as a StraightLineProjectile.",
                            projectile, projectile.ProjectileType.GetString()));
                    }
                }
            }
        }
    }
}
