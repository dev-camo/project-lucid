using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original Game.Runtime 02000012; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Core/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class FSMTransitionEditorOnly : FSMTransition
	{
		// 0600002a (1.10.1 RVA 0x507AC0)
		public FSMTransitionEditorOnly(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 0600002b (1.10.1 RVA 0x507AD0); the JSON arguments are unused.
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new FSMTransitionEditorOnly(fsm, transitionId);
		}

		// 0600002c (1.10.1 RVA 0x507B30); the player build never fires it.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return false;
		}
	}
}
