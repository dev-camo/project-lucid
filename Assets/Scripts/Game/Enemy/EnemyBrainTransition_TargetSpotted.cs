using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000650; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyBrainTransition_TargetSpotted : EnemyBrainTransition
	{
		// 060021f8 (1.10.1 RVA 0x5757E0)
		protected EnemyBrainTransition_TargetSpotted(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021f9 (1.10.1 RVA 0x5757F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_TargetSpotted(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021fa (1.10.1 RVA 0x575880)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.RemoveValue<Actor>(EnemyBrainFSMKeys.FocusTarget);
		}

		// 060021fb (1.10.1 RVA 0x5759F0): a sighting stores the focus target even when the enemy cannot reach it. When
		// the enemy charges at the sighting position, that position is the target pushed on by the overshoot amount,
		// and the sighting only counts if the enemy can reach it.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			EnemyBrain brain = user.GetAs<EnemyBrain>();
			if (brain.Target == null)
				return false;

			brain.Storage.SetValue(EnemyBrainFSMKeys.FocusTarget, brain.Target);
			// The retail code reloads the definition chain at each use instead of holding it.
			if (!brain.Enemy.Definition.Traits.CacheTargetLocationOnSight)
				return true;

			Vector3 toTarget = brain.Target.WorldPosition - brain.Enemy.WorldPosition;
			float distance = toTarget.magnitude;
			Vector3 direction = toTarget / distance;
			Vector3 cachedPosition = brain.Enemy.WorldPosition + direction * (distance + brain.Enemy.Definition.Traits.OvershootAmount);
			if (!brain.CanReachTarget(cachedPosition))
				return false;

			brain.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedPosition, cachedPosition);
			user.Storage.SetValue(EnemyBrainFSMKeys.TargetCachedTime, 0f);
			return true;
		}
	}
}
