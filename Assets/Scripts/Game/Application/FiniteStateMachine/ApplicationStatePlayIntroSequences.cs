using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
	// Original Game.Runtime 02000103; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	public class ApplicationStatePlayIntroSequences : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			[GraphUnityObject(typeof(UIContainerIdentifier))]
			public List<string> DoNotCloseContainers;

			public float ControlsDelayTime;

			// 06000707 (1.10.1 RVA 0x650C80)
			public JSONCtorArgs()
			{
			}
		}

		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private sealed class StateCustomIntroSequence
		{
			public IntroSequence IntroSequence { get; }

			public Transform Start { get; }

			public bool DestroySelf { get; }

			public bool WaitForUnityEvents { get; }

			// 0600070c (1.10.1 RVA 0x6503A0)
			public StateCustomIntroSequence(IntroSequence introSequence, Transform start, bool destroySelf, bool waitForUnityEvents)
			{
				IntroSequence = introSequence;
				Start = start;
				DestroySelf = destroySelf;
				WaitForUnityEvents = waitForUnityEvents;
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		private readonly float m_controlsDelayTime;

		private readonly IReadOnlyList<UIContainerIdentifier> m_doNotCloseContainers;

		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();

		private readonly SystemRef<AddressableManager> m_addressablesManagerRef = ProcessManager.GetSystemRef<AddressableManager>();

		private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();

		private readonly SystemRef<GameplayIslandManager> m_islandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();

		// 060006fd (1.10.1 RVA 0x64F440)
		private ApplicationStatePlayIntroSequences(FiniteStateMachine fsm, string stateName, ApplicationStateEvent stateEvent, IReadOnlyList<UIContainerIdentifier> doNotCloseContainers, float controlsDelayTime) : base(fsm, stateName)
		{
			m_stateEvent = stateEvent;
			m_doNotCloseContainers = doNotCloseContainers;
			m_controlsDelayTime = controlsDelayTime;
		}

		// 060006fe (1.10.1 RVA 0x64F5C0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent stateEvent = ApplicationStateEvent.FindByName(args.EventId);
			List<UIContainerIdentifier> doNotCloseContainers = new List<UIContainerIdentifier>(args.DoNotCloseContainers.Count);
			foreach (string container in args.DoNotCloseContainers)
			{
				doNotCloseContainers.Add(UIContainerIdentifier.FindByName(container));
			}
			return new ApplicationStatePlayIntroSequences(fsm, stateId, stateEvent, doNotCloseContainers, args.ControlsDelayTime);
		}

		// 060006ff (1.10.1 RVA 0x64F850); intro callback 0600070e (RVA 0x650CE0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			if (!levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel))
			{
				return;
			}
			levelManager.StartLevel();
			levelManager.SetIntroSequencePlaying(true);
			CoroutineUtils.RunCoroutine(PlayLevelAudio(currentLevel));
			ProcessManager.GetSystem<CollectableManager>().ResetTemporaryCollectables();
			ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out Character currentCharacter);
			App app = user.GetAs<App>();
			InitialiseCharacter(currentCharacter);
			TryGetCustomIntroSequence(user, currentLevel, customIntroSequence => PlayIntroSequence(customIntroSequence, currentCharacter, app, currentLevel));
		}

		// 06000700 (1.10.1 RVA 0x64FB60)
		private void InitialiseCharacter(Character currentCharacter)
		{
			UnityEngine.Random.InitState(0);
			if (m_missionManagerRef.Get().TryGetActiveMissionState(out MissionState missionState, false) && missionState.Definition.ExclusiveToCharacter)
			{
				CharacterId characterId = missionState.Definition.CharacterId;
				if (currentCharacter.Definition.Id != characterId)
				{
					ProcessManager.GetSystem<CharacterManager>().SetCharacter(characterId, true);
				}
			}
			currentCharacter.OnStart(Vector3.zero, false);
			StackableDataHandle inputOverrideHandle = currentCharacter.AddModifierOverride<bool>(unchecked((int)0xc22df46d), false);
			currentCharacter.Storage.SetValue(ActorFSMKeys.InputOverrideModifierHandle, inputOverrideHandle);
		}

		// 06000701 (1.10.1 RVA 0x64FAE0); MoveNext 0600071f (RVA 0x6511F0), WaitUntil predicate 06000710 (RVA 0x650D20)
		private IEnumerator PlayLevelAudio(LevelManagerLevel loadedLevel)
		{
			HLAudioClipIdentifier levelAudioClipIdentifier = HLAudioClipIdentifier.None;
			bool startAtRandomTime = false;
			MissionManager missionManager = m_missionManagerRef.Get();
			if (missionManager.HasActiveMission())
			{
				levelAudioClipIdentifier = missionManager.ActiveMissionContext.MissionDefinition.MusicOverrideIdentifier;
				startAtRandomTime = missionManager.ActiveMissionContext.MissionDefinition.MusicStartAtRandomTime;
			}
			if (levelAudioClipIdentifier == HLAudioClipIdentifier.None)
			{
				levelAudioClipIdentifier = loadedLevel.LevelDefinition.BackgroundAudioClip;
			}
			AudioManager audioManager = ProcessManager.GetSystem<AudioManager>();
			yield return new WaitUntil(() => audioManager.HasSource(HLAudioSourceIdentifier.Background));
			audioManager.PlayClipAtSource(levelAudioClipIdentifier, HLAudioSourceIdentifier.Background, startAtRandomTime);
		}

		// 06000702 (1.10.1 RVA 0x64FDA0); asset callback 06000712 (RVA 0x650D50)
		private void TryGetCustomIntroSequence(IGraphUser user, LevelManagerLevel levelManagerLevel, Action<StateCustomIntroSequence> onComplete)
		{
			// Method-scope locals keep onComplete and both captures in the single DisplayClass14_0.
			IntroSequence customIntroSequence;
			Transform startingPosition;
			MissionManager missionManager = m_missionManagerRef.Get();
			MissionDefinition definition = missionManager.ActiveMissionContext?.MissionDefinition;
			if (definition == null)
			{
				onComplete(null);
				return;
			}
			if (missionManager.MissionStates.TryGetValue(definition.GetGUID(), out MissionState state))
			{
				LevelStartPositionDefinition startDefinition = missionManager.GetActiveMissionStartPosition();
				if (!(startDefinition == null))
				{
					startingPosition = levelManagerLevel.Data.GetStartingPosition(startDefinition);
					customIntroSequence = state.Tracker.CustomIntroSequence;
					if (!(customIntroSequence == null) || state.Tracker.IntroBehaviour != MissionTracker.IntroSequenceBehaviour.UseDefault)
					{
						onComplete(new StateCustomIntroSequence(customIntroSequence, startingPosition, false, false));
						return;
					}
					DataManager dataManager = ProcessManager.GetSystem<DataManager>();
					AsyncOperationHandle<GameObject> handle = m_addressablesManagerRef.Get().LoadAssetAsyncInstance(
						dataManager.IntroSequenceDefinitions[IntroSequenceIdentifier.WalkThroughGate].IntroSequence,
						m_levelManagerRef.Get().ManagedGameObjectParent,
						asset =>
						{
							customIntroSequence = asset.GetComponent<IntroSequence>();
							onComplete(new StateCustomIntroSequence(customIntroSequence, startingPosition, true, true));
						});
					user.Storage.SetValue(AppFSMKeys.IntroSequenceHandle, handle);
					return;
				}
			}
			onComplete(null);
		}

		// 06000703 (1.10.1 RVA 0x650400); completion callback 06000714 (RVA 0x650E30)
		private void PlayIntroSequence(StateCustomIntroSequence customIntroSequence, Character currentCharacter, App app, LevelManagerLevel loadedLevel)
		{
			bool hasCustomIntroSequence = customIntroSequence?.IntroSequence != null;
			if (!hasCustomIntroSequence || customIntroSequence.IntroSequence.CharacterSkipLevelEnterState)
			{
				currentCharacter.Storage.SetValue(ActorFSMKeys.SkipLevelEnter, true);
			}
			if (hasCustomIntroSequence)
			{
				loadedLevel.Data.RegisterIntroSequence(customIntroSequence.IntroSequence);
				CoroutineUtils.RunCoroutine(PlayCustomIntroSequence(customIntroSequence, currentCharacter, app));
				return;
			}
			m_islandManagerRef.Get().ForceActiveIsland(currentCharacter.WorldPosition);
			app.TryReleaseUIScreenTransition();
			loadedLevel.Data.PlayIntroSequence(IntroSequenceIdentifier.IntroRail, null, () => OnIntroSequencesCompleted(app, currentCharacter), false);
		}

		// 06000704 (1.10.1 RVA 0x6506E0); MoveNext 06000719 (RVA 0x650EB0), completion callback 06000716 (RVA 0x650E70)
		private IEnumerator PlayCustomIntroSequence(StateCustomIntroSequence stateIntroSequence, Character currentCharacter, App app)
		{
			if (stateIntroSequence.WaitForUnityEvents)
			{
				yield return null;
			}
			currentCharacter.ParentBlobShadowToRig();
			m_islandManagerRef.Get().ForceActiveIsland(currentCharacter.WorldPosition);
			app.TryReleaseUIScreenTransition();
			if (m_levelManagerRef.Get().TryGetCurrentLevel(out LevelManagerLevel level))
			{
				level.Data.PlayIntroSequence(stateIntroSequence.IntroSequence.Identifier, stateIntroSequence.Start, () => OnIntroSequencesCompleted(app, currentCharacter), stateIntroSequence.DestroySelf);
			}
		}

		// 06000705 (1.10.1 RVA 0x6507C0)
		private void OnIntroSequencesCompleted(App app, Character currentCharacter)
		{
			ProcessManager.GetSystem<UIManager>().CloseAllContainersExcept(m_doNotCloseContainers);
			currentCharacter.OnEnterLevel(m_controlsDelayTime);
			if (app.Storage.HasValue(AppFSMKeys.IntroSequenceHandle))
			{
				AsyncOperationHandle<GameObject> handle = app.Storage.GetValue<AsyncOperationHandle<GameObject>>(AppFSMKeys.IntroSequenceHandle);
				m_addressablesManagerRef.Get().ReleaseHandle(handle);
			}
			m_missionManagerRef.Get().StartMission();
			m_levelManagerRef.Get().SetIntroSequencePlaying(false);
			m_islandManagerRef.Get().ForceActiveIsland(currentCharacter.WorldPosition);
			app.SetHasGameplayControl(true, false);
			PublishMessage(app);
		}

		// 06000706 (1.10.1 RVA 0x650AE0)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
