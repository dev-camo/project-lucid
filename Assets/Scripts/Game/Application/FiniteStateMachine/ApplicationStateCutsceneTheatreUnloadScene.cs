using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
	// Original Game.Runtime 02000160; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Theatre/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class ApplicationStateCutsceneTheatreUnloadScene : FSMState
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 06000825 (1.10.1 RVA 0x661C30)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		// 06000820 (1.10.1 RVA 0x661570)
		private ApplicationStateCutsceneTheatreUnloadScene(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
		}

		// 06000821 (1.10.1 RVA 0x6615A0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateCutsceneTheatreUnloadScene(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId));
		}

		// 06000822 (1.10.1 RVA 0x661650): the scene stays loaded when the next queued cutscene
		// shares it; only a loaded, non-blank scene is unloaded, anything else just publishes.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CutsceneTheatre theatre = user.Storage.GetValueOnly<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre);
			CutsceneDefinition current = theatre.Cutscene;
			if (current == null)
			{
				PublishMessage(user);
				return;
			}
			CutsceneDefinition next = theatre.Peek();
			if (next != null && !string.IsNullOrWhiteSpace(next.NonGameplaySceneName) && next.NonGameplaySceneName == current.NonGameplaySceneName)
			{
				PublishMessage(user);
				return;
			}
			if (string.IsNullOrWhiteSpace(current.NonGameplaySceneName))
			{
				PublishMessage(user);
				return;
			}
			string sceneName = current.NonGameplaySceneName;
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			if (loadedLevels.TryGetValue(sceneName, out AsyncOperationHandle sceneHandle))
			{
				CoroutineUtils.RunCoroutine(UnloadScene(user, sceneName, loadedLevels, sceneHandle));
			}
			else
			{
				PublishMessage(user);
			}
		}

		// 06000823 (1.10.1 RVA 0x661B40); MoveNext 06000828 (RVA 0x661C50)
		private IEnumerator UnloadScene(IGraphUser user, string sceneName, Dictionary<string, AsyncOperationHandle> loadedLevels, AsyncOperationHandle sceneHandle)
		{
			yield return AddressableManager.UnloadSceneAndClearResources(sceneHandle);
			loadedLevels.Remove(sceneName);
			user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
			PublishMessage(user);
		}

		// 06000824 (1.10.1 RVA 0x6619A0)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
