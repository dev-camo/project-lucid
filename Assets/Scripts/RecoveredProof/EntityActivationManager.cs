using Hardlight;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationManager : ISystem
    {
        // Original 0x06003bac constructs these before Object's constructor.
        // Read the original registry separately for each dictionary.
        private readonly Dictionary<EntityActivationType, List<EntityActivationInstance>> m_active =
            new Dictionary<EntityActivationType, List<EntityActivationInstance>>(HardlightEnumComparers.EntityActivationTypeComparer);
        private readonly Dictionary<EntityActivationType, List<EntityActivationInstance>> m_inactive =
            new Dictionary<EntityActivationType, List<EntityActivationInstance>>(HardlightEnumComparers.EntityActivationTypeComparer);

        public EntityActivationManager()
        {
            this.SubscribeToAction(SystemAction.Update, OnUpdate);
        }

        // Original 0x06003bad. Both shutdown passes finish before either
        // dictionary is cleared. Exceptions retain the preceding mutations.
        public void Clear()
        {
            foreach (List<EntityActivationInstance> instances in m_active.Values)
                foreach (EntityActivationInstance instance in instances) instance.Shutdown();
            foreach (List<EntityActivationInstance> instances in m_inactive.Values)
                foreach (EntityActivationInstance instance in instances) instance.Shutdown();
            m_active.Clear();
            m_inactive.Clear();
        }

        // Original 0x06003bae. Always create both type rows, inactive first.
        // A callback from the first registration can affect the second lookup.
        public void Register(IEntityActivatable entity, bool active, Action onActivateCallback = null, Action onDeactivateCallback = null)
        {
            TryRegisterEntity(entity, m_inactive, !active, onActivateCallback, onDeactivateCallback, active);
            TryRegisterEntity(entity, m_active, active, onActivateCallback, onDeactivateCallback, active);
        }

        // Original 0x06003baf: reference-null check, then active/inactive order.
        public void Deregister(IEntityActivatable entity)
        {
            if (entity == null) return;
            TryDeregisterEntity(entity, m_active);
            TryDeregisterEntity(entity, m_inactive);
        }

        // Original 0x06003bb0. Retain duplicate entries and empty type rows.
        private static void TryRegisterEntity(IEntityActivatable entity, IDictionary<EntityActivationType, List<EntityActivationInstance>> dictionary,
            bool addEntity, Action onActivateCallback, Action onDeactivateCallback, bool isActive)
        {
            EntityActivationDefinition definition = entity.Definition;
            if (!dictionary.TryGetValue(definition.Type, out List<EntityActivationInstance> instances))
            {
                instances = new List<EntityActivationInstance>();
                dictionary.Add(definition.Type, instances);
            }
            if (!addEntity) return;
            var instance = new EntityActivationInstance(entity, onActivateCallback, onDeactivateCallback);
            instances.Add(instance);
            if (isActive) instance.OnActivate(true);
            else instance.OnDeactivate(true);
        }

        // Original 0x06003bb1 and natural 0x06003bb7/0x06003bb8. Definition
        // and Transform use Unity null semantics; entity and predicate use CLR
        // reference identity. Removal precedes the Transform check and shutdown.
        private static void TryDeregisterEntity(IEntityActivatable entity,
            IDictionary<EntityActivationType, List<EntityActivationInstance>> entityDictionary)
        {
            if (entity == null) return;
            EntityActivationDefinition definition = entity.Definition;
            if (definition == null || !entityDictionary.TryGetValue(definition.Type, out List<EntityActivationInstance> instances)) return;
            int index = instances.FindIndex(entityInstance => entityInstance.Entity == entity);
            if (index == -1) return;
            EntityActivationInstance instance = instances[index];
            instances.RemoveAt(index);
            if (entity.Transform == null) return;
            instance.Shutdown();
        }

        // Original 0x06003bb2 and natural 0x06003bb9/0x06003bba. Missing
        // keys/indexes fault through the original indexers. Publish the list
        // transition before the callback; do not introduce rollback.
        public void Activate(IEntityActivatable entity)
        {
            EntityActivationDefinition definition = entity.Definition;
            List<EntityActivationInstance> instances = m_inactive[definition.Type];
            int index = instances.FindIndex(entityInstance => entityInstance.Entity == entity);
            EntityActivationInstance instance = instances[index];
            instances.RemoveAt(index);
            m_active[definition.Type].Add(instance);
            instance.OnActivate();
        }

        // Original 0x06003bb3 and natural 0x06003bbb/0x06003bbc. The source
        // dictionary uses TryGetValue here, unlike Activate's direct indexer.
        public void Deactivate(IEntityActivatable entity)
        {
            EntityActivationDefinition definition = entity.Definition;
            if (!m_active.TryGetValue(definition.Type, out List<EntityActivationInstance> instances)) return;
            int index = instances.FindIndex(entityInstance => entityInstance.Entity == entity);
            EntityActivationInstance instance = instances[index];
            instances.RemoveAt(index);
            m_inactive[definition.Type].Add(instance);
            instance.OnDeactivate();
        }

        // Original 0x06003bb4. Reverse live-list traversal avoids skipping
        // ordinary removals. The second pass also sees objects activated above.
        private void OnUpdate(object context = null)
        {
            foreach (KeyValuePair<EntityActivationType, List<EntityActivationInstance>> pair in m_inactive)
            {
                List<EntityActivationInstance> instances = pair.Value;
                for (int index = instances.Count - 1; index >= 0; --index)
                {
                    EntityActivationInstance instance = instances[index];
                    if (instance.CanActivate()) Activate(instance.Entity);
                }
            }
            foreach (KeyValuePair<EntityActivationType, List<EntityActivationInstance>> pair in m_active)
            {
                List<EntityActivationInstance> instances = pair.Value;
                for (int index = instances.Count - 1; index >= 0; --index)
                {
                    EntityActivationInstance instance = instances[index];
                    if (instance.CanDeactivate()) Deactivate(instance.Entity);
                }
            }
        }

        // Original 0x06003bb5 and exact bundled string literals2025/6617.
        public virtual void GetDebugInfo(StringBuilder stringInfoBuilder)
        {
            GetDebugObjectsInfo("Active objects", m_active, stringInfoBuilder);
            stringInfoBuilder.AppendLine();
            GetDebugObjectsInfo("Inactive objects", m_inactive, stringInfoBuilder);
        }

        // Original 0x06003bb6. Preserve two live enumerations, current-culture
        // formatting, one newline for empty rows and the trailing ", " per name.
        private static void GetDebugObjectsInfo(string header, IDictionary<EntityActivationType, List<EntityActivationInstance>> activationDictionary,
            StringBuilder stringInfoBuilder)
        {
            stringInfoBuilder.AppendLine();
            stringInfoBuilder.Append(header);
            int count = 0;
            foreach (KeyValuePair<EntityActivationType, List<EntityActivationInstance>> pair in activationDictionary)
                count += pair.Value.Count;
            stringInfoBuilder.AppendLine(string.Format(" ({0}): ", count));
            foreach (KeyValuePair<EntityActivationType, List<EntityActivationInstance>> pair in activationDictionary)
            {
                List<EntityActivationInstance> instances = pair.Value;
                int instanceCount = instances.Count;
                stringInfoBuilder.Append(string.Format("{0} ({1}): ", pair.Key, instanceCount));
                if (instanceCount != 0)
                {
                    foreach (EntityActivationInstance instance in instances)
                    {
                        IEntityActivatable entity = instance.Entity;
                        if (entity != null) stringInfoBuilder.Append(entity.Transform.name + ", ");
                    }
                    stringInfoBuilder.AppendLine();
                }
                else stringInfoBuilder.AppendLine("<Empty>");
            }
        }
    }
}
