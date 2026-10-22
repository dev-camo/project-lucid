using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020003f9; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("ToRailAttach")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_RailAttach : CharacterTransition_Target
	{
		// 06001779 (1.10.1 RVA 0x793370)
		protected CharacterTransition_RailAttach(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
			
		}

		// 0600177a (1.10.1 RVA 0x793390)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_RailAttach(fsm, transitionId);
		}

		// 0600177b (1.10.1 RVA 0x7933F0)
		protected override bool IsValidType(HomingTargetType type)
		{
			return type == HomingTargetType.Rail;
		}

		// 0600177c (1.10.1 RVA 0x793400)
		protected override bool IsValidSurface(TrackManager trackManager, ISurface surface)
		{
			return trackManager.TrackableRailExists(surface);
		}
	}
}
