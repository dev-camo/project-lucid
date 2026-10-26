using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class HLHapticsRegistrySlotTests
    {
        private static List<DictionaryEntry> Entries(IDictionary dictionary)
        {
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
            return entries;
        }

        private static int IndexOf(List<DictionaryEntry> entries, string name) =>
            entries.FindIndex(entry => Equals(entry.Key, name));

        private static void Attempt(List<Exception> failures, Action action)
        {
            try { action(); }
            catch (AggregateException failure) { failures.AddRange(failure.Flatten().InnerExceptions); }
            catch (Exception failure) { failures.Add(failure); }
        }

        private static void Release(LifecycleSystemRowLease lease, List<Exception> failures)
        {
            if (lease == null) return;
            Attempt(failures, lease.CaptureRowIfCreated);
            Attempt(failures, lease.Dispose);
        }

        [Test]
        public void GenuineManagerReusesAnOwnedEarlierSlotWithoutChangingForeignState()
        {
            Assert.That(Application.isPlaying, Is.True, "genuine PlayMode lifecycle required");
            Action checkOuter = HLHapticsVerification.CaptureSharedOwnershipBoundary();
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string first = "haptics-slot-first-" + Guid.NewGuid().ToString("N");
            string second = "haptics-slot-second-" + Guid.NewGuid().ToString("N");
            LifecycleSystemRowLease firstLease = null, secondLease = null;
            var failures = new List<Exception>();
            try
            {
                firstLease = new LifecycleSystemRowLease(registry, first);
                firstLease.CaptureCreatedRow(ProcessManager.GetSystemRef(first));
                secondLease = new LifecycleSystemRowLease(registry, second);
                secondLease.CaptureCreatedRow(ProcessManager.GetSystemRef(second));
                List<DictionaryEntry> created = Entries(registry);
                int firstIndex = IndexOf(created, first), secondIndex = IndexOf(created, second);
                Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(secondIndex, Is.GreaterThanOrEqualTo(0));
                bool firstEarlier = firstIndex < secondIndex;
                LifecycleSystemRowLease earlier = firstEarlier ? firstLease : secondLease;
                string removed = firstEarlier ? first : second;
                string survivor = firstEarlier ? second : first;
                earlier.Dispose();
                Assert.That(earlier.CleanupFailures, Is.Empty);
                Assert.That(earlier.DiagnosticFailures, Is.Empty);
                Assert.That(registry.Contains(removed), Is.False);
                Assert.That(registry.Contains(survivor), Is.True);

                List<DictionaryEntry> foreign = Entries(registry);
                Action checkInner = HLHapticsVerification.CaptureSharedOwnershipBoundary();
                int observations = 0;
                string target = HLHapticsVerification.OwnedManagerRegistryName;
                Attempt(failures, () => HLHapticsVerification.ExerciseManagerAtOwnershipBoundary(() =>
                {
                    observations++;
                    List<DictionaryEntry> active = Entries(registry);
                    int managerIndex = IndexOf(active, target), survivorIndex = IndexOf(active, survivor);
                    TestContext.WriteLine("Actual manager slot=" + managerIndex + ", foreign survivor slot=" + survivorIndex);
                    Assert.That(managerIndex, Is.GreaterThanOrEqualTo(0));
                    Assert.That(managerIndex, Is.LessThan(survivorIndex), "actual reused slot precedes a surviving foreign row");
                    Assert.That(active.Count, Is.EqualTo(foreign.Count + 1));
                    active.RemoveAt(managerIndex);
                    for (int i = 0; i < foreign.Count; i++)
                    {
                        Assert.That(active[i].Key, Is.SameAs(foreign[i].Key), "foreign key identity/order " + i);
                        Assert.That(active[i].Value, Is.SameAs(foreign[i].Value), "foreign value identity/order " + i);
                    }
                }));
                Attempt(failures, () => Assert.That(observations, Is.EqualTo(1)));
                Attempt(failures, () => Assert.That(registry.Contains(target), Is.False));
                Attempt(failures, checkInner);
            }
            catch (Exception failure) { failures.Add(failure); }
            finally
            {
                Release(firstLease, failures);
                Release(secondLease, failures);
                foreach (LifecycleSystemRowLease lease in new[] { firstLease, secondLease })
                {
                    if (lease == null) continue;
                    foreach (LifecycleSystemRowLease.FailureRecord failure in lease.CleanupFailures) failures.Add(failure.Failure);
                    foreach (LifecycleSystemRowLease.FailureRecord failure in lease.DiagnosticFailures) failures.Add(failure.Failure);
                }
                Attempt(failures, checkOuter);
            }
            if (failures.Count != 0) throw new AggregateException("Haptics owned-slot body and independent cleanup failures", failures);
        }
    }
}
