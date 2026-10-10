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
    // Original Game.Runtime type 0x02000a56.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class EntityActivationLogic<T> : EntityActivationLogic where T : EntityActivationLogicDefinition
    {
        protected T m_definition;
        protected IEntityActivatable m_entity;
        // Original 0x06003b71: a failed cast stores null, then the entity is stored.
        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
        {
            m_definition = definition as T;
            m_entity = entity;
        }
        // Original 0x06003b72.
        protected EntityActivationLogic() { }
    }
}
