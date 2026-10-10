using System;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Offline;

namespace ProjectLucid.Editor.Tests
{
    // Owned managed provider instances only. No polling coroutine, Apple SDK,
    // original plugin, fabricated controller or hardware device is started.
    public sealed class PortableControllerSelectionTests
    {
        [Test]
        public void DefaultSelectionUsesOriginalUnityProviderWithIndependentNotifications()
        {
            BaseControllerProvider first = PortableControllerSelection.Create(3f);
            BaseControllerProvider second = PortableControllerSelection.Create(3f);
            Assert.That(first.GetType(), Is.EqualTo(typeof(UnityControllerNameProvider)));
            Assert.That(second.GetType(), Is.EqualTo(typeof(UnityControllerNameProvider)));
            Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(first.OnControllerConnectionUpdate, Is.Not.Null);
            Assert.That(second.OnControllerConnectionUpdate, Is.Not.Null);
            Assert.That(ReferenceEquals(first.OnControllerConnectionUpdate, second.OnControllerConnectionUpdate), Is.False);
            Assert.That(first.OnControllerConnectionUpdate.GetInvocationListCount(), Is.EqualTo(0));
            Assert.That(second.OnControllerConnectionUpdate.GetInvocationListCount(), Is.EqualTo(0));
        }

        [Test]
        public void ConnectionNotificationWorksBeforeSubscriptionAndAfterUnsubscription()
        {
            BaseControllerProvider provider = PortableControllerSelection.Create(3f);
            int calls = 0;
            Action listener = () => ++calls;
            FastAction.Invoke(provider.OnControllerConnectionUpdate);
            Assert.That(calls, Is.EqualTo(0));
            provider.OnControllerConnectionUpdate += listener;
            try
            {
                FastAction.Invoke(provider.OnControllerConnectionUpdate);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(provider.OnControllerConnectionUpdate.GetInvocationListCount(), Is.EqualTo(1));
            }
            finally { provider.OnControllerConnectionUpdate -= listener; }
            Assert.That(provider.OnControllerConnectionUpdate, Is.Not.Null);
            Assert.That(provider.OnControllerConnectionUpdate.GetInvocationListCount(), Is.EqualTo(0));
            FastAction.Invoke(provider.OnControllerConnectionUpdate);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
