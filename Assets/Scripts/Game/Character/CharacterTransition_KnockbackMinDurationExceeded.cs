using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003f0; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("KnockbackMinDurationExceeded")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_KnockbackMinDurationExceeded : FSMTransition
	{
		// 06001762 (1.10.1 RVA 0x792580)
		private CharacterTransition_KnockbackMinDurationExceeded(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001763 (1.10.1 RVA 0x792590)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_KnockbackMinDurationExceeded(fsm, transitionId);
		}

		// 06001764 (1.10.1 RVA 0x7925F0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			Character character = user.GetAs<Character>();
			return character.TryGetAbility(ActorAbilityType.Character_Knockback, out CharacterAbility_Knockback ability) && ability.IsTriggered && ability.MinDurationExceeded();
		}
	}
}
