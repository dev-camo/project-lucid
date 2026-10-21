using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000244; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("HasCompletedActiveLoop")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossTransition_HasCompletedActiveLoop : FSMTransition
	{
		// 06000c74 (1.10.1 RVA 0x70FED0)
		protected BossTransition_HasCompletedActiveLoop(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c75 (1.10.1 RVA 0x70FEE0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_HasCompletedActiveLoop(fsm, transitionId);
		}

		// 06000c76 (1.10.1 RVA 0x70FF40): the base update is not consulted. Once every boss has been active this loop the
		// active bosses are cleared (each marked as active this loop) in the same update that reports the completion.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
			bool completed = fourmen.HasCompletedActiveLoop();
			if (completed)
			{
				fourmen.ClearActiveBosses();
			}
			return completed;
		}
	}
}
