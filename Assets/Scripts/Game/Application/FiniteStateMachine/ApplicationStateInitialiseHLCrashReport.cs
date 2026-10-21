using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020000dc; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	public class ApplicationStateInitialiseHLCrashReport : FSMState
	{
		// 0600068d (1.10.1 RVA 0x648820)
		public ApplicationStateInitialiseHLCrashReport(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{
		}

		// 0600068e (1.10.1 RVA 0x648850)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ApplicationStateInitialiseHLCrashReport(fsm, (string)transitionId);
		}

		// 0600068f (1.10.1 RVA 0x6488D0). The crash report system is resolved before the
		// settings are read; it is configured and started only when crash reports are
		// enabled in the core game configuration.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			HLCrashReport crashReport = ProcessManager.GetSystem<HLCrashReport>();
			HLCrashReportSettings settings = new HLCrashReportSettings();
			if (settings.GetServiceEnabled())
			{
				crashReport.Configure(settings);
				HLCrashReport.StartService();
			}
		}
	}
}
