using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000204; bodies translated from the 1.10.1 x86-64 binary.
    // While the death flight ability is enabled, spins the boss at the ability's spin rate and moves it to the flight target.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("DeathFlight")]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_DeathFlight : ActorState_Ability<ActorAbility>
    {
        private readonly AbilityDefinition m_abilityDefinition;

        protected BossState_DeathFlight(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_DeathFlight(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null || !ability.Enabled)
                return;
            BossAbility_DeathFlight deathFlight = ability as BossAbility_DeathFlight;
            MoveToward(boss, deathFlight.CurrentTargetPosition, deathFlight.Definition.SpinRateDegreesPerSecond * context.DeltaTime);
        }

        private void MoveToward(Boss boss, Vector3 destination, Vector3 turnAmount)
        {
            boss.SetWorldRotation(Quaternion.Euler(turnAmount) * boss.WorldRotation);
            boss.SetWorldPosition(destination);
        }
    }
}
