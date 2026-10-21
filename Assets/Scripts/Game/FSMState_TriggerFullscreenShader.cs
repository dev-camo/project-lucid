using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200068f; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Generic/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("FSMState_TriggerFullscreenShader")]
	public class FSMState_TriggerFullscreenShader : FSMState_Trigger
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphEnumPopup(typeof(FullscreenShaderParametersType))]
			public string ShaderType;

			public float ShaderDuration;

			// 0600234b (1.10.1 RVA 0x5860D0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly FullscreenShaderParametersType m_shaderType;

		private readonly float m_shaderDuration;

		// 06002348 (1.10.1 RVA 0x585C00)
		protected FSMState_TriggerFullscreenShader(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_shaderType = EnumUtilities.Parse<FullscreenShaderParametersType>(ctorArgs.ShaderType);
			m_shaderDuration = ctorArgs.ShaderDuration;
		}

		// 06002349 (1.10.1 RVA 0x585C90)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMState_TriggerFullscreenShader(fsm, transitionId, args);
		}

		// 0600234a (1.10.1 RVA 0x585D70): starts the shader override unless the user already holds one.
		// The stored handle is dropped again when the effect finishes.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			FullscreenShaderManager fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
			if (!user.Storage.HasValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride))
			{
				fullscreenShaderManager.StartEffectOverride(m_shaderType, out FullscreenShaderManager.ParametersHandle parametersHandle, m_shaderDuration, true, false, () => user.Storage.RemoveValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride));
				user.Storage.SetValue(FSMStorageKeys.ShaderOverride, parametersHandle);
			}
		}
	}
}
