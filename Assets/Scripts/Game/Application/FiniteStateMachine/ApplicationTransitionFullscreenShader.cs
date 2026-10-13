using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000172; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	public class ApplicationTransitionFullscreenShader : FSMTransition
	{
		// 0600084e (1.10.1 RVA 0x663740)
		public ApplicationTransitionFullscreenShader(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 0600084f (1.10.1 RVA 0x663750)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ApplicationTransitionFullscreenShader(fsm, transitionId);
		}

		// 06000850 (1.10.1 RVA 0x6637B0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return !user.Storage.HasValue(FSMStorageKeys.ShaderOverride);
		}
	}
}
