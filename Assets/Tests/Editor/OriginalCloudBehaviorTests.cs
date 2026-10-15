using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalCloudBehaviorTests
    {
        [Test]
        public void OriginalEditorCloudRetainsConstantGettersAndDiscardedWrites()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunEditorConstants(), Is.EqualTo(16));
        }

        [Test]
        public void OriginalEditorCloudRetainsLiveKeysAndStaleCaptures()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunLiveKeysAndCaptures(), Is.EqualTo(11));
        }

        [Test]
        public void OriginalCloudNotificationsUseRealUserInfoJsonAndReceiverOrder()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunOriginalNotifications(), Is.EqualTo(11));
        }

        [Test]
        public void OriginalCloudConnectCallbacksRetainDelegateCombineRemoveOrder()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunConnectDelegateOrder(), Is.EqualTo(7));
        }

        // Port policy, not original behavior: the shipped factory constructs
        // Cloud_MacOS; the offline build selects LocalCloud at that boundary instead.
        [Test]
        public void CloudFactorySelectsOfflinePortWithoutConstructingAppleProvider()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunDefaultOfflineFactory(), Is.EqualTo(6));
        }

        [Test]
        public void OriginalMacCooldownRetainsRealSuppliedWaitIterator()
        {
            Assert.That(ProjectLucid.Editor.OriginalCloudVerification.RunMacCooldownAndWaitIterator(), Is.EqualTo(19));
        }
    }
}
