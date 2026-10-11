using Hardlight;
using NUnit.Framework;

#if PROJECT_LUCID_ORIGINAL_GAME_CENTER_ACCESS_POINT
#error Offline access-point fixtures require the default local plugin boundary.
#endif

namespace ProjectLucid.Tests
{
    [TestFixture]
    public sealed class OfflineAccessPointTests
    {
        [Test]
        public void DefaultFacadeReportsUnavailableHiddenUnfocusedAndNotPresenting()
        {
            AssertUnavailable();
            AssertGeometryUnavailable();
        }

        [Test]
        public void ShowHideFocusAndHighlightsCannotAcquirePlatformState()
        {
            GameCenterAccessPoint.ShowGameCenter();
            GameCenterAccessPoint.ShowAchievements();
            foreach (GameCenterAccessPoint.AccessPointLocation location in new[]
            {
                GameCenterAccessPoint.AccessPointLocation.TopLeading,
                GameCenterAccessPoint.AccessPointLocation.TopTrailing,
                GameCenterAccessPoint.AccessPointLocation.BottomLeading,
                GameCenterAccessPoint.AccessPointLocation.BottomTrailing
            })
            {
                GameCenterAccessPoint.ShowAccessPoint(location, true);
                GameCenterAccessPoint.SetAccessPointFocused(true);
                GameCenterAccessPoint.SetShowHighlights(true);
                AssertUnavailable();
                GameCenterAccessPoint.HideAccessPoint();
                GameCenterAccessPoint.ShowAccessPoint(location, false);
                GameCenterAccessPoint.SetAccessPointFocused(false);
                GameCenterAccessPoint.SetShowHighlights(false);
                AssertUnavailable();
            }
        }

        [Test]
        public void UnavailableGeometryRemainsStableAfterRepeatedPresentationRequests()
        {
            AssertGeometryUnavailable();
            for (int i = 0; i < 3; i++)
            {
                GameCenterAccessPoint.ShowAccessPoint(GameCenterAccessPoint.AccessPointLocation.TopLeading, true);
                GameCenterAccessPoint.SetAccessPointFocused(true);
                AssertGeometryUnavailable();
                GameCenterAccessPoint.HideAccessPoint();
                GameCenterAccessPoint.SetAccessPointFocused(false);
                AssertGeometryUnavailable();
            }
        }

        private static void AssertUnavailable()
        {
            Assert.That(GameCenterAccessPoint.IsAccessPointAvailable(), Is.False);
            Assert.That(GameCenterAccessPoint.IsAccessPointShown(), Is.False);
            Assert.That(GameCenterAccessPoint.IsAccessPointFocused(), Is.False);
            Assert.That(GameCenterAccessPoint.IsPresentingGameCenter(), Is.False);
        }

        private static void AssertGeometryUnavailable()
        {
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginX(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginScreenCoordinateX(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointOriginY(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointScreenCoordinateY(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeHeight(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateHeight(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeWidth(), Is.EqualTo(-1f));
            Assert.That(GameCenterAccessPoint.GetAccessPointSizeScreenCoordinateWidth(), Is.EqualTo(-1f));
        }
    }
}
