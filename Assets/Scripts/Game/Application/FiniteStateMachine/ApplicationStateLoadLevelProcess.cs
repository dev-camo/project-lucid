using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000139; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateLoadLevelProcess : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 060007a1 (1.10.1 RVA 0x658A20)
			public JSONCtorArgs()
			{;}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		// 0600079d (1.10.1 RVA 0x658620)
		private ApplicationStateLoadLevelProcess(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 0600079e (1.10.1 RVA 0x658650)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateLoadLevelProcess(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 0600079f (1.10.1 RVA 0x658700)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			App app = user.GetAs<App>();
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
			{
				string lastLevelLoadedGUID = app.GetLastLevelLoadedGUID();
				bool hasTrackManager = levelManager.ManagedSystems.HasSystemOfType<TrackManager>();
				if (lastLevelLoadedGUID != level.SceneDefinition.GetGUID() || !hasTrackManager)
				{
					TrackManager trackManager = level.Data.TrackManager;
					trackManager.SubscribeSystemActions();
					levelManager.ManagedSystems.ReplaceManagedSystem(trackManager);
				}
				levelManager.SetLevelActiveForFSM(level);
				PublishMessage(app);
			}
		}

		// 060007a0 (1.10.1 RVA 0x658880)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
