using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000407; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToTransporterAttach")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_TransporterAttach : CharacterTransition_Target
	{
		// 060017a1 (1.10.1 RVA 0x794980)
		protected CharacterTransition_TransporterAttach(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
			
		}

		// 060017a2 (1.10.1 RVA 0x794990)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_TransporterAttach(fsm, transitionId);
		}

		// 060017a3 (1.10.1 RVA 0x7949F0)
		protected override bool IsValidType(HomingTargetType type)
		{
			return type == HomingTargetType.Transporter;
		}

		// 060017a4 (1.10.1 RVA 0x794A00)
		protected override bool IsValidSurface(TrackManager trackManager, ISurface surface)
		{
			return trackManager.TrackableTransporterExists(surface);
		}
	}
}
