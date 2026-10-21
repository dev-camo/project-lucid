using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
	// Original Game.Runtime 0200015b; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Theatre/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class ApplicationStateCutsceneTheatreLoadScene : FSMState
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 06000813 (1.10.1 RVA 0x660E50)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		// 0600080e (1.10.1 RVA 0x660850)
		private ApplicationStateCutsceneTheatreLoadScene(FiniteStateMachine fsm, string stateName, ApplicationStateEvent stateEvent) : base(fsm, stateName)
		{
			m_stateEvent = stateEvent;
		}

		// 0600080f (1.10.1 RVA 0x6608A0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent stateEvent = ApplicationStateEvent.FindByName(args.EventId);
			return new ApplicationStateCutsceneTheatreLoadScene(fsm, stateId, stateEvent);
		}

		// 06000810 (1.10.1 RVA 0x660960): the original does not chain to FSMState.DoOnEnter here.
		// A theatre cutscene without a non-gameplay scene loads nothing and publishes nothing.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			CutsceneTheatre theatre = user.Storage.GetValueOnly<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre);
			string sceneName = theatre.Cutscene.NonGameplaySceneName;
			if (string.IsNullOrWhiteSpace(sceneName))
			{
				return;
			}
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			CoroutineUtils.RunCoroutine(LoadScene(user, sceneName, loadedLevels));
		}

		// 06000811 (1.10.1 RVA 0x660BD0); MoveNext 06000816 (RVA 0x660E70)
		private IEnumerator LoadScene(IGraphUser user, string sceneName, Dictionary<string, AsyncOperationHandle> loadedLevels)
		{
			AsyncOperationHandle<SceneInstance> loadHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive, true, 100);
			loadedLevels[sceneName] = loadHandle;
			user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
			yield return loadHandle;
			PublishMessage(user);
		}

		// 06000812 (1.10.1 RVA 0x660CB0)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
