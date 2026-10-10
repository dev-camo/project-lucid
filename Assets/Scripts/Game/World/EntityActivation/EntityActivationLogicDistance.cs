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
    // Original Game.Runtime type 0x02000a58.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicDistance : EntityActivationLogic<EntityActivationLogicDistanceDefinition>
    {
        // Original 0x06003b78: strict greater-than; reread the entity after the CharacterManager callback.
        public override bool CanTrigger()
        {
            Character character = null;
            if (!HasStarted || m_entity == null) return false;
            if (!ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out character)) return false;
            return (m_entity.Transform.position - character.WorldPosition).sqrMagnitude > m_definition.DistanceSqr;
        }
        // Original 0x06003b79.
        public EntityActivationLogicDistance() { }
    }
}
