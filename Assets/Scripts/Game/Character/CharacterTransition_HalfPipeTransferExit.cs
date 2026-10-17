using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003de; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("HalfPipeTransferExit")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_HalfPipeTransferExit : FSMTransition
	{
		// 06001735 (1.10.1 RVA 0x791170)
		private CharacterTransition_HalfPipeTransferExit(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06001736 (1.10.1 RVA 0x791180)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_HalfPipeTransferExit(fsm, transitionId);
		}

		// 06001737 (1.10.1 RVA 0x7911E0). Track contact (0x791267) and then a direction
		// override (0x791279) each exit before the transfer flag is read.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext)) return true;
			Character character = user.GetAs<Character>();
			return character.Collider.HasTrackContact() || character.IsDirectionOverrideActive() ||
				!character.Storage.GetValue(ActorFSMKeys.HalfPipeTransferActive, false, true);
		}
	}
}
