using System;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;

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
            // Original 0x06000066 (x86-64 0x1a1bab0, inlining base 0x06000055 at
            // 0x1a1b3c0) stores new WaitForSeconds(rate) at +0x18 with the rate
            // unchanged. Catches the selection dropping or rewriting the configured rate.
            WaitForSeconds firstWait = PollingWait(first), secondWait = PollingWait(second);
            Assert.That(ReferenceEquals(firstWait, secondWait), Is.False);
            Assert.That(Seconds(firstWait), Is.EqualTo(3f));
            Assert.That(Seconds(secondWait), Is.EqualTo(3f));
            Assert.That(Seconds(PollingWait(PortableControllerSelection.Create(0.25f))), Is.EqualTo(0.25f));
        }

        // Read-only observation of normally constructed objects.
        private static WaitForSeconds PollingWait(BaseControllerProvider provider)
        {
            FieldInfo field = typeof(BaseControllerProvider).GetField("m_controllerConnectionPollingWait",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(WaitForSeconds)));
            return (WaitForSeconds)field.GetValue(provider);
        }

        private static float Seconds(WaitForSeconds wait)
        {
            Assert.That(wait, Is.Not.Null);
            FieldInfo field = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(wait);
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
