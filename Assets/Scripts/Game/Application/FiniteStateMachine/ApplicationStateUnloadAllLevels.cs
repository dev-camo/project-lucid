using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000141; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateUnloadAllLevels : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 060007bf (1.10.1 RVA 0x65A780)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		// 060007ba (1.10.1 RVA 0x65A3D0)
		private ApplicationStateUnloadAllLevels(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 060007bb (1.10.1 RVA 0x65A400)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateUnloadAllLevels(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 060007bc (1.10.1 RVA 0x65A4B0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(UnloadAllLevels(user));
		}

		// 060007bd (1.10.1 RVA 0x65A540); MoveNext 060007c2 (RVA 0x65A7A0)
		private IEnumerator UnloadAllLevels(IGraphUser user)
		{
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			if (levelManager.CurrentLevelValid)
			{
				yield return levelManager.UnloadLevel();
			}
			PublishMessage(user);
		}

		// 060007be (1.10.1 RVA 0x65A5E0)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
