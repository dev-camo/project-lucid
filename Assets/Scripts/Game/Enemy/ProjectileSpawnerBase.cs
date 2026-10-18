using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000674; methods 060022ef..060022f8 and
    // nested ProjectilesAndCount 02000675. The nested struct is declared first,
    // which gives Awake the member ordinal 12 its two lambdas carry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ProjectileSpawnerBase : CharacterTrigger, IResetCapableGameplayElement
    {
        [Serializable]
        protected struct ProjectilesAndCount
        {
            public int ProjectileCount;
            public ProjectileType ProjectileType;
        }

        [SerializeField]
        protected ProjectilesAndCount[] m_projectileConfigs;
        [SerializeField]
        protected bool m_fireImmediatelyOnEntry;
        [SerializeField]
        protected bool m_fireOnStay;
        [SerializeField]
        protected float m_fireOnStayTimer;
        [SerializeField]
        protected TimeCategory m_updateTimeCategory = TimeCategory.EnemyMovement;
        protected readonly List<Projectile> m_firedProjectiles = new List<Projectile>();
        protected ProjectileManager m_projectileManager;
        private SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();
        private SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        private TimeManager m_timeManager;
        private float m_stayTimer;

        // 060022ef: the time manager callback is requested before the projectile
        // manager is fetched, and island registration last.
        protected override void Awake()
        {
            base.Awake();
            m_timeManagerRef.InvokeOnValid(manager => m_timeManager = manager);
            m_projectileManager = ProcessManager.GetSystem<ProjectileManager>();
            m_gameplayIslandManagerRef.InvokeOnValid(islandManager => islandManager.RegisterResetCapableBehaviour(this));
        }

        protected abstract void Fire();

        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            m_stayTimer = 0f;
            if (m_fireImmediatelyOnEntry)
                Fire();
        }

        // 060022f2: the early return is taken only for an ordered
        // m_fireOnStayTimer >= m_stayTimer (ucomiss/jae at 0x5810f6), so a NaN
        // interval or timer fires on every stay. Read from the disassembly; no
        // test covers this branch yet (it needs a TimeManager fixture).
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            if (!m_fireOnStay)
                return;
            m_stayTimer += m_timeManager.GetFixedDeltaTime(m_updateTimeCategory);
            if (m_stayTimer <= m_fireOnStayTimer)
                return;
            Fire();
            m_stayTimer = 0f;
        }

        // 060022f3: cools down every active projectile of each configured type,
        // without exceptions, through the projectile manager.
        public void ResetGameplayElement()
        {
            foreach (ProjectilesAndCount projectileConfig in m_projectileConfigs)
                m_projectileManager.ForceCoolDownOfType(projectileConfig.ProjectileType, false);
        }

        public Vector3 GetPosition() => transform.position;

        public bool IsDestroyed() => this == null;
    }
}
