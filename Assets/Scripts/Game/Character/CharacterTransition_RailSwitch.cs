using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003fb; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("ToRailSwitch")]
	public class CharacterTransition_RailSwitch : FSMTransition
	{
		// 06001780 (1.10.1 RVA 0x7936B0)
		protected CharacterTransition_RailSwitch(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001781 (1.10.1 RVA 0x7936C0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_RailSwitch(fsm, transitionId);
		}

		// 06001782 (1.10.1 RVA 0x793720)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			CharacterAbility_RailSwitch ability = user.GetAs<Character>().GetAbility<CharacterAbility_RailSwitch>(ActorAbilityType.Character_RailSwitch);
			return ability != null && ability.SwitchInProgress();
		}
	}
}
