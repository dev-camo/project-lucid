using Hardlight;
using Hardlight.Analytics;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000878; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerAnalyticsConsent : UIContainerGameplayControl
    {
        public readonly Bindable<AnalyticsConsentState> ConsentState = new Bindable<AnalyticsConsentState>();
        private AnalyticsConsentManager m_analyticsConsentManager;

        // 0x060030fc (0x6198B0): the page starts on the consent state the manager already holds.
        protected override void Start()
        {
            base.Start();
            m_analyticsConsentManager = ProcessManager.GetSystem<AnalyticsConsentManager>(null, true);
            ConsentState.Value = m_analyticsConsentManager.ConsentState;
        }

        // 0x060030fd (0x619950)
        public void Action_OnToggleConsent(bool value)
        {
            ConsentState.Value = value ? AnalyticsConsentState.Consented : AnalyticsConsentState.Rejected;
        }

        // 0x060030fe (0x619980): an underage player is never asked; an unanswered toggle counts as consent.
        public void Action_Close()
        {
            if (m_analyticsConsentManager.IsPlayerUnderage())
                return;

            switch (ConsentState.Value)
            {
                case AnalyticsConsentState.Rejected:
                    m_analyticsConsentManager.Declined();
                    break;
                case AnalyticsConsentState.None:
                case AnalyticsConsentState.Consented:
                    m_analyticsConsentManager.Accepted();
                    break;
            }
        }

        // 0x060030ff (0x6199F0)
        public UIContainerAnalyticsConsent()
        {
        }
    }
}
