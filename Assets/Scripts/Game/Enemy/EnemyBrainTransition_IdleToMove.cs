using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064c; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_IdleToMove : EnemyBrainTransition
	{
		// 060021ea (1.10.1 RVA 0x5749C0)
		protected EnemyBrainTransition_IdleToMove(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021eb (1.10.1 RVA 0x5749D0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_IdleToMove(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021ec (1.10.1 RVA 0x574A60)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			EnemyBrain brain = user.GetAs<EnemyBrain>();
			return brain.Storage.GetValue(EnemyBrainFSMKeys.IdleElapsedTime, 0f, true) >= brain.GetTraits().IdleSeconds;
		}
	}
}
