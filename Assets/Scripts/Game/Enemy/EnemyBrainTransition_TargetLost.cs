using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_TargetLost : EnemyBrainTransition
	{
		// 060021ed (1.10.1 RVA 0x574BA0)
		protected EnemyBrainTransition_TargetLost(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021ee (1.10.1 RVA 0x574BB0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_TargetLost(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021ef (1.10.1 RVA 0x574C40): the retail body reads the focus target without storing a default and
		// discards the null comparison.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			_ = user.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, false) == null;
		}

		// 060021f0 (1.10.1 RVA 0x574DE0): lost when there is no focus target, or when the position it is chased to
		// (the live target, or the cached sighting until its timeout) can no longer be reached.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			EnemyBrain brain = user.GetAs<EnemyBrain>();
			Actor focusTarget = brain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget, null, true);
			if (focusTarget == null)
				return true;

			EnemyTraits traits = brain.Enemy.Definition.Traits;
			if (!traits.CacheTargetLocationOnSight)
				return !brain.CanReachTarget(focusTarget.WorldPosition);

			if (!user.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedPosition))
				return true;

			float cachedSeconds = user.Storage.AdjustValue(EnemyBrainFSMKeys.TargetCachedTime, context.DeltaTime, 0f);
			if (cachedSeconds > traits.TimeoutForReachingCachedLocation)
				return true;

			return !brain.CanReachTarget(user.Storage.GetValue(EnemyBrainFSMKeys.TargetCachedPosition, Vector3.zero, true));
		}
	}
}
