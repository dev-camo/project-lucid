using System.Collections.Generic;
using HardlightProject;

namespace ProjectLucid.Tests.CrossPromoUIShared
{
    // Test-only game launch button that records the ad index each virtual full screen request carries, so a case
    // can see what Action_LaunchCrossPromo asks for without opening the cross promotion container.
    public sealed class CrossPromoLaunchButtonSdtProbe : CrossPromoLaunchButtonSDT
    {
        public readonly List<int> Requested = new List<int>();

        protected override void RequestFullScreenView(int adIndex) => Requested.Add(adIndex);
    }
}
