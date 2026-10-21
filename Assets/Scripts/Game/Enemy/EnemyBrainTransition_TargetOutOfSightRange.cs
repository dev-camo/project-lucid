using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	public class EnemyBrainTransition_TargetOutOfSightRange : EnemyBrainTransition
	{
		// 060021f1 (1.10.1 RVA 0x575250)
		protected EnemyBrainTransition_TargetOutOfSightRange(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021f2 (1.10.1 RVA 0x575260)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_TargetOutOfSightRange(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021f3 (1.10.1 RVA 0x5752F0): the retail body reads the focus target without storing a default and
		// discards the null comparison.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			_ = user.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false) == null;
		}

		// 060021f4 (1.10.1 RVA 0x575490)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			EnemyBrain brain = user.GetAs<EnemyBrain>();
			return !brain.CanSeeTarget(brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, true));
		}
	}
}
