// Preserves the original shipping declarations and inferred whole managed bodies.
// Compiler-generated identities and exceptional native fault ordering require separate qualification.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000a5b.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicMissionActivate : EntityActivationLogic<EntityActivationLogicMissionActivateDefinition>
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private bool m_canTrigger;
        // Original 0x06003b89.
        private bool IsEntityActive => m_entity.Transform.gameObject.activeSelf;
        // Original 0x06003b8a and natural 0x06003b92.
        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
        {
            base.Initialise(definition, entity);
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
        }
        // Original 0x06003b8b: only detach the LevelManager notification here.
        public override void Shutdown()
        {
            LevelManager levelManager;
            if (m_levelManagerRef.TryGet(out levelManager)) levelManager.RemoveLevelActivatedAction(OnLevelActivated);
        }
        // Original 0x06003b8c: notify the genuine generic entity first, then obtain MissionManager and enumerate the live lists.
        private void OnLevelActivated(LevelManagerLevel level)
        {
            IEntityActivatable<MissionDefinition> entity = m_entity as IEntityActivatable<MissionDefinition>;
            if (entity == null) return;
            entity.OnLevelActivated();
            MissionManager missionManager = m_missionManagerRef.Get();
            foreach (MissionDefinition mission in entity.EntitiesToEnable)
                missionManager.InvokeIfMissionActive(mission, OnMissionEnable);
            foreach (MissionDefinition mission in entity.EntitiesToDisable)
                missionManager.InvokeIfMissionActive(mission, OnMissionDisable);
        }
        // Original 0x06003b8d: bitwise assignment evaluates the right side even if the latch was already true.
        private void OnMissionEnable(MissionState missionState) => m_canTrigger |= IsEntityActive != m_definition.Deactivates;
        // Original 0x06003b8e.
        private void OnMissionDisable(MissionState missionState) => m_canTrigger |= IsEntityActive != !m_definition.Deactivates;
        // Original 0x06003b8f.
        public override void OnEnd() { base.OnEnd(); m_canTrigger = false; }
        // Original 0x06003b90.
        public override bool CanTrigger()
        {
            if (!HasStarted || !m_canTrigger) return false;
            m_canTrigger = false;
            return true;
        }
        // Original 0x06003b91: level reference then mission reference, both before the base constructor.
        public EntityActivationLogicMissionActivate() { }
    }
}
