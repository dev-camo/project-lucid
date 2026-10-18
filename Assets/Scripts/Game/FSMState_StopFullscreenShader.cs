using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000685; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("FSMState_StopFullscreenShader")]
	[GraphNodeMenuFormat("Generic/{0}")]
	public class FSMState_StopFullscreenShader : FSMState_Trigger
	{
		// 06002332 (1.10.1 RVA 0x584AC0); the trigger base constructor is inlined natively.
		protected FSMState_StopFullscreenShader(FiniteStateMachine fsm, FSMIdentifier stateId, TriggerJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06002333 (1.10.1 RVA 0x584AF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			TriggerJSONCtorArgs args = JsonUtility.FromJson<TriggerJSONCtorArgs>(jsonCtorArgs);
			return new FSMState_StopFullscreenShader(fsm, transitionId, args);
		}

		// 06002334 (1.10.1 RVA 0x584B90); every override stops before the user's
		// stored shader override handle is dropped.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			ProcessManager.GetSystem<FullscreenShaderManager>().StopAllEffectOverrides();
			user.Storage.RemoveValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride);
		}
	}
}
