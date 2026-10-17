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
	// Original Game.Runtime 020000e7; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateLoadOrUnloadScene : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityScene]
			public string SceneName;

			[GraphEnumPopup(typeof(SceneCommand))]
			public string Command;

			public bool Additive;

			public bool SetActiveOnLoad;

			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 060006ba (1.10.1 RVA 0x64A560)
			public JSONCtorArgs()
			{
			}
		}

		public enum SceneCommand
		{
			Load,
			Unload
		}

		private readonly string m_sceneName;

		private readonly SceneCommand m_command;

		private readonly bool m_additive;

		private readonly bool m_setActive;

		private readonly ApplicationStateEvent m_stateEvent;

		// 060006b5 (1.10.1 RVA 0x64A030)
		private ApplicationStateLoadOrUnloadScene(FiniteStateMachine fsm, FSMIdentifier stateId, string sceneName, SceneCommand command, bool additive, bool setActive, ApplicationStateEvent stateEvent) : base(fsm, stateId)
		{
			m_sceneName = sceneName;
			m_command = command;
			m_additive = additive;
			m_setActive = setActive;
			m_stateEvent = stateEvent;
		}

		// 060006b6 (1.10.1 RVA 0x64A0A0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateLoadOrUnloadScene(fsm, stateId, args.SceneName, (SceneCommand)Enum.Parse(typeof(SceneCommand), args.Command, true), args.Additive, args.SetActiveOnLoad, string.IsNullOrEmpty(args.EventId) ? null : ApplicationStateEvent.FindByName(args.EventId));
		}

		// 060006b7 (1.10.1 RVA 0x64A270)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(LoadScene(user));
		}

		// 060006b8 (1.10.1 RVA 0x64A300); MoveNext 060006bd (RVA 0x64A580)
		private IEnumerator LoadScene(IGraphUser user)
		{
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			if (m_command == SceneCommand.Load)
			{
				AsyncOperationHandle<SceneInstance> loadHandle = Addressables.LoadSceneAsync(m_sceneName, m_additive ? LoadSceneMode.Additive : LoadSceneMode.Single, true, 100);
				loadedLevels[m_sceneName] = loadHandle;
				user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
				yield return loadHandle;
				if (m_setActive)
				{
					SceneManager.SetActiveScene(loadHandle.Result.Scene);
				}
			}
			else if (m_command == SceneCommand.Unload && loadedLevels.TryGetValue(m_sceneName, out AsyncOperationHandle sceneHandle))
			{
				AddressableManager.UnloadSceneAndClearResources(sceneHandle);
				loadedLevels.Remove(m_sceneName);
				user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
			}
			PublishMessage(user);
		}

		// 060006b9 (1.10.1 RVA 0x64A3A0)
		private void PublishMessage(IGraphUser user)
		{
			if (m_stateEvent == null)
			{
				return;
			}
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
