using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
	// Original Game.Runtime 0200016c; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationTransitionCutsceneTheatreShouldLoadScene : FSMTransition
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public bool TrueCausesTransition;

			// 06000842 (1.10.1 RVA 0x6630E0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_trueCausesTransition;

		// 0600083f (1.10.1 RVA 0x662D10)
		private ApplicationTransitionCutsceneTheatreShouldLoadScene(FiniteStateMachine fsm, FSMIdentifier transitionId, bool trueCausesTransition) : base(fsm, transitionId)
		{
			m_trueCausesTransition = trueCausesTransition;
		}

		// 06000840 (1.10.1 RVA 0x662D30)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationTransitionCutsceneTheatreShouldLoadScene(fsm, transitionId, args.TrueCausesTransition);
		}

		// 06000841 (1.10.1 RVA 0x662DD0): the scene should load when the theatre's cutscene names a
		// non-blank scene that is not already loaded; the result is compared with TrueCausesTransition.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			CutsceneTheatre theatre = user.Storage.GetValue<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre, null, true);
			Dictionary<string, AsyncOperationHandle> loadedLevels = user.Storage.GetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null, true);
			string sceneName = theatre.Cutscene.NonGameplaySceneName;
			bool shouldLoad = false;
			if (!string.IsNullOrWhiteSpace(sceneName))
			{
				shouldLoad = !loadedLevels.ContainsKey(sceneName);
			}
			return shouldLoad == m_trueCausesTransition;
		}
	}
}
