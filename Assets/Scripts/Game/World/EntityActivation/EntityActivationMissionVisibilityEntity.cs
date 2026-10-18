using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a65; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationMissionVisibilityEntity : EntityActivationLevelVisibilityEntity, IEntityActivatable<MissionDefinition>
    {
        [SerializeField] private List<MissionDefinition> m_missionsToEnable = new List<MissionDefinition>();
        [SerializeField] private List<MissionDefinition> m_missionsToDisable = new List<MissionDefinition>();
        private bool m_entityActiveOnAwake;

        // 0x06003bbd/0x06003bbe
        public List<MissionDefinition> EntitiesToEnable => m_missionsToEnable;
        public List<MissionDefinition> EntitiesToDisable => m_missionsToDisable;

        // 0x06003bbf: activeSelf here, unlike the base's activeInHierarchy.
        protected override void Awake()
        {
            base.Awake();
            m_entityActiveOnAwake = gameObject.activeSelf;
        }

        // 0x06003bc0 (0x6C3E10): only a changed visibility is pushed back to the awake state.
        public void OnLevelActivated()
        {
            if (this == null || m_entityActiveOnAwake == gameObject.activeSelf) return;
            bool activate = m_entityActiveOnAwake;
            EntityActivationManager entityActivationManager = m_entityActivationManagerRef.Get();
            if (activate)
                entityActivationManager.Activate(this);
            else
                entityActivationManager.Deactivate(this);
        }

        // 0x06003bc1
        public void AddMission(MissionDefinition missionDefinition, bool enable)
        {
            List<MissionDefinition> missions = enable ? m_missionsToEnable : m_missionsToDisable;
            if (missions.Contains(missionDefinition)) return;
            missions.Add(missionDefinition);
        }
    }
}
