using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000207 and its arguments class 02000208; bodies translated from the 1.10.1 x86-64 binary.
    // Decides the movement target like its base, and in addition starts the named attack ability as soon as the ability reports
    // that the character can be attacked.
    [GraphNodeMenuFormat("Boss/{0}")]
    [GraphNodeDefaultName("DecideMovementOrAttack")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_DecideMovementOrAttack : BossState_DecideMovement
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class AttackAbilityJSONCtorArgs : RangeJSONCtorArgs
        {
            [GraphUnityObject(typeof(AbilityDefinition))]
            public string AttackAbilityDefinitionName;

            public AttackAbilityJSONCtorArgs()
            {
            }
        }

        private AbilityDefinition m_attackAbilityDefinition;

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_DecideMovementOrAttack(fsm, transitionId, JsonUtility.FromJson<AttackAbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected BossState_DecideMovementOrAttack(FiniteStateMachine fsm, FSMIdentifier stateId, AttackAbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            DataManagerUtilities.TryGetAbilityDefinition(ctorArgs.AttackAbilityDefinitionName, out m_attackAbilityDefinition, "State " + stateId.Name);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            if (boss.TryGetAbilityFromType(m_attackAbilityDefinition.AbilityType, out ActorAbility ability)
                && ability is IOffensiveAbility offensiveAbility
                && CanAttack(boss, offensiveAbility, 0f))
                offensiveAbility.SetAttackFlow(true);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Boss boss = user.GetAs<Boss>();
            if (boss.TryGetAbilityFromType(m_attackAbilityDefinition.AbilityType, out ActorAbility ability)
                && ability is IOffensiveAbility offensiveAbility
                && CanAttack(boss, offensiveAbility, updateContext.DeltaTime))
                offensiveAbility.SetAttackFlow(true);
        }

        // Without a current character the ability cannot attack.
        private bool CanAttack(Boss boss, IOffensiveAbility attackAbility, float deltaTime)
        {
            return ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out Character character)
                && attackAbility.CanAttack(boss, character, m_stateAnimationDefinition, deltaTime);
        }
    }
}
