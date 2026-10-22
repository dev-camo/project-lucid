using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Analytics;

namespace HardlightProject
{
	// Original Game.Runtime 02000166; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationTransitionCanTriggerAnalyticsConsentScreen : FSMTransition
	{
		private readonly SystemRef<AnalyticsConsentManager> m_consentManagerRef = ProcessManager.GetSystemRef<AnalyticsConsentManager>();

		// 06000831 (1.10.1 RVA 0x6624A0)
		public ApplicationTransitionCanTriggerAnalyticsConsentScreen(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000832 (1.10.1 RVA 0x662540)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new ApplicationTransitionCanTriggerAnalyticsConsentScreen(fsm, transitionId);
		}

		// 06000833 (1.10.1 RVA 0x662610)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-066: the original asked the consent manager; offline none is registered, so no consent screen is triggered
			{
				return ProjectLucid.Offline.OfflineAnalyticsConsumers.CanShowConsentScreenDuringBoot();
			}
			return m_consentManagerRef.Get().CanShowConsentScreenDuringBoot();
		}
	}
}
