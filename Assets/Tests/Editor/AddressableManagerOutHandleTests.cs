using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Editor;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid.Tests
{
    public sealed class AddressableManagerOutHandleTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [TestCase("null")]
        [TestCase("invalid")]
        [TestCase("missing")]
        [TestCase("cached")]
        public void LookupInitializesOutputBeforeFallibleWork(string route)
        {
            GameObject host = null;
            ResourceManager resources = null;
            AsyncOperationHandle<GameObject> seedOwner = default;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                host = new GameObject("Owned addressable output regression");
                host.SetActive(false);
                var manager = host.AddComponent<AddressableManager>();
                resources = new ResourceManager();
                seedOwner = resources.CreateCompletedOperation(host, null);
                Assert.That(seedOwner.IsValid(), Is.True);
                Assert.That(seedOwner.IsDone, Is.True);
                Assert.That(seedOwner.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                Assert.That(seedOwner.Result, Is.SameAs(host));

                // Populate only this genuine component's existing lookup: no public cache-only API exists.
                var lookup = Read<Dictionary<string, AsyncOperationHandle>>(manager, "m_preloadedAssetHandleLookup");
                var retained = Read<HashSet<AsyncOperationHandle>>(manager, "m_preloadHandles");
                Assert.That(lookup, Is.Empty);
                Assert.That(retained, Is.Empty);
                lookup.Add("cached", seedOwner);
                lookup.Add("invalid", default);
                var before = new Dictionary<string, AsyncOperationHandle>(lookup);
                var output = seedOwner;

                // Original 0x060004db @0x7C8580 clears all output bytes before lookup and conversion.
                if (route == "null")
                    Assert.Throws<ArgumentNullException>(() => manager.TryGetPreloadedAsset(null, out output));
                else if (route == "invalid")
                    Assert.Throws<Exception>(() => manager.TryGetPreloadedAsset("invalid", out output));
                else
                    Assert.That(manager.TryGetPreloadedAsset(route, out output), Is.EqualTo(route == "cached"));

                AssertHandleFields(output, route == "cached" ? seedOwner : default);
                Assert.That(Read<Dictionary<string, AsyncOperationHandle>>(manager, "m_preloadedAssetHandleLookup"), Is.SameAs(lookup));
                Assert.That(Read<HashSet<AsyncOperationHandle>>(manager, "m_preloadHandles"), Is.SameAs(retained));
                Assert.That(lookup.Count, Is.EqualTo(before.Count));
                foreach (var pair in before)
                {
                    Assert.That(lookup.ContainsKey(pair.Key), Is.True);
                    AssertHandleFields(lookup[pair.Key], pair.Value);
                }
                Assert.That(retained, Is.Empty);
                Assert.That(host.activeSelf, Is.False);
                Assert.That(seedOwner.IsValid(), Is.True, "resetting output must not release its prior operation");
                Assert.That(seedOwner.Result, Is.SameAs(host));
            },
            () =>
            {
                // The output and lookup are borrowed copies; release only the independent owner.
                if (seedOwner.IsValid())
                {
                    resources.Release(seedOwner);
                    Assert.That(seedOwner.IsValid(), Is.False);
                }
            },
            () => resources?.Dispose(),
            () => { if (host != null) UnityEngine.Object.DestroyImmediate(host); });
        }

        private static T Read<T>(AddressableManager manager, string name) =>
            (T)typeof(AddressableManager).GetField(name, Fields).GetValue(manager);

        private static void AssertHandleFields<THandle>(THandle actual, THandle expected) where THandle : struct
        {
            // Handle equality omits the location-name field; compare the entire struct, including that field.
            foreach (var field in typeof(THandle).GetFields(Fields))
                Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), field.Name);
        }
    }
}
