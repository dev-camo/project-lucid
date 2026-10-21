using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200064b; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_FireProjectileTriggered : Transition_Ability
	{
		// 060021e7 (1.10.1 RVA 0x574890)
		protected EnemyBrainTransition_FireProjectileTriggered(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021e8 (1.10.1 RVA 0x5748A0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_FireProjectileTriggered(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021e9 (1.10.1 RVA 0x574930): true while the brain's enemy has an EnemyAbility_FireProjectile of the
		// configured type that is triggered. The brain's stored target position is not touched.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.GetAs<EnemyBrain>().Enemy.TryGetAbility(m_abilityType, out EnemyAbility_FireProjectile ability) && ability.IsTriggered;
		}
	}
}
