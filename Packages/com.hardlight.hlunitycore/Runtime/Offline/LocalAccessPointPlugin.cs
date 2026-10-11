using Hardlight;

namespace ProjectLucid.Offline
{
    // Owned offline policy. The original facade and platform implementations
    // are preserved separately; this plugin does not emulate Game Center.
    internal sealed class LocalAccessPointPlugin : GameCenterAccessPointPlugin
    {
        // No platform access point exists in an offline desktop build.
        // Keep every coordinate and extent on the original unavailable sentinel.
        private const float UnavailableGeometry = -1f;

        public override void Show() { }
        public override void ShowAchievements() { }
        public override bool IsAccessPointAvailable() => false;
        public override void ShowAccessPoint(int location, bool showHighlights) { }
        public override bool IsAccessPointShown() => false;
        public override void HideAccessPoint() { }
        public override float GetAccessPointOriginX() => UnavailableGeometry;
        public override float GetAccessPointScreenCoordinateX() => UnavailableGeometry;
        public override float GetAccessPointOriginY() => UnavailableGeometry;
        public override float GetAccessPointScreenCoordinateY() => UnavailableGeometry;
        public override float GetAccessPointSizeHeight() => UnavailableGeometry;
        public override float GetAccessPointSizeScreenCoordinateHeight() => UnavailableGeometry;
        public override float GetAccessPointSizeWidth() => UnavailableGeometry;
        public override float GetAccessPointSizeScreenCoordinateWidth() => UnavailableGeometry;
        public override bool IsPresentingGameCenter() => false;
        public override void SetAccessPointFocused(bool focused) { }
        public override bool IsAccessPointFocused() => false;
        public override void SetShowHighlights(bool showHighlights) { }
    }
}
