using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000644; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_TurnTowardsPosition : ActorState
    {
        // Original Game.Runtime 02000645.
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class JSONCtorArgs : BaseJSONCtorArgs
        {
            public string PositionFSMVariableName;
            public bool RemoveWhenComplete;

            // 060021d9 (1.10.1 RVA 0x573D40)
            public JSONCtorArgs() { }
        }

        private readonly GraphStorageKey m_positionFSMVariable;
        private readonly bool m_removeWhenComplete;

        // 060021d5 (1.10.1 RVA 0x5739D0)
        protected EnemyBrainState_TurnTowardsPosition(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_positionFSMVariable = new GraphStorageKey(ctorArgs.PositionFSMVariableName);
            m_removeWhenComplete = ctorArgs.RemoveWhenComplete;
        }

        // 060021d6 (1.10.1 RVA 0x573A20)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_TurnTowardsPosition(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        }

        // 060021d7 (1.10.1 RVA 0x573AF0)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
        }

        // 060021d8 (1.10.1 RVA 0x573B00). Turns on the spot towards the stored position
        // (forward movement ignored); once facing it, the stored position is removed
        // when RemoveWhenComplete is set.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            if (user.Storage.TryGetValue(m_positionFSMVariable, out Vector3 position)
                && EnemyBrainState_Move.SetMovementImpulseForDestination(user.GetAs<EnemyBrain>(), position, true)
                && m_removeWhenComplete)
            {
                user.Storage.RemoveValue<Vector3>(m_positionFSMVariable);
            }
        }
    }
}
