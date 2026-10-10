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
    // Original Game.Runtime type 0x02000a5a.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicLevelActivate : EntityActivationLogic<EntityActivationLogicLevelActivateDefinition>
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private bool m_hasLoaded;
        private bool m_canTrigger;
        // Original 0x06003b82 and natural 0x06003b88: the callback requests immediate delivery with true.
        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
        {
            base.Initialise(definition, entity);
            m_hasLoaded = false;
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
        }
        // Original 0x06003b83.
        public override void Shutdown()
        {
            LevelManager levelManager;
            if (m_levelManagerRef.TryGet(out levelManager)) levelManager.RemoveLevelActivatedAction(OnLevelActivated);
        }
        // Original 0x06003b84.
        public override void OnEnd() { base.OnEnd(); m_canTrigger = false; }
        // Original 0x06003b85.
        public override bool CanTrigger()
        {
            if (!HasStarted || !m_canTrigger) return false;
            m_canTrigger = false;
            return true;
        }
        // Original 0x06003b86: definition precedes HasStarted when this is the first notification.
        private void OnLevelActivated(LevelManagerLevel level)
        {
            if ((m_hasLoaded || m_definition.ActivateOnLoad) && HasStarted) m_canTrigger = true;
            m_hasLoaded = true;
        }
        // Original 0x06003b87.
        public EntityActivationLogicLevelActivate() { }
    }
}
