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
    // Original Game.Runtime type 0x02000547.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class EntityActivationLogicDefinition : ScriptableObject
    {
        // Original 0x06001cac: genuine abstract contract.
        public abstract EntityActivationLogicType Type { get; }
        // Original 0x06001cad: initialise after reflective construction; do not replace the cast with a safe fallback.
        public EntityActivationLogic CreateLogic(IEntityActivatable entity)
        {
            EntityActivationLogic logic = Instantiate();
            logic.Initialise(this, entity);
            return logic;
        }
        // Original 0x06001cae: genuine abstract factory-type contract.
        protected abstract Type ScriptType { get; }
        // Original 0x06001caf.
        private EntityActivationLogic Instantiate() => (EntityActivationLogic)Activator.CreateInstance(ScriptType);
        // Original 0x06001cb0.
        protected EntityActivationLogicDefinition() { }
    }
}
