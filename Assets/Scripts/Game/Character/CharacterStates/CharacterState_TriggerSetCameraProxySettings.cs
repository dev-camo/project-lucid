using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003ab; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("TriggerSetCameraProxySettings")]
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	public class CharacterState_TriggerSetCameraProxySettings : FSMState_Trigger
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphUnityObject(typeof(CameraProxyTargetSettingsData))]
			public string TargetSettingsData;

			// 0600165e (1.10.1 RVA 0x785340)
			public JSONCtorArgs()
			{
			}
		}

		private readonly CameraProxyTargetSettingsData m_targetSettingsData;

		// 0600165b (1.10.1 RVA 0x7851B0)
		protected CharacterState_TriggerSetCameraProxySettings(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_targetSettingsData = CameraProxyTargetSettingsData.FindByName(ctorArgs.TargetSettingsData);
		}

		// 0600165c (1.10.1 RVA 0x7851F0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerSetCameraProxySettings(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600165d (1.10.1 RVA 0x7852A0)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			CameraUtilities.SetCharacterCameraProxyTargetSettings(user.GetAs<Character>(), m_targetSettingsData.TargetSettings);
		}
	}
}
