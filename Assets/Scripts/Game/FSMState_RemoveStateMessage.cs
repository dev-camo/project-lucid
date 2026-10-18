using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000683; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Generic/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class FSMState_RemoveStateMessage : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 06002331 (1.10.1 RVA 0x584AB0)
			public JSONCtorArgs()
			{;}
		}

		private readonly ApplicationStateEvent m_eventId;

		// 0600232e (1.10.1 RVA 0x584810); the base registration precedes the field store.
		public FSMState_RemoveStateMessage(FiniteStateMachine fsm, string stateName, ApplicationStateEvent eventId) : base(fsm, stateName)
		{
			m_eventId = eventId;
		}

		// 0600232f (1.10.1 RVA 0x584860); the event is resolved by name before allocation.
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent eventId = ApplicationStateEvent.FindByName(args.EventId);
			return new FSMState_RemoveStateMessage(fsm, (string)transitionId, eventId);
		}

		// 06002330 (1.10.1 RVA 0x584920); removes from the App's storage, not the
		// entering user's. The Remove result is unused.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			ProcessManager.GetSystem<App>().Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).Remove(m_eventId);
		}
	}
}
