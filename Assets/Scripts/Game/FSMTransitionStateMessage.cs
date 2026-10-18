using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200069e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Generic/{0}")]
	public class FSMTransitionStateMessage : FSMTransition
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 06002373 (1.10.1 RVA 0x587B10)
			public JSONCtorArgs()
			{;}
		}

		private readonly ApplicationStateEvent m_eventId;

		// 06002370 (1.10.1 RVA 0x5878A0); the base registration precedes the field store.
		private FSMTransitionStateMessage(FiniteStateMachine fsm, FSMIdentifier transitionId, ApplicationStateEvent eventId) : base(fsm, transitionId)
		{
			m_eventId = eventId;
		}

		// 06002371 (1.10.1 RVA 0x5878D0); the event is resolved by name before allocation.
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent eventId = ApplicationStateEvent.FindByName(args.EventId);
			return new FSMTransitionStateMessage(fsm, transitionId, eventId);
		}

		// 06002372 (1.10.1 RVA 0x587980). The base result is discarded; the transition
		// fires when the event is removed from the App's storage, consuming it once.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			base.DoUpdate(user, context);
			return ProcessManager.GetSystem<App>().Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).Remove(m_eventId);
		}
	}
}
