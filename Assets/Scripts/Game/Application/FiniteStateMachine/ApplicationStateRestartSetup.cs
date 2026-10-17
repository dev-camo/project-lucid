using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000150; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateRestartSetup : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		private class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string Event;

			// 060007f3 (1.10.1 RVA 0x65F7C0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		// 060007ee (1.10.1 RVA 0x65F2D0)
		private ApplicationStateRestartSetup(FiniteStateMachine fsm, string stateName, ApplicationStateEvent stateEvent) : base(fsm, stateName)
		{
			m_stateEvent = stateEvent;
		}

		// 060007ef (1.10.1 RVA 0x65F320)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent stateEvent = ApplicationStateEvent.FindByName(args.Event);
			return new ApplicationStateRestartSetup(fsm, stateId, stateEvent);
		}

		// 060007f0 (1.10.1 RVA 0x65F3E0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			if (levelManager.TryGetCurrentLevel(out LevelManagerLevel loadedLevel))
			{
				loadedLevel.Data.UnregisterOutroSequence(user.Storage);
				CoroutineUtils.RunCoroutine(DestroyCharacter(user, levelManager, loadedLevel));
			}
		}

		// 060007f1 (1.10.1 RVA 0x65F540); MoveNext 060007f6 (RVA 0x65F7E0)
		private IEnumerator DestroyCharacter(IGraphUser user, LevelManager levelManager, LevelManagerLevel loadedLevel)
		{
			CharacterManager characterManager = ProcessManager.GetSystem<CharacterManager>();
			if (characterManager.TryGetCurrentCharacter(out Character character))
			{
				yield return character.ExitTriggerVolumes();
				characterManager.DestroyCharacters();
			}
			levelManager.OnLevelRestart();
			user.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(loadedLevel.LevelDefinition));
			PublishMessage(user);
		}

		// 060007f2 (1.10.1 RVA 0x65F620)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
