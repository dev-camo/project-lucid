using System;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Owned provider objects only; no polling coroutine or device is started.
    public sealed class OriginalControllerProviderGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // UnityControllerNameProvider..ctor 0x06000066, x86-64 0x1a1bab0, with
        // the inlined BaseControllerProvider..ctor 0x06000055 (0x1a1b3c0):
        // Object..ctor, then WaitForSeconds..ctor 0x1a1bb05 with the incoming
        // xmm0 rate, stored at +0x18. Each selection owns a wait carrying its
        // exact rate; a hardcoded or shared wait fails here.
        [Test]
        public void SelectionPassesEachPollingRateToItsOwnOriginalWait()
        {
            FieldInfo waitField = typeof(BaseControllerProvider).GetField("m_controllerConnectionPollingWait", AnyInstance);
            FieldInfo seconds = typeof(WaitForSeconds).GetField("m_Seconds", AnyInstance);
            Assert.That(waitField, Is.Not.Null);
            Assert.That(seconds, Is.Not.Null);
            float[] rates = { 0.25f, 1.75f, -0f };
            var waits = new WaitForSeconds[rates.Length];
            for (int i = 0; i < rates.Length; i++)
            {
                BaseControllerProvider provider = PortableControllerSelection.Create(rates[i]);
                waits[i] = (WaitForSeconds)waitField.GetValue(provider);
                Assert.That(waits[i], Is.Not.Null);
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes((float)seconds.GetValue(waits[i])), 0),
                    Is.EqualTo(BitConverter.ToInt32(BitConverter.GetBytes(rates[i]), 0)), "exact rate bits " + rates[i]);
            }
            Assert.That(ReferenceEquals(waits[0], waits[1]), Is.False);
        }
    }
}
