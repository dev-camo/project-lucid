using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000136; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	public class ApplicationStateLoadLevelEnsureOneLevel : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			public bool ForceUnloadAny;

			// 06000796 (1.10.1 RVA 0x6582F0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		private readonly bool m_forceUnloadAny;

		// 06000791 (1.10.1 RVA 0x657F20)
		private ApplicationStateLoadLevelEnsureOneLevel(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent, bool forceUnloadAny) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
			m_forceUnloadAny = forceUnloadAny;
		}

		// 06000792 (1.10.1 RVA 0x657F60)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateLoadLevelEnsureOneLevel(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId), args.ForceUnloadAny);
		}

		// 06000793 (1.10.1 RVA 0x658020)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(EnsureOneLevel(user));
		}

		// 06000794 (1.10.1 RVA 0x6580B0); MoveNext 06000799 (RVA 0x658310)
		private IEnumerator EnsureOneLevel(IGraphUser user)
		{
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			LevelManagerLoadLevelParameters parameters = user.Storage.GetValue<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters, null, true);
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel))
			{
				bool differentLevel = parameters == null || !(currentLevel.SceneDefinition == parameters.LevelDefinition);
				if (m_forceUnloadAny || differentLevel)
				{
					yield return levelManager.UnloadLevel();
				}
			}
			PublishMessage(user);
		}

		// 06000795 (1.10.1 RVA 0x658150)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
