using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	public class EnemyBrainTransition_DestinationReached : EnemyBrainTransition
	{
		// 060021e4 (1.10.1 RVA 0x574670)
		protected EnemyBrainTransition_DestinationReached(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021e5 (1.10.1 RVA 0x574680)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_DestinationReached(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021e6 (1.10.1 RVA 0x574710)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(EnemyBrainFSMKeys.MoveToDestinationReached, false, true);
		}
	}
}
