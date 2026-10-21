using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064f; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyBrainTransition_TargetReached : EnemyBrainTransition
	{
		// 060021f5 (1.10.1 RVA 0x5755C0)
		protected EnemyBrainTransition_TargetReached(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021f6 (1.10.1 RVA 0x5755D0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_TargetReached(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021f7 (1.10.1 RVA 0x575660)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(EnemyBrainFSMKeys.TargetReached, false, true);
		}
	}
}
