using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000405; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ShouldRespawn")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_ShouldRespawn : FSMTransition
	{
		// 0600179a (1.10.1 RVA 0x794520)
		private CharacterTransition_ShouldRespawn(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 0600179b (1.10.1 RVA 0x794530)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_ShouldRespawn(fsm, transitionId);
		}

		// 0600179c (1.10.1 RVA 0x794590). Both flags are always read, so a missing
		// InHardFailureState is stored even when out of bounds (or al, r14b at 0x794751).
		// The native body never calls the base update.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			Character character = user.GetAs<Character>();
			bool outOfBounds = character.Storage.GetValue(ActorFSMKeys.IsOutOfBounds, false, true);
			bool hardFailure = character.Storage.GetValue(ActorFSMKeys.InHardFailureState, false, true);
			return outOfBounds | hardFailure;
		}
	}
}
