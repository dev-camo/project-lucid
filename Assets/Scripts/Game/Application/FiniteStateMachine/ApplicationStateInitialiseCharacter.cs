using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020000d7; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateInitialiseCharacter : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 0600067d (1.10.1 RVA 0x6473F0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

		private readonly SystemRef<TrackManager> m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>();

		private readonly ApplicationStateEvent m_stateEvent;

		// 06000674 (1.10.1 RVA 0x646D00)
		private ApplicationStateInitialiseCharacter(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 06000675 (1.10.1 RVA 0x646DF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateInitialiseCharacter(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 06000676 (1.10.1 RVA 0x646E90)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			InitialiseLevel(user);
		}

		// 06000677 (1.10.1 RVA 0x646EC0)
		private void InitialiseLevel(IGraphUser app)
		{
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			levelManager.TryGetCurrentLevel(out LevelManagerLevel loadedLevel);
			CameraYAxisProxyManager cameraProxy = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>();
			if (cameraProxy != null)
			{
				cameraProxy.enabled = true;
			}
			CoroutineUtils.RunCoroutine(InitialiseMissions(app, loadedLevel));
		}

		// 06000678 (1.10.1 RVA 0x647040); MoveNext 06000682 (RVA 0x6474F0), WaitUntil predicate 0600067f (RVA 0x647410)
		private IEnumerator InitialiseMissions(IGraphUser app, LevelManagerLevel loadedLevel)
		{
			MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
			app.Storage.GetValue<bool>(AppFSMKeys.IsFastLoading, false, true);
			IMissionContext activeMissionContext = missionManager.ActiveMissionContext;
			if (activeMissionContext != null)
			{
				bool isReload = app.Storage.GetValue<bool>(AppFSMKeys.RestartInProgress, false, true);
				missionManager.PurgeMissionStates(isReload);
				missionManager.LoadMissionsFromLevel(loadedLevel, isReload);
				missionManager.LoadMission(activeMissionContext.MissionDefinition, activeMissionContext.LevelDefinition);
				missionManager.CachePreLevelMissionSaveData(activeMissionContext.LevelDefinition.GetGUID());
			}
			if (ProcessManager.GetSystemRef<GameplayIslandManager>().TryGet(out GameplayIslandManager islandManager))
			{
				islandManager.SanitisePendingGameplayElements();
			}
			foreach ((string _, MissionState missionState) in missionManager.MissionStates)
			{
				MissionDefinition definition = missionState.Definition;
				if (definition.SubLevelDefinition != null)
				{
					continue;
				}
				bool isActiveMission = activeMissionContext != null && activeMissionContext.MissionDefinition == definition;
				// LoadAllMissions is read before combining with isActiveMission (0x647E4B-0x647E6E).
				bool loadAllMissions = loadedLevel.LevelDefinition.LoadAllMissions;
				if (!(isActiveMission | loadAllMissions) && missionState.SetUpComplete)
				{
					continue;
				}
				missionManager.SetUpMissionTracker(missionState);
				yield return new WaitUntil(() => missionState.SetUpComplete);
			}
			yield return ReplaceCharacter(app);
		}

		// 06000679 (1.10.1 RVA 0x647100); MoveNext 06000689 (RVA 0x648320), WaitUntil predicate 0600067c (RVA 0x6473B0)
		private IEnumerator ReplaceCharacter(IGraphUser app)
		{
			CharacterId characterId = app.Storage.GetValue<CharacterId>(AppFSMKeys.CurrentCharacterId, default, true);
			CharacterId missionCharacterId = app.Storage.GetValue<CharacterId>(AppFSMKeys.MissionCharacterId, default, true);
			if (missionCharacterId != CharacterId.None && characterId != missionCharacterId)
			{
				characterId = missionCharacterId;
			}
			yield return new WaitUntil(() => m_trackManagerRef.IsValid());
			CharacterManager characterManager = m_characterManagerRef.Get();
			yield return characterManager.LoadAllCharacters();
			characterManager.RegisterOnCharacterChange(InitialiseCharacter, false);
			characterManager.SetCharacter(characterId, false);
		}

		// 0600067a (1.10.1 RVA 0x6471A0)
		private void InitialiseCharacter(Character character)
		{
			m_characterManagerRef.Get().ReleaseOnCharacterChange(InitialiseCharacter);
			PublishEvent();
		}

		// 0600067b (1.10.1 RVA 0x647240)
		private void PublishEvent()
		{
			ProcessManager.GetSystem<App>().Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
