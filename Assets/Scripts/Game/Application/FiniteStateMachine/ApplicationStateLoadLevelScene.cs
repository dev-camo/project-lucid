using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200013b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateLoadLevelScene : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 060007a7 (1.10.1 RVA 0x658E60)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();

		// 060007a2 (1.10.1 RVA 0x658A30)
		protected ApplicationStateLoadLevelScene(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 060007a3 (1.10.1 RVA 0x658AF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateLoadLevelScene(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 060007a4 (1.10.1 RVA 0x658B90)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(LoadLevel(user));
		}

		// 060007a5 (1.10.1 RVA 0x658C20); MoveNext 060007ac (RVA 0x658EC0), WaitUntil predicate 060007a9 (RVA 0x658E80)
		private IEnumerator LoadLevel(IGraphUser user)
		{
			LevelManagerLoadLevelParameters parameters = user.Storage.GetValue<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters, null, true);
			GameplayLevelDefinition sceneDefinition = parameters.LevelDefinition;
			string sceneDefinitionGUID = sceneDefinition.GetGUID();
			MissionManager missionManager = m_missionManagerRef.Get();
			if (missionManager.HasActiveMission() && missionManager.ActiveMissionContext.MissionDefinition.SceneOverride != null)
			{
				sceneDefinition = missionManager.ActiveMissionContext.MissionDefinition.SceneOverride;
				sceneDefinitionGUID = sceneDefinition.GetGUID();
			}
			if (parameters.LevelDefinition == null)
			{
				PublishMessage(user);
				yield break;
			}
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			yield return levelManager.LoadLevelSceneForFSM(parameters.LevelDefinition, sceneDefinition);
			yield return new WaitUntil(() => levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel) && currentLevel.SceneDefinition.GetGUID() == sceneDefinitionGUID);
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
			{
				levelManager.OnLevelLoaded(level);
				PublishMessage(user);
			}
			else
			{
				HLOutput.LogError($"{nameof(ApplicationStateLoadLevelScene)} has waited for level to be loaded but still could not find level {sceneDefinition.DisplayName}.");
			}
		}

		// 060007a6 (1.10.1 RVA 0x658CC0)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
