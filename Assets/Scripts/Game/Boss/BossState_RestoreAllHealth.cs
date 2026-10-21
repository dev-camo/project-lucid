using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200020f; bodies translated from the 1.10.1 x86-64 binary.
    // Restores the boss to full health.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("RestoreAllHealth")]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_RestoreAllHealth : FSMState_Trigger
    {
        protected BossState_RestoreAllHealth(FiniteStateMachine fsm, FSMIdentifier stateId, TriggerJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_RestoreAllHealth(fsm, transitionId, JsonUtility.FromJson<TriggerJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
        {
            user.GetAs<Boss>().RestoreAllBossHealth();
        }
    }
}
