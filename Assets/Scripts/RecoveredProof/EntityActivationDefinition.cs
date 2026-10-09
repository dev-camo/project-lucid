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
    // Original Game.Runtime type 0x02000544.
    [CreateAssetMenu(fileName = "EntityActivationDefinition", menuName = "HardlightProject/DefinitionData/Definitions/EntityActivationDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationDefinition : ScriptableObject
    {
        [Tooltip("Entity activation type.")]
        [SerializeField]
        private EntityActivationType m_type;
        [SerializeField]
        [Tooltip("Logic definitions to activate an entity")]
        private List<EntityActivationLogicDefinition> m_activateLogicDefinitions;
        [SerializeField]
        [Tooltip("Logic definitions to deactivate an entity")]
        private List<EntityActivationLogicDefinition> m_deactivateLogicDefinitions;

        // Original 0x06001c9f.
        public EntityActivationType Type => m_type;
        // Original 0x06001ca0/0x06001ca1: managed list checks; lists are not constructed by the original constructor.
        public bool CanActivate => m_activateLogicDefinitions != null && m_activateLogicDefinitions.Count > 0;
        public bool CanDeactivate => m_deactivateLogicDefinitions != null && m_deactivateLogicDefinitions.Count > 0;
        // Original 0x06001ca2.
        public List<EntityActivationLogic> CreateActivationLogic(IEntityActivatable entity) => CreateLogic(entity, m_activateLogicDefinitions);
        // Original 0x06001ca3.
        public List<EntityActivationLogic> CreateDeactivationLogic(IEntityActivatable entity) => CreateLogic(entity, m_deactivateLogicDefinitions);
        // Original 0x06001ca4: allocate the output before enumerating; preserve reflection/initialisation faults and enumerator disposal.
        private static List<EntityActivationLogic> CreateLogic(IEntityActivatable entity, List<EntityActivationLogicDefinition> logicDefinitions)
        {
            List<EntityActivationLogic> logic = new List<EntityActivationLogic>();
            foreach (EntityActivationLogicDefinition definition in logicDefinitions)
                logic.Add(definition.CreateLogic(entity));
            return logic;
        }
        // Original 0x06001ca5.
        public EntityActivationDefinition() { }
    }
}
