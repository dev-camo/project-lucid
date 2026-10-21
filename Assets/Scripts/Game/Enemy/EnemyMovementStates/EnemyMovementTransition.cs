using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000659; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EnemyMovementTransition : FSMTransition
    {
        // Original Game.Runtime 0200065a (nested). The arguments carry no fields.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class BaseJSONCtorArgs
        {
            // 06002228 (1.10.1 RVA 0x577EE0)
            public BaseJSONCtorArgs() { }
        }

        // 06002227 (1.10.1 RVA 0x577ED0): the authored arguments are not consumed.
        protected EnemyMovementTransition(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId) { }
    }
}
