using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000681; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Generic/{0}")]
	public class FSMState_PublishStateMessage : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 0600232d (1.10.1 RVA 0x584800)
			public JSONCtorArgs()
			{;}
		}

		private readonly ApplicationStateEvent m_eventId;

		// 0600232a (1.10.1 RVA 0x584560); the base registration precedes the field store.
		public FSMState_PublishStateMessage(FiniteStateMachine fsm, string stateName, ApplicationStateEvent eventId) : base(fsm, stateName)
		{
			m_eventId = eventId;
		}

		// 0600232b (1.10.1 RVA 0x5845B0); the event is resolved by name before allocation.
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent eventId = ApplicationStateEvent.FindByName(args.EventId);
			return new FSMState_PublishStateMessage(fsm, (string)transitionId, eventId);
		}

		// 0600232c (1.10.1 RVA 0x584670); publishes into the App's storage, not the
		// entering user's.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			ProcessManager.GetSystem<App>().Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_eventId);
		}
	}
}
