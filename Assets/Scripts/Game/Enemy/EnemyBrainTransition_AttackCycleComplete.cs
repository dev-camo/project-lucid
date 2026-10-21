using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000649; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_AttackCycleComplete : EnemyBrainTransition
	{
		// 060021e1 (1.10.1 RVA 0x574450)
		protected EnemyBrainTransition_AttackCycleComplete(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021e2 (1.10.1 RVA 0x574460)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_AttackCycleComplete(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021e3 (1.10.1 RVA 0x5744F0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(EnemyBrainFSMKeys.AttackAnimationCompleted, false, true);
		}
	}
}
