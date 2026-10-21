using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001ec; bodies translated from the 1.10.1 x86-64 binary (0x6FE0F0..0x6FE4D0).
    // Fires the stated waves of shots at the boss's target, each shot spread by a random offset up to the accuracy.
    // The debug helper is conditional on BUILD_DEVELOPMENT and has no calls in the retail body.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_RangedAttack : ActorAbility, IAbilityTrigger, IAbilityRange
    {
        public BossAbilityDefinition_RangedAttack Definition;

        private bool m_canShoot;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b0a
        public bool IsTriggered => m_canShoot; // 06000b0b
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000b0c
        public float MaxRangeSquared => Definition.MaxRange; // 06000b0d
        public float MinRangeSquared => Definition.MinRange; // 06000b0e

        // 06000b0f: the original auto-property is never assigned, so the tolerance stays zero.
        public float DestinationReachedToleranceSquared { get; } // 06000b0f

        public float MaxFollowUpRangeSquared { get; set; } // 06000b10 / 06000b11

        private Boss m_boss;
        private float m_projectileTimerInSeconds;
        private float m_waveTimerInSeconds;
        private int m_activeWave;
        private int m_shotsTakenInWave;
        private bool m_waveOnCooldown;
        private bool m_projectileOnCooldown;
        private List<Projectile> m_projectilePool;
        private Vector3 m_currentTargetPosition;

        // 06000b12: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_RangedAttack;
        }

        // 06000b13
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
        }

        // 06000b14
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_canShoot = false;
            m_projectileTimerInSeconds = 0f;
            m_waveTimerInSeconds = 0f;
            m_activeWave = 0;
            m_shotsTakenInWave = 0;
            m_waveOnCooldown = false;
            m_projectileOnCooldown = false;
        }

        // 06000b15: waits out the wave and projectile cooldowns (the wave one wins), then either takes the next shot at the
        // target with a random spread, or ends the wave and starts the next one; the ability finishes after the last wave.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_canShoot = false;
            bool waveCoolingDown = false;
            if (m_waveOnCooldown)
            {
                m_waveTimerInSeconds = m_waveTimerInSeconds + deltaTime;
                m_waveOnCooldown = Definition.WaveCooldownSeconds > m_waveTimerInSeconds;
                waveCoolingDown = m_waveOnCooldown;
            }
            if (m_projectileOnCooldown)
            {
                m_projectileTimerInSeconds = deltaTime + m_projectileTimerInSeconds;
                m_projectileOnCooldown = Definition.ProjectileCooldownSeconds > m_projectileTimerInSeconds;
                if (waveCoolingDown || m_projectileOnCooldown)
                    return;
            }
            else if (waveCoolingDown)
            {
                return;
            }
            if (AbilityFinished)
                return;

            if (m_shotsTakenInWave < Definition.ProjectileCount)
            {
                m_currentTargetPosition = m_boss.Target.position;
                Vector3 accuracy = Definition.ProjectileAccuracy;
                float offsetX = Random.Range(-accuracy.x, accuracy.x);
                float offsetY = Random.Range(-accuracy.y, accuracy.y);
                float offsetZ = Random.Range(-accuracy.z, accuracy.z);
                m_currentTargetPosition += new Vector3(offsetX, offsetY, offsetZ);
                m_canShoot = true;
                m_projectileOnCooldown = true;
                m_projectileTimerInSeconds = 0f;
                m_shotsTakenInWave++;
                return;
            }
            m_waveOnCooldown = true;
            m_waveTimerInSeconds = 0f;
            m_activeWave++;
            m_shotsTakenInWave = 0;
            if (m_activeWave >= Definition.WaveCount)
                AbilityFinished = true;
        }

        // 06000b16: original empty body
        private void CacheDefinitionValues() { }

        // 06000b17: no work in the retail body
        [Conditional("BUILD_DEVELOPMENT")]
        private void UpdateDebug() { }

        // 06000b18
        protected override void GetDebugExtraInfo(StringBuilder extraInfo)
        {
            base.GetDebugExtraInfo(extraInfo);
        }
    }
}
