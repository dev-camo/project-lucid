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
    // Original Game.Runtime type 0x02000a55.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EntityActivationLogic
    {
        private bool m_started;
        // Original 0x06003b69.
        protected bool HasStarted => m_started;
        // Original 0x06003b6a: genuine abstract contract.
        public abstract void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity);
        // Original 0x06003b6b: genuine empty virtual body.
        public virtual void Shutdown() { }
        // Original 0x06003b6c: C# destructor retains virtual Shutdown and implicit finally calling Object.Finalize.
        ~EntityActivationLogic() { Shutdown(); }
        // Original slots 6/7/8, methods 0x06003b6d/0x06003b6e/0x06003b6f.
        public virtual void OnStart() => m_started = true;
        public virtual void OnEnd() => m_started = false;
        public virtual bool CanTrigger() => m_started;
        // Original 0x06003b70.
        protected EntityActivationLogic() { }
    }
}
