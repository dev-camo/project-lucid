using Hardlight;

namespace ProjectLucid.Tests.CrossPromoUIShared
{
    // Test-only launch button whose CanShowLaunchButton answer is set by the fixture, the way a
    // game subclass steers the original virtual gate.
    public sealed class CrossPromoLaunchButtonProbe : CrossPromoLaunchButton
    {
        public bool CanShow;

        protected override bool CanShowLaunchButton() => CanShow;
    }
}
