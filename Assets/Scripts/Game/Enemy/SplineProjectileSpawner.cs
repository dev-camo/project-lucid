using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000677; methods 06002305..06002308, bodies from the 1.10.1
    // x86-64 binary at 0x582850..0x582E60.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SplineProjectileSpawner : ProjectileSpawnerBase
    {
        [SerializeField]
        private Spline m_spline;

        // 06002305: only NightmareEggmanProjectile follows a spline; any other acquired projectile is
        // logged and left acquired. Setup is inlined with the focus (0, 50, 0) (constant at 0x29a7990)
        // and no owner collider, then the spline is handed over before the projectile fires.
        [ContextMenu("Fire!")]
        protected override void Fire()
        {
            foreach (ProjectilesAndCount projectileConfig in m_projectileConfigs)
            {
                for (int i = 0; i < projectileConfig.ProjectileCount; i++)
                {
                    Projectile projectile = m_projectileManager.AcquireProjectile(projectileConfig.ProjectileType);
                    if (projectile is NightmareEggmanProjectile nightmareProjectile)
                    {
                        nightmareProjectile.Setup(new Vector3(0f, 50f, 0f), null);
                        nightmareProjectile.SetSpline(m_spline);
                        nightmareProjectile.Fire();
                        m_firedProjectiles.AddUnique(nightmareProjectile);
                        nightmareProjectile.OnExpired += OnProjectileExpired;
                    }
                    else
                    {
                        HLOutput.LogError(string.Format("{0} is of type {1} and is not currently supported as a spline following projectile.",
                            projectile, projectile.ProjectileType.GetString()));
                    }
                }
            }
        }

        // 06002306: leaving the trigger sends every fired projectile into cooldown (allowExceptions false,
        // esi 0 at 0x582ccf).
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            foreach (Projectile projectile in m_firedProjectiles)
                projectile.ForceEnterCoolDown(false);
        }

        // 06002307
        private void OnProjectileExpired(Projectile projectile)
        {
            m_firedProjectiles.Remove(projectile);
            projectile.OnExpired -= OnProjectileExpired;
        }
    }
}
