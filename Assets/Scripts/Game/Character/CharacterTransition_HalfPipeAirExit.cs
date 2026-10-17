using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003dc; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToHalfPipeAirExit")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_HalfPipeAirExit : FSMTransition
	{
		// 0600172f (1.10.1 RVA 0x790CD0)
		private CharacterTransition_HalfPipeAirExit(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001730 (1.10.1 RVA 0x790CE0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HalfPipeAirExit(fsm, transitionId);
		}

		// 06001731 (1.10.1 RVA 0x790D40). A direction override exits before any storage
		// read (0x790DC7); an active collider stays without reading the transfer flag (0x790E96).
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext)) return true;
			Character character = user.GetAs<Character>();
			return character.IsDirectionOverrideActive() ||
				(!character.Storage.GetValue(ActorFSMKeys.HalfPipeColliderActive, false, true) && !character.Storage.GetValue(ActorFSMKeys.HalfPipeTransferActive, false, true));
		}
	}
}
