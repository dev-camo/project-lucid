using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationInstance
    {
        // Original 0x06003b56; original field offset 0x10.
        public IEntityActivatable Entity { [CompilerGenerated] get; }
        private readonly List<EntityActivationLogic> m_activateLogics;
        private readonly List<EntityActivationLogic> m_deactivateLogics;
        private readonly Action m_onActivateCallback;
        private readonly Action m_onDeactivateCallback;

        // Original 0x06003b57. Definition is fetched again for the second
        // factory. Preserve partial construction if either factory throws.
        public EntityActivationInstance(IEntityActivatable entity, Action onActivateCallback, Action onDeactivateCallback)
        {
            Entity = entity;
            m_activateLogics = Entity.Definition.CreateActivationLogic(Entity);
            m_deactivateLogics = Entity.Definition.CreateDeactivationLogic(Entity);
            m_onActivateCallback = onActivateCallback;
            m_onDeactivateCallback = onDeactivateCallback;
        }

        // Original 0x06003b58/0x06003b59: any matching rule triggers a change;
        // an empty list returns false. Foreach preserves disposal and live edits.
        public bool CanActivate()
        {
            foreach (EntityActivationLogic logic in m_activateLogics)
                if (logic.CanTrigger()) return true;
            return false;
        }

        public bool CanDeactivate()
        {
            foreach (EntityActivationLogic logic in m_deactivateLogics)
                if (logic.CanTrigger()) return true;
            return false;
        }

        // Original 0x06003b5a. Initial registration skips only ending the
        // previous rules. The callback runs before starting the opposite rules.
        public void OnActivate(bool isFirstTime = false)
        {
            if (!isFirstTime)
                foreach (EntityActivationLogic logic in m_activateLogics) logic.OnEnd();
            m_onActivateCallback?.Invoke();
            foreach (EntityActivationLogic logic in m_deactivateLogics) logic.OnStart();
        }

        // Original 0x06003b5b: same ordering, with the two lists exchanged.
        public void OnDeactivate(bool isFirstTime = false)
        {
            if (!isFirstTime)
                foreach (EntityActivationLogic logic in m_deactivateLogics) logic.OnEnd();
            m_onDeactivateCallback?.Invoke();
            foreach (EntityActivationLogic logic in m_activateLogics) logic.OnStart();
        }

        // Original 0x06003b5c. No list clearing, callback reset or completion
        // callback occurs. A fault in the first pass prevents the second pass.
        public void Shutdown()
        {
            foreach (EntityActivationLogic logic in m_activateLogics) logic.Shutdown();
            foreach (EntityActivationLogic logic in m_deactivateLogics) logic.Shutdown();
        }
    }
}
