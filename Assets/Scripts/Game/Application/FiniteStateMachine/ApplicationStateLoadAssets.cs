using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace HardlightProject
{
	// Original Game.Runtime 020000de; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateLoadAssets : FSMState
	{
		// 06000693 (1.10.1 RVA 0x648B50)
		public ApplicationStateLoadAssets(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{;}

		// 06000694 (1.10.1 RVA 0x648B80)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ApplicationStateLoadAssets(fsm, transitionId);
		}

		// 06000695 (1.10.1 RVA 0x648C00)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
			Social.localUser.Authenticate(loginSuccess => ProcessManager.GetSystemRef<GameCenter>().InvokeOnValid(gameCenter => gameCenter.OnLoginComplete(loginSuccess)));
#else
			// Offline adaptation: no platform authentication (GameKit on macOS); the same
			// cached callback reports a failed login, as GameCenter.NativeLoginRequest does.
			new Action<bool>(loginSuccess => ProcessManager.GetSystemRef<GameCenter>().InvokeOnValid(gameCenter => gameCenter.OnLoginComplete(loginSuccess)))(false);
#endif
			user.GetAs<App>().DataManager.LoadDefinitionData();
			CoroutineUtils.RunCoroutine(InitialiseSaveData(ProcessManager.GetSystem<SaveManager>(), user));
		}

		// 06000696 (1.10.1 RVA 0x648E60); MoveNext 0600069e (RVA 0x6490D0)
		private static IEnumerator InitialiseSaveData(SaveManager saveManager, IGraphUser user)
		{
			yield return saveManager.InitialiseSaveData();
			if (saveManager.NewAppVersionRequired)
			{
				yield break;
			}
			yield return saveManager.OpenSave();
			user.Storage.SetValue(AppFSMKeys.GameSaveLoaded, true);
		}
	}
}
