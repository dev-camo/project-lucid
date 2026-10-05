using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransitionLogicOp : FSMTransitionWithChildren
    {
        public enum LogicOperator
        {
            LogicAND = 0,
            LogicOR = 1,
            LogicXOR = 2,
            LogicNOT = 3
        }

        [Serializable]
        [GraphNodeDefaultName("LogicOp")]
        private class JSONCtorArgs
        {
            [GraphEnumPopup(typeof(LogicOperator))]
            public string LogicOp;
            [GraphNodeFocus("Hardlight", "FSMTransitionGraphNode", null, false)]
            [GraphNodePopup("Hardlight", "FSMTransitionGraphNode", null, false, false)]
            [GraphDisplayName("Transitions")]
            public List<string> ChildTransitionNames;
        }

        public readonly LogicOperator LogicOp;

        // HLUnityCore.Runtime.dll:0x06000495; arm64 0x1ace690.
        // The operator is assigned after the base constructor registers the node.
        public FSMTransitionLogicOp(FiniteStateMachine fsm, FSMIdentifier transitionId,
            LogicOperator logicOp, IFSMTransition transitionA = null,
            IFSMTransition transitionB = null)
            : base(fsm, transitionId, transitionA, transitionB)
        {
            LogicOp = logicOp;
        }

        // 0x06000496; arm64 0x1ace914. Missing children return null before
        // enum parsing or node registration. Enum.Parse is case insensitive,
        // accepts numeric values, and propagates malformed-value exceptions.
        public new static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            var children = new List<IFSMTransition>();
            for (int i = 0; i < args.ChildTransitionNames.Count; ++i)
            {
                if (!fsm.TransitionExists(args.ChildTransitionNames[i], out IFSMTransition child)) return null;
                children.Add(child);
            }
            LogicOperator operation = (LogicOperator)Enum.Parse(typeof(LogicOperator), args.LogicOp, true);
            var transition = new FSMTransitionLogicOp(fsm, transitionId, operation);
            for (int i = 0; i < children.Count; ++i) transition.AddChildTransition(children[i]);
            return transition;
        }

        // 0x06000497; arm64 0x1acee18. Capture the initial list count but
        // read the current property again for each child. Operator precedes names.
        public override string SerialiseRuntimeToJSON()
        {
            var args = new JSONCtorArgs
            {
                LogicOp = LogicOp.ToString(),
                ChildTransitionNames = new List<string>()
            };
            int childCount = ChildTransitions.Count;
            for (int i = 0; i < childCount; ++i)
                args.ChildTransitionNames.Add(LookupNameUsingId(ChildTransitions[i].TransitionId));
            return JsonUtility.ToJson(args);
        }

        // 0x06000498; arm64 0x1acf0e8: every initial child is evaluated.
        // AND compares successful updates with the current count after callbacks;
        // XOR means exactly one success and NOT means zero successes.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            int childCount = ChildTransitions.Count;
            int successes = 0;
            for (int i = 0; i < childCount; ++i)
                if (ChildTransitions[i].Update(user, context)) ++successes;
            switch (LogicOp)
            {
                case LogicOperator.LogicAND: return successes == ChildTransitions.Count;
                case LogicOperator.LogicOR: return successes > 0;
                case LogicOperator.LogicXOR: return successes == 1;
                case LogicOperator.LogicNOT: return successes == 0;
                default: return false;
            }
        }
    }
}
