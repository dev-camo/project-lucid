using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000652; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_WindUpCycleComplete : EnemyBrainTransition
	{
		// 06002203 (1.10.1 RVA 0x576360)
		protected EnemyBrainTransition_WindUpCycleComplete(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06002204 (1.10.1 RVA 0x576370)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_WindUpCycleComplete(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 06002205 (1.10.1 RVA 0x576400)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(EnemyBrainFSMKeys.AttackWindUpComplete, false, true);
		}
	}
}
