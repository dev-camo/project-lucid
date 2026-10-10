using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ActorState_Ability : ActorState_Effect
    {
        private ActorAbilityType m_abilityType { get; }

        protected T GetAbility<T>(Actor actor) where T : ActorAbility => actor.GetAbility<T>(m_abilityType);

        protected ActorState_Ability(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs)
        {
            m_abilityType = ctorArgs.AbilityType;
        }

        protected void AddAbilityInUse(Actor actor)
        {
            IGraphStorage storage = actor.Storage;
            if (!storage.TryGetValue(ActorFSMKeys.AbilitiesInUse,
                out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities))
                abilities = new Dictionary<ActorAbilityType, ActorAbilityInUseData>(HardlightEnumComparers.ActorAbilityTypeComparer);
            abilities[m_abilityType] = new ActorAbilityInUseData { InUse = true, TimeLastUsedSeconds = actor.GetTotalFixedTime() };
            storage.SetValue(ActorFSMKeys.AbilitiesInUse, abilities);
        }

        protected void RemoveAbilityInUse(Actor actor)
        {
            IGraphStorage storage = actor.Storage;
            if (!storage.TryGetValue(ActorFSMKeys.AbilitiesInUse,
                out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)) return;
            // Original keeps the dictionary entry and records the leave timestamp.
            abilities[m_abilityType] = new ActorAbilityInUseData { InUse = false, TimeLastUsedSeconds = actor.GetTotalFixedTime() };
            storage.SetValue(ActorFSMKeys.AbilitiesInUse, abilities);
        }

        public override void DebugInfo(Actor actor, StringBuilder stringInfoBuilder)
        {
            base.DebugInfo(actor, stringInfoBuilder);
            stringInfoBuilder.Append(string.Format("AbilityType = {0}\n", m_abilityType));
        }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        protected class AbilityJSONCtorArgs : BaseJSONCtorArgs
        {
            public ActorAbilityType AbilityType;
            public AbilityJSONCtorArgs() { }
        }
    }
}
