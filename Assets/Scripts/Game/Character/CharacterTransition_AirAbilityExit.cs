using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003c9; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("AirAbilityExit")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_AirAbilityExit : FSMTransition
	{
		// 06001701 (1.10.1 RVA 0x78F3C0)
		protected CharacterTransition_AirAbilityExit(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001702 (1.10.1 RVA 0x78F3D0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_AirAbilityExit(fsm, transitionId);
		}

		// 06001703 (1.10.1 RVA 0x78F430). A missing cached ability exits: the null
		// test at 0x78F561 jumps to the epilogue with the result still 1.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
				return true;
			CharacterAbility_Air ability = user.GetAs<Character>().Storage.GetValue<CharacterAbility_Air>(ActorFSMKeys.AirAbilityActive, null, true);
			return ability == null || !ability.AirActive;
		}
	}
}
