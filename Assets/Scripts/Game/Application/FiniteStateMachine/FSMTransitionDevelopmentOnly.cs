using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original Game.Runtime 02000011; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Core/{0}")]
	public class FSMTransitionDevelopmentOnly : FSMTransition
	{
		// 06000027 (1.10.1 RVA 0x507A40)
		public FSMTransitionDevelopmentOnly(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000028 (1.10.1 RVA 0x507A50); the JSON arguments are unused.
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new FSMTransitionDevelopmentOnly(fsm, transitionId);
		}

		// 06000029 (1.10.1 RVA 0x507AB0); this release build never fires it.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return false;
		}
	}
}
