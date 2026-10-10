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
    // Original Game.Runtime type 0x02000a57.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationLogicCharacterRespawn : EntityActivationLogic<EntityActivationLogicCharacterRespawnDefinition>
    {
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private bool m_canTrigger;
        // Original 0x06003b73.
        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
        {
            base.Initialise(definition, entity);
            m_characterManagerRef.Get().OnCharacterRespawn += OnCharacterRespawn;
        }
        // Original 0x06003b74: this does not call base.Shutdown or clear the latch.
        public override void Shutdown()
        {
            CharacterManager characterManager;
            if (m_characterManagerRef.TryGet(out characterManager))
                characterManager.OnCharacterRespawn -= OnCharacterRespawn;
        }
        // Original 0x06003b75.
        private void OnCharacterRespawn() { if (HasStarted) m_canTrigger = true; }
        // Original 0x06003b76: a successful query consumes the latch.
        public override bool CanTrigger()
        {
            if (!HasStarted || !m_canTrigger) return false;
            m_canTrigger = false;
            return true;
        }
        // Original 0x06003b77: create the real system reference before the base constructor.
        public EntityActivationLogicCharacterRespawn() { }
    }
}
