using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000647; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class EnemyBrainTransition : FSMTransition
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class BaseJSONCtorArgs
		{
			// 060021e0 (1.10.1 RVA 0x574440)
			public BaseJSONCtorArgs()
			{
			}
		}

		// 060021df (1.10.1 RVA 0x574430): ctorArgs is unused; the native body only chains to FSMTransition(fsm, transitionId).
		protected EnemyBrainTransition(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
		}
	}
}
