using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200067f; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeColour(1f, 0f, 0f)]
	[GraphNodeMenuFormat("Generic/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("Error")]
	public class FSMState_Error : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class JSONCtorArgs
		{
			public string ErrorMessage;

			// 06002329 (1.10.1 RVA 0x584550)
			public JSONCtorArgs()
			{;}
		}

		private readonly string m_errorMessage;

		// 06002326 (1.10.1 RVA 0x5842E0); the base registration precedes the field store.
		private FSMState_Error(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId)
		{
			m_errorMessage = ctorArgs.ErrorMessage;
		}

		// 06002327 (1.10.1 RVA 0x584310)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMState_Error(fsm, transitionId, args);
		}

		// 06002328 (1.10.1 RVA 0x5843B0); the state name is the virtual ToString (vtable
		// slot 3), and the log call passes no context object.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			HLOutput.LogError(string.Format("{0} - Error in FSM {1} for user {2}: {3}", ToString(), FSM, user, m_errorMessage), null);
		}
	}
}
