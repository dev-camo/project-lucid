using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000240; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("DestinationIsScuttleDistance")]
	[GraphNodeMenuFormat("Boss/Crab/{0}")]
	public class BossTransition_DestinationIsScuttleDistance : Transition_Ability
	{
		// 06000c67 (1.10.1 RVA 0x70F4E0)
		protected BossTransition_DestinationIsScuttleDistance(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06000c68 (1.10.1 RVA 0x70F4F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_DestinationIsScuttleDistance(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06000c69 (1.10.1 RVA 0x70F580): the base update is not consulted. The stored desired movement position (infinitely
		// far when none is stored, and nothing is stored for it) must lie strictly between the scuttle's minimum and maximum
		// squared ranges from the boss. A missing ability, or one that is not a scuttle, is false.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			Boss boss = user.GetAs<Boss>();
			boss.TryGetAbility<ActorAbility>(m_abilityType, out ActorAbility ability);
			if (!(ability is BossAbility_Scuttle scuttle))
			{
				return false;
			}
			Vector3 desiredPosition = boss.Storage.GetValue(BossFSMKeys.DesiredMovementPosition, Vector3.positiveInfinity, false);
			float squaredDistance = (boss.WorldPosition - desiredPosition).sqrMagnitude;
			return scuttle.MaxRangeSquared > squaredDistance && squaredDistance > scuttle.MinRangeSquared;
		}
	}
}
