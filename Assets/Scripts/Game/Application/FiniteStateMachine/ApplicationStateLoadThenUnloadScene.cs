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
	// Original Game.Runtime 020000ee; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateLoadThenUnloadScene : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class JSONCtorArgs
		{
			[GraphUnityScene]
			public string SceneName;

			// 060006ce (1.10.1 RVA 0x64BE60)
			public JSONCtorArgs()
			{
			}
		}

		private readonly string m_sceneName;

		// 060006c9 (1.10.1 RVA 0x64B8D0)
		private ApplicationStateLoadThenUnloadScene(FiniteStateMachine fsm, string stateName, string sceneName) : base(fsm, stateName)
		{
			m_sceneName = sceneName;
		}

		// 060006ca (1.10.1 RVA 0x64B920)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateLoadThenUnloadScene(fsm, stateId, args.SceneName);
		}

		// 060006cb (1.10.1 RVA 0x64B9E0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			CoroutineUtils.RunCoroutine(LoadAsyncScene(user));
		}

		// 060006cc (1.10.1 RVA 0x64BAF0)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			if (loadedLevels.TryGetValue(m_sceneName, out AsyncOperationHandle sceneHandle))
			{
				AddressableManager.UnloadSceneAndClearResources(sceneHandle);
				loadedLevels.Remove(m_sceneName);
				user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
				Resources.UnloadUnusedAssets();
			}
		}

		// 060006cd (1.10.1 RVA 0x64BA70); MoveNext 060006d1 (RVA 0x64BE80)
		private IEnumerator LoadAsyncScene(IGraphUser user)
		{
			yield return Resources.UnloadUnusedAssets();
			AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(m_sceneName, LoadSceneMode.Additive, true, 100);
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			loadedLevels[m_sceneName] = handle;
			user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
			yield return handle;
		}
	}
}
