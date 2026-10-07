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

        [Test]
        public void OriginalCloudFactorySelectsOfflineWithoutConstructingAppleProvider()
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
